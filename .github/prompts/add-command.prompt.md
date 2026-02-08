---
description: Add a new CLI command to the interactive shell
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

# Add New CLI Command

Add a new `/{{command_name}}` command to the SqlWarehouseNet interactive CLI.

## Steps

1. Read `CLI/CommandProcessor.cs` to understand the dispatch pattern
2. Add the command handler method in `CommandProcessor.cs`
3. Update the `/help` output to include the new command
4. Update the `App.cs` main loop if needed (for commands that need `ConnectionState` or services)
5. Build with `dotnet build SqlWarehouseNet/SqlWarehouseNet.csproj` and fix any errors
6. Update README.md CLI commands section

## Conventions

- Command names start with `/`
- Use `AnsiConsole.MarkupLine` for output
- Validate user input (use `IsValidIdentifier()` for SQL identifiers)
- Pass `CancellationToken` to all async operations
- Add XML doc comments to the handler method
