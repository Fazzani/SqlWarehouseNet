# SqlWarehouseNet - Azure Databricks SQL Warehouse Client

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
- **Data Export**: Export results to **CSV** or **JSON** directly from the CLI.
- **Query History**: Persistent history with ↑/↓ navigation (no duplicates).
- **Performance**: High-speed serialization using .NET 10 JSON Source Generation and parallel downloads.

## 🛠 Prerequisites

- .NET 10 SDK
- Azure Databricks SQL Warehouse
- Databricks Personal Access Token

## ⚙️ Configuration

You can configure the client using environment variables or the built-in profile manager.

### Profile Manager (Recommended)

Launch the application and use the `/profile` command:

```bash
/profile add MyProd https://adb-xxx.azuredatabricks.net <token> <warehouse_id>
/profile use MyProd
```

### Environment Variables

Alternatively, set these variables:

- `DATABRICKS_HOST`: Your workspace URL.
- `DATABRICKS_TOKEN`: Your Personal Access Token.
- `DATABRICKS_WAREHOUSE_ID`: The ID of your SQL Warehouse.

## � Installation (Global Tool)

You can install this CLI as a .NET Global Tool to use it anywhere in your terminal.

```powershell
# Via le script d'automatisation
.\setup-tool.ps1
```

Ou manuellement :

```bash
dotnet pack SqlWarehouseNet -c Release
dotnet tool install -g up.DbSql --add-source ./nupkg
```

## 📖 Usage

### Launching the CLI

Une fois installé, lancez simplement :

```bash
dbsql
```

_(Vous pouvez toujours utiliser `dotnet run --project SqlWarehouseNet` pour le développement)_

### Keyboard Shortcuts

| Shortcut           | Description                                                   |
| ------------------ | ------------------------------------------------------------- |
| **Ctrl+Enter**     | Execute current SQL query (multi-line supported)              |
| **Tab**            | Auto-complete SQL keywords or commands (context-aware)        |
| **Ctrl+Backspace** | Delete entire word (like word boundary deletion in terminals) |
| **↑ / ↓**          | Navigate query history (previous/next)                        |
| **Home / End**     | Move cursor to start / end of line                            |
| **← / →**          | Move cursor left / right                                      |
| **Escape**         | Exit application                                              |

### Auto-Complete Examples

```text
Type "SEL" → Press Tab → Completes to "SELECT"
Type "SELECT * FRO" → Press Tab → Completes to "FROM"
Type "WHERE x = 5 AND" → Press Tab → Proposes "AND", "OR"
Type "/exp" → Press Tab → Completes to "/export "
```

### CLI Commands

```text
/export csv [path]                    Export last results to CSV file
/export json [path]                   Export last results to JSON file
/profile list                         List all saved connection profiles
/profile use <name>                   Switch to a specific profile
/profile add <name> <host> <token> <wid>  Add a new connection profile
/profile delete <name>                Delete a connection profile
/clear                                Clear the terminal screen
/help                                 Show command reference
/quit or /q                           Exit the application
```

## 🔧 Advanced Features

### Streaming Results Display

Results are displayed progressively as data downloads complete, with a progress bar showing download status. The first 50 rows are shown to prevent terminal overwhelm for large datasets.

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
- History is case-sensitive and ordered by time

### Performance Optimizations

1. **Parallel Downloads**: Up to 8 concurrent chunk downloads (configurable)
2. **Binary Format**: Apache Arrow EXTERNAL_LINKS reduces network bandwidth
3. **LZ4 Compression**: Enabled via X-Databricks-Result-Compression header
4. **Connection Pooling**: Reuses single HttpClient to prevent socket exhaustion
5. **Smart Display**: Shows top 50 rows only; truncation warning for large datasets

## 📊 Performance

By using **Apache Arrow**, this client avoids the overhead of JSON parsing for large datasets, allowing for significantly faster data retrieval and lower memory usage compared to standard text-based APIs.

## 📁 Project Structure

- [SqlWarehouseNet/Program.cs](SqlWarehouseNet/Program.cs): Main entry point containing the interactive loop, Arrow parsing, and UI logic.
- `profiles.json`: Stores local profile configurations (host, token, warehouse ID).
- `.sqlwarehouse_history`: Stores your query history (deduplicated).

## 📋 Testing

Test the CLI with this sample query:

```sql
SELECT * FROM your_schema.your_table LIMIT 10
```

Type `/help` inside the application to see all available shortcuts and commands.

## 🛡 License

This project is licensed under the MIT License.
