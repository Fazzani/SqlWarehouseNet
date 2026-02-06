using System.Text.Json;
using Spectre.Console;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public class ProfileService : IProfileService
{
    public string UserProfileDir { get; }
    public string HistoryFile { get; }
    public string ProfilesFile { get; }
    public string TablesCacheFile { get; }
    public string SchemasCacheFile { get; }
    private const int MaxCacheSize = 1000;
    private const int MaxHistorySize = 500;

    public ProfileService()
    {
        UserProfileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sqlwarehouse"
        );
        if (!Directory.Exists(UserProfileDir))
        {
            Directory.CreateDirectory(UserProfileDir);
        }

        HistoryFile = Path.Combine(UserProfileDir, ".sqlwarehouse_history");
        ProfilesFile = Path.Combine(UserProfileDir, "profiles.json");
        TablesCacheFile = Path.Combine(UserProfileDir, ".sqlwarehouse_tables_cache");
        SchemasCacheFile = Path.Combine(UserProfileDir, ".sqlwarehouse_schemas_cache");
    }

    public DatabricksProfile? LoadDefaultProfile()
    {
        if (!File.Exists(ProfilesFile))
            return null;
        try
        {
            var json = File.ReadAllText(ProfilesFile);
            var config = JsonSerializer.Deserialize(json, JsonContext.Default.ProfileConfig);
            if (config == null || string.IsNullOrEmpty(config.DefaultProfile))
                return null;

            config.Profiles.TryGetValue(config.DefaultProfile, out var profile);
            if (profile != null)
                profile.Name = config.DefaultProfile;
            return profile;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not load default profile: {ex.Message.EscapeMarkup()}"
            );
            return null;
        }
    }

    public ProfileConfig LoadConfig()
    {
        if (!File.Exists(ProfilesFile))
            return new ProfileConfig();
        try
        {
            return JsonSerializer.Deserialize(
                    File.ReadAllText(ProfilesFile),
                    JsonContext.Default.ProfileConfig
                ) ?? new ProfileConfig();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not load config: {ex.Message.EscapeMarkup()}"
            );
            return new ProfileConfig();
        }
    }

    public void SaveConfig(ProfileConfig config)
    {
        try
        {
            File.WriteAllText(
                ProfilesFile,
                JsonSerializer.Serialize(config, JsonContext.Default.ProfileConfig)
            );
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not save config: {ex.Message.EscapeMarkup()}"
            );
        }
    }

    public List<string> LoadHistory()
    {
        if (!File.Exists(HistoryFile))
            return new List<string>();
        try
        {
            var lines = File.ReadAllLines(HistoryFile);
            var deduplicated = new List<string>();
            foreach (var line in lines)
            {
                if (
                    deduplicated.Count == 0
                    || !deduplicated[^1].Equals(line, StringComparison.Ordinal)
                )
                {
                    deduplicated.Add(line);
                }
            }

            // Trim history to the most recent entries
            if (deduplicated.Count > MaxHistorySize)
            {
                deduplicated = deduplicated.Skip(deduplicated.Count - MaxHistorySize).ToList();
                TruncateHistoryFile(deduplicated);
            }

            return deduplicated;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not load history: {ex.Message.EscapeMarkup()}"
            );
            return new List<string>();
        }
    }

    public void SaveHistoryEntry(string query)
    {
        try
        {
            if (File.Exists(HistoryFile))
            {
                var lastLine = File.ReadLines(HistoryFile).LastOrDefault();
                if (lastLine?.Equals(query, StringComparison.Ordinal) == true)
                    return;
            }
            File.AppendAllLines(HistoryFile, [query]);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not save history entry: {ex.Message.EscapeMarkup()}"
            );
        }
    }

    public HashSet<string> LoadTablesCache()
    {
        try
        {
            if (!File.Exists(TablesCacheFile))
                return new HashSet<string>();
            return new HashSet<string>(File.ReadAllLines(TablesCacheFile));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not load tables cache: {ex.Message.EscapeMarkup()}"
            );
            return new HashSet<string>();
        }
    }

    public HashSet<string> LoadSchemasCache()
    {
        try
        {
            if (!File.Exists(SchemasCacheFile))
                return new HashSet<string>();
            return new HashSet<string>(File.ReadAllLines(SchemasCacheFile));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not load schemas cache: {ex.Message.EscapeMarkup()}"
            );
            return new HashSet<string>();
        }
    }

    public void SaveTablesCache(HashSet<string> cache)
    {
        try
        {
            var data = cache.Count > MaxCacheSize ? cache.Take(MaxCacheSize) : cache;
            File.WriteAllLines(TablesCacheFile, data);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not save tables cache: {ex.Message.EscapeMarkup()}"
            );
        }
    }

    public void SaveSchemasCache(HashSet<string> cache)
    {
        try
        {
            var data = cache.Count > MaxCacheSize ? cache.Take(MaxCacheSize) : cache;
            File.WriteAllLines(SchemasCacheFile, data);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Could not save schemas cache: {ex.Message.EscapeMarkup()}"
            );
        }
    }

    private void TruncateHistoryFile(List<string> trimmedHistory)
    {
        try
        {
            File.WriteAllLines(HistoryFile, trimmedHistory);
        }
        catch
        { /* Best-effort truncation */
        }
    }
}
