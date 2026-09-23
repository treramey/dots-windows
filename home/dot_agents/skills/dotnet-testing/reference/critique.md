# Critique

Architectural review of a test suite. **Opinionated** and **prescriptive** — the counterpart to `audit`'s descriptive measurement. Where audit asks "what is the suite?", critique asks "what should it become?"

Output is a prioritized list of refactors with effort estimates. No commands required unless evidence is needed; this is a thinking exercise informed by the code.

Critique is a **complete artifact on its own**. It does not require `audit` to have run first. Run it standalone when the question is "what should this suite become?"; pair it with audit only when you also want the measured baseline.

## When to use

- When the user is about to extend the suite (adding a feature) and wants to know if the structure can absorb the addition.
- When tests pass but feel painful — slow CI, flaky runs, hard to onboard new contributors, "we have tests but bugs still ship."
- When migrating between testing patterns (e.g., adopting Testcontainers, splitting unit/integration projects).
- After `audit` flagged a *structural* dimension (Pyramid, Inventory) — audit can spot the smell, but the prescriptive refactor lives here.

Critique is **not** the right tool for:

- Reviewing a single test file. Delegate to `agents/dotnet-test-reviewer.md` — it inherits the shared rules and reports findings at the line level.
- Finding *which* tests are missing. That's a `coverage` question. Load [reference/coverage.md](coverage.md), or run `audit` — its Coverage dimension now enumerates uncovered classes when the score is low.
- Measuring how good the suite is. That's `audit`. Load [reference/audit.md](audit.md).
- Raising the coverage percentage. That's a measurement-driven outcome — run `audit` → `to-plan` instead. Critique's recommendations are about *shape*, not *quantity*.

## Scope decisions

Critique operates at the **project and solution level**. If the user's target is:

- A single `*Tests.cs` file → delegate to `dotnet-test-reviewer` agent. Do not run critique.
- A single `*.Tests.csproj` → run critique scoped to that project.
- A solution → run critique across all test projects, reporting per-project findings + a cross-cutting summary.

If the scope is unclear, ask. Don't run a solution-wide critique when the user wanted file-level feedback.

## Procedure

Work through these dimensions in order. Each produces zero or more recommendations. Stop work and synthesize once you have enough material.

### 1. Pyramid shape

What ratio of unit / integration / e2e tests does the suite actually have? Is it appropriate for the production code?

Evidence to gather:

```bash
# Unit-ish: pure xUnit Fact/Theory, no WebApplicationFactory or container fixtures
# Integration-ish: WebApplicationFactory, Testcontainers, IClassFixture<WebApplicationFactory<…>>
grep -rEn ': IClassFixture<WebApplicationFactory|: IAsyncLifetime|TestcontainersBuilder|IContainer ' \
    --include='*.cs' tests/
```

Smells:

- **Inverted pyramid**: >30% integration in a domain-heavy codebase. Integration tests stress wiring; if business logic only has integration coverage, the logic isn't well-isolated.
- **No integration tests** in a project that owns HTTP endpoints, EF queries, or message handlers. Unit tests can't catch wire-up bugs.
- **All-or-nothing**: 100% unit or 100% integration. Both reads as a smell — either the production code is hostile to isolation, or the team distrusts unit tests.

Recommendations carry verbs like "extract a `Calculator` from `OrderService` so business rules can be unit tested without WebApplicationFactory" — specific, not "add more unit tests."

### 2. Coupling between tests and production

How much do tests reach across module boundaries? Where does the test suite duplicate production wiring?

Evidence:

```bash
# InternalsVisibleTo claims — the test project is treated as a friend assembly
grep -rEn 'InternalsVisibleTo' --include='*.cs' --include='AssemblyInfo.cs' src/

# Shared test fixtures or base classes referenced across projects
grep -rEn 'TestBase|TestFixtureBase|: TestBase\b' --include='*.cs' tests/

# Test projects depending on multiple production projects
grep -rEnh '<ProjectReference Include="[^"]+\.csproj"' --include='*.Tests.csproj' tests/
```

Smells:

- **Friend-assembly access for routine cases**. `InternalsVisibleTo` for narrow legitimate cases (testing a `record` ctor) is fine; spraying it across the codebase to test "easier" indicates production design is hostile to testability.
- **Shared mutable test base classes**. A 200-line `TestBase` consumed by 40 test classes couples them all to the same setup. When the setup changes, every test inherits the breakage.
- **Test project referencing five production projects**. The test sits at the wrong layer — it's testing more than one unit.

Recommendation pattern: "Move `OrderTestBase` into `tests/Common/` if shared, otherwise inline the two methods each consumer actually uses."

