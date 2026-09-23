# Code coverage workflow guide

This document covers the full workflow for code coverage analysis across different environments.

______________________________________________________________________

## Workflow overview

```text
Configure project -> Run tests -> Collect coverage -> Generate report -> Analyze -> Improve tests
```

______________________________________________________________________

## Method 1: .NET CLI (recommended for CI/CD)

### Step 1: Verify project setup

Confirm the test project references the required package (version pinned via Central Package Management):

```xml
<ItemGroup>
  <PackageReference Include="coverlet.collector">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

### Step 2: Run tests and collect coverage

Basic run:

```powershell
# Run tests and collect coverage
dotnet test --collect:"XPlat Code Coverage"
```

Specify output directory:

```powershell
# Direct coverage results to a known location
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

Use a runsettings file:

```powershell
# Advanced configuration
dotnet test --settings coverage.runsettings
```

### Step 3: Generate HTML report

Install ReportGenerator:

```powershell
# Global install
dotnet tool install -g dotnet-reportgenerator-globaltool

# Or local install
dotnet new tool-manifest
dotnet tool install dotnet-reportgenerator-globaltool
```

Generate the report:

```powershell
# HTML report
reportgenerator `
  -reports:"**\coverage.cobertura.xml" `
  -targetdir:"coveragereport" `
  -reporttypes:Html

# Open the report
start coveragereport\index.html
```

### Step 4: Analyze results

In the generated HTML report, check:

1. **Overall coverage** — line coverage, branch coverage
2. **Module coverage** — distribution across projects
3. **File coverage** — identify low-coverage files
4. **Risk areas** — red regions for uncovered code

______________________________________________________________________

## Method 2: Visual Studio + Fine Code Coverage

### Step 1: Install Fine Code Coverage

1. Open Visual Studio
2. Extensions → Manage Extensions
3. Search for "Fine Code Coverage"
4. Install and restart

### Step 2: Configure options

1. Tools → Options → Fine Code Coverage
2. Enable the following:
   - Run (Common) → Enable: `True`
   - Editor Colouring Line Highlighting: `True`

### Step 3: Run tests

1. Open Test Explorer (Test → Test Explorer)
2. Run all tests or specific tests
3. Fine Code Coverage displays results automatically

### Step 4: View coverage

**Open the Fine Code Coverage window:**

- View → Other Windows → Fine Code Coverage

**Enable editor indicators:**

- Tools → FCC Toggle Indicators

**Color coding:**

- Green: covered by tests
- Yellow: partially covered (some branches untested)
- Red: not covered

### Step 5: Improve coverage

Using the red markers:

1. Identify uncovered code blocks
2. Decide whether tests are warranted
3. Add new test cases
4. Re-run tests to verify

______________________________________________________________________

## Method 3: VS Code

### Step 1: Install extensions

Confirm C# Dev Kit is installed:

1. Press `Ctrl+Shift+X` to open Extensions
2. Search for "C# Dev Kit"
3. Install and reload

### Step 2: Open Test Explorer

1. Click the beaker icon in the Activity Bar
2. Or run command: `Testing: Focus on Test Explorer View`

### Step 3: Run tests with coverage

In Test Explorer:

1. Click "Run Tests with Coverage"
2. Wait for tests to complete

### Step 4: View results

**Test coverage view:**

- Shows a tree of coverage information
- Coverage percentage per file

**Editor display:**

- Green: covered code
- Red: uncovered code
- Hit counts: how many times each line ran

**File explorer:**

- Coverage percentage next to file names

### Step 5: Toggle inline coverage

Use the shortcut `Ctrl+; Ctrl+Shift+I` or run:

- `Test: Show Inline Coverage`

______________________________________________________________________

## Method 4: CI/CD integration

### GitHub Actions

Create `.github/workflows/test-coverage.yml`:

```yaml
name: Test with Coverage

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run tests with coverage
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

      - name: Install ReportGenerator
        run: dotnet tool install -g dotnet-reportgenerator-globaltool

      - name: Generate coverage report
        run: |
          reportgenerator \
            -reports:**/coverage.cobertura.xml \
            -targetdir:coverage \
            -reporttypes:Html;Cobertura

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: coverage/Cobertura.xml
          fail_ci_if_error: true
```

### Azure DevOps

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '9.0.x'

  - task: DotNetCoreCLI@2
    displayName: 'Restore packages'
    inputs:
      command: 'restore'

  - task: DotNetCoreCLI@2
    displayName: 'Build solution'
    inputs:
      command: 'build'
      arguments: '--no-restore'

  - task: DotNetCoreCLI@2
    displayName: 'Run tests with coverage'
    inputs:
      command: 'test'
      arguments: '--no-build --collect:"XPlat Code Coverage"'
      publishTestResults: true

  - task: PublishCodeCoverageResults@1
    displayName: 'Publish coverage report'
    inputs:
      codeCoverageTool: 'Cobertura'
      summaryFileLocation: '$(Agent.TempDirectory)/**/*coverage.cobertura.xml'
      reportDirectory: '$(Build.SourcesDirectory)/coverage'
```

