# Migrating xUnit 2.x to xUnit 3.x

xUnit 3 is a re-packaged, re-namespaced release that targets the Microsoft Testing Platform (MTP) and tightens several long-standing footguns (`async void`, `SkippableFact`, `xunit.abstractions`). This document covers the package swap, the breaking changes that will catch most projects, and the new features worth adopting after the move.

## Package replacements

| v2 package                  | v3 package                        | Notes                                       |
| --------------------------- | --------------------------------- | ------------------------------------------- |
| `xunit`                     | `xunit.v3`                        | Main meta-package                           |
| `xunit.assert`              | `xunit.v3.assert`                 | Assertion library                           |
| `xunit.core`                | `xunit.v3.core`                   | Core types                                  |
| `xunit.abstractions`        | —                                 | Removed; types moved into `Xunit` namespace |
| `xunit.runner.visualstudio` | `xunit.runner.visualstudio` (3.x) | Same package id, bump to 3.x                |

Use `xunit.v3`, **not** `xunit`. The old `xunit` package id continues to publish v2 releases and will not resolve to v3.

**Full csproj template:** [templates/xunit-upgrade/xunit-v3-project.csproj](../templates/xunit-upgrade/xunit-v3-project.csproj) (no version pins — CPM owns them).

## Minimum runtime

- **.NET 8.0+** (recommended) or **.NET Framework 4.7.2+**
- **Not supported**: .NET Core 3.1, .NET 5, .NET 6, .NET 7.

If the project targets one of the unsupported frameworks, multi-target or bump first.

## Breaking changes

### 1. `OutputType` must be `Exe`

xUnit 3 ships its own entry point — test projects are now executable.

```xml
<!-- v2 -->
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <IsPackable>false</IsPackable>
</PropertyGroup>

<!-- v3 -->
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <OutputType>Exe</OutputType>
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

### 2. `async void` test methods are rejected

v2 tolerated them; v3 fails fast.

```csharp
// v2 — silently dangerous
[Fact]
public async void Method_Scenario_ShouldDoSomething() { await SomethingAsync(); }

// v3 — fix to async Task
[Fact]
public async Task Method_Scenario_ShouldDoSomething() { await SomethingAsync(); }
```

Search the test suite for `async void` before the build will, and replace every match with `async Task`.

### 3. `IAsyncLifetime` now inherits `IAsyncDisposable`

Implementations must adapt:

```csharp
// v2
public class MyFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}

// v3 — DisposeAsync now returns ValueTask (from IAsyncDisposable)
public class MyFixture : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

If the class also implements `IDisposable`, only `DisposeAsync` runs in v3 — consolidate all cleanup there.

### 4. `SkippableFact` is removed

The third-party `Xunit.SkippableFact` package is no longer needed — skipping is native:

```csharp
// v2
[SkippableFact]
public void OnlyOnWindows()
{
    Skip.IfNot(OperatingSystem.IsWindows());
    // ...
}

// v3
[Fact]
public void OnlyOnWindows()
{
    Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows-only test.");
    // ...
}
```

### 5. SDK-style projects only

Legacy `.csproj` formats are not supported. If the project still uses the older XML layout, convert it before upgrading xUnit.

### 6. Custom `DataAttribute` signature changed

If you've written custom `DataAttribute` subclasses (`GetData(MethodInfo)` overrides), the signature changes — the v3 contract is `async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo, DisposalTracker)`.

**Full example:** [templates/xunit-upgrade/migration-examples.cs](../templates/xunit-upgrade/migration-examples.cs) — every breaking change with the v2 form (commented) and the v3 form (compilable) side by side, including `async void`, `IAsyncLifetime`, `SkippableFact` (imperative and declarative), and custom `DataAttribute`.

## Upgrade steps

A predictable order keeps the build green between steps:

1. **Branch.** Don't migrate on `master` directly.
2. **Update the `.csproj`.**
   - Replace `xunit` → `xunit.v3`, `xunit.assert` → `xunit.v3.assert` (if referenced separately).
   - Bump `xunit.runner.visualstudio` to 3.x.
   - Remove any `xunit.abstractions` reference.
   - Add `<OutputType>Exe</OutputType>`.
3. **Fix `async void` test methods** — grep, replace, build.
4. **Update `using` directives.** `using Xunit.Abstractions;` → `using Xunit;` (or delete; `Xunit` is likely already imported).
5. **Adapt `IAsyncLifetime` implementations** to return `ValueTask`.
6. **Replace `SkippableFact`** with `Assert.Skip*` calls.
7. **Refactor any custom `DataAttribute`** to the new contract.
8. **Build, run, fix.**

**Full step-by-step checklist:** [templates/xunit-upgrade/upgrade-checklist.md](../templates/xunit-upgrade/upgrade-checklist.md) — copy this into a PR description; tick boxes as you go.

### .NET 10 SDK + MTP-mode note

xUnit 3 enables Microsoft Testing Platform by default. With the .NET 10 SDK, `dotnet test` can run natively in MTP mode — configure `global.json` to opt in:

```json
{
    "sdk": { "version": "10.0.100" },
    "test": { "runner": "Microsoft.Testing.Platform" }
}
```

If you temporarily disable MTP via `<EnableMicrosoftTestingPlatform>false</EnableMicrosoftTestingPlatform>` to unblock an IDE, do **not** also keep the MTP `test` section in `global.json` — the two clash and `dotnet test` will fail. Remove the `global.json` `test` section instead.

