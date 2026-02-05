using System.Text.Json;
using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public class ProfileService
{
    public string UserProfileDir { get; }
    public string HistoryFile { get; }
    public string ProfilesFile { get; }
    public string TablesCacheFile { get; }
    public string SchemasCacheFile { get; }
    private const int MaxCacheSize = 1000;

    public ProfileService()
    {
        UserProfileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sqlwarehouse");
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
        if (!File.Exists(ProfilesFile)) return null;
        try
        {
            var json = File.ReadAllText(ProfilesFile);
            var config = JsonSerializer.Deserialize(json, JsonContext.Default.ProfileConfig);
            if (config == null || string.IsNullOrEmpty(config.DefaultProfile)) return null;
            
            config.Profiles.TryGetValue(config.DefaultProfile, out var profile);
            if (profile != null) profile.Name = config.DefaultProfile;
            return profile;
        }
        catch { return null; }
    }

    public ProfileConfig LoadConfig()
    {
        if (!File.Exists(ProfilesFile)) return new ProfileConfig();
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(ProfilesFile), JsonContext.Default.ProfileConfig) ?? new ProfileConfig();
        }
        catch { return new ProfileConfig(); }
    }

    public void SaveConfig(ProfileConfig config)
    {
        File.WriteAllText(ProfilesFile, JsonSerializer.Serialize(config, JsonContext.Default.ProfileConfig));
    }

    public List<string> LoadHistory()
    {
        if (!File.Exists(HistoryFile)) return new List<string>();
        try
        {
            var lines = File.ReadAllLines(HistoryFile).ToList();
            var deduplicated = new List<string>();
            foreach (var line in lines)
            {
                if (deduplicated.Count == 0 || !deduplicated[^1].Equals(line, StringComparison.Ordinal))
                {
                    deduplicated.Add(line);
                }
            }
            return deduplicated;
        }
        catch { return new List<string>(); }
    }

    public void SaveHistoryEntry(string query)
    {
        try
        {
            if (File.Exists(HistoryFile))
            {
                var lastLine = File.ReadLines(HistoryFile).LastOrDefault();
                if (lastLine?.Equals(query, StringComparison.Ordinal) == true) return;
            }
            File.AppendAllLines(HistoryFile, new[] { query });
        }
        catch { }
    }

    public HashSet<string> LoadTablesCache()
    {
        try
        {
            if (!File.Exists(TablesCacheFile)) return new HashSet<string>();
            return new HashSet<string>(File.ReadAllLines(TablesCacheFile));
        }
        catch { return new HashSet<string>(); }
    }

    public HashSet<string> LoadSchemasCache()
    {
        try
        {
            if (!File.Exists(SchemasCacheFile)) return new HashSet<string>();
            return new HashSet<string>(File.ReadAllLines(SchemasCacheFile));
        }
        catch { return new HashSet<string>(); }
    }

    public void SaveTablesCache(HashSet<string> cache)
    {
        try
        {
            var data = cache.Count > MaxCacheSize ? cache.Take(MaxCacheSize) : cache;
            File.WriteAllLines(TablesCacheFile, data);
        }
        catch { }
    }

    public void SaveSchemasCache(HashSet<string> cache)
    {
        try
        {
            var data = cache.Count > MaxCacheSize ? cache.Take(MaxCacheSize) : cache;
            File.WriteAllLines(SchemasCacheFile, data);
        }
        catch { }
    }
}
