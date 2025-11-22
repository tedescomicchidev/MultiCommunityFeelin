using System.Globalization;
using System.Text;
using System.Text.Json;
using AgentOrchestration.Infrastructure;
using AgentOrchestration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentOrchestration.Services;

public sealed class ReportWriter
{
    private readonly ReportOptions _options;
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriter(IOptions<ReportOptions> options, ILogger<ReportWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task WriteReportAsync(WeeklyReport report, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.OutputDirectory);
        var jsonPath = Path.Combine(_options.OutputDirectory, $"weekly-report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);

        var csvPath = Path.Combine(_options.OutputDirectory, $"weekly-report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
        await File.WriteAllTextAsync(csvPath, BuildCsv(report), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Persisted weekly report to {JsonPath} and {CsvPath}", jsonPath, csvPath);
    }

    private static string BuildCsv(WeeklyReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Title,Url,PublishedDate,Author,Worker1Score,Worker2Score,ValidatedScore,AnalysisNotes,ValidatorComments,CorrelationId");
        foreach (var item in report.Items)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(item.Title),
                Escape(item.Url),
                item.PublishedDate.ToString("O", CultureInfo.InvariantCulture),
                Escape(item.Author ?? string.Empty),
                item.Worker1Score.ToString(CultureInfo.InvariantCulture),
                item.Worker2Score.ToString(CultureInfo.InvariantCulture),
                item.ValidatedScore.ToString(CultureInfo.InvariantCulture),
                Escape(item.AnalysisNotes),
                Escape(item.ValidatorComments),
                item.CorrelationId
            }));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return $"\"{value.Replace("\"", "'" )}\"";
    }
}
