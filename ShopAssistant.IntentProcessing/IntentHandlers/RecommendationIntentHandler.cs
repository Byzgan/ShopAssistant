// ReSharper disable RedundantIfElseBlock
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
namespace ShopAssistant.IntentProcessing.IntentHandlers;

using System.Net;
using System.Text;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Contracts.Enums;
using Contracts.Interfaces.Integrations;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.Localization;
using Contracts.Models.Chat;
using Contracts.Models.Integrations;
using Contracts.Models.Intent;
using Helpers;

/// <summary>
/// Handles the Recommend intent: fuzzy category understanding, multi-turn slot filling, 
/// and category disambiguation (user can pick from options if needed).
/// </summary>
public class RecommendationIntentHandler(IRecommendationService recommendationService, ILocalizationService localizationService, IConfiguration configuration) : IIntentHandler
{
    /// <inheritdoc/>
    public Intent Intent => Intent.Recommend;
    
    private const string RecommendationCacheScope = "recommendation";
    private const string ProductCategoryCacheScope = "product_categories";
    private const string UnknownCategory = "_Unknown_";
    private readonly string _defaultLanguage = configuration.GetValue<string>("Languages:Default") ?? "en";

    /// <inheritdoc/>
    public async Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);
        
        // If user made a selection, accept it as Category and continue
        if (collectedData.TryGetValue("CategorySelected", out var selectedCategory) && !string.IsNullOrWhiteSpace(selectedCategory))
        {
            collectedData["Category"] = selectedCategory.Trim();
            collectedData.Remove("CategoryAmbiguity");
            collectedData.Remove("CategorySelected");
            collectedData.Remove("PossibleCategories");
        }

        var category = collectedData["Category"];
        var preference = collectedData["Preference"].Trim();
        var discountOnly = collectedData.TryGetValue("DiscountOnly", out var disc) ? disc.Trim().ToLowerInvariant() : "no";
        bool requireDiscount = IsAffirmativeDiscountAnswer(discountOnly, lang);

        decimal? parsedBudget = null;
        if (collectedData.TryGetValue("Budget", out var budgetRaw))
        {
            var budgetInput = budgetRaw.Trim();
            if (!string.IsNullOrWhiteSpace(budgetInput) && TryParsePrice(budgetInput, out var parsed))
                parsedBudget = parsed;
        }

        var recRequest = new RecommendationRequest
        {
            Category = category != UnknownCategory ? string.Empty : category,
            Budget = parsedBudget,
            Preference = preference,
            DiscountOnly = requireDiscount
        };

        // Get product recommendations from the service based on user criteria
        var recommendations = await recommendationService.GetRecommendationsAsync(recRequest, CancellationToken.None);

        // Compose header message
        var header = localizationService.GetMessage("ResultsHeader", lang, RecommendationCacheScope);

        bool hasUrl = recommendations.Any(r => !string.IsNullOrWhiteSpace(r.Url));
        string response;

        if (recommendations.Count == 0)
        {
            var noResults = localizationService.GetMessage("NoResults", lang, RecommendationCacheScope);
            response = noResults;
        }
        else if (hasUrl)
        {
            response = $"<div><span>{header}</span><div style=\"display:flex;flex-wrap:wrap;align-items:center;gap:0.25em;margin-top:0.16em;\">";
            foreach (var result in recommendations)
            {
                response += !string.IsNullOrWhiteSpace(result.Url)
                    ? FormatProductTag(result.Name, result.Price.ToString(CultureInfo.InvariantCulture), result.Url)
                    : FormatProductChip(result.Name, result.Price.ToString(CultureInfo.InvariantCulture));
            }
            response += "</div></div>";
        }
        else
        {
            response = header + "\n";
            foreach (var result in recommendations)
            {
                response += $"- {result.Name} — {result.Price}\n";
            }
        }

        return new ChatResponse
        {
            Answer = response.Trim()
        };
    }

    /// <inheritdoc/>
    public async Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
    {
        var lang = NormalizeLanguage(language);

        // 1. CATEGORY (with ambiguity handling)
        if (!collectedData.TryGetValue("Category", out var categoryInput) || string.IsNullOrWhiteSpace(categoryInput))
        {
            var prompt = localizationService.GetMessage("AskCategory", lang, RecommendationCacheScope);

            return new DialogStepResult(DialogStepStatus.InProgress, "Category", prompt);
        } 
        else if (!collectedData.TryGetValue("CategoryAmbiguity", out var categoryAmbiguity) || categoryAmbiguity == "0")
        {
            // Try to map the input to a known category (fuzzy/synonym)
            var matchedCategory = await MatchCategory(categoryInput, lang);

            if (!string.IsNullOrWhiteSpace(matchedCategory.CanonicalCategory) && !matchedCategory.IsAmbiguous)
            {
                collectedData["Category"] = matchedCategory.CanonicalCategory;
                collectedData["CategoryAmbiguity"] = "0";
            }
            else
            {
                if (matchedCategory.CloseMatches.Any())
                {
                    collectedData["CategoryAmbiguity"] = "1";
                    var categoryPrompt = await GetCategoryOptionsPrompt(lang, collectedData);
                    var alternatives = matchedCategory.CloseMatches.Select(x => new ClarificationAlternative
                    {
                        SlotValue = x.category,
                        SlotType = "Category",
                        DisplayName = localizationService.GetMessage(x.category, lang, ProductCategoryCacheScope),
                        Score = 0.5f,
                        MatchType = x.matchType
                    }).ToList();

                    return new DialogStepResult(DialogStepStatus.InProgress, "CategorySelected", categoryPrompt, ClarificationType.Category, alternatives);
                }
            }
        }

        // 2. BUDGET
        if (!collectedData.TryGetValue("Budget", out var budget) || string.IsNullOrWhiteSpace(budget))
        {
            var prompt = localizationService.GetMessage("AskBudget", lang, RecommendationCacheScope);
            return new DialogStepResult(DialogStepStatus.InProgress, "Budget", prompt);
        }

        // 3. PREFERENCE
        if (!collectedData.TryGetValue("Preference", out var preference) || string.IsNullOrWhiteSpace(preference))
        {
            var prompt = localizationService.GetMessage("AskPreference", lang, RecommendationCacheScope);
            return new DialogStepResult(DialogStepStatus.InProgress, "Preference", prompt);
        }

        // 4. DISCOUNTONLY
        if (!collectedData.TryGetValue("DiscountOnly", out var discountOnly) || string.IsNullOrWhiteSpace(discountOnly))
        {
            var prompt = localizationService.GetMessage("AskDiscountOnly", lang, RecommendationCacheScope);
            return new DialogStepResult(DialogStepStatus.InProgress, "DiscountOnly", prompt);
        }

        // Completed!
        return new DialogStepResult(DialogStepStatus.Completed);
    }

    private async Task<CategoryMatchResult> MatchCategory(string userInput, string lang)
    {
        var known = await recommendationService.GetKnownCategoriesAsync(lang);

        userInput = userInput.Trim().ToLowerInvariant();
        var result = new CategoryMatchResult();

        // Exact match or synonym
        foreach (var cat in known)
        {
            if (cat.Value.Any(syn => string.Equals(syn, userInput, StringComparison.OrdinalIgnoreCase)))
            {
                result.CanonicalCategory = cat.Key;
                return result;
            }
        }

        // Fuzzy (Levenshtein <= 2) or substring
        foreach (var cat in known)
        {
            foreach (var syn in cat.Value)
            {
                if ((userInput.Length >= 3 && syn.Contains(userInput, StringComparison.OrdinalIgnoreCase)) || StringDistanceHelper.LevenshteinDistance(userInput, syn) <= 2)
                {
                    result.CloseMatches.Add((cat.Key, MatchType.Fuzzy));
                }
            }
        }

        result.CloseMatches = result.CloseMatches.Distinct().ToList();

        switch (result.CloseMatches.Count)
        {
            case 1:
                result.CanonicalCategory = result.CloseMatches[0].category;
                break;
            case > 1:
                result.IsAmbiguous = true;
                break;
        }

        return result;
    }

    private static bool TryParsePrice(string priceString, out decimal price)
    {
        price = 0;
        // Remove non-digit/decimal separator, e.g., "$1,299.99" → "1299.99"
        var cleaned = new string(priceString.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray()).Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out price);
    }


    /// <summary>
    /// Builds the category options prompt, optionally using restricted category set.
    /// </summary>
    private async Task<string> GetCategoryOptionsPrompt(string lang, Dictionary<string, string>? collectedData = null)
    {
        var prompt = localizationService.GetMessage("AskSelectCategory", lang, RecommendationCacheScope);
        var sb = new StringBuilder();
        sb.Append(prompt);

        var knownCategories = await recommendationService.GetKnownCategoriesAsync(lang);
        IEnumerable<string> cats = knownCategories.Keys;
        if (collectedData != null && collectedData.TryGetValue("PossibleCategories", out var possible) && !string.IsNullOrWhiteSpace(possible))
        {
            cats = possible.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c));
        }

        sb.Append(" ");
        sb.Append(string.Join(", ", cats.Select(c => $"<b>{WebUtility.HtmlEncode(c)}</b>")));

        return sb.ToString();
    }

    /// <summary>
    /// Determines if answer means "discount only" (multi-language support).
    /// </summary>
    private static bool IsAffirmativeDiscountAnswer(string? answer, string lang)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        
        var value = answer.Trim().ToLowerInvariant();
        
        if (lang.StartsWith("en") && value is "yes" or "y" or "only" or "discount" or "with discount" or "sure") 
            return true;

        if (lang.StartsWith("no") && value is "ja" or "kun" or "rabatt" or "med rabatt" or "selvfølgelig") 
            return true;

        return false;
    }

    /// <summary>
    /// Returns a clickable product tag styled as a compact chip (for products with URLs).
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
    /// Returns a product chip styled as a tag (for products without URLs).
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

    private string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? _defaultLanguage
            : language.Trim().ToLowerInvariant();
    }
}
