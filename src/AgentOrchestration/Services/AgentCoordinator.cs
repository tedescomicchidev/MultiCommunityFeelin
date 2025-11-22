using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestration.Services;

public sealed class AgentCoordinator
{
    private readonly IMessageBusFactory _busFactory;
    private readonly TechCommunityClient _communityClient;
    private readonly SentimentAnalyzer _analyzer;
    private readonly ReportWriter _reportWriter;
    private readonly IOptions<AgentRuntimeOptions> _runtimeOptions;
    private readonly ILogger<AgentCoordinator> _logger;

    public AgentCoordinator(
        IMessageBusFactory busFactory,
        TechCommunityClient communityClient,
        SentimentAnalyzer analyzer,
        ReportWriter reportWriter,
        IOptions<AgentRuntimeOptions> runtimeOptions,
        ILogger<AgentCoordinator> logger)
    {
        _busFactory = busFactory;
        _communityClient = communityClient;
        _analyzer = analyzer;
        _reportWriter = reportWriter;
        _runtimeOptions = runtimeOptions;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var bus = _busFactory.Create();
        var posts = await _communityClient.GetThisWeekPostsAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Fetched {Count} posts for the current week", posts.Count);

        if (posts.Count == 0)
        {
            _logger.LogWarning("No posts detected for the current week. Exiting early.");
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var validator = new ValidatorAgent(bus, posts, _reportWriter, _logger, _runtimeOptions.Value.ValidationQueueName);
        var validatorTask = validator.RunAsync(posts.Count * 2, linkedCts.Token);

        var worker1 = new SentimentWorkerAgent("worker1", _runtimeOptions.Value.Worker1QueueName, bus, _analyzer, _logger, _runtimeOptions.Value.ValidationQueueName);
        var worker2 = new SentimentWorkerAgent("worker2", _runtimeOptions.Value.Worker2QueueName, bus, _analyzer, _logger, _runtimeOptions.Value.ValidationQueueName);

        var workerTasks = new[]
        {
            worker1.RunAsync(linkedCts.Token),
            worker2.RunAsync(linkedCts.Token)
        };

        var orchestrator = new OrchestratorAgent(bus, _runtimeOptions.Value, _logger);
        await orchestrator.DispatchAsync(posts, cancellationToken).ConfigureAwait(false);

        var report = await validatorTask.ConfigureAwait(false);
        linkedCts.Cancel();

        try
        {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected when cancelling background loops
        }

        _logger.LogInformation("Validated {Count} posts for week {Week}", report.Items.Count, report.AnalysisWeek);
    }
}
