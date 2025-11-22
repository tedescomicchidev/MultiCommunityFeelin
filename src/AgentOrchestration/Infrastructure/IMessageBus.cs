using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestration.Infrastructure;

public interface IMessageBus
{
    ValueTask PublishAsync<T>(string channel, T message, CancellationToken cancellationToken);
    IAsyncEnumerable<T> SubscribeAsync<T>(string channel, CancellationToken cancellationToken);
}

public interface IMessageBusFactory
{
    IMessageBus Create();
}

internal sealed class MessageBusFactory : IMessageBusFactory
{
    private readonly IOptions<AgentRuntimeOptions> _options;
    private readonly ILogger<MessageBusFactory> _logger;

    public MessageBusFactory(IOptions<AgentRuntimeOptions> options, ILogger<MessageBusFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IMessageBus Create()
    {
        if (_options.Value.UseInMemoryTransport || string.IsNullOrWhiteSpace(_options.Value.QueueConnectionString))
        {
            _logger.LogInformation("Using in-memory channel transport for agent communication.");
            return new InMemoryMessageBus();
        }

        _logger.LogInformation("Using Azure Storage queues for agent communication.");
        return new AzureQueueMessageBus(_options.Value, _logger);
    }
}

internal sealed class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, Channel<string>> _channels = new();

    public ValueTask PublishAsync<T>(string channel, T message, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(message);
        var writer = _channels.GetOrAdd(channel, _ => Channel.CreateUnbounded<string>()).Writer;
        return writer.WriteAsync(serialized, cancellationToken);
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(string channel, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = _channels.GetOrAdd(channel, _ => Channel.CreateUnbounded<string>()).Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var payload))
            {
                yield return JsonSerializer.Deserialize<T>(payload)!;
            }
        }
    }
}

internal sealed class AzureQueueMessageBus : IMessageBus
{
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger _logger;

    public AzureQueueMessageBus(AgentRuntimeOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask PublishAsync<T>(string channel, T message, CancellationToken cancellationToken)
    {
        var queue = await GetQueueAsync(channel, cancellationToken).ConfigureAwait(false);
        var serialized = JsonSerializer.Serialize(message);
        await queue.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(serialized)), cancellationToken);
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(string channel, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = await GetQueueAsync(channel, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            QueueMessage[] messages = Array.Empty<QueueMessage>();
            try
            {
                messages = await queue.ReceiveMessagesAsync(maxMessages: 16, visibilityTimeout: TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed reading from queue {Queue}", channel);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            if (messages.Length == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

            foreach (var message in messages)
            {
                try
                {
                    var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
                    yield return JsonSerializer.Deserialize<T>(payload)!;
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message from {Queue}", channel);
                }
            }
        }
    }

    private async Task<QueueClient> GetQueueAsync(string channel, CancellationToken cancellationToken)
    {
        var client = new QueueClient(_options.QueueConnectionString, channel);
        await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return client;
    }
}
