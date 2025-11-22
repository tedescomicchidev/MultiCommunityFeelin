namespace AgentOrchestration.Models;

public record ValidatedPost(
    string Title,
    string Url,
    DateTimeOffset PublishedDate,
    string? Author,
    int Worker1Score,
    int Worker2Score,
    int ValidatedScore,
    string AnalysisNotes,
    string ValidatorComments,
    string CorrelationId);
