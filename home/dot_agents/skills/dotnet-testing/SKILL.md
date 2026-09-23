---
name: dotnet-testing
description: |
  .NET testing guidance for writing, structuring, reviewing, planning, and troubleshooting unit and integration tests. Use for xUnit or TUnit; NSubstitute test doubles; AutoFixture or Bogus data; AwesomeAssertions; FluentValidation tests; TimeProvider and IFileSystem isolation; coverlet coverage; WebApplicationFactory endpoint tests; Testcontainers databases; .NET Aspire distributed tests; test project setup, naming, migrations, audits, critiques, and improvement plans. Trigger whenever a user asks to write or fix .NET tests, verify behavior, mock dependencies, generate test data, assert objects, measure coverage, scaffold a test project, review a test suite, or turn findings into implementation tasks. Excludes unrelated application architecture and NUnit/MSTest except migrations to xUnit.
user-invocable: true
argument-hint: "[topic] [your question]"
---

# .NET Testing

One umbrella for every .NET testing topic. Load this skill, then route by topic argument to the matching reference under `reference/<topic>.md`. Shared rules below apply to every topic.

## Setup

Before answering:

1. **Check the consumer repo's `Directory.Packages.props`** for the canonical versions of test packages (xunit, NSubstitute, AutoFixture, Bogus, AwesomeAssertions, FluentValidation.AspNetCore, coverlet.collector, Microsoft.NET.Test.Sdk, Testcontainers.*, Aspire.*). Use those versions in any code you produce — do not invent versions.
2. **Check for existing test projects** in `tests/` or `*.Tests/` patterns. Match the layout the repo already uses; do not introduce a parallel structure.
3. **Load the relevant reference** by Reading `reference/<topic>.md` based on the user's argument or, if the keyword is unambiguous, on the user's intent.

## Shared rules

These apply regardless of which reference is loaded.

### FIRST principles

Every test must be:

- **Fast** — milliseconds. No I/O, no network, no `Task.Delay`, no real database. If a test needs a real dependency, see [reference/testcontainers.md](reference/testcontainers.md) or [reference/aspnet-integration.md](reference/aspnet-integration.md) — it's an integration test, not a unit test.
- **Independent** — each test builds fresh state. No test ordering. No `IClassFixture` mutable state. Static state is a defect.
- **Repeatable** — same result every run, every machine. No `DateTime.Now`, no `DateTimeOffset.UtcNow`, no `Random` without a seed, no `Guid.NewGuid()` as the system under test. Inject a `TimeProvider` (see [reference/datetime.md](reference/datetime.md)).
- **Self-validating** — pass/fail by assertion alone. No "look at the console output and decide." Use AwesomeAssertions (see [reference/awesome-assertions.md](reference/awesome-assertions.md)).
- **Timely** — write the test alongside (or before) the code, not weeks later.

### 3A structure

Every test method is Arrange / Act / Assert, in that order, with **at most one Act**:

```csharp
[Fact]
public void Method_Scenario_Expected()
{
    // Arrange
    var sut = new Sut();
    const int input = 1;

    // Act
    var result = sut.Method(input);

    // Assert
    result.Should().Be(expected);
}
```

If a test needs two Acts to assert, it is two tests.

### Naming convention

`[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`. Underscore-separated. PascalCase each part.

- Good: `IsValidEmail_WhenInputIsNull_ShouldReturnFalse`
- Good: `ProcessOrder_WhenInventoryIsEmpty_ShouldThrowInvalidOperationException`
- Bad: `TestEmail`, `EmailTest1`, `Test_IsValidEmail`, `is_valid_email_returns_false`

Class name: `{ClassUnderTest}Tests` in a `{ProductionNamespace}.Tests` namespace. One test class per production class. Folder structure under `tests/` mirrors `src/`.

See [reference/naming.md](reference/naming.md) for the full rule set.

### Repo-default packages

xUnit-based. No other framework unless the user is migrating *to* xUnit (see [reference/xunit-upgrade.md](reference/xunit-upgrade.md)) or evaluating TUnit (see [reference/tunit.md](reference/tunit.md)).

| Concern         | Package                                                                          | Reference                                                                                                                                                                  |
| --------------- | -------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Runner          | `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`                 | [reference/xunit-setup.md](reference/xunit-setup.md)                                                                                                                       |
| Assertions      | `AwesomeAssertions`                                                              | [reference/awesome-assertions.md](reference/awesome-assertions.md), [reference/complex-equivalency.md](reference/complex-equivalency.md)                                   |
| Test doubles    | `NSubstitute`                                                                    | [reference/nsubstitute.md](reference/nsubstitute.md)                                                                                                                       |
| Test data       | `AutoFixture` (+ `AutoFixture.AutoNSubstitute`, `AutoFixture.Xunit2`)            | [reference/autofixture.md](reference/autofixture.md)                                                                                                                       |
| Fake data       | `Bogus`                                                                          | [reference/bogus.md](reference/bogus.md)                                                                                                                                   |
| Validator tests | `FluentValidation.TestHelper`                                                    | [reference/fluentvalidation.md](reference/fluentvalidation.md)                                                                                                             |
| Coverage        | `coverlet.collector`                                                             | [reference/coverage.md](reference/coverage.md)                                                                                                                             |
| Integration     | `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.*`, `Aspire.Hosting.Testing` | [reference/aspnet-integration.md](reference/aspnet-integration.md), [reference/testcontainers.md](reference/testcontainers.md), [reference/aspire.md](reference/aspire.md) |

