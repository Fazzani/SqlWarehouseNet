# SqlWarehouseNet - Azure Databricks SQL Warehouse Client

[![CI/CD](https://github.com/fazza/SqlWarehouse-net/actions/workflows/ci.yml/badge.svg)](https://github.com/fazza/SqlWarehouse-net/actions/workflows/ci.yml)

A high-performance .NET 10 interactive CLI for executing SQL queries against Azure Databricks using the Databricks SQL Statement Execution API and Apache Arrow.

## 🚀 Features

- **Interactive Loop**: Persistent shell with query history and multi-line support.
- **Apache Arrow Support**: Fast, binary data retrieval using the `ARROW_STREAM` format and `EXTERNAL_LINKS`.
- **Intelligent Auto-Complete**: Context-aware SQL keyword suggestions and command completion.
- **Advanced UI**:
    - Beautiful ASCII tables with `Spectre.Console`.
    - Real-time progress bars for polling and data downloads.
    - Multi-line query editing (Enter for newline, **Ctrl+Enter** to execute).
    - Streaming results display (show data as it downloads, not after).
- **Profile Management**: Manage multiple Databricks workspaces/warehouses with `/profile` commands.
- **Secure Token Handling**: Tokens are prompted interactively (masked input) — never passed as visible CLI arguments.
- **Data Export**: Export results to **CSV** or **JSON** directly from the CLI.
- **Configurable Display**: Adjust the max rows shown with `/rows <n>` (default: 50).
- **Query History**: Persistent history (capped at 500 entries) with ↑/↓ navigation (no duplicates).
- **Graceful Shutdown**: Ctrl+C and Escape trigger clean resource disposal via `CancellationToken`.
- **Performance**: High-speed serialization using .NET 10 JSON Source Generation, parallel downloads, and exponential backoff polling.

## 🏗 Architecture

```text
SqlWarehouseNet/
├── Program.cs              # Slim entry point — bootstraps services
├── App.cs                  # Main application loop & lifecycle management
├── CLI/
│   ├── CommandProcessor.cs # Command dispatch, SQL injection validation, display
│   └── InteractiveShell.cs # Multi-line input, history, auto-complete
├── Models/
│   ├── ConnectionState.cs  # Mutable connection state (replaces ref params)
│   ├── Models.cs           # API request/response DTOs
│   └── JsonContext.cs      # AOT-safe JSON source generation
└── Services/
    ├── IDatabricksService.cs   # Interface for Databricks API
    ├── IProfileService.cs      # Interface for profile/config management
    ├── IExportService.cs       # Interface for data export
    ├── DatabricksService.cs    # HTTP client, polling, Arrow streaming
    ├── ProfileService.cs       # Profile, history, cache persistence
    ├── ExportService.cs        # CSV/JSON export
    ├── ArrowUtils.cs           # Apache Arrow type extraction (no reflection)
    └── SqlCompletionService.cs # SQL keyword & table auto-complete
```

## 🛠 Prerequisites

- .NET 10 SDK
- Azure Databricks SQL Warehouse
- Databricks Personal Access Token

## ⚙️ Configuration

You can configure the client using environment variables or the built-in profile manager.

### Profile Manager (Recommended)

Launch the application and use the `/profile` command. The token is prompted securely (masked input):

```bash
/profile add MyProd https://adb-xxx.azuredatabricks.net <warehouse_id>
# → You will be prompted: "Enter access token: ****"
/profile use MyProd
```

### Environment Variables

Alternatively, set these variables:

- `DATABRICKS_HOST`: Your workspace URL.
- `DATABRICKS_TOKEN`: Your Personal Access Token.
- `DATABRICKS_WAREHOUSE_ID`: The ID of your SQL Warehouse.

## 📦 Installation (Global Tool)

You can install this CLI as a .NET Global Tool to use it anywhere in your terminal.

```powershell
# Automated setup (pack + install)
.\setup-tool.ps1
```

Manually:

```bash
dotnet pack SqlWarehouseNet -c Release --output ./nupkg
dotnet tool install -g up.DbSql --add-source ./nupkg
```

### Publishing to NuGet.org

The CI/CD pipeline publishes automatically when you push a version tag:

```bash
git tag v1.1.0
git push origin v1.1.0
```

Requires the `NUGET_API_KEY` secret configured in your GitHub repository settings.

## 📖 Usage

### Launching the CLI

Une fois installé, lancez simplement :

```bash
dbsql
```

_(Vous pouvez toujours utiliser `dotnet run --project SqlWarehouseNet` pour le développement)_

### Keyboard Shortcuts

| Shortcut            | Description                                                   |
| ------------------- | ------------------------------------------------------------- |
| **Ctrl+Enter**      | Execute current SQL query (multi-line supported)              |
| **Tab**             | Auto-complete SQL keywords or commands (context-aware)        |
| **Ctrl+Backspace**  | Delete entire word (like word boundary deletion in terminals) |
| **↑ / ↓**           | Navigate query history (previous/next)                        |
| **Home / End**      | Move cursor to start / end of line                            |
| **← / →**           | Move cursor left / right                                      |
| **Ctrl+← / Ctrl+→** | Jump to previous / next word                                  |
| **Escape**          | Exit application (graceful shutdown)                          |
| **Ctrl+C**          | Cancel current operation / exit                               |

### Auto-Complete Examples

```text
Type "SEL" → Press Tab → Completes to "SELECT"
Type "SELECT * FRO" → Press Tab → Completes to "FROM"
Type "WHERE x = 5 AND" → Press Tab → Proposes "AND", "OR"
Type "/exp" → Press Tab → Completes to "/export "
```

### CLI Commands

```text
/catalogs                             List all catalogs
/schemas [catalog]                    List schemas (optional: in specific catalog)
/tables [schema]                      List tables (optional: in specific schema)
/export csv [path]                    Export last results to CSV file
/export json [path]                   Export last results to JSON file
/rows <n>                             Set the max display rows (default: 50)
/profile list                         List all saved connection profiles
/profile use <name>                   Switch to a specific profile
/profile add <name> <host> <wid>      Add a new profile (token prompted securely)
/profile delete <name>                Delete a connection profile
/profile edit                         Open profiles JSON in default editor
/clear                                Clear the terminal screen
/help                                 Show command reference
/quit or /q                           Exit the application
```

## 🔧 Advanced Features

### Streaming Results Display

Results are displayed progressively as data downloads complete, with a progress bar showing download status. The table is rendered once after all visible rows are accumulated to prevent double-display artifacts.

### Multi-Line Query Support

Press **Enter** to add a new line, and **Ctrl+Enter** to execute:

```sql
SQL> SELECT column1, column2
     FROM my_table
     WHERE condition = true
[Ctrl+Enter to execute]
```

### History Management

- Query history is automatically saved to `.sqlwarehouse_history`
- Navigate with ↑/↓ arrows
- Duplicate consecutive queries are prevented
- History is capped at **500 entries** and auto-trimmed on load
- History is case-sensitive and ordered by time

### Security

- **Token masking**: `/profile add` prompts for the token interactively using masked input — tokens never appear in shell history or process arguments.
- **SQL injection prevention**: `/schemas` and `/tables` commands validate identifiers against a strict `^[\w][\w.]*$` regex before interpolation.
- **Graceful shutdown**: Escape and Ctrl+C properly dispose all resources (HttpClient, CancellationTokenSource) instead of calling `Environment.Exit`.

### Performance Optimizations

1. **Parallel Downloads**: Up to 8 concurrent chunk downloads with ordered result reassembly
2. **Binary Format**: Apache Arrow EXTERNAL_LINKS reduces network bandwidth
3. **LZ4 Compression**: Enabled via `X-Databricks-Result-Compression` header
4. **Exponential Backoff Polling**: Starts at 500ms, scales up to 10s (reduces API load)
5. **Connection Safety**: HttpClient is recreated on profile switch (BaseAddress is immutable after first request)
6. **AOT-Safe Serialization**: All JSON uses source-generated `JsonContext` — no runtime reflection
7. **Smart Display**: Configurable row limit via `/rows <n>`; table rendered once after accumulation
8. **Compiled Regex**: `[GeneratedRegex]` used for SQL parsing and identifier validation

## 📊 Performance

By using **Apache Arrow**, this client avoids the overhead of JSON parsing for large datasets, allowing for significantly faster data retrieval and lower memory usage compared to standard text-based APIs.

## 📁 Project Structure

| File                               | Description                                                        |
| ---------------------------------- | ------------------------------------------------------------------ |
| `Program.cs`                       | Slim entry point — bootstraps services and starts `App`            |
| `App.cs`                           | Main interactive loop, lifecycle, and CancellationToken management |
| `CLI/CommandProcessor.cs`          | Command dispatch, SQL injection validation, table display          |
| `CLI/InteractiveShell.cs`          | Multi-line input with history and auto-complete                    |
| `Models/ConnectionState.cs`        | Encapsulated mutable connection state                              |
| `Models/Models.cs`                 | Databricks API DTOs and custom exception                           |
| `Models/JsonContext.cs`            | AOT-safe JSON source generation context                            |
| `Services/IDatabricksService.cs`   | Interface for testability                                          |
| `Services/IProfileService.cs`      | Interface for testability                                          |
| `Services/IExportService.cs`       | Interface for testability                                          |
| `Services/DatabricksService.cs`    | HTTP calls, polling with backoff, Arrow streaming                  |
| `Services/ProfileService.cs`       | Profile, history, and cache persistence                            |
| `Services/ExportService.cs`        | CSV and JSON export                                                |
| `Services/ArrowUtils.cs`           | Arrow type extraction (20+ types, no reflection)                   |
| `Services/SqlCompletionService.cs` | SQL keyword and table auto-complete                                |

## 📋 Testing

Test the CLI with this sample query:

```sql
SELECT * FROM your_schema.your_table LIMIT 10
```

Type `/help` inside the application to see all available shortcuts and commands.

## 🛡 License

This project is licensed under the MIT License.
