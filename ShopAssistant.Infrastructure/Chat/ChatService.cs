// ReSharper disable ConvertToPrimaryConstructor
namespace ShopAssistant.Infrastructure.Chat;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.Localization;
using Contracts.Models.Intent;
using ShopAssistant.Contracts.Interfaces.Analytics;
using ShopAssistant.Contracts.Interfaces.Chat;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.Analytics;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Contracts.Models.User;

/// <summary>
/// Main service for the shop assistant chat.
/// </summary>
public class ChatService : IChatService
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IIntentProcessingService _intentProcessingService;
    private readonly ITopicRolePermissionProvider _topicPermissionProvider;
    private readonly IUserChatContextService _userChatContextService;
    private readonly IUserContext _userContext;
    private readonly ILocalizationService _localizationService;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IKnowledgeBaseService knowledgeBaseService,
        IIntentProcessingService intentProcessingService,
        ITopicRolePermissionProvider topicPermissionProvider,
        IUserChatContextService userChatContextService,
        IUserContext userContext,
        ILocalizationService localizationService,
        IAnalyticsRepository analyticsRepository,
        ILogger<ChatService> logger)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _intentProcessingService = intentProcessingService ?? throw new ArgumentNullException(nameof(intentProcessingService));
        _topicPermissionProvider = topicPermissionProvider ?? throw new ArgumentNullException(nameof(topicPermissionProvider));
        _userChatContextService = userChatContextService ?? throw new ArgumentNullException(nameof(userChatContextService));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _analyticsRepository = analyticsRepository ?? throw new ArgumentNullException(nameof(analyticsRepository));
        _logger = logger;
    }

    /// <summary>
    /// Processes a chat message from the user, handling multi-turn dialogs, intent detection, and clarification.
    /// </summary>
    /// <param name="request">The chat request containing user message and context.</param>
    public async Task<ChatResponse?> ProcessMessageAsync(ChatRequest request)
    {
        if (_userContext.CurrentUser == null)
        {
            return new ChatResponse
            {
                Answer = _localizationService.GetMessage("not_authenticated", request.Language, "global")
            };
        }

        var user = _userContext.CurrentUser;
        var uniqueUserId = user.UniqueUserId;
        var question = request.Message.Trim();
        var language = request.Language.Trim();

        // 1. Validate inputs
        _logger.LogInformation("[{UserId}] {Timestamp} - Step 1: Validate inputs", uniqueUserId, DateTime.Now.ToString("O"));
        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(language))
            return null;

        var allowedTopics = await _topicPermissionProvider.GetAllowedTopicsForRole(user.Role);

        // 2. FAQ cache lookup
        _logger.LogInformation("[{UserId}] {Timestamp} - Step 2: Try to return answer from cache (FAQ)", uniqueUserId, DateTime.Now.ToString("O"));
        var cachedKnowledgeItem = await _knowledgeBaseService.FindCachedAnswerAsync(question, language, allowedTopics);
        if (cachedKnowledgeItem is { Answer: { Length: > 0 } })
        {
            await _analyticsRepository.SaveFaqQueryLogAsync(new FaqQueryLogEntry
            {
                UserId = uniqueUserId,
                ExternalSystem = user.ExternalSystem,
                InputText = question,
                Topic = cachedKnowledgeItem.Topic.ToString(),
                Language = language,
                CreatedAt = DateTime.UtcNow,
                FaqHit = true,
                FaqId = cachedKnowledgeItem.Id
            });

            return new ChatResponse
            {
                Answer = cachedKnowledgeItem.Answer
            };
        }

        IntentDetectionResult detectedIntent = new IntentDetectionResult();

        if (request.UserClarification is { Type: ClarificationType.None })
        {
            request.UserClarification = null;
            detectedIntent.Intent = Intent.FAQ;
        }

        // 3. Handle user clarification after ambiguity prompt (intent or category)
        if (request.UserClarification != null && detectedIntent.Intent != Intent.Unknown)
        {
            var pending = await _userChatContextService.GetPendingIntentAsync(uniqueUserId);

            if (request.UserClarification.Type == ClarificationType.Intent && Enum.TryParse<Intent>(request.UserClarification.Value, out var parsedIntent))
            {
                detectedIntent.Intent = parsedIntent;

                return await ProcessIntentStepAsync(
                    detectedIntent.Intent,
                    new Dictionary<string, string>(),
                    language,
                    user,
                    question,
                    request.UserClarification
                );
            }

            if (request.UserClarification.Type == ClarificationType.Category)
            {
                if (pending != null)
                {
                    pending.CollectedData["Category"] = request.UserClarification.Value;

                    return await ProcessIntentStepAsync(
                        pending.Intent,
                        pending.CollectedData,
                        language,
                        user,
                        question,
                        request.UserClarification
                    );
                }

                // Handle case where there is no pending intent/context.
                return new ChatResponse
                {
                    Answer = _localizationService.GetMessage("category_clarification_no_context", language, "global")
                };
            }
        }

        // 4. Multi-turn dialog check
        _logger.LogInformation("[{UserId}] {Timestamp} - Step 3: Multi-turn dialog", uniqueUserId, DateTime.Now.ToString("O"));
        var pendingIntentContext = await _userChatContextService.GetPendingIntentAsync(uniqueUserId);
        if (pendingIntentContext != null)
        {
            if (!string.IsNullOrWhiteSpace(pendingIntentContext.CurrentField))
                pendingIntentContext.CollectedData[pendingIntentContext.CurrentField] = question;

            return await ProcessIntentStepAsync(
                pendingIntentContext.Intent,
                pendingIntentContext.CollectedData,
                language,
                user,
                question,
                request.UserClarification
            );
        }

        // 5. Detect user intent
        if (detectedIntent.Intent == Intent.Unknown)
        {
            _logger.LogInformation("[{UserId}] {Timestamp} - Step 4: Detect user intent (with clarification)", uniqueUserId, DateTime.Now.ToString("O"));
            detectedIntent = await _intentProcessingService.DetectIntentAsync(language, question);
            
            if (detectedIntent.Intent != Intent.Unknown)
            {
                // Log all alternatives in bulk (if ambiguity detected)
                var alternatives = detectedIntent.Alternatives;
                if (alternatives != null && alternatives.Any())
                {
                    // Map alternatives to log entries
                    var logEntries = alternatives.Select(alt => new IntentLogEntry
                    {
                        UserId = uniqueUserId,
                        UserRole = (int)user.Role,
                        ExternalSystem = user.ExternalSystem,
                        InputText = question,
                        DetectedIntent = Enum.TryParse<Intent>(alt.SlotValue, out var intentValue)
                            ? intentValue
                            : Intent.Unknown,
                        Score = alt.Score,
                        MatchType = alt.MatchType,
                        Language = language,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    // Bulk save
                    await _analyticsRepository.SaveIntentLogAsync(logEntries);
                }
                else
                {
                    // Fallback: save single intent detection result
                    await _analyticsRepository.SaveIntentLogAsync(new List<IntentLogEntry>
                    {
                        new IntentLogEntry
                        {
                            UserId = uniqueUserId,
                            UserRole = (int)user.Role,
                            ExternalSystem = user.ExternalSystem,
                            InputText = question,
                            DetectedIntent = detectedIntent.Intent,
                            Score = detectedIntent.MatchScore,
                            MatchType = MatchType.None,
                            Language = language,
                            CreatedAt = DateTime.UtcNow
                        }
                    });
                }

                // Return clarification prompt to the user (intent or category slot)
                if (alternatives is { Count: > 1 })
                {
                    return new ChatResponse
                    {
                        Answer = string.Empty,
                        IsClarification = true,
                        ClarificationPrompt = detectedIntent.ClarificationPrompt ?? _localizationService.GetMessage("clarify_ambiguity", language, "global"),
                        ClarificationType = detectedIntent.ClarificationType,
                        Alternatives = alternatives
                    };
                }
            }
        }
        
        // 6. If intent found, collect data and process
        if (detectedIntent.Intent != Intent.Unknown && detectedIntent.Intent != Intent.FAQ)
        {
            var collectedData = detectedIntent.ExtraData != null
                ? new Dictionary<string, string>(detectedIntent.ExtraData)
                : new Dictionary<string, string>();

            return await ProcessIntentStepAsync(detectedIntent.Intent, collectedData, language, user, question);
        }

        // 7. Fallback: semantic KB search with role access control
        _logger.LogInformation("[{UserId}] {Timestamp} - Step 5: Fallback: semantic KB search", uniqueUserId, DateTime.Now.ToString("O"));
        SearchResult? searchResult = await _knowledgeBaseService.FindSemanticAnswerAsync(question, language, allowedTopics);
        if (searchResult != null)
        {
            if (searchResult.Score > 0.98)
            {
                KnowledgeItem knowledgeItem = CreateKnowledgeItem(question, searchResult.Answer, language, searchResult.Topic);
                await _knowledgeBaseService.SaveAnswerToCacheAsync(question, language, knowledgeItem);
            }

            await _analyticsRepository.SaveFaqQueryLogAsync(new FaqQueryLogEntry
            {
                UserId = uniqueUserId,
                ExternalSystem = user.ExternalSystem,
                InputText = question,
                Topic = searchResult.Topic.ToString(),
                Language = language,
                CreatedAt = DateTime.UtcNow,
                FaqHit = true,
                FaqId = searchResult.KnowledgeId
            });

            return new ChatResponse
            {
                Answer = searchResult.Answer
            };
        }

        // 8. Default reply
        _logger.LogInformation("[{UserId}] {Timestamp} - Step 6: Default reply if nothing found", uniqueUserId, DateTime.Now.ToString("O"));

        await _analyticsRepository.SaveFaqQueryLogAsync(new FaqQueryLogEntry
        {
            UserId = uniqueUserId,
            ExternalSystem = user.ExternalSystem,
            InputText = question,
            Topic = null,
            Language = language,
            CreatedAt = DateTime.UtcNow,
            FaqHit = false,
            FaqId = null
        });

        return new ChatResponse
        {
            Answer = _localizationService.GetMessage("not_found", language, "global")
        };
    }

    /// <summary>
    /// Processes an intent step, managing multi-turn dialogue states and responses.
    /// Handles progression of pending intents, slot filling, completions, and error management.
    /// </summary>
    /// <param name="intent">The identified intent to process.</param>
    /// <param name="collectedData">The collected data for the current intent.</param>
    /// <param name="language">The language used by the user.</param>
    /// <param name="user">The current authenticated user.</param>
    /// <param name="question">The user's original question.</param>
    /// <param name="clarification">User clarification for ambiguity, if any.</param>
    /// <returns>A ChatResponse containing the next dialogue prompt or final response.</returns>
    private async Task<ChatResponse> ProcessIntentStepAsync(Intent intent, Dictionary<string, string> collectedData, string language, User user, string question, UserClarification? clarification = null)
    {
        var handler = _intentProcessingService.GetHandlerForIntent(intent);
        if (handler == null)
        {
            await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, null);

            await _analyticsRepository.SaveFaqQueryLogAsync(new FaqQueryLogEntry
            {
                UserId = user.UniqueUserId,
                ExternalSystem = user.ExternalSystem,
                InputText = question,
                Language = language,
                CreatedAt = DateTime.UtcNow,
                FaqHit = false,
                FaqId = null
            });

            return new ChatResponse
            {
                Answer = _localizationService.GetMessage("not_found", language, "global")
            };
        }

        try
        {
            // [GENERALIZED CLARIFICATION] - For slot clarifications, fill in the correct collectedData slot
            if (clarification is { Type: ClarificationType.Category })
            {
                collectedData["Category"] = clarification.Value;
            }

            DialogStepResult stepResult = await handler.GetNextStep(collectedData, language);
            switch (stepResult.Status)
            {
                case DialogStepStatus.InProgress:
                    // The intent requires additional information; prompt the user for next input
                    var pending = new PendingIntentContext
                    {
                        Intent = intent,
                        CollectedData = collectedData,
                        CurrentField = stepResult.Field!,
                        CurrentPrompt = stepResult.Prompt!
                    };

                    await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, pending);

                    return new ChatResponse
                    {
                        Answer = pending.CurrentPrompt,
                        IsClarification = stepResult.Alternatives?.Count > 0,
                        ClarificationType = stepResult.Alternatives?.Count > 0 ? stepResult.ClarificationType : null,
                        ClarificationPrompt = stepResult.Alternatives?.Count > 0
                            ? _localizationService.GetMessage("clarify_ambiguity", language, "global")
                            : null,
                        Alternatives = stepResult.Alternatives
                    };

                case DialogStepStatus.Completed:
                    // Intent data collection is complete; generate final response
                    var intentResponse = await handler.HandleAsync(collectedData, language);
                    await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, null);

                    if (intentResponse == null)
                        return new ChatResponse
                        {
                            Answer = _localizationService.GetMessage("request_processed", language, "global")
                        };

                    return intentResponse;
                default:
                    // Defensive: Unknown dialog step status encountered
                    await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, null);
                    
                    await LogFailedFaqQueryAsync(user.UniqueUserId, user.ExternalSystem, question, language);

                    return new ChatResponse
                    {
                        Answer = _localizationService.GetMessage("system_error", language, "global")
                    };
            }
        }
        catch (InvalidOperationException ex)
        {
            // Handle known validation errors gracefully
            await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, null);
            await LogFailedFaqQueryAsync(user.UniqueUserId, user.ExternalSystem, question, language);

            return new ChatResponse
            {
                Answer = ex.Message
            };
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions and provide a generic error message
            _logger.LogError(ex, "Unhandled exception in GetNextStep");
            await _userChatContextService.SetPendingIntentAsync(user.UniqueUserId, null);

            await LogFailedFaqQueryAsync(user.UniqueUserId, user.ExternalSystem, question, language);

            return new ChatResponse
            {
                Answer = _localizationService.GetMessage("system_error", language, "global")
            };
        }
    }

    private Task LogFailedFaqQueryAsync(string userId, string externalSystem, string inputText, string language)
    {
        return _analyticsRepository.SaveFaqQueryLogAsync(new FaqQueryLogEntry
        {
            UserId = userId,
            ExternalSystem = externalSystem,
            InputText = inputText,
            Topic = null,
            Language = language,
            CreatedAt = DateTime.UtcNow,
            FaqHit = false,
            FaqId = null
        });
    }

    /// <summary>
    /// Creates a knowledge base item for caching.
    /// </summary>
    private static KnowledgeItem CreateKnowledgeItem(string question, string answer, string language, KnowledgeTopic topic)
    {
        return new KnowledgeItem
        {
            Questions = [question],
            Answer = answer,
            Language = language,
            Topic = topic
        };
    }
}
