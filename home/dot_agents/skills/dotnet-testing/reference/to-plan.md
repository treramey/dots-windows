# To Plan

Takes audit and/or critique findings from the current conversation and produces a testing-improvement plan document. Do NOT modify any test or production code — this skill writes one markdown file.

Output path: `docs/testing-plans/<YYYY-MM-DD>-<slug>.md`. `<slug>` is the dominant theme (e.g. `ban-violations`, `coverage-gap`, `pyramid-rebalance`). Create the directory if missing. Override with `--output <path>`.

## Input modes

`to-plan` accepts any of three input combinations. The plan's *shape* changes with the input — don't try to force every plan into the same template fields.

| Input             | Plan shape                                                                                                                                                                                                                            | Typical slices                                                                                                                                                                                                                |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Audit only**    | Measurement-driven. Goals are numeric: raise %, kill N violations, cap M slow tests. The work breakdown is dominated by net-new test writing and tool config changes.                                                                 | "Write `*Tests.cs` for `<class>`" (one per uncovered class or namespace group); "Add `coverlet.runsettings` first-party filter"; "Replace `Thread.Sleep` in `<file>:<line>`"; "Move slow test to `IntegrationTests` project". |
| **Critique only** | Architecture-driven. Goals are structural: extract a seam, split a project, kill a shared base class. The work breakdown is dominated by production-code refactors that *enable* tests, paired with the test changes that prove them. | "Extract `IClock` and inject everywhere"; "Split `OrderService` into `Calculator` + `Orchestrator` so business rules can be unit-tested without WAF"; "Move `OrderTestBase` into per-test inlines".                           |
| **Both**          | Synthesis. Use when audit and critique both surfaced material findings and the plan needs to cover both axes (e.g., coverage gap *and* a structural barrier preventing the gap from closing).                                         | Order slices so architectural enablers come first, then the measurement-driven tests they unblock.                                                                                                                            |

If the user has run *neither* audit *nor* critique, do not synthesize — ask which one is appropriate for their question and load the corresponding reference first.

## Process

1. Identify which inputs are available in the current conversation. Treat `--audit <path>` / `--critique <path>` flags as overrides pointing at saved reports.

2. Pick the plan shape from the table above. If both inputs are present, decide whether to merge or to lead with one — usually critique-driven refactors are sequenced first when they unblock audit-driven test writing.

3. Distill the inputs into the template below. Don't paraphrase findings verbatim; the plan is a synthesis, not a digest. **Do not move audit findings into "Out of scope"** — if audit said Coverage is 1/4, the plan's Target state must include a coverage threshold and the Work Breakdown must contain the test-writing slices to reach it. Punting on measurement findings defeats the purpose of an audit-driven plan.

4. **Quiz the user on the Work Breakdown before writing.** Show the proposed slices as a numbered list — title, scope, blocked-by. Ask:

   - Does the granularity feel right?
   - Are the dependencies correct?
   - Should any slices be merged or split?

   Iterate until the user approves. The Work Breakdown is load-bearing — `/dotnet-testing to-issues` lifts it directly into tasks.

5. Write the plan to disk using the template. Confirm the path. Suggest `/dotnet-testing to-issues` as the next step.

<plan-template>

# Testing improvement plan — <slug>

> Generated <YYYY-MM-DD>. Source: \<audit (<date>) | critique (<date>) | audit (<date>) + critique (<date>)>.

## Current state

Distilled from the inputs. If audit ran: dimension scores, most-severe findings with file:line, coverage headline, ban-violation count, uncovered-class list (or rollup) when Coverage scored ≤ 2. If critique ran: top architectural smells with file:line evidence.

## Target state

What "good" looks like at the end of this work. Measurable. Examples: "Zero ban violations." "Coverage ≥ 70% line, ≥ 60% branch on first-party assemblies." "All `*Tests.cs` files match three-part naming." "No `Skip = "..."` without a ticket key." "Production `OrderService` split so its business rules can be unit-tested without WAF."

When the plan is audit-driven and Coverage was low, the Target state **must** include a concrete coverage threshold. Do not write a plan that observes "Coverage 1/4" in the current state and then omits a coverage target.

## Strategy

How we get there. Architectural decisions from critique, distilled to one or two sentences each. Examples: "Extract `IClock` and `FakeClock`. Inject everywhere production code reads `DateTime.UtcNow`." "Split integration tests into a new `*.IntegrationTests` project so they're excluded from the default `dotnet test` filter."

## Work breakdown

Vertical slices, ordered by dependency. Each slice cuts end-to-end (production change + test change + verification). Each slice is independently demoable.

1. **<title>**

   - Scope: <one-line scope>
   - Acceptance: <gate>
   - Blocked by: \<slice number, or None>
   - Type: AFK (default) or HITL

2. **<title>**

   - …

Prefer many thin slices over few thick ones.

## Out of scope

What this plan does NOT address. Common omissions: migrating off xUnit; performance / load / security testing; documentation reshuffles.

**Do not** put a finding from the source audit/critique into Out of scope unless the user explicitly carved it out during the Work Breakdown quiz. If audit flagged Coverage 1/4 and you find yourself writing "Coverage % uplift is out of scope," you're producing a different plan than the inputs justify — go back to step 3 and rebuild the Work Breakdown so the slices actually move the score.

## How we know we're done

Measurable conditions. When all are true, the plan is complete:

- [ ] `grep -rEn 'Thread\.Sleep|Task\.Delay' tests/` returns zero
- [ ] CI coverage gate at <threshold>% and passing
- [ ] Every `Skip = "..."` carries a `LAAIR-####` or `SVAPI-####` ticket key

If a condition is not measurable from `dotnet test` or `grep`, it does not belong in this list.

</plan-template>

<vertical-slice-rules>
- Each slice delivers a narrow but COMPLETE path through every layer touched (production code, tests, verification).
- A completed slice is demoable on its own. If finishing slice 5 doesn't visibly change anything, it's not a slice.
- Prefer many thin slices over few thick ones. A 6-hour slice that touches four projects is two slices.
- A slice that only changes test code is fine when the plan is purely test-quality (naming, library migration). A slice that requires a production change must include the production change in the same slice as the test change that needs it.
</vertical-slice-rules>

Do NOT create tasks, file issues, or modify code. This skill writes one file and stops.
