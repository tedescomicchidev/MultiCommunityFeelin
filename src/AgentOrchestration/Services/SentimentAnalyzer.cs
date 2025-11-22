using AgentOrchestration.Models;
using System.Text.RegularExpressions;

namespace AgentOrchestration.Services;

public sealed class SentimentAnalyzer
{
    // This component can be swapped for a Microsoft Agent Framework planner or Azure OpenAI skill without
    // changing the worker or validator contracts. The lightweight heuristic below enables offline execution.
    private static readonly string[] PositiveKeywords = ["great", "thanks", "awesome", "excellent", "love", "helpful", "success", "resolved"];
    private static readonly string[] NegativeKeywords = ["issue", "problem", "fail", "error", "bug", "blocked", "concern", "confused"];

    public Task<SentimentResult> ScoreAsync(SentimentWorkItem workItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = workItem.Post.Body ?? string.Empty;
        var score = 5;

        var positiveHits = PositiveKeywords.Sum(k => Regex.Matches(body, k, RegexOptions.IgnoreCase).Count);
        var negativeHits = NegativeKeywords.Sum(k => Regex.Matches(body, k, RegexOptions.IgnoreCase).Count);

        score += Math.Min(positiveHits, 5);
        score -= Math.Min(negativeHits, 5);
        score = Math.Clamp(score, 1, 10);

        var sentiment = score switch
        {
            >= 8 => "Highly positive and solution oriented",
            >= 6 => "Optimistic with minor concerns",
            >= 4 => "Neutral or mixed sentiment",
            >= 2 => "Frustrated and seeking help",
            _ => "Negative experience reported"
        };

        var result = new SentimentResult(
            workItem.Post.CorrelationId,
            workItem.WorkerName,
            score,
            sentiment,
            confidence: Math.Min(1, 0.5 + 0.05 * Math.Abs(positiveHits - negativeHits)),
            CompletedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }
}
