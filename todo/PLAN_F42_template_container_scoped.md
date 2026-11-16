# PLAN — F42: the C# template accepts the `container-scoped` marker, and its notes N1–N5 are reproduced

> Status: **epoch 1 open, 2026-11-16: approved by the plan gate (`53616e7e`, `proceed`, four findings accepted and folded in); built on the branch, before its code gate.** Branch `fix/f42-template-container-scoped` from `main` `07a0f5b`.
> Parent: `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` § *Epoch 9* items 2 and 4.2, rows F42-a, F42-b,
> F42-c; the follow-up F42 in `dew_flow_conventions · todo/PLAN_test_leftovers_merged_core_followups.md`; its Node half is
> `dew_flow_conventions · todo/PLAN_F41_container_scoped.md` (F41, approved by its plan gate on 2026-11-16). One PR, over
> this repository's PR cap by the owner's word of 2026-11-16. The copies follow it: benchmark re-syncs its adapted copy to
> this PR's merge commit (row F42-c), and rag_qln's E7.S3 copies from it.

## Plan changes

> Epoch 1 · approved 2026-11-16 · by reviewers · state: open

## Goal

The template (`tests/Mcp.Tests/Leftovers/`) reads the `container-scoped` marker exactly as the shipped Node guard will
after F41, case for case; and the five notes benchmark's E6.S2 left on copied template files are each reproduced, then
fixed if they reproduce and apply, or recorded with the run that did not reproduce them.

## The defect, and the notes (read at `07a0f5b`; no .NET run yet)

- **The marker.** `LeftoversGuardExemptions.cs:11` lists six exemptible classes, so
  `testsToDelete-exempt: container-scoped — …` is "an unknown class" (`:53`). The class's doc comment (`:5-7`) says "a
  marker that exempts nothing is a finding of class `exemption`", while `Read` (`:29-37`) passes a valid marker with
  nothing beside it: the comment is wrong, the code is what F41 relies on.
- **N1** (by reading): `Removal.RemoveFolder` ends in `Directory.Delete(dir, recursive: true)` (`Removal.cs:50-54`). On
  Windows that refuses a read-only file, and git marks its object files read-only, so a claimed folder holding a clone
  is neither removed by `Dispose` nor by the sweep there.
