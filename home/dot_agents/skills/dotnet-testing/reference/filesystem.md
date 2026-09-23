# Testing filesystem code

`System.IO` is static. Code that calls `File.ReadAllText`, `File.WriteAllText`, `Directory.CreateDirectory`, or `Path.Combine` directly cannot be unit-tested without touching real disk — slow, environment-dependent, and a race-condition factory under parallel tests.

The fix is `System.IO.Abstractions` — an interface wrapper over the static APIs. Inject `IFileSystem` in production, substitute `MockFileSystem` in tests.

FIRST applies — see SKILL.md. For interaction-level mocking and `IFileSystem` substitutes for failure injection, see [reference/nsubstitute.md](nsubstitute.md).

## Why static `System.IO` is hard to test

| Problem              | Symptom                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------ |
| Speed                | Disk I/O is 10-100x slower than in-memory operations. Slow tests = the suite stops being run.    |
| Environment coupling | Tests pass on your laptop, fail in CI because of path / permission differences.                  |
| Side effects         | A test leaves a file behind; the next test sees it; tests stop being *Independent*.              |
| Concurrency          | Parallel tests writing to the same path race each other.                                         |
| Failure simulation   | Hard to provoke `UnauthorizedAccessException`, `IOException`, disk-full conditions in real life. |

## Packages

| Project    | Package                                 |
| ---------- | --------------------------------------- |
| Production | `System.IO.Abstractions`                |
| Tests      | `System.IO.Abstractions.TestingHelpers` |

Versions are pinned in `Directory.Packages.props`.

## Refactor: replace static `File.*` with `IFileSystem`

Before — direct static call:

```csharp
public class LegacyConfigurationService
{
    public string LoadConfig(string path) => File.ReadAllText(path);
    public void SaveConfig(string path, string content) => File.WriteAllText(path, content);
    public bool ConfigExists(string path) => File.Exists(path);
}
```

After — depend on `IFileSystem`:

```csharp
public class ConfigService
{
    private readonly IFileSystem _fileSystem;

    public ConfigService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string LoadConfig(string path) => _fileSystem.File.ReadAllText(path);
    public void SaveConfig(string path, string contents) => _fileSystem.File.WriteAllText(path, contents);
    public bool ConfigExists(string path) => _fileSystem.File.Exists(path);
}
```

DI registration:

```csharp
services.AddSingleton<IFileSystem, FileSystem>();
```

The shape of `IFileSystem` mirrors the static surface — `_fileSystem.File`, `_fileSystem.Directory`, `_fileSystem.Path`, `_fileSystem.FileInfo`, `_fileSystem.DirectoryInfo`. Anywhere you used to type `File.ReadAllText`, type `_fileSystem.File.ReadAllText`.

**Full example:** [templates/filesystem/abstractions-basics.cs](../templates/filesystem/abstractions-basics.cs) — the legacy anti-example, a full `ConfigurationService` with JSON support, a `FileManagerService` with copy/backup/list/metadata, and a `FilePermissionService` demonstrating exception handling.

## `MockFileSystem` — the test double

`MockFileSystem` is an in-memory implementation of `IFileSystem`. You seed it with files, run your SUT, and assert on what's left in memory.

### Seeding files

```csharp
var mockFs = new MockFileSystem(new Dictionary<string, MockFileData>
{
    ["/configs/app.json"]      = new MockFileData("{ \"key\": \"value\" }"),
    ["/configs/secrets.json"]  = new MockFileData("{}"),
    ["/logs/"]                 = new MockDirectoryData(),       // empty directory
});

var service = new ConfigService(mockFs);
var config = service.LoadConfig("/configs/app.json");

config.Should().Contain("value");
```

`MockFileData` accepts a `string`, `byte[]`, or stream — covers text, binary, and large-blob scenarios. `MockDirectoryData` seeds an empty directory.

### Dynamic additions with `AddFile`

```csharp
var mockFs = new MockFileSystem();
mockFs.AddFile(@"C:\dynamic\file.txt", new MockFileData("dynamic content"));

mockFs.File.Exists(@"C:\dynamic\file.txt").Should().BeTrue();
```

