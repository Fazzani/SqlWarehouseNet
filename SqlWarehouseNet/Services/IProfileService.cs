using SqlWarehouseNet.Models;

namespace SqlWarehouseNet.Services;

public interface IProfileService
{
    string UserProfileDir { get; }
    string ProfilesFile { get; }
    DatabricksProfile? LoadDefaultProfile();
    ProfileConfig LoadConfig();
    void SaveConfig(ProfileConfig config);
    List<string> LoadHistory();
    void SaveHistoryEntry(string query);
    HashSet<string> LoadTablesCache();
    HashSet<string> LoadSchemasCache();
    void SaveTablesCache(HashSet<string> cache);
    void SaveSchemasCache(HashSet<string> cache);
}
