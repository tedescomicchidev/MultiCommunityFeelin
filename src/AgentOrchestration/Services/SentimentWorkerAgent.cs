using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
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
        await foreach (var workItem in _bus.SubscribeAsync<SentimentWorkItem>(_queueName, cancellationToken))
        {
            try
            {
                if (workItem.Post is null)
                {
                    _logger.LogWarning("{Worker} received an empty work item", _name);
                    continue;
                }

                var result = await _analyzer.ScoreAsync(workItem, cancellationToken).ConfigureAwait(false);
                await _bus.PublishAsync(_validationQueue, result, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Worker} completed sentiment for {Title} with score {Score}", _name, workItem.Post.Title, result.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} failed to process work item {Correlation}", _name, workItem.Post?.CorrelationId);
            }
        }
    }
}
