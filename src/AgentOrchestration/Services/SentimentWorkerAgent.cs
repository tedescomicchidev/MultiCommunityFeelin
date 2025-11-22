using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Agents;
using Microsoft.Extensions.Logging;

namespace AgentOrchestration.Services;

public sealed class SentimentWorkerAgent
{
    private readonly string _name;
    private readonly string _queueName;
    private readonly IMessageBus _bus;
    private readonly SentimentAnalyzer _analyzer;
    private readonly ILogger _logger;
    private readonly string _validationQueue;

    public SentimentWorkerAgent(string name, string queueName, IMessageBus bus, SentimentAnalyzer analyzer, ILogger logger, string validationQueue = "validation")
    {
        _name = name;
        _queueName = queueName;
        _bus = bus;
        _analyzer = analyzer;
        _logger = logger;
        _validationQueue = validationQueue;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _bus.SubscribeAsync<AgentMessage<SentimentWorkItem>>(_queueName, cancellationToken))
        {
            try
            {
                if (message.Payload.Post is null)
                {
                    _logger.LogWarning("{Worker} received an empty work item", _name);
                    continue;
                }

                var result = await _analyzer.ScoreAsync(message.Payload, cancellationToken).ConfigureAwait(false);
                var outbound = AgentMessage.Create(_name, _validationQueue, result, message.TraceId, message.CorrelationId);
                await _bus.PublishAsync(outbound, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Worker} completed sentiment for {Title} with score {Score}", _name, message.Payload.Post.Title, result.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} failed to process work item {Correlation}", _name, message.Payload.Post?.CorrelationId);
            }
        }
    }
}
