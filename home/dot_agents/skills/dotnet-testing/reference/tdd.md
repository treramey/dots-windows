# Test-Driven Development

Test-first authoring of new features and bug fixes via the red-green-refactor loop. Apply when the user wants TDD, says "red-green-refactor", asks for test-first development, or is adding a new feature where the public interface is not yet fixed.

The shared rules from `SKILL.md` still apply — FIRST, 3A, three-part naming, repo-default packages, absolute bans. TDD changes *when* you write the test, not *how*.

## Philosophy

**Tests verify behavior through public interfaces, not implementation details.** Code can change entirely; tests shouldn't.

**Good tests** are integration-style: they exercise real code paths through public APIs. They describe *what* the system does, not *how*. A good test reads like a specification — `CancelOrder_WhenOrderIsPending_ShouldMarkOrderCancelled` tells you exactly what capability exists.

**Bad tests** are coupled to implementation. They mock internal collaborators, reach into private state via reflection, or assert on the call order of internal methods. Warning sign: the test breaks when you refactor but the behavior hasn't changed.

See [reference/nsubstitute.md](nsubstitute.md) for what to mock (system boundaries only — interfaces representing external systems, time, file I/O, randomness) and what NOT to mock (your own concrete classes, internal collaborators, anything in the same module).

## Anti-pattern: horizontal slices

**DO NOT write all tests first, then all implementation.** Treating RED as "write five tests" and GREEN as "make all five pass" produces crap tests:

- Bulk-written tests verify *imagined* behavior, not *actual* behavior.
- You end up testing shapes (data structures, method signatures) rather than user-facing behavior.
- Tests become insensitive to real changes — they pass when behavior breaks and fail when behavior is fine.
- You outrun your headlights, committing to test structure before understanding the implementation.

```
WRONG (horizontal):
  RED:   test1, test2, test3, test4, test5
  GREEN: impl1, impl2, impl3, impl4, impl5

RIGHT (vertical):
  RED→GREEN: test1→impl1
  RED→GREEN: test2→impl2
  RED→GREEN: test3→impl3
```

One test → one implementation → repeat. Each test responds to what you learned in the previous cycle.

## Workflow

### 1. Plan

Before writing any code:

- [ ] Confirm with user what interface changes are needed (the public surface — the methods consumers call).
- [ ] Confirm with user which behaviors to test, in priority order.
- [ ] Identify opportunities for **deep modules**: a small interface hiding lots of implementation. Prefer deep over shallow — a class with two methods and 200 lines of logic beats one with twenty methods and 200 lines of logic.
- [ ] Design interfaces for testability: accept dependencies, don't create them; return results, don't produce side effects; keep parameter lists short.
- [ ] List behaviors to test (not implementation steps).
- [ ] Get user approval on the plan.

Ask: *"What should the public interface look like? Which behaviors are most important to test?"*

**You can't test everything.** Focus on critical paths and complex logic.

### 2. Tracer bullet

Write ONE test that confirms ONE thing. End-to-end through the public interface.

```
RED:   Write the test. Run `dotnet test --filter <name>`. Confirm it fails for the right reason
       (not a compile error, not "method not found" — an assertion failure).
GREEN: Write minimum production code to pass. Re-run. Confirm pass.
```

This is the tracer bullet — it proves the path works end-to-end (test wiring, project references, package resolution, the assertion library, the mock framework if any).

### 3. Incremental loop

For each remaining behavior:

```
RED:   Next test → fails for the right reason
GREEN: Minimum code → passes
```

Rules:

- **One test at a time.** Never write two failing tests in a row.
- **Only enough code to pass the current test.** No speculative features.
- **Don't anticipate future tests.** If you think "I should also handle the null case", that's the next RED, not premature code.
- **Test observable behavior.** Three-part name describes the behavior; the test exercises the public API; the assertion verifies the outcome the caller would see.

### 4. Refactor

After all tests pass, only then:

- **Never refactor while RED.** Get to GREEN first.
- Extract duplication.
- Deepen modules (move complexity behind simple interfaces; combine shallow helpers).
- Apply SOLID principles only where they emerge naturally — don't impose them.
- Consider what the new code reveals about existing code. If a new test forced an awkward hack, the existing design is wrong; fix it.
- Run the full test suite after each refactor step.

## Per-cycle checklist

```
[ ] Test name follows Method_Scenario_ExpectedBehavior — see reference/naming.md
[ ] Test arranges + acts + asserts in that order; one Act per test
[ ] Test uses public interface only — no reflection, no private accessors
[ ] Test exercises real code paths, not mocks of internal collaborators
[ ] Mocks (if any) target system boundaries — see reference/nsubstitute.md
[ ] Test would survive an internal refactor with no behavior change
[ ] Assertion uses AwesomeAssertions .Should() — see reference/awesome-assertions.md
[ ] Test FAILS for the right reason before production code is written
[ ] Production code added is the minimum to pass — no speculation
[ ] Full suite passes after each cycle
```

## Cross-references

- [reference/fundamentals.md](fundamentals.md) — FIRST and 3A applied in depth.
- [reference/naming.md](naming.md) — three-part name patterns for the failing test.
- [reference/nsubstitute.md](nsubstitute.md) — what to mock (boundaries) and what not to mock (your own code).
- [reference/awesome-assertions.md](awesome-assertions.md) — the `.Should()` assertion that drives the RED.
- [reference/builder-pattern.md](builder-pattern.md) — fluent `.WithX()` builders so the test's Arrange section reads as intent, not boilerplate.
- [reference/autofixture.md](autofixture.md) — when the test doesn't care about specific values, let AutoFixture supply them.
- `agents/dotnet-test-reviewer.md` — delegate to it after the cycle to lint the new test against shared rules before committing.

Do NOT write multiple failing tests in a single RED. Do NOT mock your own concrete classes. Do NOT refactor while any test is failing. Do NOT add code the current test doesn't demand.
