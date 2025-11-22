using System;
using Microsoft.Agents;

namespace AgentOrchestration.Infrastructure;

public static class AgentMessagingExtensions
{
    public static ValueTask PublishAsync<T>(this IMessageBus bus, AgentMessage<T> message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.To);
        return bus.PublishAsync(message.To, message, cancellationToken);
    }
}
