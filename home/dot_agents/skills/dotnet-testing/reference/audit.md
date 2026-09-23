# Audit

Measure the test suite as it is **today**. Run commands, gather facts, score the result, output a report. Do **not** suggest changes — `critique` is the prescriptive counterpart; `audit` is descriptive.

This is mechanical: every dimension below corresponds to a verifiable observation. If a check requires interpretation, it belongs in `critique`.

## When to use

- Onboarding to an unfamiliar repo and you need to know "what shape is the test suite in?"
- Before a refactor, to capture a baseline for after-comparison.
- Periodically (quarterly) to spot drift from the shared rules.
- Before a release, as a gate signal alongside CI.

## Targets

- A solution (`*.sln`) — audit every test project it contains.
- A test project (`*.Tests.csproj`) — audit one project in isolation.
- A subdirectory — audit the test projects nested under it.

If the target is ambiguous, run `git ls-files '*.Tests.csproj'` and ask the user which one(s) to audit.

## Procedure

Score each dimension 0–4 using the criteria embedded below. Run the commands; don't hand-wave.

### 1. Inventory

Cheap and concrete. Run from solution root.

```bash
# Test projects
find . -name '*.Tests.csproj' -not -path '*/bin/*' -not -path '*/obj/*'

# Total tests (xUnit discovery — needs build)
dotnet test --list-tests --no-build 2>/dev/null | grep -E '^\s+[A-Z]' | wc -l

# Skipped tests (xUnit) — flag any without a ticket key in the Skip message per SKILL.md ban
grep -rEn 'Skip\s*=\s*"[^"]*"' --include='*.cs' tests/ src/
```

Report: number of test projects, total tests, skipped count, list of skip strings that **do not** contain a ticket pattern (`LAAIR-\d+`, `SVAPI-\d+`).

**Score 0–4**: 0 = no tests; 1 = sparse (\<1 test per production class on average); 2 = some coverage, large gaps; 3 = mostly mirrored, a few classes uncovered; 4 = every production class has a `*Tests` peer.

### 2. Coverage

Use coverlet via the standard test command. See [reference/coverage.md](coverage.md) for the full workflow; this dimension needs both the headline numbers **and** — when the score is low — the specific classes that need tests.

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./.audit-coverage
# locate the Cobertura file
find ./.audit-coverage -name coverage.cobertura.xml
```

For each `coverage.cobertura.xml`, read `line-rate` and `branch-rate` from the root element. Report headline per project and overall (weighted by line count).

**Score 0–4**: 0 = no coverage data produced; 1 = \<40% line-rate; 2 = 40–60%; 3 = 60–80%; 4 = >80% with branch-rate within 10 points of line-rate.

#### Headline contamination check

Before scoring, verify the headline reflects **first-party** assemblies only. List the `<package name="…">` elements in the Cobertura file:

```bash
grep -oE '<package name="[^"]+"' .audit-coverage/**/coverage.cobertura.xml | sort -u
```

If the list includes third-party assemblies (e.g., `SvApi.Caching`, `SvApi.SSO`, `TrafficLogger`), the headline is contaminated and the score is unreliable. Flag this as a finding in its own right ("coverage signal is contaminated — `coverlet.runsettings` needs `<Include>[FirstParty]*</Include>` filter") and use the first-party-only line-rate to score the dimension. Dimension 6 (Infrastructure) also flags this from the runsettings side — same root cause, different observation point; double-listing is fine.

#### Uncovered-class enumeration (required when Score ≤ 3)

Parse the Cobertura file to list classes with `line-rate < 0.8`. These are the classes the plan will target for new tests — emit them, don't summarize them. Headline percentage is the *signal*; the class list is the *work*.

```bash
# Classes under 80% line-rate, sorted worst first.
python3 - <<'PY' .audit-coverage/**/coverage.cobertura.xml
import sys, xml.etree.ElementTree as ET
for path in sys.argv[1:]:
    root = ET.parse(path).getroot()
    rows = []
    for cls in root.iter('class'):
        rate = float(cls.get('line-rate', 0))
        if rate < 0.8:
            name = cls.get('name')
            lines = sum(1 for _ in cls.iter('line'))
            rows.append((rate, lines, name, cls.get('filename')))
    for rate, lines, name, filename in sorted(rows):
        print(f"{rate:.2f}  {lines:4d}  {name}  ({filename})")
PY
```

Report the top ~25 (worst-first). Each row becomes a candidate work item for the plan: "write `*Tests.cs` for `<class>` to cover its public surface." If the list is enormous (>50 classes), group by namespace/folder and report group rollups instead of every class.

When Score is 4 (>80%), the enumeration is optional — the plan, if any, will be about polish, not net-new test classes.

### 3. Health signals

Look for SKILL.md ban violations. Each finding is concrete; quote the file:line.

```bash
# Thread.Sleep / Task.Delay in test code — Fast violation
grep -rEn 'Thread\.Sleep\(|Task\.Delay\(' --include='*.cs' tests/

