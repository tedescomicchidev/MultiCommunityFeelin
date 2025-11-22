namespace AgentOrchestration.Models;

public record SentimentResult(
    string CorrelationId,
    string WorkerName,
    int Score,
    string AnalysisNotes,
    double? Confidence,
    DateTimeOffset CompletedAt);
