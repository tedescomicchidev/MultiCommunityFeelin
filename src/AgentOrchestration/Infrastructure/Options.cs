namespace AgentOrchestration.Infrastructure;

public sealed class CommunitySourceOptions
{
    public string BaseUrl { get; set; } = "https://techcommunity.microsoft.com/category/azure-ai-foundry";
    public int DefaultPageSize { get; set; } = 50;
}

public sealed class AgentRuntimeOptions
{
    public bool UseInMemoryTransport { get; set; } = true;
    public string? QueueConnectionString { get; set; }
    public string Worker1QueueName { get; set; } = "worker1";
    public string Worker2QueueName { get; set; } = "worker2";
    public string ValidationQueueName { get; set; } = "validation";
}

public sealed class ReportOptions
{
    public string OutputDirectory { get; set; } = "./output";
}
