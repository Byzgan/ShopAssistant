namespace ShopAssistant.Contracts.Interfaces.Analytics;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShopAssistant.Contracts.Models.Analytics;

/// <summary>
/// Repository for logging detected intents and user inputs,
/// as well as providing analytics for dashboards and continuous improvement.
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// Saves a collection of intent log entries to the database, respecting the configured logging mode.
    /// - If <c>IntentLoggingMode.All</c>, all entries are saved.
    /// - If <c>IntentLoggingMode.NonDetected</c>, only entries with <c>DetectedIntent == "Unknown"</c> are saved.
    /// Skips saving if there are no matching entries.
    /// </summary>
    /// <param name="entries">The intent log entries to save.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="entries"/> is null.</exception>
    Task SaveIntentLogAsync(IReadOnlyCollection<IntentLogEntry> entries);

    /// <summary>
    /// Queries and filters past log entries for analytics or review.
    /// Supports filtering by user, intent, score, date, etc.
    /// </summary>
    /// <param name="query">Filter and paging options for the log query.</param>
    Task<IReadOnlyList<IntentLogEntry>> GetIntentLogsAsync(IntentLogQuery query);

    /// <summary>
    /// Returns statistics for each detected intent type,
    /// filtered by external system and date range (for dashboard analytics).
    /// </summary>
    /// <param name="externalSystem">Optional filter for a specific external system (shop, marketplace, etc). If null, queries all systems.</param>
    /// <param name="from">Start of the date range (inclusive). If null, no lower bound.</param>
    /// <param name="to">End of the date range (inclusive). If null, no upper bound.</param>
    /// <returns>List of intent statistics DTOs (Intent, Count).</returns>
    Task<IList<IntentStats>> GetIntentStatsAsync(string externalSystem, DateTime? from, DateTime? to);

    /// <summary>
    /// Saves a new unanswered question to the database.
    /// </summary>
    Task SaveUnansweredQuestionAsync(UnansweredQuestion unanswered);

    /// <summary>
    /// Queries unanswered questions for analytics/review, supports filtering by external system, user, and date range.
    /// </summary>
    /// <param name="externalSystem">The external system identifier.</param>
    /// <param name="from">The start date (inclusive) of the period to include statistics for, or null for no lower bound.</param>
    /// <param name="to">The end date (inclusive) of the period to include statistics for, or null for no upper bound.</param>
    Task<IReadOnlyList<UnansweredQuestion>> GetUnansweredFaqQueriesAsync(string externalSystem, DateTime? from, DateTime? to);

    /// <summary>
    /// Saves a single FAQ query attempt to the database.
    /// </summary>
    Task SaveFaqQueryLogAsync(FaqQueryLogEntry faqQueryLogEntry);


    /// <summary>
    /// Retrieves FAQ hit/miss statistics broken out per topic and per metric (answered vs unanswered).
    /// Returns flat rows: for each topic there will be one row with Metric="Answered" and one with Metric="Unanswered".
    /// </summary>
    /// <param name="externalSystem">Optional filter for the external system / channel (e.g., shop). Pass null to include all.</param>
    /// <param name="from">Inclusive start of time window (UTC). Pass null to ignore.</param>
    /// <param name="to">Inclusive end of time window (UTC). Pass null to ignore.</param>
    /// <returns>List of metrics, grouped by topic then metric (Answered first).</returns>
    Task<IList<FaqStatsMetric>> GetFaqStatsAsync(string externalSystem, DateTime? from, DateTime? to);
}
