// =============================================================================
// Stream processing and configuration-management examples
// =============================================================================

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace FileSystemTestingExamples;

#region Stream-processing service

/// <summary>
/// Stream processor — process large files line-by-line, not by reading everything into memory.
/// </summary>
public class StreamProcessorService
{
    private readonly IFileSystem _fileSystem;

    public StreamProcessorService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Count the lines in a file using a stream (memory-efficient).
    /// </summary>
    public async Task<int> CountLinesAsync(string filePath)
    {
        using var stream = _fileSystem.File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        int lineCount = 0;
        while (await reader.ReadLineAsync() != null)
        {
            lineCount++;
        }

        return lineCount;
    }

    /// <summary>
    /// Process a large file one line at a time, writing the transformed lines to an output file.
    /// </summary>
    public async Task ProcessLargeFileAsync(
        string inputPath,
        string outputPath,
        Func<string, string> processor)
    {
        using var inputStream = _fileSystem.File.OpenRead(inputPath);
        using var outputStream = _fileSystem.File.Create(outputPath);
        using var reader = new StreamReader(inputStream);
        using var writer = new StreamWriter(outputStream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var processedLine = processor(line);
            await writer.WriteLineAsync(processedLine);
        }
    }

    /// <summary>
    /// Compute line/word/character statistics for a file.
    /// </summary>
    public async Task<FileStatistics> GetFileStatisticsAsync(string filePath)
    {
        var stats = new FileStatistics();

        using var stream = _fileSystem.File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            stats.LineCount++;
            stats.CharacterCount += line.Length;
            stats.WordCount += line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        return stats;
    }

    public class FileStatistics
    {
        public int LineCount { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
    }
}

#endregion

#region Configuration manager service

/// <summary>
/// Full-lifecycle configuration manager: load / save / backup / list / restore.
/// </summary>
public class ConfigManagerService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configDirectory;

    public ConfigManagerService(IFileSystem fileSystem, string configDirectory = "config")
    {
        _fileSystem = fileSystem;
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// Ensure the config directory exists.
    /// </summary>
    public void InitializeConfigDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_configDirectory) &&
            !_fileSystem.Directory.Exists(_configDirectory))
        {
            _fileSystem.Directory.CreateDirectory(_configDirectory);
        }
    }

    /// <summary>
    /// Load AppSettings; if the file doesn't exist, create the defaults and return them.
    /// </summary>
    public async Task<AppSettings> LoadAppSettingsAsync()
    {
        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");

        if (!_fileSystem.File.Exists(configPath))
        {
            var defaultSettings = new AppSettings();
            await SaveAppSettingsAsync(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var jsonContent = await _fileSystem.File.ReadAllTextAsync(configPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
            return settings ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Save AppSettings as pretty-printed JSON.
    /// </summary>
    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        InitializeConfigDirectory();

        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");
        var jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await _fileSystem.File.WriteAllTextAsync(configPath, jsonContent);
    }

    /// <summary>
    /// Copy the current config into a timestamped file under config/backup/.
    /// </summary>
    public string BackupConfiguration()
    {
        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");

        if (!_fileSystem.File.Exists(configPath))
        {
            throw new FileNotFoundException("Cannot back up — config file does not exist");
        }

        var backupDirectory = _fileSystem.Path.Combine(_configDirectory, "backup");
        if (!_fileSystem.Directory.Exists(backupDirectory))
        {
            _fileSystem.Directory.CreateDirectory(backupDirectory);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"appsettings_{timestamp}.json";
        var backupPath = _fileSystem.Path.Combine(backupDirectory, backupFileName);

        _fileSystem.File.Copy(configPath, backupPath);
        return backupPath;
    }

    /// <summary>
    /// List backups newest-first.
    /// </summary>
    public IEnumerable<string> ListBackups()
    {
        var backupDirectory = _fileSystem.Path.Combine(_configDirectory, "backup");

        if (!_fileSystem.Directory.Exists(backupDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return _fileSystem.Directory.GetFiles(backupDirectory, "appsettings_*.json")
                          .OrderByDescending(f => f);
    }

    /// <summary>
    /// Copy a backup over the current appsettings.json.
    /// </summary>
    public async Task RestoreFromBackupAsync(string backupPath)
    {
        if (!_fileSystem.File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupPath}");
        }

        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");
        var content = await _fileSystem.File.ReadAllTextAsync(backupPath);
        await _fileSystem.File.WriteAllTextAsync(configPath, content);
    }

    public class AppSettings
    {
        public string ApplicationName { get; set; } = "FileSystem Testing Demo";
        public string Version { get; set; } = "1.0.0";
        public DatabaseSettings Database { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Server=localhost;Database=TestDb;";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class LoggingSettings
    {
        public string Level { get; set; } = "Information";
        public bool EnableFileLogging { get; set; } = true;
        public string LogDirectory { get; set; } = "logs";
    }
}

#endregion

#region Tests

public class StreamProcessorServiceTests
{
    [Fact]
    public async Task CountLinesAsync_MultiLineFile_ShouldReturnLineCount()
    {
        var content = "Line 1\nLine 2\nLine 3\nLine 4";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["test.txt"] = new MockFileData(content)
        });

        var service = new StreamProcessorService(mockFileSystem);

        var result = await service.CountLinesAsync("test.txt");

        result.Should().Be(4);
    }

    [Fact]
    public async Task CountLinesAsync_EmptyFile_ShouldReturnZero()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["empty.txt"] = new MockFileData("")
        });

        var service = new StreamProcessorService(mockFileSystem);

        var result = await service.CountLinesAsync("empty.txt");

        result.Should().Be(0);
    }

    [Fact]
    public async Task ProcessLargeFileAsync_TransformEachLine_ShouldWriteOutput()
    {
        var inputContent = "hello\nworld\ntest";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["input.txt"] = new MockFileData(inputContent)
        });

        var service = new StreamProcessorService(mockFileSystem);

        await service.ProcessLargeFileAsync("input.txt", "output.txt", line => line.ToUpper());

        var outputContent = mockFileSystem.File.ReadAllText("output.txt");
        outputContent.Should().Contain("HELLO");
        outputContent.Should().Contain("WORLD");
        outputContent.Should().Contain("TEST");
    }

    [Fact]
    public async Task GetFileStatisticsAsync_ShouldReturnLineWordCharCounts()
    {
        var content = "Hello World\nThis is a test\nThird line";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["stats.txt"] = new MockFileData(content)
        });

        var service = new StreamProcessorService(mockFileSystem);

        var result = await service.GetFileStatisticsAsync("stats.txt");

        result.LineCount.Should().Be(3);
        result.WordCount.Should().Be(8); // Hello, World, This, is, a, test, Third, line
    }
}

