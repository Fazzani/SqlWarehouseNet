# Copilot Instructions — SqlWarehouseNet

## Project Overview

SqlWarehouseNet (`dbsql`) is a high-performance .NET 10 interactive CLI for Azure Databricks SQL Warehouses. It uses the Databricks SQL Statement Execution API with Apache Arrow streaming.

## Tech Stack

- **Language**: C# 12, .NET 10
- **UI**: Spectre.Console for rich terminal output
- **Data**: Apache.Arrow for binary columnar data
- **Serialization**: System.Text.Json with source generation (`JsonContext`)
- **Distribution**: NuGet global tool (`up.DbSql`)

## Architecture Conventions

- **Program.cs** is a slim entry point — all logic lives in `App.cs`
- **Services** are interface-driven (`IDatabricksService`, `IProfileService`, `IExportService`)
- **CLI/** contains user interaction: `CommandProcessor` (dispatch) and `InteractiveShell` (input/history)
- **Models/** holds DTOs, connection state, and JSON source generation context

## Coding Standards

- Use `nullable` reference types everywhere (`<Nullable>enable</Nullable>`)
- Prefer `sealed` classes unless inheritance is needed
- Use `CancellationToken` for all async operations — graceful shutdown via `Ctrl+C`
- Use `[GeneratedRegex]` for compile-time regex (no reflection)
- Use JSON source generation (`[JsonSerializable]`) — no runtime reflection for serialization
- Validate SQL identifiers with `SafeIdentifierRegex` to prevent SQL injection
- Dispose resources properly — `IDisposable` pattern on `App` and services
- Use `Channel<T>` for streaming results between producer/consumer

## Error Handling

- Wrap API calls in `try/catch` with user-friendly `AnsiConsole.MarkupLine` error messages
- Use exponential backoff for API polling
- Never expose raw tokens — mask sensitive input

## Testing

- Build with: `dotnet build SqlWarehouseNet/SqlWarehouseNet.csproj`
- Run with: `dotnet run --project SqlWarehouseNet`
- Package with: `dotnet pack -c Release`

## CLI Commands Reference

Commands start with `/`: `/profile`, `/schemas`, `/tables`, `/rows`, `/export`, `/clear`, `/help`, `/exit`
SQL queries are entered directly and executed with Ctrl+Enter.
