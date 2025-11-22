using System;

namespace Microsoft.Agents;

/// <summary>
/// Lightweight message envelope aligned with Microsoft.Agents semantics for agent-to-agent delivery.
/// </summary>
/// <typeparam name="T">Payload type carried between agents.</typeparam>
public sealed record AgentMessage<T>(string From, string To, T Payload, string? TraceId = null, string? CorrelationId = null)
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public static class AgentMessage
{
    public static AgentMessage<T> Create<T>(string from, string to, T payload, string? traceId = null, string? correlationId = null)
        => new(from, to, payload, traceId, correlationId ?? Guid.NewGuid().ToString("N"));
}
