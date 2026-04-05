using System.Text.Json;
using System.Text.Json.Serialization;

namespace kDriveWebDav.Config;

/// <summary>
/// Manages reading and writing account configurations stored as JSON on disk.
/// </summary>
public sealed class ConfigManager
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _configPath;

    public ConfigManager(string? configPath = null)
    {
        _configPath = configPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kdrive-webdav", "accounts.json");
    }

    /// <summary>Loads all configured accounts from disk.</summary>
    public List<AccountConfig> LoadAccounts()
    {
        if (!File.Exists(_configPath))
            return [];

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<List<AccountConfig>>(json, s_jsonOptions) ?? [];
    }

    /// <summary>Persists the list of accounts to disk.</summary>
    public void SaveAccounts(List<AccountConfig> accounts)
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(accounts, s_jsonOptions);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>Adds or replaces an account by name.</summary>
    public void AddOrUpdateAccount(AccountConfig account)
    {
        var accounts = LoadAccounts();
        var existing = accounts.FindIndex(a => a.Name.Equals(account.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            accounts[existing] = account;
        else
            accounts.Add(account);
        SaveAccounts(accounts);
    }

    /// <summary>Removes an account by name. Returns true if found and removed.</summary>
    public bool RemoveAccount(string name)
    {
        var accounts = LoadAccounts();
        var removed = accounts.RemoveAll(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveAccounts(accounts);
            return true;
        }
        return false;
    }

    /// <summary>Returns the path where configuration is stored.</summary>
    public string ConfigPath => _configPath;
}