# Real wall-clock in test code — Repeatable violation
grep -rEn 'DateTime\.(Now|UtcNow|Today)|DateTimeOffset\.(Now|UtcNow)' --include='*.cs' tests/

# Reflection on private members — ban
grep -rEn 'BindingFlags\.(NonPublic|Instance)|GetMethod\("[^"]+",' --include='*.cs' tests/

# Mocking concrete classes — ban (Substitute.For<ConcreteClass>; heuristic: not interface I…)
grep -rEn 'Substitute\.For<[A-HJ-Z][A-Za-z]*>' --include='*.cs' tests/

# Slowest 10 tests (run the suite first)
dotnet test --logger "console;verbosity=detailed" 2>&1 | \
    grep -E 'Passed.*\[' | sort -k3 -hr | head -10
```

Report each grep finding as a row: dimension, file:line, snippet. The slowest-10 list is informational, not a score driver unless any test exceeds a Fast budget (>500ms suggests integration test in a unit project).

**Score 0–4**: 0 = pervasive ban violations (>10 across the suite); 1 = several (5–10); 2 = a handful (1–5); 3 = zero ban violations, some slow tests; 4 = zero violations, every test under the Fast budget *or* explicitly categorized as integration.

### 4. Package compliance

Grep test csprojs for banned packages.

```bash
grep -rEnh '<PackageReference Include="(Moq|FluentAssertions|MSTest\.|NUnit|FakeItEasy|MSTest)"' \
    --include='*.csproj' tests/
```

Report any matches. Each line is a finding.

**Score 0–4**: 0 = multiple banned packages in active use; 2 = one banned package; 4 = no banned packages.

### 5. Pyramid shape (light read — full shape analysis is `critique`)

Count tests by category attribute or naming heuristic.

```bash
# Tests categorized as integration
grep -rEnh '\[Trait\("Category"\s*,\s*"Integration"\)\]|: IClassFixture<WebApplicationFactory' \
    --include='*.cs' tests/ | wc -l
```

Report ratio of integration vs unit. The audit notes the number; whether the ratio is *appropriate* is a `critique` question.

**Score 0–4**: 0 = >50% integration; 1 = 30–50%; 2 = 15–30%; 3 = 5–15%; 4 = \<5% (healthy pyramid). Score 0 also if there are *zero* integration tests in a project that needs them — but that judgment belongs in `critique`.

### 6. Infrastructure

Test discovery, run, and measurement wiring. Every check reads a file or greps for a switch; each finding has a path (or `ABSENT` if the file doesn't exist).

```bash
# Locate runsettings files
find . -name '*.runsettings' -not -path '*/bin/*' -not -path '*/obj/*'

# Required runsettings switches — run only if a runsettings file exists
grep -EnH '<Format>|<SkipAutoProps>|<ExcludeByAttribute>|<DeterministicReport>|<Include>' \
    $(find . -name '*.runsettings' -not -path '*/bin/*' -not -path '*/obj/*')

# Test csproj wiring — required properties + SDK packages
grep -EnH '<IsTestProject>|<IsPackable>|Microsoft\.NET\.Test\.Sdk|xunit\.runner\.visualstudio|coverlet\.collector' \
    $(git ls-files '*.Tests.csproj')

