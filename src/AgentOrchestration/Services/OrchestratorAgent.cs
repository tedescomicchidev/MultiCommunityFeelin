using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Agents;
using Microsoft.Extensions.Logging;

namespace AgentOrchestration.Services;

public sealed class OrchestratorAgent
{
    private readonly IMessageBus _bus;
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger _logger;

    public OrchestratorAgent(IMessageBus bus, AgentRuntimeOptions options, ILogger logger)
    {
        _bus = bus;
        _options = options;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyList<CommunityPost> posts, CancellationToken cancellationToken)
    {
        foreach (var post in posts)
        {
            var workItem1 = new SentimentWorkItem(post, "worker1", DateTimeOffset.UtcNow);
            var workItem2 = new SentimentWorkItem(post, "worker2", DateTimeOffset.UtcNow);

            var message1 = AgentMessage.Create("orchestrator", _options.Worker1QueueName, workItem1, correlationId: post.CorrelationId);
            var message2 = AgentMessage.Create("orchestrator", _options.Worker2QueueName, workItem2, correlationId: post.CorrelationId);

            await _bus.PublishAsync(message1, cancellationToken).ConfigureAwait(false);
            await _bus.PublishAsync(message2, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Dispatched sentiment job for {Title} ({Correlation})", post.Title, post.CorrelationId);
        }
    }
}