**Do not add** `MSTest`, `NUnit`, `FluentAssertions` (the legacy MIT fork), `Moq`, `FakeItEasy`. They are not the standard in this repo's solutions.

Pin all versions in `Directory.Packages.props`; the scaffold script (`scripts/new-test-project.ps1`) reads from there. Do not hard-code versions in `.csproj` files.

### Absolute bans

Match-and-refuse. If you're about to write any of these, rewrite the test differently.

- **Never mock a concrete class.** Test doubles target abstractions only — `interface I…` or `abstract class …`. If you need to substitute a concrete class, the production code has a missing seam.
- **Never test private or internal members through reflection.** `InternalsVisibleTo` for friend-assembly access in narrowly-scoped cases is acceptable; `BindingFlags.NonPublic` is not. Test the public surface; if a private behavior is worth testing in isolation, it deserves its own type.
- **Never `Thread.Sleep` or `Task.Delay` to wait for timing.** Inject `TimeProvider`. For async coordination use awaitable signals (`TaskCompletionSource`).
- **Never share mutable state between tests.** `IClassFixture<T>` is acceptable when the fixture is immutable after construction; `ICollectionFixture<T>` is acceptable only with an explicit, documented reason.
- **Never assert on log message text as the primary verification.** Assert behavior. Log inspection is secondary diagnostic, never the gate.
- **Never write a test that requires a specific run order.** xUnit does not guarantee order, and depending on it makes the suite non-deterministic.
- **Never commit `[Fact(Skip = "...")]` or `[Theory(Skip = "...")]` without a tracked work-item key in the skip message.** Example: `Skip = "LAAIR-2099 — pending repo fixture"`.

## Routing rules

1. **No argument**: render the topic table below and ask which topic to load.
2. **First word matches a topic**: load `reference/<topic>.md` via Read. Everything after the topic name is the user's specific question — apply the reference's guidance to it.
3. **First word doesn't match a topic**: general invocation. Apply the shared rules above and propose the one or two most relevant topics before answering. If you load a reference on inference, say which one and why so the user can redirect.

For multi-topic tasks (e.g., "test a validator that depends on a clock and the file system"), load each topic's reference in sequence — `reference/fluentvalidation.md`, then `reference/datetime.md`, then `reference/filesystem.md` — applying the shared rules across all of them.

## Topics

### Actions (meta-commands that operate on a target)

Two independent flows share a downstream. Pick the verb that matches what you want fixed; each is a complete artifact on its own.

```
audit     → to-plan → to-issues   (measurement-driven: coverage, ban violations, slow tests)
critique  → to-plan → to-issues   (architecture-driven: pyramid shape, coupling, structural drift)
```

`audit` and `critique` do **not** gate each other. Run either alone, or run both and let `to-plan` synthesize. Skip `to-plan` entirely when the work is small enough to fix inline.

| Topic       | Reference                                        | Use when                                                                                                                                                                                                                                                                                                                                                                       |
| ----------- | ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `audit`     | [reference/audit.md](reference/audit.md)         | Measuring the current state — descriptive, runs commands, scores 5 dimensions (Inventory, Coverage, Health, Compliance, Pyramid), enumerates uncovered classes when Coverage is low. Onboarding, baselining before a refactor, periodic drift check, or when the goal is "raise coverage / kill ban violations / cap slow tests."                                              |
| `critique`  | [reference/critique.md](reference/critique.md)   | Prescriptive architectural review — pyramid shape, coupling, structural mirroring, missing categories, cross-cutting smells. Output is a prioritized refactor list. Stands alone; no audit prerequisite.                                                                                                                                                                       |
| `to-plan`   | [reference/to-plan.md](reference/to-plan.md)     | Turn audit findings, critique findings, or both into a single durable plan document (committed markdown). Audit-only input produces a measurement-driven plan (raise %, kill bans); critique-only input produces an architecture-driven plan (split, extract, refactor); both inputs synthesize. Skip when the audit/critique found three small things you can fix inline now. |
| `to-issues` | [reference/to-issues.md](reference/to-issues.md) | Decompose a `to-plan` document's Work Breakdown into vertical-slice TaskCreate tasks in dependency order, then hand off to the agent for in-conversation implementation. Tasks live in this conversation, not in an external tracker.                                                                                                                                          |

### Foundations

