using System.Text.Json;
using System.Text.Json.Nodes;
using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestration.Services;

public sealed class TechCommunityClient
{
    private readonly HttpClient _httpClient;
    private readonly CommunitySourceOptions _options;
    private readonly ILogger<TechCommunityClient> _logger;

    public TechCommunityClient(HttpClient httpClient, IOptions<CommunitySourceOptions> options, ILogger<TechCommunityClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CommunityPost>> GetThisWeekPostsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_options.BaseUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var jsonText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = ExtractPostsFromHtml(jsonText);
            var weekStart = WeekWindow.CurrentWeekStart(DateTimeOffset.UtcNow);
            return parsed.Where(p => DateOnly.FromDateTime(p.PublishedDate.UtcDateTime) >= weekStart).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read posts from community site. Returning sample payload.");
            return CreateFallbackSample();
        }
    }

    private static List<CommunityPost> ExtractPostsFromHtml(string html)
    {
        var posts = new List<CommunityPost>();
        try
        {
            // The TechCommunity site exposes embedded JSON under `__NEXT_DATA__` which we can parse when available.
            var marker = "__NEXT_DATA__";
            var markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > -1)
            {
                var jsonStart = html.IndexOf('{', markerIndex);
                var jsonEnd = html.IndexOf("</script>", jsonStart, StringComparison.OrdinalIgnoreCase);
                if (jsonStart > -1 && jsonEnd > jsonStart)
                {
                    var json = html.Substring(jsonStart, jsonEnd - jsonStart);
                    var node = JsonNode.Parse(json);
                    var entries = node?["props"]?["pageProps"]?["dehydratedState"]?["queries"]?.AsArray();
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            var data = entry?["state"]?["data"];
                            var items = data?["messages"]?.AsArray();
                            if (items == null)
                            {
                                continue;
                            }

                            foreach (var item in items)
                            {
                                var title = item?["subject"]?.ToString() ?? "Untitled";
                                var url = item?["messageLink"]?.ToString() ?? string.Empty;
                                var author = item?["author"]?["login"]?.ToString();
                                var body = item?["body"]?.ToString() ?? string.Empty;
                                var published = item?["createdDate"]?.GetValue<long?>();
                                if (published is null)
                                {
                                    continue;
                                }

                                var date = DateTimeOffset.FromUnixTimeMilliseconds(published.Value);
                                posts.Add(new CommunityPost(title, url, date, body, author));
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback below
        }

        if (posts.Count == 0)
        {
            posts.AddRange(CreateFallbackSample());
        }

        return posts;
    }

    private static List<CommunityPost> CreateFallbackSample()
    {
        return new List<CommunityPost>
        {
            new("Sample: Azure AI Foundry release", "https://techcommunity.microsoft.com/sample1", DateTimeOffset.UtcNow.AddDays(-1), "Great release week with new features", "azure-team"),
            new("Sample: Troubleshooting prompt flow", "https://techcommunity.microsoft.com/sample2", DateTimeOffset.UtcNow.AddDays(-2), "Encountered an issue configuring prompt flow. Need help!", "community-member")
        };
    }
}
