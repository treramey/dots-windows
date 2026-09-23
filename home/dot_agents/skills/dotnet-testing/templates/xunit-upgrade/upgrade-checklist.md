# xUnit 2.x -> 3.x upgrade checklist

## Pre-upgrade preparation

### Environment

- [ ] **Target framework**

  - [ ] .NET 8.0 or later (recommended), or
  - [ ] .NET Framework 4.7.2 or later
  - [ ] Not supported: .NET Core 3.1, .NET 5/6/7

- [ ] **Project format**

  - [ ] Confirm SDK-style csproj — starts with `<Project Sdk="Microsoft.NET.Sdk">`

- [ ] **IDE version**

  - [ ] Visual Studio 2022 17.8+
  - [ ] Rider 2023.3+
  - [ ] VS Code (latest)

### Code inventory

- [ ] **Identify `async void` test methods**

  - [ ] Search pattern: `async void`
  - [ ] Regex: `async\s+void.*\[(Fact|Theory)\]`
  - [ ] Files to update: \_\_\_\_\_\_

- [ ] **Check `IAsyncLifetime` implementations**

  - [ ] Look for classes that implement `IAsyncLifetime`
  - [ ] Check whether they also implement `IDisposable`
  - [ ] Plan to move `Dispose` logic into `DisposeAsync`

- [ ] **Identify `SkippableFact` / `SkippableTheory` usage**

  - [ ] Search for `[SkippableFact]` and `[SkippableTheory]`
  - [ ] Plan replacements with `Assert.Skip` or `SkipUnless`

- [ ] **Custom DataAttribute subclasses**

  - [ ] Identify classes inheriting from `DataAttribute`
  - [ ] Plan to update to the v3 async API

### Dependencies

- [ ] **Record current package versions**

  - [ ] xunit: \_\_\_\_\_\_
  - [ ] xunit.runner.visualstudio: \_\_\_\_\_\_
  - [ ] Microsoft.NET.Test.Sdk: \_\_\_\_\_\_
  - [ ] AwesomeAssertions / FluentAssertions: \_\_\_\_\_\_
  - [ ] NSubstitute / Moq: \_\_\_\_\_\_
  - [ ] AutoFixture: \_\_\_\_\_\_

- [ ] **Compatibility**

  - [ ] Confirm each library supports xUnit 3.x
  - [ ] Pay particular attention to AutoFixture.Xunit3

### Backup

- [ ] **Create an upgrade branch**

  ```bash
  git checkout -b feature/upgrade-xunit-v3
  git push -u origin feature/upgrade-xunit-v3
  ```

______________________________________________________________________

## Execution

### Project file

- [ ] **Update `OutputType`**

  ```xml
  <OutputType>Exe</OutputType>
  ```

- [ ] **Update package references**

  - [ ] Remove `xunit` -> add `xunit.v3`
  - [ ] Remove `xunit.abstractions` (no longer needed)
  - [ ] Bump `xunit.runner.visualstudio` to 3.x
  - [ ] Bump `Microsoft.NET.Test.Sdk` to current

- [ ] **Add `xunit.runner.json`** (optional)

  ```json
  {
    "$schema": "https://xunit.net/schema/v3/xunit.runner.schema.json",
    "parallelAlgorithm": "conservative",
    "maxParallelThreads": 4
  }
  ```

### Code changes

- [ ] **Fix `async void` tests**

  - [ ] Change every `async void` to `async Task`
  - [ ] Verify replacements: \_\_\_\_\_\_

- [ ] **Update `using` directives**

  - [ ] Remove `using Xunit.Abstractions;`

- [ ] **Adapt `IAsyncLifetime` implementations**

  - [ ] Consolidate cleanup into `DisposeAsync`

- [ ] **Replace `SkippableFact` / `SkippableTheory`**

  - [ ] Use `Assert.Skip` or `SkipUnless` / `SkipWhen`

- [ ] **Update custom DataAttribute**

  - [ ] Implement the new async `GetData`

### Build & test

- [ ] **Clean & restore**

  ```bash
  dotnet clean
  dotnet restore
  ```

- [ ] **Build**

  ```bash
  dotnet build
  ```

  - [ ] Compile errors: \_\_\_\_\_\_
  - [ ] Resolved each one

- [ ] **Run tests**

  ```bash
  dotnet test --verbosity normal
  ```

  - [ ] Result: passed \_\_\_\_\_\_ / failed \_\_\_\_\_\_ / skipped \_\_\_\_\_\_

______________________________________________________________________

## Post-upgrade verification

### Functional

- [ ] **All tests pass**

  - [ ] Unit tests: \_\_\_\_\_\_
  - [ ] Integration tests: \_\_\_\_\_\_

- [ ] **Performance comparison**

  - [ ] Pre-upgrade run time: \_\_\_\_\_\_
  - [ ] Post-upgrade run time: \_\_\_\_\_\_

### CI/CD

- [ ] **Test execution**

  ```bash
  dotnet test --configuration Release --logger trx
  ```

- [ ] **Test reports**

  - [ ] Report format parsed correctly
  - [ ] Results displayed correctly

- [ ] **Parallelism configuration**

  - [ ] Tune `maxParallelThreads` for the CI environment

### Docs & onboarding

- [ ] **Project documentation updated**

  - [ ] README.md
  - [ ] CONTRIBUTING.md

- [ ] **Knowledge transfer**

  - [ ] Share upgrade notes
  - [ ] Introduce new feature usage

______________________________________________________________________

## Optional: adopt new features

- [ ] **Dynamic skipping**

  - [ ] Use `Assert.Skip`, `SkipUnless`, or `SkipWhen`

- [ ] **Explicit tests**

  - [ ] Mark with `[Fact(Explicit = true)]`

- [ ] **Assembly fixtures**

  - [ ] Global resource management via `[assembly: AssemblyFixture(typeof(...))]`

- [ ] **Test pipeline startup**

  - [ ] Implement `ITestPipelineStartup` for global init

______________________________________________________________________

## Issue log

| Issue | Resolution | Status |
| ----- | ---------- | ------ |
|       |            |        |
|       |            |        |
|       |            |        |

______________________________________________________________________

## Sign-off

- [ ] Developer: \_\_\_\_\_\_\_\_\_\_\_\_\_\_ Date: \_\_\_\_\_\_\_\_\_\_\_\_\_\_
- [ ] Code review: \_\_\_\_\_\_\_\_\_\_\_\_\_\_ Date: \_\_\_\_\_\_\_\_\_\_\_\_\_\_
- [ ] QA: \_\_\_\_\_\_\_\_\_\_\_\_\_\_ Date: \_\_\_\_\_\_\_\_\_\_\_\_\_\_
