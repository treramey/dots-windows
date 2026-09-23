// =============================================================================
// MockFileSystem testing examples — System.IO.Abstractions.TestingHelpers
// =============================================================================

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace FileSystemTestingExamples.Tests;

#region MockFileSystem basics

/// <summary>
/// ConfigurationService tests — happy paths exercised via MockFileSystem.
/// </summary>
public class ConfigurationServiceTests
{
    [Fact]
    public async Task LoadConfigurationAsync_WhenFileExists_ShouldReturnContent()
    {
        // Arrange — seed the in-memory filesystem
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["config.json"] = new MockFileData("{ \"key\": \"value\" }")
        });

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync("config.json");

        // Assert
        result.Should().Be("{ \"key\": \"value\" }");
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenFileMissing_ShouldReturnDefault()
    {
        // Arrange — empty filesystem
        var mockFileSystem = new MockFileSystem();
        var service = new ConfigurationService(mockFileSystem);
        var defaultValue = "default_config";

        // Act
        var result = await service.LoadConfigurationAsync("nonexistent.json", defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithContent_ShouldWriteFile()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new ConfigurationService(mockFileSystem);
        var configPath = "config.json";
        var content = "{ \"setting\": true }";

        await service.SaveConfigurationAsync(configPath, content);

        // Assert against the resulting filesystem state
        mockFileSystem.File.Exists(configPath).Should().BeTrue();
        var savedContent = await mockFileSystem.File.ReadAllTextAsync(configPath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WhenDirectoryMissing_ShouldCreateDirectory()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new ConfigurationService(mockFileSystem);
        var configPath = @"C:\configs\app\settings.json";

        await service.SaveConfigurationAsync(configPath, "content");

        mockFileSystem.Directory.Exists(@"C:\configs\app").Should().BeTrue();
        mockFileSystem.File.Exists(configPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_WithValidJson_ShouldDeserialise()
    {
        var settings = new AppSettings
        {
            ApplicationName = "Test App",
            Version = "1.0.0"
        };
        var json = JsonSerializer.Serialize(settings);

        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["settings.json"] = new MockFileData(json)
        });

        var service = new ConfigurationService(mockFileSystem);

        var result = await service.LoadJsonConfigurationAsync<AppSettings>("settings.json");

        result.Should().NotBeNull();
        result!.ApplicationName.Should().Be("Test App");
        result.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_WithInvalidJson_ShouldReturnNull()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["invalid.json"] = new MockFileData("{ invalid json }")
        });

        var service = new ConfigurationService(mockFileSystem);

        var result = await service.LoadJsonConfigurationAsync<AppSettings>("invalid.json");

        result.Should().BeNull();
    }
}

/// <summary>
/// Settings POCO for the tests above.
/// </summary>
public class AppSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

#endregion

#region Directory operations

/// <summary>
/// FileManagerService tests — directory ops and metadata via MockFileSystem.
/// </summary>
public class FileManagerServiceTests
{
    [Fact]
    public void CopyFileToDirectory_WhenSourceExists_ShouldCopy()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\source\test.txt"] = new MockFileData("test content")
        });

        var service = new FileManagerService(mockFileSystem);

        var result = service.CopyFileToDirectory(@"C:\source\test.txt", @"C:\target");

        result.Should().Be(@"C:\target\test.txt");
        mockFileSystem.File.Exists(@"C:\target\test.txt").Should().BeTrue();
        mockFileSystem.File.ReadAllText(@"C:\target\test.txt").Should().Be("test content");
    }

    [Fact]
    public void CopyFileToDirectory_WhenTargetDirectoryMissing_ShouldCreate()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\source\file.txt"] = new MockFileData("content")
        });

        var service = new FileManagerService(mockFileSystem);

        service.CopyFileToDirectory(@"C:\source\file.txt", @"C:\target\subfolder");

        mockFileSystem.Directory.Exists(@"C:\target\subfolder").Should().BeTrue();
        mockFileSystem.File.Exists(@"C:\target\subfolder\file.txt").Should().BeTrue();
    }

    [Fact]
    public void CopyFileToDirectory_WhenSourceMissing_ShouldThrowFileNotFoundException()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FileManagerService(mockFileSystem);

        var action = () => service.CopyFileToDirectory(@"C:\nonexistent.txt", @"C:\target");
        action.Should().Throw<FileNotFoundException>()
              .WithMessage("*Source file does not exist*");
    }

    [Fact]
    public void BackupFile_WhenFileExists_ShouldCreateTimestampedBackup()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\data\important.txt"] = new MockFileData("important data")
        });

        var service = new FileManagerService(mockFileSystem);

        var backupPath = service.BackupFile(@"C:\data\important.txt");

        backupPath.Should().StartWith(@"C:\data\important_");
        backupPath.Should().EndWith(".txt");
        mockFileSystem.File.Exists(backupPath).Should().BeTrue();
        mockFileSystem.File.ReadAllText(backupPath).Should().Be("important data");
    }

    [Fact]
    public void BackupFile_WhenFileMissing_ShouldThrowFileNotFoundException()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FileManagerService(mockFileSystem);

        var action = () => service.BackupFile(@"C:\nonexistent.txt");
        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetFileInfo_WhenFileExists_ShouldReturnMetadata()
    {
        var content = "Hello, World!";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\test.txt"] = new MockFileData(content)
        });

        var service = new FileManagerService(mockFileSystem);

        var result = service.GetFileInfo(@"C:\test.txt");

        result.Should().NotBeNull();
        result!.Name.Should().Be("test.txt");
        result.Size.Should().Be(content.Length);
    }

    [Fact]
    public void GetFileInfo_WhenFileMissing_ShouldReturnNull()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FileManagerService(mockFileSystem);

        var result = service.GetFileInfo(@"C:\nonexistent.txt");

        result.Should().BeNull();
    }

    [Fact]
    public void ListFiles_WithPattern_ShouldReturnMatchingFiles()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\data\file1.txt"] = new MockFileData("content1"),
            [@"C:\data\file2.txt"] = new MockFileData("content2"),
            [@"C:\data\file3.csv"] = new MockFileData("content3"),
            [@"C:\other\file4.txt"] = new MockFileData("content4")
        });

        var service = new FileManagerService(mockFileSystem);

        var result = service.ListFiles(@"C:\data", "*.txt").ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(f => f.EndsWith("file1.txt"));
        result.Should().Contain(f => f.EndsWith("file2.txt"));
    }

    [Fact]
    public void ListFiles_WhenDirectoryMissing_ShouldReturnEmpty()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FileManagerService(mockFileSystem);

        var result = service.ListFiles(@"C:\nonexistent");

        result.Should().BeEmpty();
    }
}