### Verifying writes, auto-created directories, metadata, and patterns

After the SUT writes, assert against the final filesystem state — `mockFs.File.Exists(path)`, `mockFs.File.ReadAllText(path)`, `mockFs.Directory.Exists(dir)`. `MockFileSystem` auto-creates parent directories when the SUT calls `CreateDirectory`. Metadata is on `mockFs.FileInfo.New(path)` (`Length`, `Exists`, `Name`). Pattern listings via `mockFs.Directory.GetFiles(dir, "*.txt")` behave like the real `Directory.GetFiles`. All four patterns are exercised in the template.

**Full example:** [templates/filesystem/mockfilesystem-examples.cs](../templates/filesystem/mockfilesystem-examples.cs) — `ConfigurationService`, `FileManagerService`, and `FilePermissionService` tests; parameterised tricky-filename Theory; per-test isolation demo; and a shared `FileTestDataHelper` for canonical seeds.

### Stream-based reads

`MockFileSystem` supports `OpenRead`, `OpenWrite`, `Create`, and the related stream APIs, so line-by-line processors work the same way they do against real disk:

```csharp
public async Task<int> CountLinesAsync(string filePath)
{
    using var stream = _fileSystem.File.OpenRead(filePath);
    using var reader = new StreamReader(stream);

    var count = 0;
    while (await reader.ReadLineAsync() != null) count++;
    return count;
}
```

**Full example:** [templates/filesystem/stream-and-config-examples.cs](../templates/filesystem/stream-and-config-examples.cs) — stream processor (CountLines / ProcessLargeFile / GetFileStatistics), full lifecycle `ConfigManagerService` with backup/restore, and an integration test exercising the whole lifecycle.

## Simulating I/O failures

For "what does my code do when the disk denies permission" tests, swap `MockFileSystem` for an `IFileSystem` substitute via NSubstitute and configure the throw:

```csharp
[Fact]
public void TryReadFile_WhenAccessDenied_ShouldReturnFalse()
{
    var mockFileSystem = Substitute.For<IFileSystem>();
    var mockFile       = Substitute.For<IFile>();

    mockFileSystem.File.Returns(mockFile);
    mockFile.Exists("protected.txt").Returns(true);
    mockFile.ReadAllText("protected.txt")
            .Throws(new UnauthorizedAccessException("Access denied"));

    var sut = new FilePermissionService(mockFileSystem);

    var result = sut.TryReadFile("protected.txt", out var content);

    result.Should().BeFalse();
    content.Should().BeNull();
}
```

`MockFileSystem` is for happy-path state. `Substitute.For<IFileSystem>` is for "make this specific call throw."

A handler that catches everything important:

```csharp
public bool TryReadFile(string filePath, out string? content)
{
    content = null;
    try
    {
        if (!_fileSystem.File.Exists(filePath))
            return false;

        content = _fileSystem.File.ReadAllText(filePath);
        return true;
    }
    catch (UnauthorizedAccessException) { return false; }
    catch (IOException)                 { return false; }
}
```

See [reference/nsubstitute.md](nsubstitute.md) for the wider substitute API (`When…Do`, `Throws`, argument matchers).

## Cross-platform paths

Don't hard-code `\\` or `/`. Use `_fileSystem.Path.Combine`:

```csharp
var configPath = _fileSystem.Path.Combine("configs", "app.json");
var content = _fileSystem.File.ReadAllText(configPath);
```

`MockFileSystem` normalises paths under the hood, but if your production code has `"configs\\app.json"` hard-coded it'll still break on Linux at runtime.

The same applies to `Path.GetDirectoryName`, `Path.GetFileName`, `Path.GetFileNameWithoutExtension`, `Path.GetExtension` — all of them are on `_fileSystem.Path` rather than the static `Path` class.

## Defensive existence checks

Tests catch the bug only if production code actually has the defensive check:

```csharp
public string LoadConfig(string path)
{
    if (!_fileSystem.File.Exists(path))
        throw new ConfigNotFoundException(path);

    return _fileSystem.File.ReadAllText(path);
}
```

