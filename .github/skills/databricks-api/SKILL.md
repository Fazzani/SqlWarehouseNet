# Databricks SQL API Skill

Expert knowledge for building .NET applications that integrate with the Databricks SQL Statement Execution API.

## API Patterns

### Statement Execution Flow

1. **POST** `/api/2.0/sql/statements` with SQL query, warehouse ID, and `ARROW_STREAM` format
2. **Poll** the statement status until `SUCCEEDED`, `FAILED`, or `CANCELED`
3. **Download** Arrow IPC chunks from external links in parallel
4. **Deserialize** `RecordBatch` objects with `ArrowStreamReader`

### Request format

```json
{
    "warehouse_id": "<warehouse-id>",
    "statement": "SELECT * FROM catalog.schema.table",
    "format": "ARROW_STREAM",
    "disposition": "EXTERNAL_LINKS",
    "wait_timeout": "0s"
}
```

### Polling with exponential backoff

- Start at 500ms delay
- Multiply by 1.5 each iteration
- Cap at 5s max delay
- Always pass `CancellationToken`

### Arrow IPC streaming

- Use `ArrowStreamReader` to deserialize downloaded chunks
- Extract column values via `ArrowUtils.GetValue()` — handles all Arrow types without reflection
- Stream rows to display via `Channel<T>` for real-time table rendering

## Security rules

- **Never** log or display access tokens
- Always validate SQL identifiers with regex `^[\w][\w.]*$` before interpolating into queries
- Use masked input (`*`) for token prompts
- Dispose `HttpClient` instances properly when connection changes

## Error handling

- Surface user-friendly errors via `AnsiConsole.MarkupLine("[red]...[/]")`
- Handle HTTP 429 (rate limiting) with retry
- Handle network timeouts gracefully
- Provide statement ID in error messages for debugging

## Performance tips

- Use `EXTERNAL_LINKS` disposition for large results (enables parallel chunk download)
- Download Arrow chunks with `Task.WhenAll` for parallelism
- Use `Channel<RecordBatch>` to stream results as they arrive
- Prefer `JsonSerializer` with source-generated `JsonContext` for zero-reflection serialization