# CI test step — Azure Pipelines first, GitHub Actions fallback
grep -nE 'dotnet test|XPlat Code Coverage|--collect|PublishCodeCoverageResults|threshold' \
    azure-pipelines.yml .github/workflows/*.yml 2>/dev/null

# xunit runner config (parallelism, diagnostic mode)
find . -name 'xunit.runner.json' -not -path '*/bin/*' -not -path '*/obj/*'
```

Each absent or misconfigured switch is a finding. Quote `file:line` (or `ABSENT`):

- No `*.runsettings` file at all → coverage is raw; auto-properties and generated code pad the headline.
- `<SkipAutoProps>` missing or `false` → trivial `get;`/`set;` accessors count toward coverage.
- `<ExcludeByAttribute>` missing `CompilerGeneratedAttribute` / `GeneratedCodeAttribute` / `Obsolete` → source-generated and obsolete members count.
- `<Include>[FirstParty]*</Include>` (or equivalent first-party filter) absent → third-party assemblies contaminate the headline.
- `<Format>` not `cobertura` → the parser in Coverage dimension and downstream skills can't read the output.
- Test csproj missing `<IsTestProject>true</IsTestProject>` → `dotnet test` may skip the project silently.
- Test csproj missing `<IsPackable>false</IsPackable>` → risks the test assembly being packed into a nupkg.
- Test csproj missing any of `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector` → discovery, runner, or coverage collection silently no-ops.
- CI yml runs `dotnet test` but does not pass `--collect:"XPlat Code Coverage"` → coverage isn't being produced in CI.
- CI produces coverage but does not gate on a threshold → drift is invisible.
- `xunit.runner.json` with `"parallelizeAssembly": false` *and* `"parallelizeTestCollections": false` → needlessly slow CI without justification.

**Score 0–4**: 0 = no `*.runsettings` and CI does not collect coverage; 1 = runsettings present but missing two or more of `SkipAutoProps` / `ExcludeByAttribute` / first-party filter, *or* test csprojs missing the SDK package trio; 2 = runsettings mostly right but CI either skips coverage or has no threshold gate; 3 = runsettings correct, CI collects coverage but has no threshold gate (or a token gate ≤ 30%); 4 = runsettings configured, every test csproj has the SDK package trio + `<IsTestProject>` + `<IsPackable>false</IsPackable>`, CI runs `dotnet test` with coverage collection and a real threshold gate.

The full runsettings template lives at [templates/coverage/runsettings.xml](../templates/coverage/runsettings.xml); csproj layout rules are in [reference/xunit-setup.md](xunit-setup.md). Quote these as the fix target when emitting findings — audit stays descriptive, but the user needs to know where the correct shape is documented.

## Output format

A scored report, dimensions in order, worst-first. Quote file:line for every finding. Close with one sentence on the single worst dimension and a routing suggestion (see "Next-step routing" below).

### Next-step routing

`audit` is a complete artifact on its own. The closing recommendation depends on what scored worst:

- **Coverage ≤ 3 or Health ≤ 2** → go straight to `to-plan` with this audit as the input. These are *measurement* findings (raise %, kill ban violations, cap slow tests) and don't need critique's architectural pass to become actionable. The uncovered-class list emitted above is the work breakdown. (Coverage uses ≤ 3 because anything under 80% has measurable test-writing work; Health uses ≤ 2 because a single-digit number of slow tests is inline-fixable without a plan.)
- **Pyramid ≤ 2** → load `critique` next. Pyramid shape is a question about architecture — whether the production code can be unit-tested at all — and that judgment lives in critique, not audit.
- **Inventory ≤ 2 with healthy Coverage/Health** → the issue is structural mirroring / missing `*Tests.cs` peers. Load `critique` for the prescriptive read.
- **Compliance ≤ 2** → banned-package finding is a one-line fix; usually doesn't need a plan at all. Fix inline and re-audit.
- **Infrastructure ≤ 2 with ≤ 4 findings** → each finding is a one-line edit to a runsettings, csproj, or CI yml file. Fix inline and re-audit. Cross-link [templates/coverage/runsettings.xml](../templates/coverage/runsettings.xml) and [reference/xunit-setup.md](xunit-setup.md) as the shape targets.
- **Infrastructure ≤ 1 with > 4 findings spanning runsettings + csproj + CI** → `to-plan`; the findings list is the work breakdown.
- **Everything ≥ 3** → no follow-up needed. The suite is healthy; this audit is a baseline.

If the user already ran `critique` in this conversation, suggest `to-plan` regardless of scores — they're done gathering inputs.

```
.NET test audit — <target>
Date: <YYYY-MM-DD>

Dimension       Score  Headline
-----------     -----  ----------------------------------------
Inventory       3      48 tests across 4 projects, 2 skipped (1 without ticket)
Coverage        2      53% line / 41% branch (overall)
Health          1      6 Thread.Sleep, 3 DateTime.Now violations
Compliance      4      No banned packages
Pyramid         3      8% integration
Infrastructure  2      3 wiring gaps

Findings (worst first):

[high] tests/Foo.Tests/BarTests.cs:42 — Thread.Sleep(500) in Bar_WhenAsync_ShouldReturn
    Thread.Sleep(500); // wait for async event
    Violates: Fast (SKILL.md ban). Replace with TimeProvider or awaitable signal.

[high] ...

Next: Health is the lowest score (measurement-shaped). Go to `to-plan` with this audit as input — the ban-violation file:line list is the work breakdown.
```

If a check fails to run (no `dotnet test` build, missing solution file, no coverage tool), report the failure verbatim and lower confidence in the score — do not invent.

## What audit is not

- It does not propose fixes. `critique` does.
- It does not write code or run modifications.
- It does not subjectively grade test "quality" beyond the dimensions above. There is no "vibes" score.

## Cross-references

- [reference/coverage.md](coverage.md) — full coverage workflow if the headline numbers warrant a deeper read.
- [reference/critique.md](critique.md) — load after audit if the prescriptive next step is needed.
- [reference/xunit-setup.md](xunit-setup.md) — canonical test csproj / folder layout; the shape Infrastructure dimension scores against.
- [templates/coverage/runsettings.xml](../templates/coverage/runsettings.xml) — full runsettings template; the shape Infrastructure dimension scores against.
- `agents/dotnet-test-reviewer.md` — delegate to it for per-file review when audit surfaces a single bad test class.