public class ConfigManagerServiceTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly ConfigManagerService _service;

    public ConfigManagerServiceTests()
    {
        _mockFileSystem = new MockFileSystem();
        _service = new ConfigManagerService(_mockFileSystem, "test-config");
    }

    [Fact]
    public async Task LoadAppSettingsAsync_WhenMissing_ShouldReturnDefaultsAndCreateFile()
    {
        var result = await _service.LoadAppSettingsAsync();

        result.Should().NotBeNull();
        result.ApplicationName.Should().Be("FileSystem Testing Demo");
        result.Version.Should().Be("1.0.0");
        result.Database.Should().NotBeNull();
        result.Logging.Should().NotBeNull();

        // Defaults are now persisted
        var configPath = @"test-config\appsettings.json";
        _mockFileSystem.File.Exists(configPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAppSettingsAsync_WithSettings_ShouldWriteFile()
    {
        var settings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "Test App",
            Version = "2.0.0",
            Database = new ConfigManagerService.DatabaseSettings
            {
                ConnectionString = "Server=test;Database=TestDb;",
                TimeoutSeconds = 60
            }
        };

        await _service.SaveAppSettingsAsync(settings);

        var configPath = @"test-config\appsettings.json";
        _mockFileSystem.File.Exists(configPath).Should().BeTrue();

        var savedContent = await _mockFileSystem.File.ReadAllTextAsync(configPath);
        var savedSettings = JsonSerializer.Deserialize<ConfigManagerService.AppSettings>(savedContent);

        savedSettings!.ApplicationName.Should().Be("Test App");
        savedSettings.Version.Should().Be("2.0.0");
        savedSettings.Database.ConnectionString.Should().Be("Server=test;Database=TestDb;");
        savedSettings.Database.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public async Task LoadAppSettingsAsync_WhenExists_ShouldReturnPersistedSettings()
    {
        var settings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "Existing App",
            Version = "3.0.0"
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var configPath = @"test-config\appsettings.json";
        _mockFileSystem.AddFile(configPath, new MockFileData(json));

        var result = await _service.LoadAppSettingsAsync();

        result.ApplicationName.Should().Be("Existing App");
        result.Version.Should().Be("3.0.0");
    }

    [Fact]
    public void BackupConfiguration_WhenConfigExists_ShouldCreateTimestampedBackup()
    {
        var settings = new ConfigManagerService.AppSettings();
        var json = JsonSerializer.Serialize(settings);
        var configPath = @"test-config\appsettings.json";
        _mockFileSystem.AddFile(configPath, new MockFileData(json));

        var backupPath = _service.BackupConfiguration();

        _mockFileSystem.File.Exists(backupPath).Should().BeTrue();
        backupPath.Should().StartWith(@"test-config\backup\appsettings_");
        backupPath.Should().EndWith(".json");

        var backupContent = _mockFileSystem.File.ReadAllText(backupPath);
        backupContent.Should().Be(json);
    }

    [Fact]
    public void BackupConfiguration_WhenConfigMissing_ShouldThrowFileNotFoundException()
    {
        var action = () => _service.BackupConfiguration();
        action.Should().Throw<FileNotFoundException>()
              .WithMessage("*Cannot back up*");
    }

    [Fact]
    public void ListBackups_WithMultipleBackups_ShouldReturnNewestFirst()
    {
        _mockFileSystem.AddFile(@"test-config\backup\appsettings_20240101_100000.json",
            new MockFileData("{}"));
        _mockFileSystem.AddFile(@"test-config\backup\appsettings_20240102_100000.json",
            new MockFileData("{}"));
        _mockFileSystem.AddFile(@"test-config\backup\appsettings_20240103_100000.json",
            new MockFileData("{}"));

        var backups = _service.ListBackups().ToList();

        backups.Should().HaveCount(3);
        backups[0].Should().Contain("20240103"); // newest first
    }

    [Fact]
    public void ListBackups_NoBackups_ShouldReturnEmpty()
    {
        var backups = _service.ListBackups();

        backups.Should().BeEmpty();
    }

    [Fact]
    public async Task RestoreFromBackupAsync_WhenBackupExists_ShouldRestore()
    {
        var originalSettings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "Original App"
        };
        var backupSettings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "Backup App"
        };

        var configPath = @"test-config\appsettings.json";
        var backupPath = @"test-config\backup\appsettings_backup.json";

        _mockFileSystem.AddFile(configPath,
            new MockFileData(JsonSerializer.Serialize(originalSettings)));
        _mockFileSystem.AddFile(backupPath,
            new MockFileData(JsonSerializer.Serialize(backupSettings)));

        await _service.RestoreFromBackupAsync(backupPath);

        var restoredContent = await _mockFileSystem.File.ReadAllTextAsync(configPath);
        var restoredSettings = JsonSerializer.Deserialize<ConfigManagerService.AppSettings>(restoredContent);
        restoredSettings!.ApplicationName.Should().Be("Backup App");
    }

    [Fact]
    public async Task RestoreFromBackupAsync_WhenBackupMissing_ShouldThrowFileNotFoundException()
    {
        var action = async () => await _service.RestoreFromBackupAsync(@"nonexistent.json");
        await action.Should().ThrowAsync<FileNotFoundException>();
    }
}

