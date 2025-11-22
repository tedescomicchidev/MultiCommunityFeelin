using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Agents;
using Microsoft.Extensions.Logging;

namespace AgentOrchestration.Services;

public sealed class ValidatorAgent
{
    private readonly IMessageBus _bus;
    private readonly IReadOnlyList<CommunityPost> _posts;
    private readonly ReportWriter _writer;
    private readonly ILogger _logger;
    private readonly string _validationQueue;

    public ValidatorAgent(IMessageBus bus, IReadOnlyList<CommunityPost> posts, ReportWriter writer, ILogger logger, string validationQueue)
    {
        _bus = bus;
        _posts = posts;
        _writer = writer;
        _logger = logger;
        _validationQueue = validationQueue;
    }

    public async Task<WeeklyReport> RunAsync(int expectedResultCount, CancellationToken cancellationToken)
    {
        var cache = _posts.ToDictionary(p => p.CorrelationId);
        var buckets = new Dictionary<string, List<SentimentResult>>();
        var validated = new List<ValidatedPost>();

        await foreach (var message in _bus.SubscribeAsync<AgentMessage<SentimentResult>>(_validationQueue, cancellationToken))
        {
            var result = message.Payload;
            if (!cache.ContainsKey(result.CorrelationId))
            {
                _logger.LogWarning("Validator received unknown correlation {Correlation}", result.CorrelationId);
                continue;
            }

            if (!buckets.TryGetValue(result.CorrelationId, out var list))
            {
                list = new List<SentimentResult>();
                buckets[result.CorrelationId] = list;
            }

            list.Add(result);
            _logger.LogInformation("Validator observed sentiment result {Index}/{Expected} for correlation {Correlation}", list.Count, 2, result.CorrelationId);

            if (list.Count == 2)
            {
                var post = cache[result.CorrelationId];
                var ordered = list.OrderBy(x => x.CompletedAt).ToList();
                var normalized = NormalizeScores(ordered);
                var notes = string.Join(" | ", ordered.Select(o => $"{o.WorkerName}:{o.AnalysisNotes}"));
                validated.Add(new ValidatedPost(
                    post.Title,
                    post.Url,
                    post.PublishedDate,
                    post.Author,
                    ordered[0].Score,
                    ordered[1].Score,
                    normalized,
                    notes,
                    BuildComments(ordered, normalized),
                    result.CorrelationId));
            }

            if (validated.Count == _posts.Count)
            {
                break;
            }

            if (buckets.Sum(b => b.Value.Count) >= expectedResultCount)
            {
                _logger.LogWarning("Validator reached expected results without validating all posts. Remaining {Remaining}", _posts.Count - validated.Count);
                break;
            }
        }

        var report = new WeeklyReport(WeekWindow.CurrentWeekLabel(DateTimeOffset.UtcNow), validated, DateTimeOffset.UtcNow);
        await _writer.WriteReportAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private static int NormalizeScores(IReadOnlyList<SentimentResult> results)
    {
        var average = results.Average(r => r.Score);
        var deviation = Math.Abs(results[0].Score - results[1].Score);
        if (deviation >= 3)
        {
            return (int)Math.Round(average);
        }

        return Math.Clamp((int)Math.Round(average), 1, 10);
    }

    private static string BuildComments(IReadOnlyList<SentimentResult> results, int normalizedScore)
    {
        var deviation = Math.Abs(results[0].Score - results[1].Score);
        if (deviation == 0)
        {
            return "Scores were consistent across workers.";
        }

        return $"Normalized scores by averaging due to deviation of {deviation}; final={normalizedScore}.";
    }
}
