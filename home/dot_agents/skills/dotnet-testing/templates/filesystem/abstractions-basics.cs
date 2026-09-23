// =============================================================================
// Filesystem abstraction basics: refactor untestable static-IO code to use IFileSystem
// =============================================================================

using System.IO.Abstractions;
using System.Text.Json;

namespace FileSystemTestingExamples;

#region The problem — untestable code

/// <summary>
/// Untestable configuration service (anti-example).
/// Directly uses System.IO statics — cannot be unit-tested without touching real disk.
/// </summary>
public class LegacyConfigurationService
{
    public string LoadConfig(string configPath)
    {
        // Problem: no way to control the file content from a test
        return File.ReadAllText(configPath);
    }

    public void SaveConfig(string configPath, string content)
    {
        // Problem: writes to real disk — leaks state into other tests
        File.WriteAllText(configPath, content);
    }

    public bool ConfigExists(string configPath)
    {
        // Problem: depends on real filesystem state
        return File.Exists(configPath);
    }
}

/*
 * Why LegacyConfigurationService is bad:
 *   1. Speed — disk I/O is 10-100x slower than in-memory operations.
 *   2. Environment coupling — passes locally, fails in CI.
 *   3. Side effects — leaves files behind for the next test.
 *   4. Concurrency — parallel tests writing the same path race.
 *   5. Failure simulation — can't easily provoke permission/lock errors.
 */

#endregion

#region The solution — depend on IFileSystem

/// <summary>
/// Testable configuration service.
/// Constructor-injects IFileSystem; in production wire `FileSystem`, in tests wire `MockFileSystem`.
/// </summary>
public class ConfigurationService
{
    private readonly IFileSystem _fileSystem;

    public ConfigurationService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Load configuration text. Returns defaultValue when the file is missing or read fails.
    /// </summary>
    public async Task<string> LoadConfigurationAsync(string filePath, string defaultValue = "")
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return defaultValue;
        }

        try
        {
            return await _fileSystem.File.ReadAllTextAsync(filePath);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Save configuration text. Creates the target directory if missing.
    /// </summary>
    public async Task SaveConfigurationAsync(string filePath, string value)
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        await _fileSystem.File.WriteAllTextAsync(filePath, value);
    }

    /// <summary>
    /// Load a JSON-serialised configuration. Returns default when the file is missing or invalid.
    /// </summary>
    public async Task<T?> LoadJsonConfigurationAsync<T>(string filePath) where T : class
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return default;
        }

        try
        {
            var jsonContent = await _fileSystem.File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(jsonContent);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Serialise and save a JSON-formatted configuration.
    /// </summary>
    public async Task SaveJsonConfigurationAsync<T>(string filePath, T settings) where T : class
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await _fileSystem.File.WriteAllTextAsync(filePath, jsonContent);
    }
}

#endregion

#region File-management service

/// <summary>
/// File management service — copy, backup, list files and directories via IFileSystem.
/// </summary>
public class FileManagerService
{
    private readonly IFileSystem _fileSystem;

    public FileManagerService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Copy a file into a target directory. Creates the target directory if it doesn't exist.
    /// </summary>
    public string CopyFileToDirectory(string sourceFilePath, string targetDirectory)
    {
        if (!_fileSystem.File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file does not exist: {sourceFilePath}");
        }

        if (!_fileSystem.Directory.Exists(targetDirectory))
        {
            _fileSystem.Directory.CreateDirectory(targetDirectory);
        }

        var fileName = _fileSystem.Path.GetFileName(sourceFilePath);
        var targetFilePath = _fileSystem.Path.Combine(targetDirectory, fileName);

        _fileSystem.File.Copy(sourceFilePath, targetFilePath, overwrite: true);
        return targetFilePath;
    }

    /// <summary>
    /// Backup a file with a timestamped name (e.g. data_20240101_100000.txt).
    /// </summary>
    public string BackupFile(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        var fileNameWithoutExtension = _fileSystem.Path.GetFileNameWithoutExtension(filePath);
        var extension = _fileSystem.Path.GetExtension(filePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var backupFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";
        var backupFilePath = _fileSystem.Path.Combine(directory ?? "", backupFileName);

        _fileSystem.File.Copy(filePath, backupFilePath);
        return backupFilePath;
    }

    /// <summary>
    /// Return file metadata, or null if the file doesn't exist.
    /// </summary>
    public FileInfoData? GetFileInfo(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return null;
        }

        var fileInfo = _fileSystem.FileInfo.New(filePath);
        return new FileInfoData
        {
            Name = fileInfo.Name,
            FullPath = fileInfo.FullName,
            Size = fileInfo.Length,
            CreationTime = fileInfo.CreationTime,
            LastWriteTime = fileInfo.LastWriteTime,
            IsReadOnly = fileInfo.IsReadOnly
        };
    }

    /// <summary>
    /// List files matching a search pattern in a directory.
    /// </summary>
    public IEnumerable<string> ListFiles(string directoryPath, string searchPattern = "*.*")
    {
        if (!_fileSystem.Directory.Exists(directoryPath))
        {
            return Enumerable.Empty<string>();
        }

        return _fileSystem.Directory.GetFiles(directoryPath, searchPattern);
    }

    /// <summary>
    /// Ensure a directory exists, creating it if necessary.
    /// </summary>
    public bool EnsureDirectoryExists(string directoryPath)
    {
        try
        {
            if (!_fileSystem.Directory.Exists(directoryPath))
            {
                _fileSystem.Directory.CreateDirectory(directoryPath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public class FileInfoData
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool IsReadOnly { get; set; }
    }
}

#endregion

#region File-permission service (error-handling demonstration)

/// <summary>
/// Demonstrates the I/O exceptions production code typically has to swallow.
/// </summary>
public class FilePermissionService
{
    private readonly IFileSystem _fileSystem;

    public FilePermissionService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Attempt to read a file, returning false on every documented failure mode.
    /// </summary>
    public bool TryReadFile(string filePath, out string? content)
    {
        content = null;

        try
        {
            if (!_fileSystem.File.Exists(filePath))
            {
                return false;
            }

            content = _fileSystem.File.ReadAllText(filePath);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Insufficient permissions
            return false;
        }
        catch (IOException)
        {
            // File locked or other I/O failure
            return false;
        }
    }

    /// <summary>
    /// Attempt to write a file; create the directory and retry once if the first attempt fails.
    /// </summary>
    public async Task<bool> TrySaveFileAsync(string filePath, string content)
    {
        try
        {
            await _fileSystem.File.WriteAllTextAsync(filePath, content);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            var directory = _fileSystem.Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            try
            {
                _fileSystem.Directory.CreateDirectory(directory);
                await _fileSystem.File.WriteAllTextAsync(filePath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

#endregion

#region DI registration example

/*
 * In ASP.NET Core, register the production implementation alongside the consumer services:
 *
 * // Program.cs or Startup.cs
 *
 * services.AddSingleton<IFileSystem, FileSystem>();
 *
 * services.AddScoped<ConfigurationService>();
 * services.AddScoped<FileManagerService>();
 * services.AddScoped<FilePermissionService>();
 */

#endregion