#endregion

#region Integration test — full lifecycle

public class ConfigManagerIntegrationTests
{
    [Fact]
    public async Task FullLifecycle_CreateModifyBackupRestore()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var service = new ConfigManagerService(mockFileSystem, "app-config");

        // Step 1 — first load creates defaults
        var settings = await service.LoadAppSettingsAsync();
        settings.ApplicationName.Should().Be("FileSystem Testing Demo");

        // Step 2 — modify and save
        settings.ApplicationName = "Modified App";
        settings.Database.ConnectionString = "Server=production;Database=ProdDb;";
        await service.SaveAppSettingsAsync(settings);

        // Step 3 — backup
        var backupPath = service.BackupConfiguration();
        mockFileSystem.File.Exists(backupPath).Should().BeTrue();

        // Step 4 — modify again, beyond the backup
        settings.ApplicationName = "Another Modification";
        await service.SaveAppSettingsAsync(settings);

        // Step 5 — restore from backup
        await service.RestoreFromBackupAsync(backupPath);

        // Assert — restored to the backup snapshot, not the latest state
        var restoredSettings = await service.LoadAppSettingsAsync();
        restoredSettings.ApplicationName.Should().Be("Modified App");
        restoredSettings.Database.ConnectionString.Should().Be("Server=production;Database=ProdDb;");
    }
}

#endregion
