---
description: Add a new service to the project following the interface-driven pattern
mode: agent
tools:
    [
        "semantic_search",
        "read_file",
        "replace_string_in_file",
        "create_file",
        "run_in_terminal",
        "manage_todo_list",
    ]
---

# Add New Service

Create a new service following the project's interface-driven architecture.

## Steps

1. Create `Services/I{{ServiceName}}.cs` — define the interface
2. Create `Services/{{ServiceName}}.cs` — implement the interface
3. Wire the service in `App.cs` constructor (manual DI)
4. If the service needs `ConnectionState`, pass it through method parameters
5. If the service is `IDisposable`, add cleanup in `App.Dispose()`
6. Build and verify

## Template interface

```csharp
namespace SqlWarehouseNet.Services;

public interface I{{ServiceName}}
{
    // Methods here — always accept CancellationToken
}
```

## Template implementation

```csharp
namespace SqlWarehouseNet.Services;

public sealed class {{ServiceName}} : I{{ServiceName}}, IDisposable
{
    // Implementation with proper disposal
}
```

## Conventions

- `sealed` classes by default
- `CancellationToken` on all async methods
- User-friendly errors via `AnsiConsole.MarkupLine`
- JSON serialization via `JsonContext` source generation