#endregion

#region Failure injection via NSubstitute

/// <summary>
/// FilePermissionService tests — use NSubstitute when MockFileSystem can't easily throw the right exception.
/// </summary>
public class FilePermissionServiceTests
{
    [Fact]
    public void TryReadFile_WhenFileReadable_ShouldReturnTrueAndContent()
    {
        // Happy-path via MockFileSystem
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["readable.txt"] = new MockFileData("file content")
        });

        var service = new FilePermissionService(mockFileSystem);

        var result = service.TryReadFile("readable.txt", out var content);

        result.Should().BeTrue();
        content.Should().Be("file content");
    }

    [Fact]
    public void TryReadFile_WhenFileMissing_ShouldReturnFalse()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FilePermissionService(mockFileSystem);

        var result = service.TryReadFile("nonexistent.txt", out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryReadFile_WhenAccessDenied_ShouldReturnFalse()
    {
        // Substitute IFileSystem so we can throw UnauthorizedAccessException on ReadAllText
        var mockFileSystem = Substitute.For<IFileSystem>();
        var mockFile = Substitute.For<IFile>();

        mockFileSystem.File.Returns(mockFile);
        mockFile.Exists("protected.txt").Returns(true);
        mockFile.ReadAllText("protected.txt")
                .Throws(new UnauthorizedAccessException("Access denied"));

        var service = new FilePermissionService(mockFileSystem);

        var result = service.TryReadFile("protected.txt", out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryReadFile_WhenFileLocked_ShouldReturnFalse()
    {
        var mockFileSystem = Substitute.For<IFileSystem>();
        var mockFile = Substitute.For<IFile>();

        mockFileSystem.File.Returns(mockFile);
        mockFile.Exists("locked.txt").Returns(true);
        mockFile.ReadAllText("locked.txt")
                .Throws(new IOException("File is in use by another process"));

        var service = new FilePermissionService(mockFileSystem);

        var result = service.TryReadFile("locked.txt", out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public async Task TrySaveFileAsync_HappyPath_ShouldReturnTrue()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FilePermissionService(mockFileSystem);

        var result = await service.TrySaveFileAsync("output.txt", "content");

        result.Should().BeTrue();
        mockFileSystem.File.Exists("output.txt").Should().BeTrue();
    }

    [Fact]
    public async Task TrySaveFileAsync_WhenDirectoryMissingButCreatable_ShouldCreateAndWrite()
    {
        var mockFileSystem = new MockFileSystem();
        var service = new FilePermissionService(mockFileSystem);

        var result = await service.TrySaveFileAsync(@"C:\new\folder\file.txt", "content");

        result.Should().BeTrue();
        mockFileSystem.File.Exists(@"C:\new\folder\file.txt").Should().BeTrue();
    }
}

#endregion

#region Advanced test patterns

public class AdvancedFileSystemTestPatterns
{
    /// <summary>
    /// MockFileSystem accepts nested directory structures in its constructor seed.
    /// </summary>
    [Fact]
    public void ComplexDirectoryStructure_ShouldBeAddressable()
    {
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\app\configs\app.json"] = new MockFileData("""
                {
                  "apiUrl": "https://api.test.com",
                  "timeout": 30
                }
                """),
            [@"C:\app\logs\app.log"] = new MockFileData("2024-01-01 10:00:00 INFO Application started"),
            [@"C:\app\data\users.csv"] = new MockFileData("Name,Age\nJohn,25\nJane,30"),
            [@"C:\temp\"] = new MockDirectoryData()
        });

        mockFileSystem.Directory.Exists(@"C:\app\configs").Should().BeTrue();
        mockFileSystem.Directory.Exists(@"C:\app\logs").Should().BeTrue();
        mockFileSystem.Directory.Exists(@"C:\temp").Should().BeTrue();
        mockFileSystem.File.Exists(@"C:\app\configs\app.json").Should().BeTrue();
    }

    /// <summary>
    /// AddFile adds a file after construction.
    /// </summary>
    [Fact]
    public void AddFile_DynamicAddition_ShouldBeReadable()
    {
        var mockFileSystem = new MockFileSystem();

        mockFileSystem.AddFile(@"C:\dynamic\file.txt", new MockFileData("dynamic content"));

        mockFileSystem.File.Exists(@"C:\dynamic\file.txt").Should().BeTrue();
        mockFileSystem.File.ReadAllText(@"C:\dynamic\file.txt").Should().Be("dynamic content");
    }

    /// <summary>
    /// MockFileSystem handles non-ASCII and otherwise tricky filenames without trouble.
    /// </summary>
    [Theory]
    [InlineData("simple.txt")]
    [InlineData("file with spaces.txt")]
    [InlineData("file-with-hyphens.txt")]
    [InlineData("file_with_underscores.txt")]
    [InlineData("ファイル.txt")]
    public void CopyFile_VariousFilenames_ShouldPreserveName(string fileName)
    {
        var mockFileSystem = new MockFileSystem();
        var sourceFile = $@"C:\source\{fileName}";
        mockFileSystem.AddFile(sourceFile, new MockFileData("test content"));

        var service = new FileManagerService(mockFileSystem);

        var result = service.CopyFileToDirectory(sourceFile, @"C:\target");

        result.Should().Be($@"C:\target\{fileName}");
        mockFileSystem.File.Exists(result).Should().BeTrue();
    }

    /// <summary>
    /// Test isolation — give each test its own MockFileSystem.
    /// </summary>
    [Fact]
    public void FileSystemIsolation_ShouldNotShareState()
    {
        var mockFileSystem1 = new MockFileSystem();
        mockFileSystem1.AddFile("test.txt", new MockFileData("content1"));

        var mockFileSystem2 = new MockFileSystem();
        mockFileSystem2.AddFile("test.txt", new MockFileData("content2"));

        mockFileSystem1.File.ReadAllText("test.txt").Should().Be("content1");
        mockFileSystem2.File.ReadAllText("test.txt").Should().Be("content2");
    }
}

#endregion

#region Test-data helpers

/// <summary>
/// Shared helpers for building canonical filesystem seeds.
/// </summary>
public static class FileTestDataHelper
{
    /// <summary>
    /// Standard test layout — config / logs / data / temp.
    /// </summary>
    public static Dictionary<string, MockFileData> CreateTestFileStructure()
    {
        return new Dictionary<string, MockFileData>
        {
            [@"C:\app\configs\app.json"] = new MockFileData("""
                {
                  "apiUrl": "https://api.test.com",
                  "timeout": 30
                }
                """),
            [@"C:\app\logs\app.log"] = new MockFileData("2024-01-01 10:00:00 INFO Application started"),
            [@"C:\app\data\users.csv"] = new MockFileData("Name,Age\nJohn,25\nJane,30"),
            [@"C:\temp\"] = new MockDirectoryData()
        };
    }

    /// <summary>
    /// appsettings.json + appsettings.Development.json layout.
    /// </summary>
    public static Dictionary<string, MockFileData> CreateConfigTestStructure()
    {
        return new Dictionary<string, MockFileData>
        {
            [@"C:\config\appsettings.json"] = new MockFileData("""
                {
                  "ConnectionStrings": {
                    "DefaultConnection": "Server=localhost;Database=TestDb;"
                  },
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information"
                    }
                  }
                }
                """),
            [@"C:\config\appsettings.Development.json"] = new MockFileData("""
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Debug"
                    }
                  }
                }
                """)
        };
    }
}

public class FileTestDataHelperUsageTests
{
    [Fact]
    public void UsingPredefinedTestStructure()
    {
        var mockFileSystem = new MockFileSystem(FileTestDataHelper.CreateTestFileStructure());

        mockFileSystem.File.Exists(@"C:\app\configs\app.json").Should().BeTrue();
        mockFileSystem.Directory.Exists(@"C:\temp").Should().BeTrue();
    }
}

#endregion
