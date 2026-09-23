# Code coverage with coverlet

Coverage tells you which lines and branches the test suite executed. It does **not** tell you whether those tests assert anything meaningful, so treat the number as a hint, not a goal. A 100% coverage suite full of assertion-free tests is worse than a 60% suite with sharp assertions.

The repo standard is `coverlet.collector` for collection, Cobertura for reporting, and a CI gate on a project-specific threshold.

## Common misconceptions

- **100% coverage != no bugs.** Coverage only means the lines ran, not that anything was verified.
- **Higher is not always better.** Effectiveness matters more than the number.
- **Coverage as a KPI backfires.** Developers will write assertion-free tests to hit the target. Tie it to behaviour instead.

## Tool landscape

| Tool                                 | Notes                                | Best for                       |
| ------------------------------------ | ------------------------------------ | ------------------------------ |
| Visual Studio Enterprise             | Built-in coverage UI                 | Devs on Enterprise             |
| Fine Code Coverage (FCC)             | Free VS extension, editor highlights | VS Community / Professional    |
| `dotnet test` + `coverlet.collector` | Cross-platform CLI                   | CI/CD, scripted runs           |
| VS Code C# Dev Kit                   | Built-in "Run Tests with Coverage"   | VS Code workflow               |
| ReportGenerator                      | Merges Cobertura, produces HTML      | CI reports & local exploration |

## Running coverage locally

```bash
# Collect coverage for the whole solution
dotnet test --collect:"XPlat Code Coverage"

# Direct results into a known directory
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Use a runsettings file (recommended once exclusions matter)
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

The collector writes a `coverage.cobertura.xml` per test project under a GUID-named folder inside `--results-directory` (or `TestResults/`). Aggregate them with `reportgenerator`:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;Cobertura"

# macOS/Linux
open coveragereport/index.html
# Windows
start coveragereport/index.html
```

For in-editor feedback:

- **Visual Studio Enterprise** — built-in (Test menu → Analyze Code Coverage).
- **Visual Studio Community / Professional** — install *Fine Code Coverage*; Tools → Options → Fine Code Coverage → Enable + Editor Colouring.
- **VS Code** — install C# Dev Kit, open the Test Explorer, run "Run Tests with Coverage". Toggle inline coverage with `Ctrl+; Ctrl+Shift+I`.

End-to-end workflow per environment (CLI, VS+FCC, VS Code, GitHub Actions, Azure DevOps): [templates/coverage/workflow.md](../templates/coverage/workflow.md).

## Configuring the test project

The test project needs the `coverlet.collector` package. Versions are pinned via Central Package Management — never hard-code in the `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="coverlet.collector">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

For finer control (exclusions, threshold gates, multiple formats), add a `coverage.runsettings`. The full template — every supported `<Format>`, every exclusion knob, threshold types/stats, and `RunConfiguration` knobs — is at [templates/coverage/runsettings.xml](../templates/coverage/runsettings.xml). Short example:

```xml
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura,opencover</Format>
          <Exclude>
            [*.Tests]*
            [*]*.Migrations.*
            [*]Program
            [*]Startup
          </Exclude>
          <ExcludeByAttribute>
            ExcludeFromCodeCoverage
            GeneratedCodeAttribute
            CompilerGenerated
          </ExcludeByAttribute>
          <Threshold>60</Threshold>
          <ThresholdType>line,branch</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
          <UseSourceLink>true</UseSourceLink>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

`ThresholdStat`:

- `total` — overall coverage must meet the gate.
- `individual` — *every* module must meet the gate.

## Excluding code from coverage

Three ways, in order of preference:

1. **`[ExcludeFromCodeCoverage]` on the type or method** — most targeted, most discoverable.
   ```csharp
   [ExcludeFromCodeCoverage]
   public class Program { /* host bootstrap */ }
   ```
2. **`<Exclude>` / `<ExcludeByFile>` in runsettings** — for generated code (`*.Migrations.*`, `*.g.cs`).
3. **`<ExcludeByAttribute>`** — apply existing attributes (`[GeneratedCode]`, `[Obsolete]`) without code changes.

Worth excluding: EF Core migrations, `Program.cs`/`Startup.cs`, generated gRPC/OpenAPI clients, DTO/record-only files with no behaviour. **Not** worth excluding: simple getters/setters with logic, factory methods, exception handlers — these are typically where the bugs land.

## Reading the report

