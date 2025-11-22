using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentOrchestration.Services;

public sealed class AgentRuntimeService : BackgroundService
{
    private readonly AgentCoordinator _coordinator;
    private readonly ILogger<AgentRuntimeService> _logger;

    public AgentRuntimeService(AgentCoordinator coordinator, ILogger<AgentRuntimeService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent runtime starting at {Time}", DateTimeOffset.UtcNow);
        try
        {
            await _coordinator.RunAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Agent runtime completed successfully at {Time}", DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Agent runtime cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent runtime encountered an error.");
            throw;
        }
    }
}