- **N2** (a code gate's note in benchmark): "`DirectoryLinks.Run` drains only stdout, after the exit". At `07a0f5b`
  `DirectoryLinks.cs:54-55` starts BOTH reads before the wait (`StandardOutput.ReadToEndAsync()`, `_ =
  StandardError.ReadToEndAsync()`), so the note may not reproduce as written; on the kill path (`:61-63`) the read tasks
  are left unawaited.
- **N3**: `DirectoryLinks.cs:33-34` gives `cmd /c mklink /J` its paths after a block list of command-interpreter
  characters; a path that holds one is skipped (`Assert.Skip`), never quoted.
- **N4**: `LeftoversGuardTests.MountedSelectorPick` (`LeftoversGuardTests.cs:573-592`) asserts `WaitForExit(2 min)`
  before anything kills the child, so a hanging `node` is left running after the assertion fails.
- **N5**: `LeftoversAudit.Discover` (`LeftoversAudit.cs:38-52`) returns `[]` when `Directory.Exists(tmp)` is false; a
  temp root that exists but cannot be read may read as empty (no claim found), which would let the audit pass a run that
  left a claimed folder.

## Decisions

1. **The marker, case for case with F41** (its plan's decisions 1–6): form unchanged; valid when the general reason rule
   holds AND an owner is named after `live and die with` / `lives and dies with` (an optional article `the`, `a`, `an`,
   `its`, `this`, `that`, then a word of at least two characters that is not an article); a declaration; clears
   `shared-store` on its line and the next only when the hit is `DROP DATABASE`, `DROP SCHEMA` or `FLUSHDB`; never any
   other class; the guard cannot know the dropped database is the container's (stated in the doc comment). In C#:
   `GuardExemptions` learns the kind, and `LeftoversGuard`'s per-line filter (`:190`) asks for the `shared-store` hit's
   kind, so `SharedStoreHits` (`:248`) gains a kind with its text (or a sibling that keeps its contract). Every caller of
   both is read first. The doc comment of `GuardExemptions` is corrected.
2. **A note is fixed only when it reproduces and applies.** Each reproduction is a test that runs here or on the hosted
   leg where the OS matters, RED first. One that does not reproduce is recorded in this plan with the run (its command
   and output), and nothing is changed for it.
3. **N1's fix, if it reproduces:** before the recursive delete on Windows, clear the read-only attribute on the folder's
   files without following a link or junction (a reparse point is unlinked, as `UnlinkTopLevelLinks` does at the top
   level; inside, the walk does not enter one). The test makes a claimed folder holding `git init` with one commit and
   removes it on `windows-latest`.
4. **N2:** the test makes the child write more stderr than a pipe holds (over 64 KiB) and asserts `Run` returns within
   its timeout WITH the child exited on its own (`TimedOut` false, exit code 0) and its stdout read whole — returning
   because the timeout killed a blocked child is not "not reproducing" (the plan gate, round 1). If so, N2 is recorded
   as not reproducing at `07a0f5b`; the unawaited reads on the kill path are awaited (bounded) only if a test shows a
   leak.
5. **N3:** quoting for `cmd` is not attempted (its quoting rules are not a list one can trust); the skip with its reason
   stays and is recorded as the bounded behaviour, unless a test shows a path WITHOUT a blocked character that `mklink`
   still mis-parses (a space: the test uses a link path with a space).
6. **N4:** the child is killed (whole tree) on the timeout path before the assertion fails, in a `try`/`finally`. The test
   is a seam: the program text is made to hang with a live event-loop handle (`setInterval(() => {}, 1000)`; an unresolved
   promise alone lets node exit) under a short timeout given to a test-only overload; the test first asserts the child
   was still running when the timeout came, then that it is gone after the failure (the plan gate, round 1).
7. **N5:** the test makes a temp root that exists and is unreadable (`chmod 000`, on Linux and macOS, not as root) with a
   claimed folder inside, AND a root that exists beneath a parent the process cannot traverse (the parent `chmod 000`),
   where `Directory.Exists` answers false. In both, `Discover` must not answer "nothing claimed": a root that is
   missing is told apart from one that cannot be reached (the parent's entry is read with the native `lstat` the template
   already uses; a permission error is not "missing"), and the audit throws `LeftoversAuditException` naming the root
   and the error, so it is red (the plan gate, round 1). The test skips, with its reason, on Windows (ACLs, not modes)
   and when the process is privileged (`Environment.IsPrivilegedProcess`: root reads through `chmod 000`).
8. **Builds here** use `-p:AppHostRuntimeIdentifier=linux-x64`, `-m:4` and `nice -n 19`; no Docker on this machine. The
   tests that need Docker are not part of this plan.

## Files

- `tests/Mcp.Tests/Leftovers/LeftoversGuardExemptions.cs`, `LeftoversGuard.cs`, `LeftoversGuardTests.cs` (the marker;
  N4).
- `tests/Mcp.Tests/Leftovers/Removal.cs` and a test beside its tests (N1); `tests/Mcp.Tests/DirectoryLinks.cs` and
  `DirectoryLinksTests.cs` (N2, N3); `tests/Mcp.Tests/Leftovers/LeftoversAudit.cs` and `LeftoversAuditTests.cs` (N5).
- `research/module_tests.md` (the template's grammar and limits), `research/PLAN_test_leftovers_E3.md` only if it states
  the grammar; `todo/README.md` (this plan's row).

## Test scenarios

The marker — the F41 plan's scenarios, in C#, against the template's `LeftoversGuard` (same names, same expectations):
P1–P7 clean (both spellings; over `DROP DATABASE`, `DROP SCHEMA`, `FLUSHDB`; same line; `DROP DATABASE production_db`
under a valid marker, the stated limit); N1–N4 and N16 `exemption` (no owner word, placeholder, no reason, neither
spelling, "with the"); N5–N8 `shared-store` (`TRUNCATE TABLE users`, `FLUSHALL`, `deleteMany({})`, `DROP TABLE users`);
N9 two lines above → `shared-store`; N10 `docker-run`; N11 `docker-name`; N12 `ryuk-off`; N13 `owner-blind`; N14
`raw-temp`; N15 the `shared-store` marker without `lock:` → `exemption`; N17 `raw-worktree`; N18 `legacy-name`. RED
first for new behaviour, then GREEN, then RED with the code change reverted; unchanged behaviour passes before and
after.

The notes — one test each as decisions 3–7 say, RED first where it reproduces.

The whole suite: `Mcp.Tests` on Linux here (the Docker-dependent tests excepted, named), and all three hosted legs green
on the PR.

## Reproductions (2026-11-16)

Machine: Linux (WSL2), no Docker, no Windows. Build: `nice -n 19 dotnet build tests/Mcp.Tests -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64` (CI builds
`Release`); runs: `nice -n 19 ./tests/Mcp.Tests/bin/Release/net10.0/Mcp.Tests --filter-method "<name>"`. Each test was written and run BEFORE any fix.

| Note | Run before any fix | Verdict |
|---|---|---|
| N1 read-only files, Windows | **not run here** (Windows only; the git variant ran on Linux and passed: Linux deletes read-only files). The read-only-attribute test is skipped here ("a read-only attribute blocks a delete on Windows only"). | **Fix built without a Windows run** (the code review's reading: recursive `Directory.Delete` does not clear `FILE_ATTRIBUTE_READONLY`, and git writes read-only objects): on Windows only, `Removal.ClearReadOnlyBelow` clears the attribute of every file and folder before the delete and never enters or changes a link or junction. The clearing is pinned on every OS (RED: the stub left the files read-only → `Expected files to contain only items matching Not(…ReadOnly…)`; GREEN; RED again with the clearing neutralised). The two Windows tests run on the `windows-latest` leg and are the end-to-end proof. |
| N2 stderr not drained | `--filter-method "*A_tool_that_writes_more*"` → `total: 1, succeeded: 1` (200,000 bytes to stderr and to stdout; `TimedOut` false, exit 0, 200,000 `y` read). Mutation: the `_ = process.StandardError.ReadToEndAsync();` line removed → `failed … (1m 00s 458ms) Expected boolean to be False because the child must exit by itself, not be killed while blocked on a full pipe, but found True.` (restored). | **Does not reproduce** at `07a0f5b`: `Run` starts both reads before the wait. The test stays as a pin and has teeth (the mutation). The unawaited reads on the kill path: no test showed a leak; nothing changed. |
| N3 `mklink /J` and a space | Windows only: skipped here ("mklink /J is Windows only"). | **Not run here.** The block list and its skip stay (decision 5); the Windows leg runs the space case. |
| N4 hanging `node` | `--filter-method "*A_node_that_does_not_answer*"` → `failed (2s 436ms) Expected Liveness.ProbeProcess(pid).State to be ProbeState.Dead {value: 1} because the child is not left running after the assertion failed, but found ProbeState.Live {value: 0}.` (the child was Live at the timeout; the test's own `finally` then killed it). | **Reproduces.** Fixed: `RunNode` ends and reaps the child tree in a `finally`. GREEN: `total: 1, succeeded: 1`. With only that kill neutralised: the same `found ProbeState.Live` failure. |
| N5 unreachable temp root | `--filter-method "*A_temp_root*"` → `total: 4, failed: 1, succeeded: 3`: `A_temp_root_beneath_a_parent_that_cannot_be_traversed_is_not_a_missing_one` failed with `Expected a <LeftoversAuditException> to be thrown because a root nobody can reach is not a root that is not there, but no exception was thrown.` The same-suite case of a root that exists but cannot be listed (`chmod 000`) PASSED before any fix: `Directory.Exists` is true there and the listing throws `UnauthorizedAccessException` naming the path, so the audit is red. | **Half reproduces.** The untraversable parent reads as "missing" and the audit passes: fixed (`RootExists` reads the entry with the native `lstat`; a permission error throws `LeftoversAuditException` naming root and error; a link to nothing is still missing). The unlistable root is kept as a pin (it was never silent). GREEN: `total: 4, succeeded: 4`. With `Directory.Exists` put back: the same RED. |

Code gate and review follow-up (same day): the unlistable root (`chmod 000`) now also throws `LeftoversAuditException` naming the root and the error (RED: `Expected a <LeftoversAuditException> … but found <System.UnauthorizedAccessException>`); a root that is a link is judged by its final target with the native `lstat` (RED: no exception for a target beneath an untraversable parent); the run record is checked before the root is touched; ENOTDIR is "missing"; the failure keeps the registry's groups. Decision 7 is met in both cases, no deviation remains.

## What this does not do

The Node guard (F41, conventions); benchmark's re-sync (F42-c) and rag_qln's copy (E7.S3), each in its own repository;
a shared reading of Testcontainers reuse or of Ryuk set through `SetEnvironmentVariable` (F43, a pointer).

## Definition of Done

- [ ] The marker scenarios pass in C#, case for case with F41's, RED-GREEN-RED recorded in the PR.
- [ ] N1–N5 each reproduced or recorded as not reproducing, with the run; each reproduced and applicable one fixed with
      its test RED first.
- [ ] `GuardExemptions`' doc comment says what the code does.
- [ ] The docs that describe the template's grammar say the same as the rule after F41.
- [ ] coai's plan and code gates resolved; an Opus review passed; all hosted legs green; merged.
