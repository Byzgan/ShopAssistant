#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
namespace ShopAssistant.IntentProcessing.IntentHandlers;

using Microsoft.Extensions.Configuration;
using System.Net;
using Contracts.Enums;
using Contracts.Interfaces.Integrations;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.Localization;
using Contracts.Models.Chat;
using Contracts.Models.Integrations;
using Helpers;

/// <summary>
/// Handles the ProductSearch intent. Collects all required information in a multi-turn scenario,
/// then returns product search results using the external product search service.
/// </summary>
public class ProductSearchIntentHandler(IProductSearchService productSearchService, ILocalizationService localizationService, IConfiguration configuration) : IIntentHandler
{
    public Intent Intent => Intent.ProductSearch;
    private const string CacheScope = "product_search";
    private readonly string _defaultLanguage = configuration.GetValue<string>("Languages:Default") ?? "en";

    /// <inheritdoc/>
    public async Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        var productType = collectedData["ProductType"].Trim();
        var brand = collectedData["Brand"].Trim();

        // Handle "no brand" type answers
        if (IsNegativeBrandAnswer(brand, lang))
            brand = ""; 

        // Build the structured request for the external service
        var searchRequest = new ProductSearchRequest
        {
            ProductType = productType,
            Brand = brand
        };
       
        // Call the external product search service
        var results = await productSearchService.SearchProductsAsync(searchRequest, CancellationToken.None);

        // Return formatted results, or a message if none found
        if (results.Count == 0)
        {
            var noResultKey = string.IsNullOrWhiteSpace(brand) ? "NoResults_NoBrand" : "NoResults";
            return CreateResponse(lang, noResultKey, productType, brand);
        }

        if (!string.IsNullOrWhiteSpace(brand))
            brand = char.ToUpper(brand[0]) + brand[1..].ToLowerInvariant();
        var header = CreateResponse(lang, "ResultsHeader", productType, brand).Answer;

        // Determine if any product has a URL
        bool hasUrl = results.Any(r => !string.IsNullOrWhiteSpace(r.Url));

        string response;

        if (hasUrl)
        {
            // Format as HTML with clickable product links
            // Tag / chip layout for products, using helper methods
            response = $"<div><span>{header}</span><div style=\"display:flex;flex-wrap:wrap;align-items:center;gap:0.25em;margin-top:0.16em;\">";
            foreach (var result in results)
            {
                response += !string.IsNullOrWhiteSpace(result.Url)
                    ? FormatProductTag(result.Name, result.Price, result.Url)
                    : FormatProductChip(result.Name, result.Price);
            }
            response += "</div></div>";
        }
        else
        {
            // Plain text fallback
            response = header;
            foreach (var result in results)
            {
                response += $"- {result.Name} — {result.Price}\n";
            }
        }

