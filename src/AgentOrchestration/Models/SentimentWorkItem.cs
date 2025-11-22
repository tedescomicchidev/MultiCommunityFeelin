namespace AgentOrchestration.Models;

public record SentimentWorkItem(
    CommunityPost Post,
    string WorkerName,
    DateTimeOffset RequestedAt);
