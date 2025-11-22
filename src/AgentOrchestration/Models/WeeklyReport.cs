namespace AgentOrchestration.Models;

public record WeeklyReport(
    string AnalysisWeek,
    IReadOnlyList<ValidatedPost> Items,
    DateTimeOffset GeneratedAt);
