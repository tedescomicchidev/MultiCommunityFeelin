# MultiCommunityFeelin

Production-ready .NET 8 multi-agent service that retrieves Microsoft Azure AI Foundry Tech Community posts for the current week, runs two independent sentiment scorers, validates results, and exports a weekly report (JSON + CSV). Agents communicate over durable queues (Azure Storage queues) or fast in-memory channels for local development.

## Architecture

- **Orchestrator** (see `Services/OrchestratorAgent.cs`): pulls the latest Tech Community content, normalizes metadata, and fans out work to two sentiment worker queues.
- **Worker 1 & Worker 2** (see `Services/SentimentWorkerAgent.cs`): independently score sentiment on a 1–10 scale using the shared `SentimentAnalyzer` while capturing short emotional notes. Workers are horizontally scalable—add more instances on the same queue.
- **Validator** (see `Services/ValidatorAgent.cs`): collects worker outputs, detects disagreements, normalizes scores, and produces the final weekly dataset.
- **Message transport** (see `Infrastructure/IMessageBus.cs`): pluggable transport with in-memory channels for development and Azure Storage queues for durability in production.
- **Report writer** (see `Services/ReportWriter.cs`): persists JSON and CSV snapshots to a configurable directory.

## Project layout

```
src/AgentOrchestration/
  AgentOrchestration.csproj
  Program.cs                // Host builder & DI wiring
  appsettings.json          // Sample configuration
  Infrastructure/           // Messaging and options
  Models/                   // Data contracts for posts, scores, reports
  Services/                 // Agent implementations and utilities
```

## Running locally

1. Ensure .NET 8 SDK is installed.
2. From the repository root run:
   ```bash
   dotnet run --project src/AgentOrchestration/AgentOrchestration.csproj
   ```
3. Inspect the generated JSON/CSV files under `src/AgentOrchestration/output/`.

### Production transport (Azure Storage queues)
Set the connection string and disable in-memory transport in `appsettings.json` or environment variables:
```json
"AgentRuntime": {
  "UseInMemoryTransport": false,
  "QueueConnectionString": "<AzureStorageConnectionString>",
  "Worker1QueueName": "worker1",
  "Worker2QueueName": "worker2",
  "ValidationQueueName": "validation"
}
```
The service will automatically create queues if they do not exist.

## Design notes

- **Batching and durability:** Agents communicate exclusively through queues/channels so worker instances can scale out independently.
- **Error handling:** Each agent catches and logs failures without stopping the pipeline; Azure queue transport retries on transient errors.
- **Normalization:** Validator averages worker scores when deviations are large and annotates the rationale in `validatorComments`.
- **Extensibility:** Swap `SentimentAnalyzer` with an Azure OpenAI or Agent Framework plug-in while keeping the orchestration contract intact.

## Output schema

```json
{
  "analysisWeek": "2024-06-10 to 2024-06-16",
  "items": [
    {
      "title": "",
      "url": "",
      "publishedDate": "",
      "sentimentScore": 0,
      "analysisNotes": "",
      "validatedScore": 0,
      "validatorComments": ""
    }
  ]
}
```

The generated CSV mirrors the JSON fields and includes the correlation id used for message tracking.