        return new ChatResponse { Answer = response.Trim() };
    }

    public async Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        // 1. Always ask for ProductType first
        if (!collectedData.TryGetValue("ProductType", out var productType) || string.IsNullOrWhiteSpace(productType))
            return new DialogStepResult(DialogStepStatus.InProgress, "ProductType", GetTranslation(lang, "AskProductType"));

        // 2. Try to infer Brand from ProductType if not provided
        if (collectedData.TryGetValue("Brand", out var brand) && !string.IsNullOrWhiteSpace(brand))
            return new DialogStepResult(DialogStepStatus.Completed);
        
        var inferred = DetectBrandFromProductType(productType);
        if (!string.IsNullOrWhiteSpace(inferred))
            collectedData["Brand"] = inferred; // pre-fill
        else
            return new DialogStepResult(DialogStepStatus.InProgress, "Brand", string.Format(GetTranslation(lang, "AskBrand"), productType));

        // All steps done: dialog complete
        return new DialogStepResult(DialogStepStatus.Completed);
    }

    private static string? DetectBrandFromProductType(string productType)
    {
        if (string.IsNullOrWhiteSpace(productType))
            return null;

        var p = productType.ToLowerInvariant();

        // List of brands and their associated keywords for detection
        var brandKeywords = new (string Brand, string[] Keywords)[]
        {
            ("Apple",    ["iphone", "macbook", "ipad", "airpods", "apple", "iwatch", "apple watch", "imac", "mac mini", "mac studio"]),
            ("Samsung",  ["galaxy", "samsung", "galaxy watch", "galaxy buds", "note", "tab", "samsung watch", "samsung buds"]),
            ("Microsoft",["surface", "microsoft", "xbox"]),
            ("Sony",     ["playstation", "ps5", "ps4", "sony", "walkman", "bravia", "xperia"]),
            ("Amazon",   ["kindle", "amazon", "echo", "fire tv", "alexa", "fire tablet"]),
            ("Google",   ["pixel", "google", "nest", "chromecast", "google home", "pixel buds"]),
            ("Xiaomi",   ["xiaomi", "mi band", "redmi", "poco", "mi tv", "amazfit"]),
            ("Huawei",   ["huawei", "honor band", "matebook", "watch gt"]),
            ("Fitbit",   ["fitbit", "versa", "sense", "inspire", "charge"]),
            ("Garmin",   ["garmin", "forerunner", "fenix", "vivoactive", "venu"]),
            ("Lenovo",   ["lenovo", "thinkpad", "ideapad", "legion", "yoga"]),
            ("ASUS",     ["asus", "zenbook", "vivobook", "rog", "tuf"]),
            ("HP",       ["hp", "pavilion", "spectre", "envy", "omen"]),
            ("Dell",     ["dell", "inspiron", "xps", "alienware", "latitude"]),
            ("Acer",     ["acer", "aspire", "predator", "swift", "nitro"]),
            ("OPPO",     ["oppo", "oppo band", "oppo watch"]),
            ("Vivo",     ["vivo", "vivo band", "vivo watch"]),
            ("Realme",   ["realme", "realme band", "realme watch"]),
        };

        // Tokenize the productType for more accurate word boundary matching
        var tokens = p.Split([' ', '-', '_', '.', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokenSet = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);

        // 1. Exact token match for each brand's keywords
        foreach (var (brand, keywords) in brandKeywords)
        {
            if (keywords.Any(keyword => tokenSet.Contains(keyword)))
                return brand;
        }

        // 2. Substring match for keywords >= 4 chars (to avoid false positives)
        foreach (var (brand, keywords) in brandKeywords)
        {
            if (keywords.Any(keyword => keyword.Length >= 4 && p.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return brand;
        }

        // 3. Fuzzy match: allow for minor typos (Levenshtein distance <= 1) for keywords >= 4 chars
        foreach (var (brand, keywords) in brandKeywords)
        {
            foreach (var keyword in keywords)
            {
                if (keyword.Length < 4) continue;

                foreach (var token in tokenSet)
                {
                    if (StringDistanceHelper.LevenshteinDistance(token, keyword) == 1)
                        return brand;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Helper for localizing system prompts and responses.
    /// </summary>
    private ChatResponse CreateResponse(string lang, string messageKey, params object[] args)
    {
        var message = GetTranslation(lang, messageKey);

        if (args is not { Length: > 0 } || !message.Contains('{'))
        {
            return new ChatResponse
            {
                Answer = message
            };
        }

        try
        {
            message = string.Format(message, args);
        }
        catch (FormatException) {}
        
        return new ChatResponse
        {
            Answer = message
        };
    }

    private static bool IsNegativeBrandAnswer(string? answer, string lang)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return true; // blank = no preference

        var value = answer.Trim().ToLowerInvariant();

        // English negatives
        if (lang.StartsWith("en") && value is "no" or "none" or "any" or "no brand" or "not")
            return true;

        // Norwegian negatives
        if (lang.StartsWith("no") && value is "nei" or "ingen" or "hvilket som helst" or "ikke")
            return true;

        return false;
    }

    private string GetTranslation(string lang, string messageKey)
    {
        return localizationService.GetMessage(messageKey, lang, CacheScope);
    }

    private string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? _defaultLanguage
            : language.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns a clickable product tag styled as a compact modern chip (for products with URLs).
    /// </summary>
    private static string FormatProductTag(string name, string price, string url)
    {
        name = WebUtility.HtmlEncode(name);
        price = WebUtility.HtmlEncode(price);
        url = WebUtility.HtmlEncode(url);

        var title = $"{name} {price}";

        return $"""
                    <a href="{url}" target="_blank" class="link-tag" title="{title}">
                        <svg xmlns='http://www.w3.org/2000/svg' class='product-icon' viewBox='0 0 20 20'>
                            <path d='M13 3h4v4m0-4L10 11' stroke='currentColor' stroke-width='1.3' stroke-linecap='round' stroke-linejoin='round'/>
                        </svg>
                        <span class="product-name">{name}</span>
                        <span class="product-price">{price}</span>
                    </a>
                """;
    }

    /// <summary>
    /// Returns a product chip styled as a compact modern tag (for products without URLs).
    /// </summary>
    private static string FormatProductChip(string name, string price)
    {
        name = WebUtility.HtmlEncode(name);
        price = WebUtility.HtmlEncode(price);

        var title = $"{name} {price}";

        return $"""
                    <span class="product-chip" title="{title}">
                        <svg xmlns='http://www.w3.org/2000/svg' class='product-icon' viewBox='0 0 20 20'>
                            <rect x='3' y='7' width='14' height='6' rx='2.5' fill='currentColor' opacity='.12'/>
                            <rect x='3' y='7' width='14' height='6' rx='2.5' stroke='currentColor' stroke-width='1.0'/>
                        </svg>
                        <span class="product-name">{name}</span>
                        <span class="product-price">{price}</span>
                    </span>
                """;
    }

}