______________________________________________________________________

## Coverage improvement strategy

### Stage 1: Build a baseline (target 60-70%)

1. **Identify core modules:**
   - Business logic
   - Data validation
   - Calculation logic
2. **Write basic tests:**
   - Main flow tests
   - Basic boundary tests
3. **Run coverage:**
   - Establish the initial baseline

### Stage 2: Critical gaps (target 70-80%)

1. **Analyze gaps:**
   - Inspect red regions
   - Identify critical uncovered code
2. **Add tests:**
   - Boundary condition tests
   - Exception scenario tests
   - Branch coverage tests
3. **Continuous monitoring:**
   - Track coverage change per PR

### Stage 3: Polish (target 80-85%)

1. **Deep analysis:**
   - Inspect yellow regions (partially covered)
   - Confirm every branch has tests
2. **Quality improvements:**
   - Tighten assertions
   - Add boundary conditions
   - Test error paths
3. **Exclude unnecessary code:**
   - `[ExcludeFromCodeCoverage]`
   - Patterns in runsettings

### Stage 4: Maintenance

1. **CI/CD gate:**
   - Coverage may not drop
   - New code must include tests
2. **Regular reviews:**
   - Weekly coverage report review
   - Identify risk areas
3. **Continuous improvement:**
   - Refactor high-complexity code
   - Backfill missing tests

______________________________________________________________________

## Coverage report interpretation

### Key metrics

1. **Line coverage**
   - Calculation: lines executed / total lines
   - Target: >= 70%
2. **Branch coverage**
   - Calculation: branches executed / total branches
   - Target: >= 60%
   - **More important than line coverage**
3. **Method coverage**
   - Calculation: methods executed / total methods
   - Target: >= 75%

### Color coding

| Color  | Range  | Status   | Action                           |
| ------ | ------ | -------- | -------------------------------- |
| Green  | >= 75% | Good     | Maintain                         |
| Yellow | 50-74% | Warning  | Evaluate whether more tests help |
| Red    | < 50%  | Danger   | Priority for new tests           |
| Grey   | N/A    | Excluded | Verify exclusion is intentional  |

______________________________________________________________________

## Common troubleshooting

### Problem 1: Coverage shows 0%

**Possible causes:**

- `coverlet.collector` not installed
- runsettings configuration error
- Tests did not actually execute

**Solutions:**

```powershell
# Confirm package installed
dotnet list package | Select-String "coverlet"

# Reinstall
dotnet add package coverlet.collector

# Clear cache and re-test
dotnet clean
dotnet test --collect:"XPlat Code Coverage"
```

### Problem 2: ReportGenerator cannot find coverage files

**Solutions:**

```powershell
# Use absolute paths
reportgenerator `
  -reports:"$(Get-Location)\TestResults\**\coverage.cobertura.xml" `
  -targetdir:"coveragereport" `
  -reporttypes:Html

# Or full path
reportgenerator `
  -reports:"C:\Projects\MyApp\TestResults\{guid}\coverage.cobertura.xml" `
  -targetdir:"coveragereport" `
  -reporttypes:Html
```

### Problem 3: VS Code does not show coverage

**Solutions:**

1. Confirm C# Dev Kit is installed
2. Re-run "Run Tests with Coverage"
3. Check whether the lcov file was produced
4. Reload window (`Ctrl+Shift+P` → `Reload Window`)

### Problem 4: Coverage failure in CI/CD

**Solutions:**

```yaml
# Debug step for GitHub Actions
- name: Display coverage files
  run: |
    echo "Coverage files:"
    find . -name "coverage.cobertura.xml"

- name: Run tests with verbose output
  run: dotnet test --verbosity detailed --collect:"XPlat Code Coverage"
```

______________________________________________________________________

## Checklist

Before running coverage analysis, confirm:

### Project setup

- [ ] `coverlet.collector` is installed
- [ ] Test project is configured (`IsTestProject=true`)
- [ ] runsettings file is well-formed (if used)

### Test execution

- [ ] All tests pass
- [ ] Correct collector arguments used
- [ ] Coverage files generated

### Report review

- [ ] Report opens correctly
- [ ] Coverage numbers match expectations
- [ ] Improvement areas identified

### CI/CD integration

- [ ] Pipeline succeeds
- [ ] Coverage report uploaded
- [ ] Gate matches team standard

______________________________________________________________________

## Best practices

### DO

- Review coverage reports regularly
- Focus on branch coverage over line coverage
- Exclude generated code (auto-generated files)
- Integrate coverage checks into CI/CD
- Set realistic coverage targets (70-85%)
- Pair complexity metrics with coverage when assessing test need

### DON'T

- Treat coverage as a KPI
- Write assertion-free tests to hit a number
- Chase 100% coverage
- Look at line coverage in isolation
- Test trivial getters/setters
- Add tests blindly without first reading the red regions

______________________________________________________________________

## Related resources

- [Coverlet documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator documentation](https://github.com/danielpalme/ReportGenerator)
- [Fine Code Coverage](https://github.com/FortuneN/FineCodeCoverage)
- [Microsoft coverage documentation](https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage)
