using AgentOrchestration.Infrastructure;
using AgentOrchestration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging(options => options.AddConsole());
        services.Configure<CommunitySourceOptions>(context.Configuration.GetSection("CommunitySource"));
        services.Configure<AgentRuntimeOptions>(context.Configuration.GetSection("AgentRuntime"));
        services.Configure<ReportOptions>(context.Configuration.GetSection("Report"));
        services.AddHttpClient();
        services.AddHttpClient<TechCommunityClient>()
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<IMessageBusFactory, MessageBusFactory>();
        services.AddSingleton<TechCommunityClient>();
        services.AddSingleton<ReportWriter>();
        services.AddSingleton<SentimentAnalyzer>();

        services.AddSingleton<AgentCoordinator>();
        services.AddHostedService<AgentRuntimeService>();
    })
    .Build();

await host.RunAsync();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => (int)msg.StatusCode == 429)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
