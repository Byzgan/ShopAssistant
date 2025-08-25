#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
namespace ShopAssistant.IntentProcessing.IntentHandlers;

using Microsoft.Extensions.Configuration;
using Contracts.Enums;
using Contracts.Interfaces.Integrations;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.Localization;
using Contracts.Interfaces.User;
using Contracts.Models.Chat;

/// <summary>
/// Handles order status requests (multi-turn and single-turn). 
/// Supports "latest order" and "order number" cases, with localization.
/// </summary>
public class OrderStatusIntentHandler(IOrderService orderService, IUserContext userContext, ILocalizationService localizationService, IConfiguration configuration) : IIntentHandler
{
    public Intent Intent => Intent.OrderStatus;

    private const string CacheScope = "order_status";
    private readonly string _defaultLanguage = configuration.GetValue<string>("Languages:Default") ?? "en";

    public async Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        if (userContext.CurrentUser == null)
            return CreateCommonResponse(lang, "not_authenticated");

        // Case 1: User asks about the latest order status
        if (collectedData.TryGetValue("Latest", out var latestValue) && latestValue.ToLowerInvariant() == "true")
        {
            var status = await orderService.GetLatestOrderStatusAsync(userContext.CurrentUser, language);
            return CreateResponse(lang, "LatestOrderStatus", status);
        }

        // Case 2: User provides an explicit order number (by contract, it must exist here)
        string orderNumber = collectedData.TryGetValue("OrderNumber", out var value)
            ? value.Trim()
            : collectedData["Value"].Trim(); // fallback for single-turn

        var result = await orderService.GetOrderStatusAsync(userContext.CurrentUser, orderNumber, language);
        
        return string.IsNullOrWhiteSpace(result) 
            ? CreateResponse(lang, "OrderNotFound", orderNumber) 
            : CreateResponse(lang, "OrderStatus", orderNumber, result);
    }

    /// <summary>
    /// Determines the next required field and prompt for multi-turn dialog.
    /// Returns (field, prompt) or null if all required data is present.
    /// </summary>
    public async Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        // If 'Latest' is present and "true", dialog is ready to be handled
        if (collectedData.TryGetValue("Latest", out var latestValue) && latestValue.ToLowerInvariant() == "true")
            return new DialogStepResult(DialogStepStatus.Completed);

        // Otherwise, require an order number (explicit or via Value)
        if ((!collectedData.TryGetValue("OrderNumber", out var explicitOrderNumber) || string.IsNullOrWhiteSpace(explicitOrderNumber))
            && (!collectedData.TryGetValue("Value", out var value) || string.IsNullOrWhiteSpace(value)))
        {
            return new DialogStepResult(DialogStepStatus.InProgress, "OrderNumber", GetTranslation(lang, "AskOrderNumber"));
        }

        // All fields present: dialog complete
        return new DialogStepResult(DialogStepStatus.Completed);
    }

    private string GetTranslation(string language, string messageKey)
    {
        return localizationService.GetMessage(messageKey, language, CacheScope);
    }

    private ChatResponse CreateResponse(string language, string messageKey, params object[] args)
    {
        var message = GetTranslation(language, messageKey);
        if (args is { Length: > 0 })
            message = string.Format(message, args);

        return new ChatResponse
        {
            Answer = message
        };
    }

    private ChatResponse CreateCommonResponse(string language, string messageKey)
    {
        var message = GetTranslation(language, messageKey);

        return new ChatResponse
        {
            Answer = message
        };
    }

    private string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? _defaultLanguage
            : language.Trim().ToLowerInvariant();
    }
}
