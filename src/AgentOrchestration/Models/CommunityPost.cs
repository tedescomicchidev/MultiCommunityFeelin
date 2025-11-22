using System.Text.Json.Serialization;

namespace AgentOrchestration.Models;

public record CommunityPost(
    string Title,
    string Url,
    DateTimeOffset PublishedDate,
    string Body,
    string? Author)
{
    [JsonIgnore]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}
