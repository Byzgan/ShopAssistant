using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.Intent;

namespace ShopAssistant.Infrastructure.TextProcessing.Intent;

/// <summary>
/// Provides intent detection and dispatches to the appropriate intent handler.
/// Also exposes direct handler access for handler-driven multi-turn dialog.
/// </summary>
public class IntentProcessingService : IIntentProcessingService
{
    private readonly IIntentDetector _intentDetector;
    private readonly Dictionary<Contracts.Enums.Intent, IIntentHandler> _intentHandlerDict;
    private readonly ILogger<IntentProcessingService>? _logger;

    public IntentProcessingService(IIntentDetector intentDetector, IEnumerable<IIntentHandler> intentHandlers, ILogger<IntentProcessingService> logger)
    {
        _intentDetector = intentDetector ?? throw new ArgumentNullException(nameof(intentDetector));
        
        if (intentHandlers == null) 
            throw new ArgumentNullException(nameof(intentHandlers));

        _logger = logger;

        _intentHandlerDict = new Dictionary<Contracts.Enums.Intent, IIntentHandler>();

        foreach (var handler in intentHandlers)
        {
            if (!_intentHandlerDict.TryAdd(handler.Intent, handler))
                throw new InvalidOperationException($"Duplicate handler for intent: {handler.Intent}");
        }
    }

    /// <inheritdoc />
    public async Task<IntentDetectionResult> DetectIntentAsync(string language, string message)
    {
        return await _intentDetector.DetectIntentAsync(language, message);
    }

    /// <inheritdoc />
    public async Task<ChatResponse?> HandleAsync(Contracts.Enums.Intent intent, IUserContext userContext, Dictionary<string, string> collectedData, string language)
    {
        if (_intentHandlerDict.TryGetValue(intent, out var handler))
            return await handler.HandleAsync(collectedData, language);

        _logger?.LogWarning("No handler registered for intent {Intent}", intent);

        return null;
    }

    /// <inheritdoc />
    public IIntentHandler? GetHandlerForIntent(Contracts.Enums.Intent intent)
    {
        _intentHandlerDict.TryGetValue(intent, out var handler);
        return handler;
    }
}