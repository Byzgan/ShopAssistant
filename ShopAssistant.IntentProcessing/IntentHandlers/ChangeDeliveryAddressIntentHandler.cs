namespace ShopAssistant.IntentProcessing.IntentHandlers;

using Microsoft.Extensions.Configuration;
using Contracts.Enums;
using Contracts.Interfaces.Integrations;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.User;
using Contracts.Models.Chat;
using Contracts.Interfaces.Localization;

/// <summary>
/// Handles the ChangeDeliveryAddress intent using full multi-turn collected data.
/// </summary>
public class ChangeDeliveryAddressIntentHandler(IOrderService orderService, IUserContext userContext, ILocalizationService localizationService, IConfiguration configuration) : IIntentHandler
{
    public Intent Intent => Intent.ChangeDeliveryAddress;

    private const string CacheScope = "change_delivery_address";

    private readonly string _defaultLanguage = configuration.GetValue<string>("Languages:Default") ?? "en";

    public async Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        if (userContext.CurrentUser is null)
            return CreateCommonResponse(lang, "not_authenticated");

        if (!IsConfirmationAccepted(collectedData, lang))
            return CreateResponse(lang, "AddressChangeCancelled");

        var orderNumber = collectedData["OrderNumber"].Trim();
        var newAddress = collectedData["NewAddress"].Trim();

        bool result;
        try
        {
            result = await orderService.ChangeDeliveryAddressAsync(userContext.CurrentUser, orderNumber, newAddress, lang);
        }
        catch (Exception ex)
        {
            return CreateResponse(lang, "ChangeAddressError", ex.Message);
        }

        return result 
            ? CreateResponse(lang, "ChangeAddressSuccess", orderNumber, newAddress) 
            : CreateResponse(lang, "ChangeAddressFailed", orderNumber);
    }

    /// <summary>
    /// Determines the next required field and prompt for multi-turn dialog.
    /// Returns (field, prompt) or null if all required data is present.
    /// </summary>
    public async Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        if (userContext.CurrentUser is null)
            throw new InvalidOperationException(GetTranslation(lang, "LoginRequired"));

        // Step 1: Order number
        if (!collectedData.TryGetValue("OrderNumber", out var orderNumber) || string.IsNullOrWhiteSpace(orderNumber))
            return new DialogStepResult(DialogStepStatus.InProgress, "OrderNumber", GetTranslation(lang, "AskOrderNumber"));

        // Step 2: Check if delivery address can be changed for this order number
        if (!string.IsNullOrWhiteSpace(orderNumber) && (!collectedData.TryGetValue("ChangePossibleChecked", out var checkedFlag) || checkedFlag != "true"))
        {
            bool canChange;
            try
            {
                // Call external service to check change possibility
                canChange = await orderService.CanChangeDeliveryAddressAsync(userContext.CurrentUser, orderNumber.Trim());
            }
            catch
            {
                throw new InvalidOperationException(GetTranslation(lang, "InvalidOrderNumber"));
            }

            if (!canChange)
                throw new InvalidOperationException(GetTranslation(lang, "ChangeNotAllowed"));

            // Mark this check as done to avoid repeating
            collectedData["ChangePossibleChecked"] = "true";
        }

        // Step 3: New address
        if (!collectedData.TryGetValue("NewAddress", out var newAddress) || string.IsNullOrWhiteSpace(newAddress))
            return new DialogStepResult(DialogStepStatus.InProgress, "NewAddress", GetTranslation(lang, "AskNewAddress"));

        // Step 4: Confirmation (yes/no)
        if (!collectedData.TryGetValue("Confirmation", out var confirmation) || string.IsNullOrWhiteSpace(confirmation))
            return new DialogStepResult(DialogStepStatus.InProgress, "Confirmation", GetTranslation(lang, "ConfirmationRequired"));

        // All fields present: dialog complete
        return new DialogStepResult(DialogStepStatus.Completed);
    }

    private bool IsConfirmationAccepted(Dictionary<string, string> data, string lang)
    {
        if (!data.TryGetValue("Confirmation", out var confirmation) || string.IsNullOrWhiteSpace(confirmation))
            return false;

        var trimmed = confirmation.Trim().ToLowerInvariant();
        return lang == "no" ? trimmed.StartsWith("j") : trimmed.StartsWith("y");
    }

    private string GetTranslation(string lang, string messageKey)
    {
        return localizationService.GetMessage(messageKey, lang, CacheScope);
    }

    private ChatResponse CreateResponse(string lang, string messageKey, params object[] args)
    {
        var message = GetTranslation(lang, messageKey);
        if (args is { Length: > 0 })
            message = string.Format(message, args);

        return new ChatResponse
        {
            Answer = message
        };
    }

    private ChatResponse CreateCommonResponse(string language, string messageKey)
    {
        var message = GetTranslation(messageKey, language);

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


