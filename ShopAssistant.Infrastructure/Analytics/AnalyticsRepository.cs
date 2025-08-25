namespace ShopAssistant.Infrastructure.Analytics;

using System.Data;
using Microsoft.Extensions.Options;
using Dapper;
using Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.Analytics;
using ShopAssistant.Contracts.Models.Analytics;


/// <summary>
/// SQL Server implementation of IAnalyticsRepository using Dapper.
/// Handles insert and query operations for intent logs.
/// </summary>
public class AnalyticsRepository(IDbConnection dbConnection, IOptions<AnalyticsOptions> analyticsOptions, IOptions<IntentLoggingOptions> options) : IAnalyticsRepository
{

    /// <summary>
    /// Saves a collection of intent log entries to the database, respecting the configured logging mode.
    /// - If <c>IntentLoggingMode.All</c>, all entries are saved.
    /// - If <c>IntentLoggingMode.NonDetected</c>, only entries with <c>DetectedIntent == "Unknown"</c> are saved.
    /// Skips saving if there are no matching entries.
    /// </summary>
    /// <param name="entries">The intent log entries to save.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="entries"/> is null.</exception>
    public async Task SaveIntentLogAsync(IReadOnlyCollection<IntentLogEntry> entries)
    {
        if (!analyticsOptions.Value.Enabled)
            return;

        var entriesToSave = options.Value.Mode == IntentLoggingMode.NonDetected 
            ? entries.Where(e => e.DetectedIntent == Intent.Unknown).ToList() 
            : entries;

        if (entriesToSave.Count == 0) 
            return;

        const string sql = """
            INSERT INTO IntentLogs
                (UserId, UserRole, ExternalSystem, InputText, DetectedIntent, Score, MatchType, Language, CreatedAt, ExtraData)
            VALUES
                (@UserId, @UserRole, @ExternalSystem, @InputText, @DetectedIntent, @Score, @MatchType, @Language, @CreatedAt, @ExtraData);
        """;

        // Dapper will execute the insert once per entry, but within a single call and open connection.
        await dbConnection.ExecuteAsync(sql, entriesToSave);

        await EnforceRetentionAsync();
    }

    /// <summary>
    /// Queries intent logs with optional filters.
    /// </summary>
    public async Task<IReadOnlyList<IntentLogEntry>> GetIntentLogsAsync(IntentLogQuery query)
    {
        if (query == null) 
            throw new ArgumentNullException(nameof(query));

        var sql = """
            SELECT Id, UserId, InputText, DetectedIntent, Score, MatchType, Language, CreatedAt, ExtraData
            FROM IntentLogs
            WHERE 1=1
        """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(query.UserId))
        {
            sql += " AND UserId = @UserId";
            parameters.Add("UserId", query.UserId);
        }
        if (!string.IsNullOrEmpty(query.DetectedIntent))
        {
            sql += " AND DetectedIntent = @DetectedIntent";
            parameters.Add("DetectedIntent", query.DetectedIntent);
        }
        if (!string.IsNullOrEmpty(query.Language))
        {
            sql += " AND Language = @Language";
            parameters.Add("Language", query.Language);
        }
        if (query.MinScore.HasValue)
        {
            sql += " AND Score >= @MinScore";
            parameters.Add("MinScore", query.MinScore.Value);
        }
        if (query.MaxScore.HasValue)
        {
            sql += " AND Score <= @MaxScore";
            parameters.Add("MaxScore", query.MaxScore.Value);
        }
        if (query.From.HasValue)
        {
            sql += " AND CreatedAt >= @From";
            parameters.Add("From", query.From.Value);
        }
        if (query.To.HasValue)
        {
            sql += " AND CreatedAt <= @To";
            parameters.Add("To", query.To.Value);
        }

        sql += " ORDER BY CreatedAt DESC";

        if (query.Limit.HasValue)
        {
            sql += " OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";
            parameters.Add("Limit", query.Limit.Value);
            parameters.Add("Offset", query.Offset ?? 0);
        }
        else
        {
            sql += " OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY";
            parameters.Add("Limit", 100);
            parameters.Add("Offset", 0);
        }

        var list = await dbConnection.QueryAsync<IntentLogEntry>(sql, parameters);

        return list.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns statistics for each detected intent type,
    /// filtered by external system and date range (for dashboard analytics).
    /// </summary>
    /// <param name="externalSystem">External system name.</param>
    /// <param name="from">Start of the date range (inclusive). If null, no lower bound.</param>
    /// <param name="to">End of the date range (inclusive). If null, no upper bound.</param>
    /// <returns>List of intent statistics DTOs (IntentId, IntentName, Count).</returns>
    public async Task<IList<IntentStats>> GetIntentStatsAsync(string externalSystem, DateTime? from, DateTime? to)
    {
        const string sql = """
                                   SELECT DetectedIntent AS IntentId, COUNT(*) AS Count
                                   FROM IntentLogs
                                   WHERE (@externalSystem IS NULL OR ExternalSystem = @externalSystem)
                                     AND (@from IS NULL OR CreatedAt >= @from)
                                     AND (@to IS NULL OR CreatedAt <= @to)
                                   GROUP BY DetectedIntent
                                   ORDER BY COUNT(*) DESC
                           """;

        var rows = await dbConnection.QueryAsync<(byte IntentId, int Count)>(sql, new { externalSystem, from, to });
        
        var result = rows.Select(x => new IntentStats
        {
            Intent = (Intent)x.IntentId,
            Count = x.Count
        }).ToList();

        return result;
    }