| Color  | Meaning                                          |
| ------ | ------------------------------------------------ |
| Green  | Line executed                                    |
| Yellow | Partially covered (one branch taken, others not) |
| Red    | Never executed                                   |
| Grey   | Excluded — verify the exclusion is intentional   |

Three metrics matter:

- **Line coverage** — `lines hit / lines total`. The headline number; the least informative. Target >= 70%.
- **Branch coverage** — `branches hit / branches total`. Truer signal — an `if` with one branch tested is 50% branch, 100% line. Target >= 60%; **this is the metric to optimise**.
- **Method coverage** — `methods hit / methods total`. Catches dead methods. Target >= 75%.

When reviewing a report:

1. Walk red regions first — entirely untested.
2. Then yellow — partial branch coverage usually means a missing exception path or boundary test.
3. Ignore green; nothing actionable.

## Cyclomatic complexity as the test-count floor

Cyclomatic complexity for a method is roughly `1 + (number of branching points)` — each `if`, `for`, `while`, `case`, `&&`, `||`, `?:`, and `?.` adds one. It is the **minimum** number of tests needed to cover every path.

Worked example:

```csharp
public int Max(int[] array)
{
    if (array == null || array.Length == 0)   // +2 (null check + length check)
    {
        throw new ArgumentException("array must not be empty.");
    }

    int max = array[0];

    for (int i = 1; i < array.Length; i++)    // +1 (loop)
    {
        if (array[i] > max)                   // +1 (conditional)
        {
            max = array[i];
        }
    }

    return max;                               // +1 (method baseline)
}
// Total cyclomatic complexity = 5 -> at least 5 test cases:
//   1. null input               -> ArgumentException
//   2. empty array              -> ArgumentException
//   3. single element           -> loop body not entered
//   4. max at index 0           -> loop runs, never updates max
//   5. max in the middle        -> loop runs, updates max
```

If coverage shows the method is 100% line-covered but only has 3 tests, something is unreachable or the tests cover multiple paths per row — investigate, don't celebrate. VS extensions like *CodeMaintainability* (maintainability + complexity index) and *CodeMaid* (Spade pane visualises structure + per-method complexity) surface this inline.

## Four families of useful tests

For each uncovered region, ask which family is missing — most useful tests fall into one:

1. **Happy path** — typical valid input, dominant flow.
2. **Boundary** — `min`, `max`, `min-1`, `max+1`, empty, single-element.
3. **Branch** — each side of every `if`/`switch`/`when`.
4. **Exception** — null, invalid type, downstream failure, cancellation.

## CI threshold gates

Wire the threshold into the pipeline so coverage stops regressing silently.

### GitHub Actions

```yaml
name: Test with Coverage

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - run: dotnet restore
      - run: dotnet build --no-restore

      - name: Run tests with coverage
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage" --results-directory ./coverage

      - name: Install ReportGenerator
        run: dotnet tool install -g dotnet-reportgenerator-globaltool

      - name: Generate coverage report
        run: |
          reportgenerator \
            -reports:./coverage/**/coverage.cobertura.xml \
            -targetdir:./coverage/report \
            -reporttypes:"Html;Cobertura"

      - name: Enforce threshold
        run: |
          coverage=$(grep -oP 'line-rate="\K[0-9.]+' ./coverage/report/Cobertura.xml | head -1)
          threshold=0.80
          awk "BEGIN {exit !($coverage >= $threshold)}" \
            || (echo "Coverage $coverage below $threshold" && exit 1)

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: coverage/report/Cobertura.xml
          fail_ci_if_error: true
```

### Azure DevOps

```yaml
trigger:
  branches:
    include: [main, develop]

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '9.0.x'

  - task: DotNetCoreCLI@2
    displayName: 'Restore'
    inputs: { command: 'restore' }

  - task: DotNetCoreCLI@2
    displayName: 'Build'
    inputs:
      command: 'build'
      arguments: '--no-restore'

  - task: DotNetCoreCLI@2
    displayName: 'Test with coverage'
    inputs:
      command: 'test'
      arguments: '--no-build --collect:"XPlat Code Coverage"'
      publishTestResults: true

  - task: PublishCodeCoverageResults@1
    displayName: 'Publish coverage'
    inputs:
      codeCoverageTool: 'Cobertura'
      summaryFileLocation: '$(Agent.TempDirectory)/**/*coverage.cobertura.xml'
      reportDirectory: '$(Build.SourcesDirectory)/coverage'
```