Then test both halves — the throw and the fallback. The template covers both.

## File copy and backup patterns

```csharp
public string BackupFile(string filePath)
{
    if (!_fileSystem.File.Exists(filePath))
        throw new FileNotFoundException($"File not found: {filePath}");

    var directory = _fileSystem.Path.GetDirectoryName(filePath);
    var nameNoExt = _fileSystem.Path.GetFileNameWithoutExtension(filePath);
    var extension = _fileSystem.Path.GetExtension(filePath);
    var stamp     = DateTime.Now.ToString("yyyyMMdd_HHmmss");

    var backupName = $"{nameNoExt}_{stamp}{extension}";
    var backupPath = _fileSystem.Path.Combine(directory ?? "", backupName);

    _fileSystem.File.Copy(filePath, backupPath);
    return backupPath;
}
```

The stream-and-config template extends this into a lifecycle service with backup directories, listing, and restore.

## Parameterised filename tests

Because `MockFileSystem` doesn't actually touch disk, it cheerfully accepts spaces, hyphens, underscores, and non-ASCII characters — useful for filename edge-case coverage:

```csharp
[Theory]
[InlineData("simple.txt")]
[InlineData("file with spaces.txt")]
[InlineData("file-with-hyphens.txt")]
[InlineData("file_with_underscores.txt")]
[InlineData("ファイル.txt")]
public void CopyFile_VariousFilenames_ShouldPreserveName(string fileName)
{
    var mockFs = new MockFileSystem();
    var sourceFile = $@"C:\source\{fileName}";
    mockFs.AddFile(sourceFile, new MockFileData("test content"));
    var sut = new FileManagerService(mockFs);

    var result = sut.CopyFileToDirectory(sourceFile, @"C:\target");

    result.Should().Be($@"C:\target\{fileName}");
    mockFs.File.Exists(result).Should().BeTrue();
}
```

## Test isolation

Give each test its own `MockFileSystem`. Two tests that share a static fixture will eventually collide. If you find yourself constructing the same seed in several tests, extract a helper — the templates show a `FileTestDataHelper` static class with `CreateTestFileStructure()` and `CreateConfigTestStructure()` factories.

## Do / Don't

**Do**

- Take `IFileSystem` as a constructor dependency for any class that touches files.
- Build paths with `_fileSystem.Path.Combine`, never with raw separators.
- Check `_fileSystem.File.Exists` before reading; ensure directory exists before writing.
- Use `MockFileSystem` for state; use `Substitute.For<IFileSystem>` for failure injection.
- Give each test its own `MockFileSystem`.
- Test both success and failure paths — `UnauthorizedAccessException`, `IOException`, `DirectoryNotFoundException` all matter to API callers.

**Don't**

- Touch the real disk from a unit test. If you need real I/O behaviour, write an integration test against a temp directory in a separate test project.
- Hard-code `\\` or `/`.
- Skip the exception-handling tests.
- Share a `MockFileSystem` across tests via a static field — one test's writes become another's pre-seeded data.

## Performance

`MockFileSystem` operations run in memory and are typically 10-100x faster than real disk I/O. Keep the seed minimal — don't pre-load a hundred megabytes of `MockFileData` to test a function that reads one file.

## Checklist

- [ ] Production code takes `IFileSystem` — no `using System.IO; File.X(…)` at call sites.
- [ ] DI registers `FileSystem` as the production implementation of `IFileSystem`.
- [ ] Tests use `MockFileSystem` for state, `Substitute.For<IFileSystem>` for failure injection.
- [ ] Path construction goes through `_fileSystem.Path.Combine` / `_fileSystem.Path.GetDirectoryName`.
- [ ] Each test creates its own `MockFileSystem` — no shared static instances.
- [ ] Both happy-path and failure paths (`UnauthorizedAccessException`, `IOException`) are covered.

## Sibling references

[reference/nsubstitute.md](nsubstitute.md) · [reference/fundamentals.md](fundamentals.md) · [reference/xunit-setup.md](xunit-setup.md) · [reference/datetime.md](datetime.md)