| Topic             | Reference                                                    | Use when                                                                                                     |
| ----------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| `fundamentals`    | [reference/fundamentals.md](reference/fundamentals.md)       | Learning testing from scratch; deeper FIRST + 3A treatment than the shared section above.                    |
| `xunit-setup`     | [reference/xunit-setup.md](reference/xunit-setup.md)         | Creating a test project, `dotnet new xunit`, csproj layout, `xunit.runner.json`, test-project folder layout. |
| `naming`          | [reference/naming.md](reference/naming.md)                   | Naming tests, naming classes, naming Theory data, edge cases for the three-part pattern.                     |
| `builder-pattern` | [reference/builder-pattern.md](reference/builder-pattern.md) | Test Data Builder pattern, fluent `.With…()` builders, replacing dense Arrange blocks.                       |
| `output-logging`  | [reference/output-logging.md](reference/output-logging.md)   | `ITestOutputHelper`, diagnosing flaky tests with structured test output.                                     |

### Test data

| Topic         | Reference                                            | Use when                                                                                                                                                |
| ------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `autofixture` | [reference/autofixture.md](reference/autofixture.md) | AutoFixture basics, `ISpecimenBuilder` customization, AutoData/Theory, NSubstitute integration, Bogus integration — one document, sections per concern. |
| `bogus`       | [reference/bogus.md](reference/bogus.md)             | Realistic fake data via `Faker<T>` (names, addresses, emails, dates). Use standalone or together with AutoFixture.                                      |

### Test doubles

| Topic         | Reference                                            | Use when                                                                                                                       |
| ------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `nsubstitute` | [reference/nsubstitute.md](reference/nsubstitute.md) | Mocks, stubs, spies, fakes. `Substitute.For`, `Returns`, `Received`, `Throws`, `Arg.Any`, `Arg.Is`, `ConfigureAwait` patterns. |

### Assertions

| Topic                 | Reference                                                            | Use when                                                                                   |
| --------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `awesome-assertions`  | [reference/awesome-assertions.md](reference/awesome-assertions.md)   | Fluent `Should()` assertions across primitives, collections, exceptions, async results.    |
| `complex-equivalency` | [reference/complex-equivalency.md](reference/complex-equivalency.md) | `BeEquivalentTo`, deep object/DTO comparison, exclusion rules, custom equivalency steps.   |
| `fluentvalidation`    | [reference/fluentvalidation.md](reference/fluentvalidation.md)       | Testing `AbstractValidator<T>` rules with `TestValidate` / `ShouldHaveValidationErrorFor`. |

### Special scenarios

| Topic        | Reference                                          | Use when                                                                                                                              |
| ------------ | -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `datetime`   | [reference/datetime.md](reference/datetime.md)     | Testing time-dependent code. `TimeProvider`, `FakeTimeProvider`, avoiding `DateTime.Now` in production code so it can be substituted. |
| `filesystem` | [reference/filesystem.md](reference/filesystem.md) | Testing file/directory code via `System.IO.Abstractions` (`IFileSystem`, `MockFileSystem`).                                           |

### Coverage

| Topic      | Reference                                      | Use when                                                                                    |
| ---------- | ---------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `coverage` | [reference/coverage.md](reference/coverage.md) | Measuring with coverlet, Cobertura output, CI threshold gates, `[ExcludeFromCodeCoverage]`. |

### Integration

| Topic                | Reference                                                          | Use when                                                                                                                            |
| -------------------- | ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------- |
| `aspnet-integration` | [reference/aspnet-integration.md](reference/aspnet-integration.md) | ASP.NET Core endpoint tests. `WebApplicationFactory`, `TestServer`, full CRUD workflow tests, custom service registration in tests. |
| `testcontainers`     | [reference/testcontainers.md](reference/testcontainers.md)         | Containerized SQL Server, Postgres, MySQL, MongoDB, Redis, Elasticsearch tests. Real database behavior under Docker.                |
| `aspire`             | [reference/aspire.md](reference/aspire.md)                         | .NET Aspire `DistributedApplication` testing, microservice and inter-service communication tests.                                   |
| `tunit`              | [reference/tunit.md](reference/tunit.md)                           | TUnit framework basics, advanced DI and parallel execution, migration from xUnit.                                                   |
| `xunit-upgrade`      | [reference/xunit-upgrade.md](reference/xunit-upgrade.md)           | Migrating xUnit 2.x → 3.x, framework-version migration patterns.                                                                    |

## Scripts, agents, and templates

- `scripts/new-test-project.ps1` — scaffolds a new test project against the consumer's `Directory.Packages.props` versions. Use when standing up a fresh test project; do not hand-craft the `.csproj`.
- `agents/dotnet-test-reviewer.md` — specialist reviewer for existing tests. Delegate to it when reviewing a PR's tests or auditing a suite against the shared rules above. The agent inherits this skill's shared rules; you do not need to re-explain them.
- `templates/<topic>/*.cs` (and `*.csproj`, `*.runsettings`) — ready-to-copy code samples organized per topic. References link to them with relative paths (`../templates/<topic>/<file>`). Load a template **only when the user's question demands the full example**; the reference itself carries enough illustrative inline snippets for most questions. Templates are real artifacts a developer can copy into a test project.

## On asking the user

If the user's request fits multiple topics ambiguously (e.g., "this DateTime comparison fails" — could be `datetime`, `complex-equivalency`, or `awesome-assertions`), ask one clarifying question with the candidate topics named. Don't load three references speculatively.
