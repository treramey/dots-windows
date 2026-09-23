# To Issues

Takes a plan markdown written by `/dotnet-testing to-plan` and creates one `TaskCreate` task per Work Breakdown slice, in dependency order. The agent then works through tasks in order, marking each `in_progress` / `completed`.

Tasks live in this conversation only. This skill does NOT file to ADO, GitHub, or any external tracker. If your team needs work items in their tracker, run any ADO CLI step or use the global `/to-issues` skill separately — the namespace prefix prevents collision with this verb.

## Input

A path to a plan markdown (positional arg), defaulting to the most-recently-modified file in `docs/testing-plans/`. If none exists, ask the user to run `/dotnet-testing to-plan` first.

The plan must contain a `## Work breakdown` section in the [to-plan.md](to-plan.md) shape. If parsing fails, surface the malformed section verbatim — do NOT guess.

## Process

1. Read the plan. Parse the Work Breakdown section into candidate slices. For each: title, scope, acceptance, blocked-by, AFK/HITL.

2. Re-check each slice against the rules below. The plan author should have applied them; re-check anyway. Any violation: surface it with a proposed split before continuing.

3. **Quiz the user on the final task list.** Show the slices in dependency order with title, type, scope, acceptance, blocked-by. Ask:

   - Does the order feel right?
   - Anything to merge or split?
   - Anything to drop (out of scope after seeing the full list)?

   Iterate until the user approves.

4. Create tasks via `TaskCreate` in dependency order so blocking IDs resolve to real values. For each approved slice:

   - `TaskCreate({ subject: "<title>", description: "<scope + acceptance + plan path>" })`
   - For dependents: `TaskUpdate({ taskId, addBlockedBy: ["<blocker ID>"] })`

5. Hand off to implementation. Pick the first unblocked task, set it `in_progress`, and begin work. Re-read the plan markdown when context for a slice is needed.

<vertical-slice-rules>
- Each slice cuts end-to-end — production code change + test rewrite + verification — when the plan needs production changes. Test-only plans (naming, library swap) may have test-only slices.
- One demoable behavior change per slice. Mark HITL only when human judgment is required: dropping a test, changing unit/integration ratios, choosing between two valid patterns. Mechanical fixes are AFK.
- Prefer thin over thick. A slice that spans more than three projects or more than ~30 files is two slices.
- Circular dependencies between slices mean one slice, not two.
</vertical-slice-rules>

<slice-task-template>

**Title:** <slice title from plan>

**Scope:** <one-line scope>

**Acceptance:**

- <gate condition from plan>

**Source:** docs/testing-plans/<file>.md, slice #<n>

**Type:** AFK or HITL

</slice-task-template>

Do NOT file to ADO / GitHub / Jira. Do NOT re-plan inline — if slices look wrong, fix the plan markdown and re-run this skill. Do NOT auto-prioritize beyond dependencies; severity and risk were inputs to the plan's strategy, not to this step.