The `coverage-analysis` skill has the full Azure DevOps pattern (and the threshold-enforcement step that pairs with it) for this repo specifically. The cross-environment walkthrough (CLI, VS+FCC, VS Code, GH Actions, Azure DevOps) plus the troubleshooting playbook lives in [templates/coverage/workflow.md](../templates/coverage/workflow.md).

## Improving coverage — priority order

When you need to lift a number, work in stages instead of carpet-bombing the codebase with tests.

| Stage         | Target | What to do                                                         |
| ------------- | ------ | ------------------------------------------------------------------ |
| Baseline      | 60-70% | Cover core business logic, primary flow, basic boundaries          |
| Critical gaps | 70-80% | Cover red regions: boundaries, exception paths, missing branches   |
| Polish        | 80-85% | Cover yellow regions, tighten assertions, exclude generated code   |
| Maintain      | hold   | CI gate; new code must come with tests; review coverage diff on PR |

Within each stage, prioritise by risk:

1. **High** — business logic, money, authorisation, validation, exception paths.
2. **Medium** — data transformation, query logic, formatters.
3. **Low** — simple getters/setters, DTOs, generated code (often better excluded than tested).

## Troubleshooting

- **Coverage 0%** — `coverlet.collector` not installed in the test project, `<Exclude>` patterns too broad, or tests didn't run. Verify with `dotnet list package | grep coverlet`.
- **ReportGenerator finds no files** — point `-reports:` at the actual GUID subfolder under `TestResults/`; `**/coverage.cobertura.xml` from the repo root usually works.
- **VS Community/Professional doesn't show coverage** — install Fine Code Coverage; enable in Tools → Options.
- **VS Code doesn't show coverage** — C# Dev Kit missing, or lcov file not produced; reload window.
- **Coverage dropped after a refactor that added no code** — a previously-covered branch is now dead code; delete it.
- **CI flaky around coverage** — run `dotnet test --verbosity detailed --collect:"XPlat Code Coverage"` and `find . -name coverage.cobertura.xml` in the job to diagnose.

## Do / Don't

**Do:**

- Review coverage reports regularly.
- Prefer branch coverage over line coverage.
- Exclude truly generated code (migrations, designer files).
- Enforce thresholds in CI.
- Set realistic targets (70-85%).
- Pair complexity numbers with coverage when assessing risk.

**Don't:**

- Treat coverage as a KPI.
- Write assertion-free tests to hit a number.
- Chase 100%.
- Look at line coverage in isolation.
- Test trivial getters/setters.
- Add tests blindly without first reading the red regions.

## Pre-flight checklist

- [ ] `coverlet.collector` referenced in every test project.
- [ ] `coverage.runsettings` checked in if exclusions are needed.
- [ ] `dotnet test --collect:"XPlat Code Coverage"` runs locally.
- [ ] Generated code excluded by attribute or pattern.
- [ ] CI publishes the report and enforces a threshold.
- [ ] Team aligned that the number is a hint, not a target.

## Guiding principle

Coverage is a means, not an end. Use it to find blind spots, not to grade developers. The questions that matter:

- Is the critical business logic tested?
- Do the tests verify *behaviour*, not just execute lines?
- Does the suite give the team confidence to refactor?

Coverage answers the first question approximately. The other two require reading the tests.

## Templates

- [templates/coverage/runsettings.xml](../templates/coverage/runsettings.xml) — full `coverage.runsettings` with every supported knob (formats, includes/excludes by glob/attribute, thresholds, `RunConfiguration`).
- [templates/coverage/workflow.md](../templates/coverage/workflow.md) — end-to-end workflow guide: CLI, VS + FCC, VS Code, GitHub Actions, Azure DevOps, plus improvement stages and troubleshooting.

## Cross-references

- [reference/fundamentals.md](fundamentals.md) — what coverage is measuring (the executed lines/branches inside the test pyramid).
- [reference/awesome-assertions.md](awesome-assertions.md) — assertion library; coverage means little if the assertions are weak.
- [reference/xunit-setup.md](xunit-setup.md) — `coverlet.collector` lives in the test `.csproj`; this file shows the canonical setup.
- [reference/aspnet-integration.md](aspnet-integration.md) — integration tests contribute to coverage but exclude `Program`/`Startup` cleanly with `[ExcludeFromCodeCoverage]`.