Migration steps from VSTest mode to MTP mode:

1. Add `"test": { "runner": "Microsoft.Testing.Platform" }` to `global.json`.
2. Remove `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` from MSBuild.
3. Remove `<TestingPlatformCaptureOutput>` and `<TestingPlatformShowTestsFailure>`.
4. Drop the extra `--` from CLI commands: `dotnet test -- --report-trx` → `dotnet test --report-trx`.
5. Use explicit `--solution`/`--project` flags instead of positional paths.

Mixed VSTest + MTP projects in the same solution are not supported — migrate every test project together.

## New features worth adopting

### Dynamic skipping

```csharp
[Fact]
public void Method_Scenario_ShouldDoSomething()
{
    Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows-only path.");
    Assert.SkipWhen(IsCi(), "Local-only test.");
    // ...
}
```

Declarative variants on `[Fact(SkipUnless = ..., Skip = "...")]` are also available — the template shows both styles.

Reminder from SKILL.md: skipping a test without a ticket key in the message is banned. `Assert.Skip("LAAIR-2099 — pending fixture")` is fine; `Assert.Skip("flaky")` is not.

### `[Explicit]` tests

Mark tests that should only run when named explicitly (slow local-only diagnostics, scratch tests). They are excluded from default discovery.

```csharp
[Fact(Explicit = true)]
public void StressTest_RunsForTenMinutes() { /* ... */ }
```

### `MatrixTheoryData`

Combinatorial expansion without hand-listing every row:

```csharp
public static MatrixTheoryData<string, int> Data = new(
    new[] { "a", "b" },
    new[] { 1, 2, 3 });

[Theory, MemberData(nameof(Data))]
public void Method_Scenario_ShouldDoSomething(string s, int i) { /* 6 rows */ }
```

### Assembly fixtures

An `IAssemblyFixture<T>` is shared across every test in an assembly — one rung above `ICollectionFixture`. Useful for expensive one-time setup (an in-memory database, a containerised dependency). Treat the fixture as immutable after construction; mutable assembly-scoped state is the worst kind of shared state.

### Test pipeline startup

`ITestPipelineStartup` runs once before any test, for true global initialisation — set environment variables, prime caches, etc.

### `[Test]` attribute

Functionally identical to `[Fact]` — useful for codebases migrating to xUnit from frameworks where `[Test]` is the standard.

### `Testcontainers.XunitV3`

If the project uses Testcontainers, the v3 integration package manages container lifecycle automatically and replaces hand-rolled `IAsyncLifetime` implementations:

```xml
<PackageReference Include="Testcontainers.XunitV3" />
```

See [reference/testcontainers.md](testcontainers.md) for usage.

**Full example:** [templates/xunit-upgrade/new-features-examples.cs](../templates/xunit-upgrade/new-features-examples.cs) — every new-feature pattern above as a runnable test, plus culture-override and diagnostics examples.

## `xunit.runner.json`

The v3 runner config schema:

```json
{
  "$schema": "https://xunit.net/schema/v3/xunit.runner.schema.json",
  "parallelAlgorithm": "conservative",
  "maxParallelThreads": 4,
  "diagnosticMessages": true,
  "internalDiagnosticMessages": false,
  "methodDisplay": "classAndMethod",
  "preEnumerateTheories": true,
  "stopOnFail": false
}
```

**Full template:** [templates/xunit-upgrade/xunit.runner.json](../templates/xunit-upgrade/xunit.runner.json) — drop next to the `.csproj`, set `<None Update="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />`, and tune from there.

## Common issues

- **`xunit.abstractions` not found** — remove the `using`; the types moved into `Xunit`.
- **IDE can't discover tests** — Visual Studio 2022 17.8+, Rider 2023.3+, latest VS Code. If still broken, temporarily set `<EnableMicrosoftTestingPlatform>false</EnableMicrosoftTestingPlatform>` (with the global.json caveat above).
- **`async void` build error** — replace with `async Task`. Not optional in v3.
- **`SkippableFact` unresolved** — remove `Xunit.SkippableFact` package reference; replace with `Assert.Skip*` calls.

## Checklist

### Before

- [ ] Target framework is .NET 8+ or .NET Framework 4.7.2+
- [ ] Project is SDK-style
- [ ] All `async void` test methods catalogued
- [ ] `IAsyncLifetime` implementations identified
- [ ] Third-party package compatibility checked
- [ ] Upgrade on a branch

### During

- [ ] `xunit.v3` referenced (not `xunit`)
- [ ] `xunit.abstractions` removed
- [ ] `<OutputType>Exe</OutputType>` added
- [ ] All `async void` tests fixed
- [ ] `IAsyncLifetime` returns `ValueTask`
- [ ] `SkippableFact` replaced with `Assert.Skip*`
- [ ] Custom `DataAttribute` (if any) refactored
- [ ] Build succeeds; full suite runs

### After

- [ ] Functional regression run
- [ ] CI pipeline green
- [ ] `xunit.runner.json` reviewed
- [ ] (.NET 10) `global.json` MTP section configured if MTP mode is desired

## Sibling references

[reference/xunit-setup.md](xunit-setup.md) · [reference/fundamentals.md](fundamentals.md) · [reference/testcontainers.md](testcontainers.md) · [reference/tunit.md](tunit.md)
