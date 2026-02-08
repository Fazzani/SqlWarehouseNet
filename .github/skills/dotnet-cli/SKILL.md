# .NET CLI Tool Skill

Best practices for building high-quality .NET global tools with Spectre.Console.

## Project structure

```
ProjectRoot/
├── Program.cs              # Entry point — slim, delegates to App
├── App.cs                  # Main loop, lifecycle, IDisposable
├── CLI/
│   ├── CommandProcessor.cs # Command dispatch & rendering
│   └── InteractiveShell.cs # Input, history, autocomplete
├── Models/
│   ├── DTOs.cs             # API DTOs
│   └── JsonContext.cs       # Source-generated JSON context
└── Services/
    ├── IService.cs          # Interface
    └── Service.cs           # Implementation
```

## Spectre.Console patterns

### Rich output

```csharp
AnsiConsole.MarkupLine("[bold green]Success:[/] Query completed");
AnsiConsole.MarkupLine("[bold red]Error:[/] Connection failed");
```

### Tables

```csharp
var table = new Table();
table.AddColumn("Column1");
table.AddRow("Value1");
AnsiConsole.Write(table);
```

### Progress bars

```csharp
await AnsiConsole.Progress()
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("Downloading...");
        // update task.Value
    });
```

## NuGet global tool packaging

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>dbsql</ToolCommandName>
<PackageId>up.DbSql</PackageId>
```

## Async patterns

- Always propagate `CancellationToken` through all async methods
- Wire `Console.CancelKeyPress` to `CancellationTokenSource`
- Use `Channel<T>` for producer/consumer streaming
- Use `Task.WhenAll` for parallel I/O operations
- Use `await using` for `IAsyncDisposable` resources

## JSON source generation

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(MyDto))]
internal partial class JsonContext : JsonSerializerContext { }
```

Always use `JsonContext.Default.MyDto` instead of runtime reflection.

## Compile-time regex

```csharp
[GeneratedRegex(@"^[\w][\w.]*$")]
private static partial Regex SafeIdentifierRegex();
```
