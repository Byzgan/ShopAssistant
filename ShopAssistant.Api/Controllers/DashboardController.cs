namespace ShopAssistant.Api.Controllers;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Interfaces.Analytics;
using Contracts.Interfaces.Localization;
using Contracts.Interfaces.User;
using Contracts.Models.User;

/// <summary>
/// Provides analytics endpoints for the shop-owner dashboard,
/// including intent frequency, unanswered questions, and FAQ hit/miss statistics.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin")]
public class DashboardController(IAnalyticsRepository analyticsRepository, IUserContext userContext, ILocalizationService localizationService) : ControllerBase
{
    private User CurrentUser => userContext.CurrentUser ?? throw new InvalidOperationException("No authenticated user found in the current context.");

    /// <summary>
    /// Returns frequency statistics for each detected intent,
    /// optionally filtered by date range.
    /// </summary>
    /// <param name="language">Language code for localization (e.g., "en", "no").</param>
    /// <param name="from">Start date filter (optional).</param>
    /// <param name="to">End date filter (optional).</param>
    /// <returns>List of intent frequency statistics.</returns>
    [HttpGet("intent-stats")]
    public async Task<IActionResult> GetIntentStats([FromQuery] string language, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var user = CurrentUser;
        
        var stats = await analyticsRepository.GetIntentStatsAsync(user.ExternalSystem, from, to);

        var result = stats.Select(x => new
        {
            IntentId = (int)x.Intent,
            IntentName = localizationService.GetMessage(x.Intent.ToString(), language, "intents"),
            x.Count
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Returns unanswered customer questions for the dashboard,
    /// filtered by external system and date range if specified.
    /// </summary>
    /// <param name="from">Optional: start of date range (inclusive).</param>
    /// <param name="to">Optional: end of date range (inclusive).</param>
    /// <returns>List of unanswered question DTOs.</returns>
    [HttpGet("unanswered-questions")]
    public async Task<IActionResult> GetUnansweredQuestions([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var questions = await analyticsRepository.GetUnansweredFaqQueriesAsync(CurrentUser.ExternalSystem, from, to);

        return Ok(questions);
    }

    /// <summary>
    /// Returns FAQ hit/miss statistics, including total questions, answered from FAQ, and not answered,
    /// optionally filtered by date range.
    /// </summary>
    /// <param name="language">Language code for localization (e.g., "en", "no").</param>
    /// <param name="from">Start date filter (optional).</param>
    /// <param name="to">End date filter (optional).</param>
    /// <returns>FAQ statistics DTO.</returns>
    [HttpGet("faq-stats")]
    public async Task<IActionResult> GetFaqStats([FromQuery] string language, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var user = CurrentUser;

        var stats = await analyticsRepository.GetFaqStatsAsync(user.ExternalSystem, from, to);

        foreach (var metric in stats)
        {
            if (!string.IsNullOrWhiteSpace(metric.Topic))
                metric.Topic = localizationService.GetMessage(metric.Topic, language, "knowledge_topics");
        }

        return Ok(stats);
    }
}
