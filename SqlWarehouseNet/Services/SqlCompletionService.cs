using System.Text.RegularExpressions;

namespace SqlWarehouseNet.Services;

public class SqlCompletionService
{
    public void UpdateTablesCache(string sqlQuery, HashSet<string> tablesCache, HashSet<string> schemasCache)
    {
        try
        {
            var fromMatches = Regex.Matches(
                sqlQuery, 
                @"\b(?:FROM|JOIN)\s+(?:(?<schema>\w+)\.)?(?<table>\w+)",
                RegexOptions.IgnoreCase
            );
            
            foreach (Match match in fromMatches)
            {
                if (match.Groups["table"].Success)
                {
                    var tableName = match.Groups["table"].Value;
                    tablesCache.Add(tableName);
                    
                    if (match.Groups["schema"].Success)
                    {
                        var schemaName = match.Groups["schema"].Value;
                        schemasCache.Add(schemaName);
                        tablesCache.Add($"{schemaName}.{tableName}");
                    }
                }
            }
        }
        catch { }
    }

    public List<string> GetSqlSuggestions(string fullInput, string lastWord, HashSet<string> tablesCache, HashSet<string> schemasCache)
    {
        var suggestions = new List<string>();
        var sqlKeywords = new[] 
        { 
            "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "LIKE",
            "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON",
            "GROUP", "ORDER", "BY", "HAVING", "LIMIT", "OFFSET", "DISTINCT",
            "AS", "WITH", "CASE", "WHEN", "THEN", "ELSE", "END",
            "COUNT", "SUM", "AVG", "MIN", "MAX",
            "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER",
            "UNION", "EXCEPT", "INTERSECT"
        };
        
        if (lastWord.Length < 2) return suggestions;
        
        string contextUpper = fullInput.ToUpper();
        
        if (contextUpper.Contains("SELECT") && !contextUpper.Contains("FROM"))
        {
            suggestions.AddRange(sqlKeywords.Where(k => 
                k.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase) &&
                (k == "FROM" || k == "DISTINCT")));
        }
        else if (contextUpper.Contains("FROM"))
        {
            suggestions.AddRange(tablesCache.Where(t => t.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)));
            if (suggestions.Count < 5 && !contextUpper.Contains("WHERE") && !contextUpper.Contains("LIMIT"))
            {
                suggestions.AddRange(sqlKeywords.Where(k => 
                    k.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase) &&
                    (k == "WHERE" || k == "GROUP" || k == "ORDER" || k == "LIMIT" || k == "INNER" || k == "LEFT" || k == "RIGHT" || k == "JOIN")));
            }
        }
        else if (contextUpper.Contains("WHERE"))
        {
            suggestions.AddRange(sqlKeywords.Where(k => 
                k.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase) &&
                (k == "AND" || k == "OR" || k == "ORDER" || k == "GROUP" || k == "LIMIT")));
        }
        else
        {
            suggestions.AddRange(tablesCache.Where(t => t.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)));
            suggestions.AddRange(sqlKeywords.Where(k => k.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)));
        }
        
        return suggestions.Distinct().OrderBy(s => s).ToList();
    }
}
