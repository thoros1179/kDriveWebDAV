using System.Text.Json;
using kDriveWebDav.Config;

namespace kDriveWebDav.Tests.Config;

public sealed class ConfigManagerTests : IDisposable
{
    // Each test instance uses its own temp directory so tests stay isolated.
    private readonly string _tempDir;
    private readonly string _configFile;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _configFile = Path.Combine(_tempDir, "accounts.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ------------------------------------------------------------------ LoadAccounts

    [Fact]
    public void LoadAccounts_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var sut = new ConfigManager(_configFile);

        var result = sut.LoadAccounts();

        Assert.Empty(result);
    }

    [Fact]
    public void LoadAccounts_WhenFileExists_DeserializesAccounts()
    {
        var json = """
            [
              { "Name": "Alice", "Token": "tok1", "DriveId": 11 },
              { "Name": "Bob",   "Token": "tok2", "DriveId": 22 }
            ]
            """;
        File.WriteAllText(_configFile, json);
        var sut = new ConfigManager(_configFile);

        var result = sut.LoadAccounts();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("tok1",  result[0].Token);
        Assert.Equal(11,      result[0].DriveId);
        Assert.Equal("Bob",   result[1].Name);
    }

    [Fact]
    public void LoadAccounts_WhenFileContainsEmptyArray_ReturnsEmptyList()
    {
        File.WriteAllText(_configFile, "[]");
        var sut = new ConfigManager(_configFile);

        var result = sut.LoadAccounts();

        Assert.Empty(result);
    }

    // ------------------------------------------------------------------ SaveAccounts

    [Fact]
    public void SaveAccounts_CreatesDirectoryAndWritesJson()
    {
        var nestedFile = Path.Combine(_tempDir, "sub", "accounts.json");
        var sut = new ConfigManager(nestedFile);

        sut.SaveAccounts([new AccountConfig { Name = "Ron", Token = "t", DriveId = 99 }]);

        Assert.True(File.Exists(nestedFile));
        var written = File.ReadAllText(nestedFile);
        Assert.Contains("Ron", written);
    }

    [Fact]
    public void SaveAccounts_WhenCalledTwice_OverwritesFile()
    {
        var sut = new ConfigManager(_configFile);

        sut.SaveAccounts([new AccountConfig { Name = "First", Token = "t1", DriveId = 1 }]);
        sut.SaveAccounts([new AccountConfig { Name = "Second", Token = "t2", DriveId = 2 }]);

        var accounts = sut.LoadAccounts();
        Assert.Single(accounts);
        Assert.Equal("Second", accounts[0].Name);
    }

    // ------------------------------------------------------------------ AddOrUpdateAccount

    [Fact]
    public void AddOrUpdateAccount_NewAccount_AppendsToList()
    {
        var sut = new ConfigManager(_configFile);

        sut.AddOrUpdateAccount(new AccountConfig { Name = "Alice", Token = "ta", DriveId = 1 });
        sut.AddOrUpdateAccount(new AccountConfig { Name = "Bob",   Token = "tb", DriveId = 2 });

        var accounts = sut.LoadAccounts();
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.Name == "Alice");
        Assert.Contains(accounts, a => a.Name == "Bob");
    }

    [Fact]
    public void AddOrUpdateAccount_ExistingName_ReplacesEntry()
    {
        var sut = new ConfigManager(_configFile);
        sut.AddOrUpdateAccount(new AccountConfig { Name = "Alice", Token = "old", DriveId = 1 });

        sut.AddOrUpdateAccount(new AccountConfig { Name = "Alice", Token = "new", DriveId = 99 });

        var accounts = sut.LoadAccounts();
        Assert.Single(accounts);
        Assert.Equal("new", accounts[0].Token);
        Assert.Equal(99,    accounts[0].DriveId);
    }

    [Fact]
    public void AddOrUpdateAccount_MatchIsCaseInsensitive()
    {
        var sut = new ConfigManager(_configFile);
        sut.AddOrUpdateAccount(new AccountConfig { Name = "alice", Token = "old", DriveId = 1 });

        sut.AddOrUpdateAccount(new AccountConfig { Name = "ALICE", Token = "new", DriveId = 2 });

        var accounts = sut.LoadAccounts();
        Assert.Single(accounts);
        Assert.Equal("new", accounts[0].Token);
    }

    // ------------------------------------------------------------------ RemoveAccount

    [Fact]
    public void RemoveAccount_ExistingAccount_RemovesItAndReturnsTrue()
    {
        var sut = new ConfigManager(_configFile);
        sut.AddOrUpdateAccount(new AccountConfig { Name = "Alice", Token = "t", DriveId = 1 });

        var result = sut.RemoveAccount("Alice");

        Assert.True(result);
        Assert.Empty(sut.LoadAccounts());
    }

    [Fact]
    public void RemoveAccount_NonExistentAccount_ReturnsFalse()
    {
        var sut = new ConfigManager(_configFile);

        var result = sut.RemoveAccount("Ghost");

        Assert.False(result);
    }

    [Fact]
    public void RemoveAccount_MatchIsCaseInsensitive()
    {
        var sut = new ConfigManager(_configFile);
        sut.AddOrUpdateAccount(new AccountConfig { Name = "alice", Token = "t", DriveId = 1 });

        var result = sut.RemoveAccount("ALICE");

        Assert.True(result);
        Assert.Empty(sut.LoadAccounts());
    }

    // ------------------------------------------------------------------ ConfigPath

    [Fact]
    public void ConfigPath_WhenCustomPathGiven_ReturnsThatPath()
    {
        var sut = new ConfigManager(_configFile);

        Assert.Equal(_configFile, sut.ConfigPath);
    }

    [Fact]
    public void ConfigPath_WhenNoCustomPath_ContainsKdriveWebdavSegment()
    {
        var sut = new ConfigManager();

        Assert.Contains("kdrive-webdav", sut.ConfigPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("accounts.json", sut.ConfigPath, StringComparison.OrdinalIgnoreCase);
    }
}