    /// <summary>
    /// Saves a new unanswered question to the database.
    /// </summary>
    public async Task SaveUnansweredQuestionAsync(UnansweredQuestion unanswered)
    {
        if (!analyticsOptions.Value.Enabled)
            return;

        const string sql = """
            INSERT INTO UnansweredQuestionsLogs
                (UserId, ExternalSystem, InputText, Language, CreatedAt)
            VALUES
                (@UserId, @ExternalSystem, @InputText, @Language, @CreatedAt)
        """;
        
        await dbConnection.ExecuteAsync(sql, unanswered);

        await EnforceRetentionAsync();
    }

    /// <summary>
    /// Queries unanswered questions for analytics/review, supports filtering by system and date.
    /// </summary>
    public async Task<IReadOnlyList<UnansweredQuestion>> GetUnansweredFaqQueriesAsync(string externalSystem, DateTime? from, DateTime? to)
    {
        const string sql = """
                                   SELECT Id, UserId, ExternalSystem, InputText, Language, CreatedAt
                                   FROM FaqQueryLogs
                                   WHERE (@externalSystem IS NULL OR ExternalSystem = @externalSystem)
                                     AND (@from IS NULL OR CreatedAt >= @from)
                                     AND (@to IS NULL OR CreatedAt <= @to)
                                     AND FaqHit = 0
                                   ORDER BY CreatedAt DESC
                           """;

        var result = await dbConnection.QueryAsync<UnansweredQuestion>(sql, new { externalSystem, from, to });

        return result.ToList().AsReadOnly();
    }

    /// <summary>
    /// Saves a single FAQ query log entry to the database for FAQ hit/miss analytics.
    /// </summary>
    /// <param name="faqQueryLogEntry">The log entry for the FAQ lookup attempt.</param>
    public async Task SaveFaqQueryLogAsync(FaqQueryLogEntry faqQueryLogEntry)
    {
        if (!analyticsOptions.Value.Enabled)
            return;

        const string sql = """
                               INSERT INTO FaqQueryLogs
                                   (UserId, ExternalSystem, InputText, Topic, Language, CreatedAt, FaqHit, FaqId)
                               VALUES
                                   (@UserId, @ExternalSystem, @InputText, @Topic, @Language, @CreatedAt, @FaqHit, @FaqId)
                           """;

        await dbConnection.ExecuteAsync(sql, faqQueryLogEntry);

        await EnforceRetentionAsync();
    }

    /// <summary>
    /// Returns FAQ hit/miss metrics grouped by topic in a flat form suitable for the chart.
    /// Each topic will emit two rows: one for answered and one for not answered.
    /// Topics that are null or empty are normalized to "Not Answered" (single canonical bucket).
    /// </summary>
    public async Task<IList<FaqStatsMetric>> GetFaqStatsAsync(string? externalSystem, DateTime? from, DateTime? to)
    {
        const string sql = """
                           WITH per_topic AS (
                               SELECT
                                   ISNULL(NULLIF(Topic, ''), '') AS Topic,
                                   SUM(CASE WHEN FaqHit = 1 THEN 1 ELSE 0 END) AS AnsweredFromFaq,
                                   SUM(CASE WHEN FaqHit = 0 THEN 1 ELSE 0 END) AS NotAnswered
                               FROM FaqQueryLogs
                               WHERE (@externalSystem IS NULL OR ExternalSystem = @externalSystem)
                                 AND (@from IS NULL OR CreatedAt >= @from)
                                 AND (@to IS NULL OR CreatedAt <= @to)
                               GROUP BY ISNULL(NULLIF(Topic, ''), '')
                           )
                           SELECT Topic, Metric, Count FROM (
                               SELECT Topic, 'Answered' AS Metric, AnsweredFromFaq AS Count FROM per_topic
                               WHERE AnsweredFromFaq > 0
                               --UNION ALL
                               --SELECT Topic, 'NotAnswered' AS Metric, SUM(NotAnswered) AS Count FROM per_topic
                               --WHERE NotAnswered > 0 AND NULLIF(Topic, '') <> ''
                           	   --GROUP BY Topic
                               UNION ALL
                               SELECT '', 'NotAnswered' AS Metric, SUM(NotAnswered) AS Count FROM per_topic
                               WHERE NotAnswered > 0
                           ) AS combined
                           ORDER BY 
                               CASE WHEN Topic = '' THEN 1 ELSE 0 END,
                               Topic,
                               CASE 
                                   WHEN Metric = 'Answered' THEN 1
                                   WHEN Metric = 'NotAnswered' THEN 2
                                   ELSE 3
                               END;
                           """;

        var stats = await dbConnection.QueryAsync<FaqStatsMetric>(sql, new { externalSystem, from, to });

        return stats.ToList();
    }

    private async Task EnforceRetentionAsync()
    {
        int days = analyticsOptions.Value.RetentionDays;

        if (days <= 0) 
            return;

        const string sql = """
                               DECLARE @cutoff DATETIME2 = DATEADD(DAY, -@days, SYSUTCDATETIME());
                               DELETE FROM IntentLogs WHERE CreatedAt < @cutoff;
                               DELETE FROM FaqQueryLogs WHERE CreatedAt < @cutoff;
                           """;

        await dbConnection.ExecuteAsync(sql, new { days });
    }

}