### 3. Structural mirroring

Does `tests/` mirror `src/`? Are class names paired?

Evidence:

```bash
# Compare folder structures
diff <(find src -type d | sed 's|^src|tests|') <(find tests -type d) | head

# Production classes without a Tests peer
comm -23 \
  <(find src -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | xargs basename -a -s .cs | sort -u) \
  <(find tests -name '*Tests.cs' | xargs basename -a -s Tests.cs | sort -u) | head
```

Smells:

- **`tests/` not mirroring `src/`** — finding the tests for `Foo` requires guessing the project and folder. Onboarding friction.
- **Major production class with no `*Tests.cs` peer**. Specifically: types with public methods (excluding records, POCOs, DTOs, generated code) that have no corresponding test class.
- **`MiscTests.cs` or `BugTests.cs` or `IntegrationTests.cs`** — catch-all test classes that aren't paired to any production class. Tests in those files get added but rarely refactored.

Recommendation pattern: "Move `MiscTests.cs` contents into per-class test files matching their production class names."

### 4. Missing test categories

Not coverage % (audit owns that). Which *categories* of test are entirely absent?

For each test project, ask:

- Is there a test for the **happy path** of each public method?
- Is there a test for at least one **exception path** per method that documents thrown exceptions?
- For methods with **inputs**, are boundary cases tested (null, empty, zero, max)?
- For methods with **side effects** (database write, message publish, file write), is the side effect verified?
- For **async methods**, is cancellation tested?
- For **dispose / cleanup**, is the contract verified?

Smells:

- **Only happy-path tests**. The test class has `…_WhenInputIsValid_ShouldReturnX` but no `…_WhenInputIsInvalid_ShouldThrowY`.
- **No cancellation tests** for `CancellationToken`-accepting methods.
- **No null/empty boundary tests** for collection or string inputs.

Recommendation pattern: "Add boundary tests for `OrderService.ProcessAsync`: null input, cancelled token, empty order lines."

### 5. Cross-cutting smells

Things only visible across the whole suite.

- **Duplicated arrange blocks**. The same 8-line `Substitute.For<>().Returns()` setup appears in 12 tests. Suggests a missing builder (see [reference/builder-pattern.md](builder-pattern.md)).
- **Shared mock instances**. Multiple test classes new up the same set of mocks; cross-link to [reference/autofixture.md](autofixture.md) for `AutoFixture.AutoNSubstitute` and the `[AutoData]` pattern.
- **Inconsistent naming**. Half the suite uses `Method_Scenario_Expected`; half uses `Should_DoThing_When_X`. Pick one; see [reference/naming.md](naming.md).
- **Tests that test the framework**. `IsValidEmail_ReturnsTrueForValidEmail` (yes, that's what the method's name says it does) — these tests don't add safety.
- **Brittle assertions**. Heavy `BeEquivalentTo` chains where a focused property check would do (see [reference/complex-equivalency.md](complex-equivalency.md) for when each is appropriate).

## Output format

Prioritized recommendation list. Each item:

```
[priority] Title — affected scope
  Evidence (file paths or grep output that motivated this).
  Why it matters (which shared rule or smell).
  Refactor (concrete suggestion, not "improve this").
  Effort: S (hours) / M (1–2 days) / L (1 week+)
```

Group by priority (P0 = blocks the next safe refactor, P1 = pays off within a sprint, P2 = nice-to-have). Close with one sentence: the single highest-leverage change, and which `reference/<topic>.md` to load if the user wants to start it.

Maximum 8 recommendations. If you have more than 8, you've stopped prioritizing — re-cut.

## What critique is not

- Not a checklist. The procedure above is the questions to ask; the *answer* is the prioritized recommendation list, not a checked-off scorecard.
- Not a rewrite. You report what should change; the user (or a follow-up flow) makes the change.
- Not per-file review — delegate that to `agents/dotnet-test-reviewer.md` (it inherits the shared rules and reports line-level findings).
- Not coverage-driven. "X% coverage" is not a recommendation. "The OrderService boundary tests are missing for null and cancelled cases" is.

## Cross-references

- [reference/audit.md](audit.md) — descriptive counterpart; run first if you don't already have a baseline.
- [reference/builder-pattern.md](builder-pattern.md) — common refactor target when duplicated arrange blocks surface.
- [reference/autofixture.md](autofixture.md) — common refactor target when shared mock setups surface.
- [reference/naming.md](naming.md) — load if naming inconsistency is the dominant finding.
- `agents/dotnet-test-reviewer.md` — delegate file-scoped review here, not to this reference.
