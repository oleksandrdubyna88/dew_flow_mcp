# PLAN — Test leftovers, E3: one C# helper, nineteen sites, a sweep and a guard for `tests/Mcp.Tests`

> Status: **IMPLEMENTED, 2026-10-31.** Stories of
> epic E3 of the parent plan `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E3). Scope: a new
> `tests/Mcp.Tests/Leftovers/` folder (the token file, helper, owner record, liveness, the kernel-owner and identity
> reads, start-of-run sweep, end-of-run audit, guard, a janitor cross-check and an audit self-test), the 11 test files
> that create temp folders today, `.github/workflows/ci.yml` (the submodule step, a Windows leg, the self-test step), a
> new `research/module_tests.md`, and `research/architecture.md` § CI. The pin bump (the old S1) is PR #57, which
> merges alone (owner decision 1), and is not a story here.
>
> Split by Opus, 2026-10-16, against the E1 branch; re-split 2026-10-31 against the released contract
> (`dew_flow_conventions` `115bbf8`) and the owner's three decisions of 2026-10-31; plan-gate round 2 applied the same
> day (§ *Plan-gate round 2*). Models per story below. No cornerstone: E3 is not one (parent, E3 entry).

**Committed together with this document** (one commit, the shared `common/planning-docs.md` § *A plan split into epics
and stories*): its row in [todo/README.md](../todo/README.md) under *Currently open*:

```
| [PLAN_test_leftovers_E3.md](PLAN_test_leftovers_E3.md) | plan only, 2026-10-31 — re-split against release 115bbf8; the producer PR waits for R6's sweep-only records and the barrier checklist | Stories of epic E3 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`: every temp folder `tests/Mcp.Tests` makes is named `testsToDelete-<owner>-mcp-…`, claimed first, removed by the test that made it, collected by the same user's next run when a run was killed, and kept so by a guard |
```

**Done when** (from the parent, made concrete): `tests/Mcp.Tests` creates nothing it does not remove, and everything it
creates is named `testsToDelete-<owner>-mcp-<what>-<id>` by one helper, claimed before it exists (S2, S4); a killed
run's folders are collected at the start of the same user's next run, only on the two keys — scope and a `gone` owner
(S3); a guard over `tests/`, scripts and CI is red on every class of the rule's §7, on an unregistered or misassigned
token and on a scan of nothing (S4); the three stories land as ONE producer pull request, merged only under the
condition in *Order*.

## Deviations

What shipped differently from the plan above, recorded at promotion (2026-10-31). The plan text is kept as it was
written; where it and the list below disagree, the list is what runs. Each item names its evidence.

**Recorded in the plan's own DoD.** The §5 container carve-out is not recognised (this repository starts no container;
a `docker run` without `--rm` is a finding). Legacy names are listed with their exact command and never collected.
`owner-blind` in C# is a literal rule (owner decision 3), with the shipped classifier judging scripts and workflows.
macOS is not built (the helper refuses with a clear message). The Windows runner's measured owner
(`BUILTIN\Administrators`, an elevated job) is recorded by `RunnerFactsTests` in the job log; both ownership outcomes are
asserted through the two `WindowsOwnership` fixtures, never from the runner's own files.

**S2 (commits 5b94c81, 3237394, 876cdfd).**
- `Liveness.State`, scenario 12 and the State lines of scenario 13 moved from commit 2 to commit 3, with the owner
  record they need (the plan's Order has it so; commit 2's message says so).
- A temporary `ScratchDir` (a raw-temp scratch folder, exempt) served commit 2's tests of the primitives; commit 3 deleted
  it and those tests use `OwnedDir`.
- `DirectoryLinks.cs` moved out of `WorkspaceToolTests` (the link-or-junction helper was a private method there; no other
  change to that file).
- `Owner.RunRecord` takes a `warn` seam; a reparse point is `Identity.IsLink`; `Native` carries helpers beyond the plan's
  list (`WindowsEntryOf`, `FinalPathOfFollowing`, `OwnerSidOf`, among them).
- Scenario 9e checks the claim's identity after the write as well as before (a post-write identity check: a claim
  replaced after its exclusive create is not the one that was written).
- Scenario 3's `AsciiLower("SØREN")` is `sØren` — corrected against the vectors `w06`/`w07` of
  `tools/fixtures/owner-vectors.json` (the plan's expectation lower-cased the non-ASCII letter).

**S3 (commit 3432878).**
- The legacy-name handling follows Node's legacy order (`leftovers-sweep.mjs`), not the plan's prose order.
- Scenario 5e is keyed on the claim's path (`claimPath`), not on the folder's.
- A restore that fails reports a `quarantine-conflict` line and keeps the quarantine, retried every run.
- The claim's identity is captured with the folder's and re-checked before its removal (stricter than the plan).
- Extra tests beyond the plan's list; the fixture's report format is `mcp tests: leftovers — removed n, preserved n, listed n`
  with indented lines; the quarantine word is written in two pieces in the tests (the guard reads them).

**S4 (this commit's branch).**
- `LeftoversGuard.Scan(root, mountRoot = null, node = "node")` — not `Scan(root)`. The break-it trees are private folders with
  no mount, and scripts are judged by the REAL mount's classifier, so the mount is a parameter (default: the scanned root);
  `node` is the seam for "node missing". The token checks are separate calls (`TokenFindings`, `RegisteredTokenFindings`,
  `ConstantFindings`), not part of `Scan`, because a scan of a tree with no mounted rule must not report a missing rule.
- **The shipped classifier's answers win over the plan's expected counts** (each recorded once with
  `node .claude/rules/shared/tools/sweeper-scan.mjs --root <a git-initialised tree of the same files>`, which printed 9
  owner-blind lines — the ones the tests expect): `<prefix>-"$OWNER"-*` reads **broad** (the plan expected 0; the quote is
  not an owner start, `<prefix>-$OWNER-*` and `-${OWNER}-*` are owner-aware); `glob.glob(P + "*")` in Python and
  `glob(P + "*")` in JavaScript read **null** (the plan expected 1; the header of `prefix-bindings.mjs` names "a glob built
  by concatenation" as a limit) — the tests pin the limit at 0 and use `shutil.rmtree(P + n)` / `fs.rmSync(P + n)` for the
  traced case.
- **Decision 3 and `legacy-name` compose:** `$"<prefix>-{RepoToken}-"` is `legacy-name` AND `owner-blind` (the plan counted
  one); `<prefix>-conventions-*` and `<prefix>-u*` in a script are also `legacy-name` (the plan counted their `owner-blind`
  only). Line-level counts in the tests are the sets, not the plan's single class.
- The RED before the migration is **21 `raw-temp` findings in 11 files**, as planned, after one setup edit the plan did not
  list: `OwnedTests.cs` (S2) carried a reason string that named the framework temp API, which is a finding by the guard's
  own detector; reworded, not exempted. The repository scan (scenario 1) is its own class, `LeftoversRepositoryGuardTests`, so
  the guard's commit is green on its own and the scan turns green with the migration.
- The CI self-test step differs from the plan's YAML in three places, each measured with the shipped classifier over the
  workflow itself (the guard scans the workflow it ships in): the loop variable is `entry` (`case` is a reserved word); the
  folder listing builds the owner as `owner="u$(id -u)"` and matches `-iname "teststodelete-${owner}-mcp-$what-*"` (the plan's
  `teststodelete-*-mcp-…` is a **broad** glob to the classifier); and the message check is `.*/testsToDelete-[uw]…` (a `.*`
  directly before the prefix reads as a leading-star glob).
- `LeftoversJanitorTests` asserts the `foreign:` line on a **second, un-narrowed** dry run. With `--token mcp` the janitor's
  inventory is `testsToDelete-<owner>-mcp-…` of the caller's owner only (`janitor.sh:157`), so another owner's name is
  named nowhere — the plan expected a `foreign:` line under `--token`. The narrowed run asserts exit 0, the `would run:` line
  and the absence of the other owner's name; the un-narrowed run's exit code is the whole temp root's business and is not
  asserted. The plants live in a PRIVATE temp root handed to the janitor as `TMPDIR` (it reads it as `Path.GetTempPath` does)
  and not in the real one the plan names: run in the real temp root, the janitor's `find` raced the other tests of the run
  creating and removing folders there and exited 1 ("a listing failed") in 3 runs of 6 (measured); in a private root 8 runs
  of 8 were green.
- The guard's docker classes read one line at a time (no backslash-continuation joining); a literal default of a shell
  expansion (`${V:-w}`) must be an owner form, as in the Node guard; an image tag is judged wherever a test builds it (no
  pushed-image exception). The one-constant check walks the file system (not `git ls-files`), so an untracked file counts.
  `TokenFindings` returns the single owner-grammar finding for a token like `u1000` (the Node reference adds a "not a row"
  finding after it).
- `LeftoversAudit.Run(tmp, owner, registry, mine)` takes the registry and the kernel-owner seam so a test can drive the
  real run on a private temp root; an orphan claim is listed by its claim path; a linked folder is not followed.
  `LeftoversSweepFixture` gained an optional `audit` seam. The self-test's `failed-dispose` case locks a subfolder INSIDE
  the owned folder (Linux, mode 0o555) — no parent of the temp root is ever touched.
- `research/architecture.md:40` no longer states a test count (the knowledge-base rule: name no count that moves).
- Mutation checks, in the working tree and reverted: removing any one detector of the guard turns its fixture red (24
  mutations, reported with the producer PR); dropping the audit's throw turns `A_run_that_left_a_folder_throws…` red and the CI step
  red ("did not fail the run"); dropping the registry entry on the first `Dispose()` turns the unreclaimed-registry test red
  and the second self-test case red (the message then names the folder under "claimed in the temp root", not "disposed but
  not removed"). One mutant survived and is equivalent: deleting the explicit `owner-blind` branch of the exemption parser
  (the marker then fails as an unknown class, the same `exemption` finding).
- The plan reached this branch by merging `origin/main` (it was merged after the branch was cut); the merge commit goes with
  the squash.
- **Not run, by hand or on CI only:** the Windows job (the whole executable, including the self-test's Windows halves), the
  Windows janitor cross-check (scenario 9, Git for Windows' bash), the elevated run (S2 scenario 14), the hand clean-up
  record and the barrier checklist of *Order* (merge conditions (b)–(d)).

## The contract this document encodes

The released rule: `dew_flow_conventions · common/test-leftovers.md` at the `release` tip **`115bbf8`** (promoted
2026-10-30, R5 of the remainder plan) — the sha PR #57 pins. Cited as *rule §n* with `R:<line>`. The reference
implementation is Node at the same sha: `tools/lib/owned.mjs` (`O:`), `tools/lib/leftovers-sweep.mjs` (`S:`),
`tools/leftovers-check.mjs` (`G:`), `tools/janitor.sh` with its companion `common/test-leftovers-janitor.md` (`J:`),
`tools/fixtures/owner-vectors.json` (conformance vectors), `tools/lib/unmatchable-pid.mjs`. The plan that built them is
`research/PLAN_test_leftovers_merged_core.md` (its *Barrier checklist* and *Deviations*); the E1 text this document was
first written against is `research/PLAN_test_leftovers_E1.md`, superseded. This epic writes the family's **first C#
helper by shape, not by file**: same names where C# allows, same order, same three-valued answers.

- §1 (`R:15-59`) — `testsToDelete-<owner>-<repo>-<what>-<id>`. `<owner>` = `u<uid>` (POSIX real uid) or on Windows
  `w<digits>`: the account name without domain, ASCII `A`–`Z` lower-cased and nothing else, UTF-8 bytes, SHA-256,
  first four bytes big-endian unsigned, decimal (`R:19-25`; `O:82-94`). `^[uw][0-9]{1,10}$` is reserved: no `<repo>`
  token matches it (`R:26`). `TESTSTODELETE_OWNER` is validated against the grammar — invalid refuses, never falls back
  (`R:27-29`; `O:102-110`). `<repo>` in ONE constant, a row of the `## Tokens` table (`R:30-36`; `mcp` is `R:310`,
  `| mcp | dew_flow | the MCP server |`). `<what>`: no separators, NUL or control characters, no leading `-`
  (`R:37-39`); `<id>` 6–12 of `[A-Za-z0-9]` (`R:40-41`); the whole name ≤ 63, and the room for `<what>` is DERIVED from
  the owner (`R:42-45`; `O:131-135`). Every match `^teststodelete[-_]<owner>[-_]<repo>[-_]`, case-insensitive
  (`R:49-50`). A **legacy name** has a second segment outside the owner grammar (`R:51-54`): listed, never collected
  here.
- §2 (`R:61-90`) — **claim first** with an exclusive create, register the cleanup as the next statement, then create
  the resource **exclusively** (`mkdir`, never a create that succeeds on an existing folder); a folder already there is
  a squatter's: one warning, remove the own claim only while it still holds exactly what was written, draw a new id
  (`R:65-75`; `O:616-646`). An unmarked owner-form resource is nobody's: listed, never collected (`R:75-76`). An orphan
  claim is judged by its owner (`R:77-78`). Teardown by exact name, never throws, five tries 100 ms apart, on giving up
  leaves the resource prefixed AND claimed with ONE warning (`R:86-90`).
- §3 (`R:92-158`) — the record **`<owner>:<host>@<os>:<pid>[:<start>[:<ns>]]`** (`R:94`; `O:57`): host =
  `Dns.GetHostName()`, never `Environment.MachineName`, runs of `@`, `:`, whitespace → one `-`; under WSL
  `<host>~<distro>` (`R:96-101`; `O:352-359`). One line, ≤ 512 bytes; anything else unreadable (`R:115-116`; `O:398`).
  Start only where the OS gives it without a child per test process; on Linux `/proc` with `CLK_TCK` from the auxiliary
  vector, and only with the start the pid namespace as the inode of `/proc/self/ns/pid` (`R:116-123`; `O:258-319`).
  `TESTSTODELETE_RUN` is used only when it names a LIVE run of the caller (`R:112-114`; `O:426-435`). **Scope
  evidence**: the kernel owner authorises; a claim another uid owns is unreadable (`R:133-140`). **Liveness** in the
  rule's order (`R:142-153`; `O:531-552`): unreadable or unmarked → unknown; not the grammar → unknown; the record's
  owner not the name's or not the caller's → unknown; another host → unknown; on Linux no `<ns>` or another → unknown;
  pid 0, a sign, a leading zero, past 2^53, `windows:1` → unknown; **no process → `gone`, the only one**; refused,
  failed or `hidepid` → unknown; alive without comparable starts → live; starts within 2 s → live; further apart →
  unknown.
- §4 (`R:160-197`) — the start-of-run sweep, mandatory per repository: own owner and own token only; **two keys**,
  scope and abandonment (`R:170-172`); foreign, legacy, unmarked LISTED, never touched; age ≥ 10 minutes selects
  (`R:176-178`); removal **by identity** — re-check device and inode and the re-read claim, atomic rename to
  `.<name>.teststodelete-quarantine-<pid>-<hex>`, check again, then remove; a quarantine a killed sweep left is
  restored when its name is free, listed when taken (`R:179-187`; `S:17-22`); a legacy name listed with its exact
  command (`R:188-192`); **never fails the run** (`R:193`).
- §7 (`R:243-263`) — eight classes: `raw-temp` (`RUNNER_TEMP` is not one, `R:250`), `raw-worktree`, `docker-run`,
  `docker-name` (owner form only; compose `-p`/`--project-name`/`COMPOSE_PROJECT_NAME` count, `R:253`),
  `legacy-name`, `owner-blind` (no inline exemption, `R:255`, `R:260`), `shared-store`, `ryuk-off`; red on a scan of
  nothing (`R:246`); the inline exemption grammar (`R:259-263`).
- The janitor (`R:265-277`; `J:14-68`): `bash .claude/rules/shared/tools/janitor.sh --token mcp [--dry-run]` — `--token`
  narrows to the caller's owner and that token (`J:26-29`; `janitor.sh:157`); window `JANITOR_MIN_AGE_MINUTES`, default
  60 (`J:30-31`); on Windows only Git for Windows' bash and forced `--dry-run` unless `JANITOR_WINDOWS_E2E_GREEN` is set
  (`J:18-24`); `would run:` on stdout, `skipped:` / `foreign:` / `legacy:` / `unmarked:` / `notice:` on stderr; exit
  0 clean, 3 something skipped, 1 a failed listing, 2 usage (`J:45-68`). Its host id is `host_id` (`janitor.sh:186`).
- The barrier (`R:279-293`): no producer emits the owner form on a shared machine until every repository's sweeper
  stage has landed; the checklist is the merged-core plan's *Barrier checklist*, run per machine.

### Owner decisions of 2026-10-31 (binding; recorded in R6 of `dew_flow_conventions · todo/PLAN_test_leftovers_merged_core_release.md`, PR #93)

1. **The pin bump merges alone.** PR #57 pins `.claude/rules/shared` to `115bbf8` before the producer stage: a pin bump
   emits no owner-form name and `Pin check` stays red on every pull request until it moves. The old S1 is therefore
   done by that PR. The producer stage is ONE squash-merged pull request: rename, helper, sweep, guard and the
   migration of the 21 sites — stories S2–S4 are reviewable commits on one branch and merge together.
2. **The Windows kernel owner is judged by BOTH keys:** the entry's resolved path is inside the caller's profile AND
   the entry's owner SID is the caller's user SID; either unreadable → fail closed (listed, never touched). Stricter
   than the reference, which checks the profile alone (`O:215-222`; F16 in the conventions follow-ups). An elevated
   run's files are owned by `BUILTIN\Administrators`, so they fail closed and are kept (below, *Elevated runs*).
3. **`owner-blind` in C# source is a literal rule, no binding tracer:** ANY literal of the bare prefix —
   `testsToDelete` / `teststodelete`, any case, followed by `-` or `_` and NOT by an owner segment — outside the
   helper's own file is a finding, so a constant cannot carry the prefix to a sweep. Scripts (`.sh`, `.ps1`, `.mjs`,
   workflow `run:` blocks) keep the shipped tracer's semantics: a literal, or a variable the same file assigns a value
   holding the prefix. Recorded as a deviation from rule §7's "a bare name in script code" for `.cs`.

## Facts the split rests on (read on `origin/main` b352881, 2026-10-16; re-read 2026-10-31, unchanged)

- **19 `Directory.CreateTempSubdirectory("mcp-…")` sites in 11 files**, confirmed, and **no `Directory.Delete` in
  `tests/`** (the only mention is the doc comment at `tests/Mcp.Tests/LogRetentionTests.cs:79`). Per file, with the
  method that owns each site — every one of these methods is `private static` and is called only from instance test
  methods of its own class, so each can become an instance method:

  | File | Site | In | Callers |
  |---|---|---|---|
  | `DailyRunFileSinkTests.cs` | `:92` `mcp-daily-log` | `TempRoot()` | `:21`, `:31`, `:47`, `:62`, `:79` |
  | `HealthTests.cs` | `:61` `mcp-health-broken`, `:67` `mcp-health` | `BrokenSpool()` `:59`, `HealthySpool()` `:66` | `:19`, `:35` |
  | `LogRetentionTests.cs` | `:68` `mcp-retention-empty` (inline), `:117` `mcp-retention` | test `:66`, `Root(...)` `:115` | `:20`, `:33`, `:43`, `:56`, `:101` |
  | `SpoolFixtureTests.cs` | `:46` `mcp-spool-fixture` | inline | — |
  | `SpoolUsageSinkTests.cs` | `:133` `mcp-spool-broken`, `:157` `mcp-spool-fault` (+ `"\0x"`), `:175` `mcp-spool` | inline, inline, `Build(...)` `:173` | `:18`, `:36`, `:54`, `:74`, `:88`, `:114` |
  | `SurfaceFingerprintTests.cs` | `:133` `mcp-fingerprint` | `DescriptionSet(...)` `:131` | `:19`, `:38`, `:53`, `:54` |
  | `SurfaceParityTests.cs` | `:119` `mcp-parity-descriptions`, `:149` `mcp-parity` | `ConfiguredCatalog(out …)` `:115`, `BuildWorkspaceCatalog(out …)` `:147` | `:24`, `:35`, `:50`, `:72`, `:96` |
  | `TelemetryCorrelationTests.cs` | `:119` `mcp-correlation` | `OneLine(...)` `:117` | `:86`, `:102` |
  | `ToolDescriptionCatalogTests.cs` | `:102` `mcp-descriptions` (inline), `:113` `mcp-descriptions` | test `:100`, `Sets(...)` `:111` | `:16`, `:26`, `:36`, `:52`, `:67`, `:78` |
  | `ToolSurfaceTests.cs` | `:138` `mcp-surface` | `DescriptionSet(...)` `:136` | `:55`, `:67`, `:92` |
  | `WorkspaceToolTests.cs` | `:285`, `:301` `mcp-outside` (inline), `:402` `mcp-workspace` | tests, `Build(...)` `:400` | 21 tests, `:17`–`:389` |

  About **70 folders per full run** (one per factory call), each a few KB; none removed today. The longest `what`,
  `parity-descriptions` (19), fits the room under any owner (32 under `u1000`, 26 under `w` + ten digits).
- **Two more `raw-temp` sites the parent did not count**: `WorkspaceToolTests.cs:243` (`new WorkspaceRoot(Path.GetTempPath())`
  for a constructor that must throw) and `:276` (`Path.GetTempPath()` as an absolute path the tool must refuse). They
  create nothing, but rule §7's `raw-temp` class names `GetTempPath`, so the guard's RED over this repository lists
  **21** findings, not 19.
- **A leak that is not a folder.** `WorkspaceToolTests.cs:259-260` writes `outside.txt` into
  `Directory.GetParent(root)` — and `root` is a direct child of the temp root, so the file lands **in the shared temp
  root itself**, under a fixed name, never removed; the two `[InlineData]` rows of that theory (`:254-255`) write the
  same file concurrently. Observed on a development machine 2026-10-16: `%TEMP%\outside.txt`, 6 bytes, content
  `secret`, last written 2026-10-14 — exactly what that test writes.
- **`"\0x"` is not a risk for the guard.** `SpoolUsageSinkTests.cs:157` appends a NUL to the scratch path so the
  writer faults; the composed path is never created (the test asserts the fault, `:164-167`). The only resource is the
  base folder the same line creates, and a line-based guard sees that call whatever is concatenated after it.
- **The POSIX note at `LogRetentionTests.cs:79` says the opposite of what the parent reads into it.** On POSIX an open
  handle does NOT stop `Directory.Delete(recursive)`; on **Windows** it does (that test skips off Windows, `:93-98`).
  Teardown must survive a refused delete and never throw; the refusal is a Windows event, so the never-throw scenario
  provokes it per OS (S2 scenario 6).
- **`Directory.CreateTempSubdirectory` cannot be the helper's primitive.** Measured 2026-10-16 on .NET 10.0.12,
  Windows: `CreateTempSubdirectory("e3probe-")` made `e3probe-clxdyztb.njt` — prefix + `Path.GetRandomFileName()`, an
  id of 8 + `.` + 3 that breaks rule §1's `[A-Za-z0-9]{6,12}`. **.NET has no exclusive `mkdir`** (`Directory.CreateDirectory`
  succeeds on an existing folder), **no inode or file-id API, no uid read and no `kill(pid, 0)`**: S2 takes every
  OS call the contract needs through one P/Invoke file, `Native.cs`, and nothing else in the folder calls the OS by hand.
- **No Docker, no worktrees, no databases** anywhere in `tests/`, `src/` or `.github/` (`is-docker` appears only in
  `http/package-lock.json` as a transitive dependency of the contract suite). The scripts are
  `.github/scripts/branch-protection.mjs`, `http/httpyac.config.js` and the two workflows; none makes a temp path; no
  `.py`, `.sh` or `.ps1` file exists. `src/` holds no temp API and no prefix literal (`git grep` at `b352881`), so
  scanning it (S4 *Discovery*) changes no count. The guard still implements every class (rule DoD: *red on every
  class of §7*).
- **Stage A of this repository is empty, measured.** The barrier scan `tools/sweeper-scan.mjs` at `115bbf8` over
  `b352881` found no owner-blind sweeper (103 files read; remainder plan R6, 2026-10-31). Nothing here sweeps, so the
  sweep-only stage is a record, not a change.
- **Out of scope, on purpose:** `SpoolFixtureTests.cs:77-79` writes `mcp-spool-v0-correlated.jsonl` into
  `AppContext.BaseDirectory` — a fixed-name file in the build output, overwritten by every run, git-ignored, kept for a
  human to copy (`:29-31`). Not a temp resource; the guard does not flag it. `src/ServiceDefaults/McpLogging.cs:191`
  (`Directory.Delete` in the log retention) is product code.
- **How tests run here.** xUnit v3 on Microsoft Testing Platform (`tests/Mcp.Tests/Mcp.Tests.csproj:7`,
  `global.json` `test.runner`); the executable, never `dotnet test` (`CLAUDE.md:31-34`); locally through
  `node .claude/rules/shared/tools/low-priority.mjs` with at most 8 threads (`tests/Mcp.Tests/xunit.runner.json`,
  copied only off CI, `Mcp.Tests.csproj:39`). `TreatWarningsAsErrors` (`Directory.Build.props:8`), so analyzer
  warnings — `CA1416` platform checks, xUnit's `xUnit1051` cancellation-token rule, `SYSLIB1054` for `DllImport` —
  are build errors. Test classes are `public sealed`, in namespace `Mcp.Tests`; none has a fixture or a collection
  today. There is **no `research/module_tests.md`**: tests are documented in `research/architecture.md` § CI
  (`:238-251`) and the project table (`:40`, "123 tests").
- **CI** (`.github/workflows/ci.yml`): `build-test` (`:13-42`) runs on `ubuntu-latest` in Release and **does not fetch
  the `.claude/rules/shared` submodule**; `contract` (`:54`) and `plans` (`:92-93`) do. `plans` runs `pin-check.mjs`
  (`:110-111`) against the tip of the ref `.gitmodules` tracks — `branch = release` — which is why PR #57 goes first.
  `Formatted` (`:33-34`) runs `dotnet format --verify-no-changes`; on a Windows checkout it reports ENDOFLINE noise
  from `core.autocrlf` (`:29-32`), so run it in Linux or WSL, or rely on CI. Node is on the runner (the `plans` job).
  **No job runs on Windows today**; the Windows half of this epic was proven only by hand. GitHub's `windows-latest`
  runner executes jobs as an administrator, and on Windows Server an administrator's new files are likely owned by
  `BUILTIN\Administrators` (UNVERIFIED — `dew_flow_conventions · research/security/test-leftovers-merged-core.md:239-243`
  names the same hole for the janitor), which under decision 2 makes `IsMine` false there. S2 measures it first.
- **Reusable neighbours.** `LocalCpuBudgetTests.cs:20-27` finds the repository root (the folder holding `*.slnx`
  above `AppContext.BaseDirectory`) — S2 extracts it rather than copying it. `RecordingLogger.cs` is the in-repo
  pattern for a captured sink. `WorkspaceToolTests.cs:346-373` already makes a directory link or a junction, or skips.

## Order

| Story | Model (brief · code · review) | Depends on | Lands |
|---|---|---|---|
| S1 — the pin on release `115bbf8` | — | — | **PR #57**, merged alone (owner decision 1); not built here |
| S2 — the owner token and record, the C# owned-resource helper, liveness, the kernel-owner and identity reads | Opus · Sonnet · Opus | PR #57 merged | first, as THREE commits (below) |
| S3 — the start-of-run sweep, as an assembly fixture | Opus · Sonnet · Opus | S2's commit 2 green on both legs | second |
| S4 — the guard, the migration of 21 sites, the end-of-run audit | Opus · Sonnet · Opus | S2, S3 | last |

**S2 is three commits, each green on its own on BOTH CI legs** (cadence consultation, 2026-10-31): **(1)** the
runner facts (scenario 0) and the `build-test-windows` job; **(2)** `Native`, identity, `Scope.IsMine`, the probe,
with their direct tests — the two Windows ownership outcomes of sc. 13 included; **S3 is not started until this
commit is green on both legs**, because every later story trusts these answers; **(3)** naming, owner records,
claims, disposal and the registry. **One branch, one pull request, squash-merged.** S2–S4 are commits on
`feat/test-leftovers-e3`; each is reviewed as a unit; none merges alone — the guard's test over this repository is red until the migration lands, and a helper that
emits the owner form without its sweep is half the promise. **The merge condition** (rule *The barrier*; remainder plan
R6): the producer pull request merges only after (a) PR #57 is merged, (b) `dew_flow_conventions ·
todo/PLAN_test_leftovers_merged_core_release.md` R6 records every consumer's sweep-only stage — this family's seven and
the private sibling's — as landed or empty (this repository's: empty, measured above), (c) the per-machine
*Barrier checklist* of `research/PLAN_test_leftovers_merged_core.md` is recorded for every machine that runs these tests
(the Ubuntu dev VM is recorded open; the owner's Windows machine is not tested at the time of writing), and (d) the
one-time hand clean-up of S4 (the old `mcp-…` folders and `outside.txt`) is recorded per machine in the PR — each path
removed, or kept with a reason. The stories may be written, gated, built and reviewed before that; the merge waits. If the released text differs from what is cited
here, stop and report — do not resolve the difference in code.

## S1 — The pin on release `115bbf8` — done by PR #57

`dew_flow_mcp` PR #57 (`chore(rules): pin shared conventions to release 115bbf8`, branch `chore/pin-conventions-115bbf8`;
open at the time of writing) merges alone under owner decision 1, before the producer branch is cut. Nothing of it is
built here. What the old S1 carried into S2 — the constants table comparing the helper's constants with the released
text — is now S2's own DoD, written against `115bbf8` directly: `RepoToken` = the `## Tokens` row (`R:310`); `RepoRow`
= that row's `repository` column; `Prefix`, both separators, case-insensitive matching, the owner grammar, id 6–12, 63
characters = rule §1; the record grammar, 512 bytes, the 2-second tolerance = §3; 10 minutes = §4. The `plans` job is
green after the bump; the commands of the old S1 stay the producer branch's pre-push list (S4 *Commands*).

## S2 — The owner token and record, the C# owned-resource helper, liveness, the kernel-owner and identity reads

- **Goal.** One place through which every test here makes a scratch folder: the name from one constant behind the
  caller's owner token (rule §1), the claim written FIRST beside it (§2, §3), the folder made exclusively, the cleanup
  registered by construction, exact teardown that never throws (§2), and the three-valued liveness answer, the
  kernel-owner question and the identity read S3's sweep trusts (§3, §4).
- **Files.** New, all in namespace `Mcp.Tests.Leftovers`, under `tests/Mcp.Tests/Leftovers/`: `OwnedToken.cs` (the
  token and its row, this repository's own — the one file a copy rewrites, as `O:42`'s `owned-token.mjs`), `Owned.cs`,
  `OwnerToken.cs`, `Owner.cs`, `Liveness.cs`, `Scope.cs`, `Native.cs`, `OwnedDir.cs`, `UnmatchablePid.cs`; tests
  `RunnerFactsTests.cs` (the measuring test, written and run FIRST), `OwnedTests.cs`, `OwnerTests.cs`,
  `LivenessTests.cs`, `ScopeTests.cs`. Outside the folder: new `tests/Mcp.Tests/RepositoryRoot.cs`
  (`LocalCpuBudgetTests.cs:20-27`'s `FindRoot` moved into `internal static class RepositoryRoot`; `LocalCpuBudgetTests`
  reads it from there — the vectors test needs the mount's path); one property in `tests/Mcp.Tests/Mcp.Tests.csproj`
  (*Analyzers*); `.github/workflows/ci.yml` — `build-test` gains the submodule step the other two jobs have (the
  vectors file lives in the mount), and a new `build-test-windows` job (below, *CI*). Nothing else.

**The change.** A block comment at the top of `Owned.cs`, in the voice of `O:1-34`: what the helper owns, the order
claim → register → create and why, why teardown never throws, why the owner lives beside the folder and never inside
it, and the two keys with three answers.

```csharp
public static class OwnedToken                            // THIS repository's file (O:42); every other file of the folder copies to E2/E6/E7 unchanged but for its namespace line
{
    public const string RepoToken = "mcp";                 // rule §1 — the ONLY place the token is written (the guard counts it)
    public const string RepoRow = "the MCP server";        // the `repository` column of the `mcp` row (R:310): the guard's identity check
}

public static class Owned
{
    public const string Prefix = "testsToDelete-";                 // rule §1 — the ONE bare-prefix literal in .cs outside the guard's fixtures (decision 3)
    public const int MaxName = 63, IdLength = 6, StartToleranceSeconds = 2, MaxClaimBytes = 512, MaxDraws = 3;   // rule §1, §3; O:48-53
    public static readonly Regex OwnerGrammar = new(@"^[uw][0-9]{1,10}\z");   // EVERY anchored regex here ends in \z (never $, which matches before a final \n) and spells [0-9] (never \d, which takes Unicode digits)
    public static string NameBase => Prefix + OwnerToken.This + "-" + OwnedToken.RepoToken + "-";   // "testsToDelete-u1000-mcp-"
    public static int Room => MaxName - NameBase.Length - 1 - IdLength;                  // the room for `what` under THIS owner (O:132)
    public static Regex Pattern(string? owner = null);   // ^teststodelete[-_]<owner>[-_]mcp[-_], IgnoreCase | CultureInvariant (O:147-150)
    public static readonly Regex LegacyPattern;          // ^teststodelete[-_]mcp[-_] — listed, never collected (O:152)
    public static Regex FullName(string? owner = null);  // Pattern + @".+[-_][A-Za-z0-9]{6,12}\z"
    public static NameForm Classify(string name);        // Owner(owner, token?) | Legacy(token) | None — the ONE second segment (O:170-182)
    public static string TempRoot();                     // Path.GetTempPath() — the one place a test learns the temp root
    public static string NameFor(string what);           // NameBase + what + "-" + IdLength chars of [A-Za-z0-9]
    public static void DefaultWarn(string line);         // see "Warnings"
}

public static class OwnerToken                            // rule §1 <owner>; O:82-118
{
    public static string This { get; }                    // computed once; TESTSTODELETE_OWNER when set and valid, a throw when set and invalid
    public static string Compute(bool windows, uint? uid, string? accountName, Func<string, string?> env);   // the pure form, tested
    public static string Windows(string accountName);     // "w" + first four SHA-256 bytes of the ASCII-lower-cased UTF-8 name, big-endian, decimal
    public static string AsciiLower(string text);         // A–Z only — never ToLowerInvariant (it folds Ø, Ü; vectors w06, w09)
}

public sealed record Owner(string Token, string Host, long Pid, long? Start, long? Ns)   // Host = "<hostname>[~<distro>]@<linux|macos|windows>"
{
    public bool NonCanonical { get; init; }               // a pid, start or ns written with a leading zero (O:389-404)
    public static Owner This { get; }                     // computed once; Start and Ns only on Linux
    public static Owner RunRecord(Func<string, string?>? env = null);   // TESTSTODELETE_RUN when it names a LIVE run of the caller, else This (O:426-435)
    public static string HostName(string raw);            // runs of '@', ':', whitespace → "-" (O:352-354)
    public static string HostId(string raw, string distro);   // HostName, "~" + HostName(distro) under WSL (O:357-359)
    public string Format();                               // $"{Token}:{Host}:{Pid}" + (Start is null ? "" : $":{Start}" + (Ns is null ? "" : $":{Ns}"))
    public static Owner? Parse(string text);              // null = unreadable (O:397-406)
}

public enum OwnerState { Live, Gone, Unknown }
public enum ProbeState { Live, Dead, Unknown }
public sealed record Probe(ProbeState State, long? Start);   // Start only on Linux, only when Live
public sealed record Facts(string? CallerOwner = null, string? NameOwner = null, bool? KernelMine = null, bool? Hidepid = null);

public static class Liveness
{
    public static Probe ProbeProcess(long pid);                     // the real query (below); never spawns a process
    public static bool HidepidActive();                             // Linux and /proc/1 invisible (O:345-347)
    public static long? ClockTicksPerSecond();                      // AT_CLKTCK from /proc/self/auxv, read once; null = unknown, never a guessed 100 (O:277-288)
    public static long? ClockTicksFromAuxv(ReadOnlySpan<byte> auxv, int wordBytes, bool littleEndian);   // the pure form (O:258-272)
    public static long? StartFromProcStat(string statText, long btime, long? ticks);   // floor(btime + field22 / ticks) (O:291-299)
    public static long? ReadPidNamespace();                         // inode of /proc/self/ns/pid (O:311-319)
    public static OwnerState State(Owner? owner, Func<long, Probe> probe, Facts? facts = null);   // rule §3's order (O:531-552)
}

public static class Scope                                         // rule §3 "scope evidence"; decision 2
{
    public static bool IsMine(string path);                       // POSIX: lstat uid == getuid(); Windows: inside the profile AND owner SID == user SID; any failure false
    public static ClaimRead ReadClaimText(string file, Func<string, bool>? mine = null);   // Missing | Unreadable(reason) | Text(string) — O:470-491
}

public readonly record struct Identity(ulong Dev, UInt128 Ino, bool IsDirectory, bool IsLink, bool IsRegular, uint? Uid, DateTimeOffset ChangeTime, DateTimeOffset WriteTime);
internal static partial class Native                              // every call to the OS the contract needs and .NET lacks — nothing else P/Invokes
{
    // [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)], Linux:
    //   getuid(); kill(pid, 0); mkdir(path, 0x1C0 /* 0o700 — C# has no octal literal; a 0700 would be decimal */);
    //   statx(AT_FDCWD = -100, path, AT_SYMLINK_NOFOLLOW = 0x100, STATX_BASIC_STATS = 0x7ff, buffer); rename(from, to)
    // [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)], Windows:
    //   CreateDirectoryW(path, 0);
    //   CreateFileW(path, FILE_READ_ATTRIBUTES 0x80 | READ_CONTROL 0x20000, share FILE_SHARE_READ|WRITE|DELETE = 7, 0, OPEN_EXISTING = 3,
    //               FILE_FLAG_BACKUP_SEMANTICS 0x02000000 | FILE_FLAG_OPEN_REPARSE_POINT 0x00200000, 0);
    //   GetFileInformationByHandleEx(h, FileBasicInfo = 0 | FileIdInfo = 18, …); GetFinalPathNameByHandleW(h, …, VOLUME_NAME_DOS = 0);
    //   OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION 0x1000 | SYNCHRONIZE 0x00100000, false, pid); GetExitCodeProcess; WaitForSingleObject(h, 0); CloseHandle; LocalFree
    // [LibraryImport("advapi32")]: GetSecurityInfo(h, SE_FILE_OBJECT = 1, OWNER_SECURITY_INFORMATION = 0x1, out ownerSid, 0, 0, 0, out descriptor)
    internal static Identity? IdentityOf(string path);            // the entry ITSELF, a link never followed; null when it is not there
    internal static SecurityIdentifier? OwnerSidOf(string path);  // Windows: GetSecurityInfo on the SAME OPEN_REPARSE_POINT handle as IdentityOf — a link's own owner, never its target's; null on ANY failure
    internal static bool MakeDirectoryExclusively(string path);   // false = already exists (EEXIST / ERROR_ALREADY_EXISTS); any other failure throws
    internal static void RenameOnto(string from, string to);      // Linux rename(2); Windows Directory.Move (exclusive by itself)
}

public sealed class OwnedDir : IDisposable                        // one folder; `using var dir = OwnedDir.Create("x");`
{
    public string Path { get; }
    public static OwnedDir Create(string what, string? parent = null, Action<string>? warn = null);
    internal static OwnedDir Create(string what, string? parent, Action<string>? warn, Func<string> nextName, Action<string>? beforeClaimWrite = null, Action<string>? afterClaim = null);   // test seams: the names to try; a step between the claim's exclusive create and its write; a step between the claim and the folder
    public void Dispose();                                        // Removal.Quietly(Path, warn); idempotent; never throws; marks the entry Attempted, leaves the registry only when folder AND claim are gone
    public bool DisposeAttempted { get; }                         // Dispose() ran at least once
    internal static IReadOnlyList<OwnedDir> Unreclaimed();        // the process-wide registry: never disposed, AND disposed but not removed (S4 audit)
}

public sealed class OwnedScope : IDisposable                      // many folders, one Dispose — a test class's field
{
    public OwnedScope(Action<string>? warn = null);
    public string Dir(string what, string? parent = null);
    public void Dispose();                                        // every folder, last-made-first; never throws
}

internal static class Removal
{
    internal static bool Quietly(string dir, Action<string> warn);              // top-level links, the folder, then its claim; never throws (O:582-590)
    internal static bool Claim(string dir, Action<string> warn);                // only the sibling claim (O:593-602)
    internal static void ClaimIfStill(string dir, string written, Action<string> warn);   // only while it holds exactly `written` (O:445-451)
    internal static void UnlinkTopLevelLinks(string dir);                       // never what a link points at (S:102-108)
}
```

- **The owner token** (`OwnerToken.Compute`, `O:102-110`): `TESTSTODELETE_OWNER` set and non-empty → it must match
  `OwnerGrammar` or `This` throws `InvalidOperationException("owned: TESTSTODELETE_OWNER … is not an owner token")` —
  the helper then refuses to create anything (no claim, no folder); empty is unset; a value with a line end (`u1\n`)
  is INVALID — nothing is trimmed, and `\z` sees the `\n`. Windows → `Windows(Environment.UserName)`
  (`%USERNAME%`, no domain, `R:21`). Else `"u" + Native.getuid()` — the REAL uid, never the effective one.
- **`what`** (rule §1): non-empty; no `/`, `\`, NUL, other control character (`< 0x20`, `0x7f`); not starting with
  `-`; MAY hold `-`, spaces and non-ASCII; `what.Length <= Owned.Room`. Invalid ⇒ `ArgumentException` naming the value
  and the room under THIS owner, before anything exists (`O:127-136`).
- **The id**: `RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 6)`
  (unbiased) — never `Path.GetRandomFileName()`, never `CreateTempSubdirectory`. The helper WRITES 6; `FullName`
  ACCEPTS 6–12, so a name another implementation made still reads as one. Selection stays the anchored `Pattern`.
- **`Owner.This`**: `Token` = `OwnerToken.This`; `Host` = `HostId(Dns.GetHostName(), Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") ?? "")`
  + `@` + `linux` | `macos` | `windows` from `OperatingSystem.IsLinux()/IsMacOS()/IsWindows()`; any other platform throws
  `PlatformNotSupportedException` (no claim, no folder). **Not** `Environment.MachineName` (NetBIOS, cut at 15; the
  janitor reads `hostname`, `R:96-98`). `Pid` = `Environment.ProcessId`. **`Start` and `Ns` on Linux only**, as the
  reference writes them (`O:366-372`): `Start` = `StartFromProcStat(/proc/self/stat, btime of /proc/stat, ClockTicksPerSecond())`;
  when the rate or any read is missing, NO start and NO ns — never a guess. `Ns` only together with `Start`, from
  `new FileInfo("/proc/self/ns/pid").LinkTarget` matched by `^pid:\[(\d+)\]$`. On Windows and macOS no start: the
  rule allows a native API there (`R:116-120`), but writing one would make this helper's record differ from the
  Node helper's on the same machine and buys nothing the janitor reads (it reads no start: `owner-vectors.json`
  § *verdicts*) — pid reuse then PRESERVES (`R:150-151`).
- **Why the start is computed from `/proc`, not `Process.StartTime`.** `Process.GetCurrentProcess().StartTime` on Linux
  is .NET's own reading of field 22 with its own tick rate and boot time; whether it agrees with the Node helper's
  `floor(btime + field22 / CLK_TCK)` within the 2-second tolerance is not verified. Two helpers on one machine must
  write starts that compare — so the C# helper computes the same formula from the same files, with `CLK_TCK` from
  `/proc/self/auxv` (`AT_CLKTCK` = 17; word width `IntPtr.Size`; `BitConverter.IsLittleEndian`), a `/proc` read and no
  child process (`R:116-119`), exactly `O:258-299`. Scenario 10 measures the agreement with `Process.StartTime` and
  records the delta.
- **`Owner.Parse`** (`O:397-406`): longer than 512 bytes ⇒ null; strip ONE trailing line end by explicit
  `EndsWith("\r\n")` / `EndsWith("\n")`, never by the regex; match
  `^([uw][0-9]{1,10}):([^@:\s]+@(linux|macos|windows)):([0-9]+)(?::([0-9]+)(?::([0-9]+))?)?\z` (`O:57`, with `\z` and
  `[0-9]` for .NET); anything else — empty,
  `hands off`, the old grammar `devbox@linux:4711:1792053744`, `u1000:devbox@beos:1`, `u1000:devbox@linux:1:2:3:4`,
  `U1000:h@linux:1`, a second line, a lone CR — ⇒ null. A digit field with a leading zero (`007`) parses with
  `NonCanonical = true`; a field past 2^53 is stored as `long.MaxValue`, which `State` refuses.
- **`Owner.RunRecord`** (`O:426-435`): `TESTSTODELETE_RUN` unset or empty ⇒ `This`; else `Parse` it, and use it only
  when its `Token` is the caller's and `State(parsed, ProbeProcess)` is `Live`; otherwise ONE warning
  `owned: TESTSTODELETE_RUN is not the record of a live run of this user — ignored, this process's own record is used`
  and `This`. The claim and the audit use `RunRecord()`, never `This` directly.
- **`OwnedDir.Create` — claim first, then an exclusive create** (`R:65-75`; `O:616-646`):
  1. `parent ??= Owned.TempRoot()`; check `what`; at most `MaxDraws` (3) draws of `name = Owned.NameFor(what)`;
     `dir = Path.Combine(parent, name)`; `claim = dir + ".owner"`.
  2. Write the claim: `new FileStream(claim, FileMode.CreateNew, FileAccess.Write, FileShare.None)`, content
     `written = Owner.RunRecord().Format() + "\n"` (UTF-8, no BOM). The create refused because the name is taken —
     `IOException` with `HResult == unchecked((int)0x80070050)` (`ERROR_FILE_EXISTS`) on Windows, or on Linux
     `Native.IdentityOf(claim) is not null` after the throw — ⇒ the name is somebody else's: next draw (never
     `File.Exists`, which follows a link). Any other failure of the create ⇒
     `InvalidOperationException("owned: could not claim <dir>: <cause>")` — nothing was created. **A write or flush
     that fails AFTER the exclusive create** (disk full, the handle revoked): capture `Native.IdentityOf(claim)` right
     after the stream opened, and remove the claim only while its identity is still that one OR its content still
     equals `written` — a claim another run put under that name since is not ours; then throw the same
     `could not claim`. **No `Directory.Exists` pre-check**: it is a race, and the exclusive create answers it.
  3. `Native.MakeDirectoryExclusively(dir)` — Linux `mkdir(dir, 0x1C0)` (0o700: only the caller's processes read a
     scratch folder; no test here runs a child as another user), Windows `CreateDirectoryW`. `false` (the folder already
     exists under the claimed name) ⇒ ONE warning `owned: <dir> already exists under the name this run claimed — it is
     not this run's and is left alone; drawing another name`, then `Removal.ClaimIfStill(dir, written)`, then the next
     draw. Any other failure ⇒ remove the own claim (inside its own `try`), throw
     `InvalidOperationException("owned: could not create <dir>: <cause>", inner)`. Never `Directory.CreateDirectory`.
  4. Return the `OwnedDir` and add it to the registry. After the draws: throw `owned: no free name for <what> in <parent> after 3 draws`.
  The caller registers the cleanup on the same statement (`using var` or `OwnedScope.Dir`); the window inside `Create`
  is covered by step 3's own catch. Nothing is ever written inside `dir`.
- **The registry of unreclaimed instances.** One process-wide `ConcurrentDictionary<OwnedDir, byte>`, entered at
  `Create`. `Dispose()` sets `DisposeAttempted` FIRST, then tries the removal, and **removes the entry only when
  `Removal.Quietly` returned true — folder and claim both gone**; a removal that gave up (a held handle on Windows, a
  read-only subfolder on Linux) leaves the entry in place, so the audit still sees the folder. A later `Dispose()`
  tries again (idempotent on success: no second warning once the entry is gone). `OwnedScope` holds `OwnedDir`s, so
  its folders are in the registry too. `Unreclaimed()` is a snapshot of both kinds — never disposed, and disposed but
  not removed — told apart by `DisposeAttempted`. It is what lets S4's audit see a folder made under a CUSTOM
  `parent`, and a disposal that failed under any parent (cadence consultation, 2026-10-31; before it the first
  `Dispose()` dropped the entry before the removal was tried, and a failed removal left no audit signal).
- **`Removal.Quietly(dir, warn)`** (`O:582-590`): up to five tries, 100 ms apart, each: if the folder exists,
  `UnlinkTopLevelLinks(dir)` — every top-level entry whose `Attributes` carry `ReparsePoint` is removed as a LINK (a
  `DirectoryInfo` ⇒ `Directory.Delete(link)` non-recursive, which removes a junction or a directory symlink and never its
  target; a `FileInfo` ⇒ `File.Delete`) — then `Directory.Delete(dir, recursive: true)`. Then `Removal.Claim` (five
  tries). Every exception is caught. Giving up ⇒ ONE warning
  `owned: could not remove <path> (<exception type>: <message>) — left for the start-of-run sweep` and `false`. **The
  folder goes first**: when it cannot be removed its claim stays, so the leftover stays prefixed and claimed. A path
  already gone is success. `ClaimIfStill` reads the claim as a plain file (its own, just written; `O:447`) and deletes
  it only when the text equals `written`; `ENOENT` is silence.
- **Warnings.** `Owned.DefaultWarn(line)`: inside a test (`TestContext.Current.Test is not null`) ⇒
  `TestContext.Current.AddWarning(line)`; outside ⇒ `Console.Error.WriteLine(line)`. Every type takes an
  `Action<string>? warn` seam so tests capture lines instead of reading the console.
- **`OwnedScope`**: a `List<OwnedDir>` behind a lock; `Dispose` disposes in reverse order, each in its own `try`;
  idempotent.
- **`Scope.IsMine(path)`** — the kernel owner, read from the entry ITSELF, never through a link. POSIX:
  `Native.IdentityOf(path)?.Uid == Native.getuid()` (`O:223-227`). **Windows, decision 2, both keys, on ONE handle:**
  `CreateFileW` with the values in `Native` (`FILE_FLAG_OPEN_REPARSE_POINT`, so a junction or symlink is the entry
  itself and its PARENTS still resolve); (1) `GetFinalPathNameByHandleW` of that handle is strictly inside the final
  path of `Environment.GetFolderPath(SpecialFolder.UserProfile)` (its own handle), compared ordinal-ignore-case by
  path segments (`O:204-207`); AND (2) `GetSecurityInfo(handle, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION)` — the
  owner of the OPEN entry, never of a link's target — read into `new SecurityIdentifier(ptr)` and `LocalFree`d, equals
  `WindowsIdentity.GetCurrent().User`. A null owner, either read throwing, or either key false ⇒ `false`. Namespaces:
  `System.Security.Principal` (`SecurityIdentifier`, `WindowsIdentity`), `System.Security.AccessControl`; the
  path-based `System.IO.FileSystemAclExtensions.GetAccessControl` (in-box in net10.0, no package) is NOT used — it
  opens by path and follows a link. Nothing else in the repository asks who owns a file. `Dispose` never asks: teardown
  removes by the run's own exact path, whoever the kernel says owns it.
- **Elevated runs (Windows) — measured first, then asserted.** A process elevated by UAC creates files owned by
  `BUILTIN\Administrators`, not by the user's SID, so key (2) fails for the run's OWN files: `IsMine` is false, the
  sweep lists this run's old folders as `foreign-uid` and never removes them, and the audit's claim scan sees nothing
  (its registry check still does). Teardown by exact name is unaffected. Kept, by decision 2: a person removes an
  elevated run's leftovers after looking. Whether Git for Windows' `stat %u` (`J:133-135`) reads such entries as the
  caller's is NOT verified — the janitor is forced to `--dry-run` on Windows anyway (`J:22-24`). GitHub's
  `windows-latest` runner is elevated (facts, *CI*), so **scenario 0 runs before any S2 code** and prints what the
  runner's files are owned by; every Windows scenario then asserts the MEASURED answer — `IsMine` true where the owner
  SID is the user's, false (and `foreign-uid` in S3) where it is `Administrators` — never a guessed one.
- **`Scope.ReadClaimText(file, mine)`** — the ONE safe read (`O:470-491`): `Native.IdentityOf(file)` null ⇒ `Missing`;
  not a regular file (a FIFO would block, a link is not followed) ⇒ `Unreadable`; `!mine(file)` ⇒
  `Unreadable("foreign-uid")`; else `FileStream(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)`
  reading at most 513 bytes ⇒ `Text`; any throw ⇒ `Unreadable`. `Parse` of a text over 512 bytes is null.
- **`Native.IdentityOf(path)`** — Linux: `statx(AT_FDCWD = -100, path, AT_SYMLINK_NOFOLLOW = 0x100, STATX_BASIC_STATS = 0x7ff, buffer)`
  into a 256-byte buffer with the kernel's fixed layout (`stx_mask` u32 @0, `stx_nlink` u32 @16, `stx_uid` u32 @20,
  `stx_mode` u16 @28, `stx_ino` u64 @32, `stx_ctime` @96 and `stx_mtime` @112 as `{ i64 sec, u32 nsec }`,
  `stx_dev_major` u32 @136, `stx_dev_minor` u32 @140); `Dev` = major << 32 | minor; type from `stx_mode & 0xF000`
  (`0x8000` regular, `0x4000` directory, `0xA000` link); **`stx_mask` must contain every bit this reads —
  `STATX_TYPE 0x1 | STATX_MODE 0x2 | STATX_UID 0x8 | STATX_MTIME 0x40 | STATX_CTIME 0x80 | STATX_INO 0x100` — else
  throw (fail closed: a file system that answers without a field is no identity)**; errno `ENOENT` ⇒ null, any other
  ⇒ throw. `statx` is exported by glibc ≥ 2.28 — the Ubuntu runner, WSL and every supported .NET 10 Linux; musl: not
  measured (the round-2 judge names 1.2.5). Windows:
  `CreateFileW` + `GetFileInformationByHandleEx(FileIdInfo)` (`VolumeSerialNumber` u64, `FileId` 128-bit — ReFS needs
  the wide one) and `(FileBasicInfo)` for `ChangeTime` / `LastWriteTime` and the attributes; `ERROR_FILE_NOT_FOUND` /
  `ERROR_PATH_NOT_FOUND` ⇒ null. `Uid` is null on Windows. **macOS: not built** — no CI leg, no developer machine, and
  Darwin's `lstat` layout differs by symbol (`lstat$INODE64` on x86-64); `Owner.This` throws
  `PlatformNotSupportedException` there until someone measures it, and the facts of this document say so.
- **`Liveness.ProbeProcess(pid)`** — the reference's probe (`O:332-342`), never `Process.GetProcessById` (its Linux
  answer under `hidepid` and its Windows access rules are .NET's, not the contract's). **Bounds, never an unchecked
  cast:** `pid < 1` or `> 2^53` ⇒ `Unknown`; Linux `pid > int.MaxValue` ⇒ `Unknown` (`kill` takes an `int`); Windows
  `pid > uint.MaxValue` ⇒ `Unknown` (`OpenProcess` takes a `DWORD`) and `pid == 1` ⇒ `Unknown`. Linux:
  `Native.kill((int)pid, 0)` — `0` ⇒ `Live`; errno `ESRCH` (3) ⇒ `Dead`; `EPERM` or ANY other errno ⇒ `Unknown`.
  Windows: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, false, (uint)pid)` — null with
  `ERROR_INVALID_PARAMETER` (87) ⇒ `Dead`; null with `ERROR_ACCESS_DENIED` (5) or anything else ⇒ `Unknown`; with a
  handle (closed in `finally`): `GetExitCodeProcess` fails ⇒ `Unknown`; code ≠ `STILL_ACTIVE` (259) ⇒ `Dead`; 259 ⇒
  `WaitForSingleObject(h, 0)`: `WAIT_OBJECT_0` (0) ⇒ `Dead` (a process that exited with code 259 is signalled),
  `WAIT_TIMEOUT` (0x102) ⇒ `Live`, anything else ⇒ `Unknown`. That is the shape of libuv's `uv__kill` with signal 0
  (`OpenProcess` with both rights, `GetExitCodeProcess`, then the wait for the 259 case), which the reference's
  `process.kill(pid, 0)` calls — with the one difference that libuv's "any other error" becomes an errno and the
  reference reads every non-`ESRCH` errno as `unknown` (`O:338`), exactly as here. Then, Linux and `Live` only:
  `Start` = `StartFromProcStat(/proc/<pid>/stat, btime, ClockTicksPerSecond())` (null when unreadable). No
  `HasExited`, no `Process.Start`, nothing that spawns.
- **`Liveness.State(owner, probe, facts)`** — rule §3's order exactly (`O:531-552`): `owner is null` ⇒ `Unknown`;
  `NonCanonical` ⇒ `Unknown`; `owner.Token != (facts.CallerOwner ?? OwnerToken.This)` ⇒ `Unknown`; `facts.NameOwner`
  given and `!= owner.Token` ⇒ `Unknown`; `facts.KernelMine == false` ⇒ `Unknown`; `owner.Host` ≠ `Owner.This.Host`
  (ordinal, case-insensitive) ⇒ `Unknown`; this host ends `@linux` and (`owner.Ns is null` or `!= Owner.This.Ns`) ⇒
  `Unknown`; `Pid < 1` or `> 2^53` ⇒ `Unknown`; `Pid == 1` and host ends `@windows` ⇒ `Unknown`; `probe(Pid)` throws
  ⇒ `Unknown`; `Dead` ⇒ `(facts.Hidepid ?? HidepidActive()) ? Unknown : Gone` (the ONLY `Gone`); not `Live` ⇒
  `Unknown`; either start null ⇒ `Live`; `|Δ| <= StartToleranceSeconds` ⇒ `Live`; otherwise `Unknown`.
- **`UnmatchablePid`** — a port of `dew_flow_conventions · tools/lib/unmatchable-pid.mjs:32-49`: `Next(i = 0)` =
  Windows `777770 + 4·i` (≡ 2 mod 4), Linux `pid_max + 1 + i` (`/proc/sys/kernel/pid_max`, else 4194304);
  `AssertNoSuchProcess(pid)` asserts the premise through `ProbeProcess` (`Dead`), before AND after the test that
  uses it. **Windows pids are handle-table indices and the kernel clears the two low tag bits**, so
  `OpenProcess(4n + 2)` opens process `4n` — which is why the premise is asked the way the probe asks (`Dead` for
  `777770` means `777768` is not alive either) and why scenario 13 first MEASURES the aliasing on the machine:
  `ProbeProcess(Environment.ProcessId + 2)` must answer `Live`. **A test never takes a dead owner from a process it
  started and killed** — the OS hands that pid to the next process (measured by E1 on Windows, one run in fifteen).
- **Fail-closed edges, stated once:** an identity that cannot be read never compares equal to anything (`S:135`:
  `null` on either side is "changed"); any probe error other than "no such process" (`ESRCH`, `ERROR_INVALID_PARAMETER`)
  is `Unknown`; the READER's own namespace missing on Linux makes every record `Unknown` (`owner.Ns != null` is not
  enough); any exception on a P/Invoke path — `IdentityOf`, `OwnerSidOf`, the final path, `kill`, `OpenProcess` —
  gives that path's fail-closed answer (`IsMine` false, `ReadClaimText` unreadable, the probe `Unknown`), never a
  throw out of the sweep and never a `Gone`.
- **Analyzers.** `File.SetUnixFileMode` is `[UnsupportedOSPlatform("windows")]`; `SecurityIdentifier` compare and
  `WindowsIdentity` are `[SupportedOSPlatform("windows")]`: every such call sits under `OperatingSystem.IsWindows()` /
  `IsLinux()` or `CA1416` breaks the build. `Native.cs` is `static partial` with `[LibraryImport]` (`SetLastError = true`,
  `StringMarshalling.Utf8` for libc, `Utf16` for kernel32/advapi32) and `Marshal.GetLastPInvokeError()`; its generated
  marshalling is unsafe code, so `Mcp.Tests.csproj` gains `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — the test
  project only. Every async call in a test takes `TestContext.Current.CancellationToken`.
- **CI (this story).** `build-test` gains, before `Build`, the step the other two jobs already have —
  `git submodule update --init --depth 1 .claude/rules/shared` — because the vectors test and S4's token check read
  the mount and fail without it. A new job `build-test-windows` on `windows-latest`: checkout, the same submodule
  step, `setup-dotnet` 10.0.x, `dotnet build dew_flow_mcp.slnx -c Release` (no `Formatted` step: `core.autocrlf`
  noise, facts), then the test executable. **Until S4 lands the job runs
  `.\tests\Mcp.Tests\bin\Release\net10.0\Mcp.Tests.exe --filter-namespace Mcp.Tests.Leftovers`; from S4's migration
  commit on it runs the FULL executable** — all 11 migrated classes dispose through the helper on Windows, which is
  what the job proves. A migrated test whose premise is Linux-only (a POSIX mode, a `/proc` read, `root`) says so
  with `Assert.Skip("<the premise> — Linux only")`, never a `[Fact]` that fails on Windows and never a silent pass;
  the job's log lists every skip and the PR quotes the count; a test that is red on Windows for a platform reason is
  a red job, fixed or skipped with its reason before the merge. The Windows half of this epic is proven by a
  machine, not by hand.
- **Windows ownership fixtures (`WindowsOwnership.cs`, test code; commit 2).** On an elevated runner every own file
  is `Administrators`-owned, so a green leg could prove preservation and never collection. Two helpers make BOTH
  outcomes on any machine: `OwnedByCaller(path)` — `new DirectoryInfo(path)` (or `FileInfo`) `.GetAccessControl()`,
  `SetOwner(WindowsIdentity.GetCurrent().User)`, `SetAccessControl(…)` (`System.IO.FileSystemAclExtensions`,
  `System.Security.AccessControl.DirectorySecurity`): setting the owner to the caller's OWN user SID needs only
  `WRITE_OWNER` on the object, which the creator's default DACL grants — no privilege; `SeRestorePrivilege` is needed
  only to make a SID the token cannot own the owner. `ForeignToCaller(path)` — first tries `SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null))`,
  which succeeds where the token may own that group (an elevated token); where it throws (`PrivilegeNotHeldException`
  / `UnauthorizedAccessException`, a normal token without `SeRestorePrivilege`) the fallback is key (1): the entry is
  made under `Environment.GetFolderPath(SpecialFolder.CommonDocuments)` (`C:\Users\Public\Documents`, writable by every
  user and OUTSIDE the caller's profile), so `IsMine` is false by the path key whatever the SID. Both helpers return
  the path they prepared; neither skips.

**Scenarios** (RED first — write the tests against the signatures above before the types exist and record the build
failure, then stub the types and record each scenario failing on its assertion before the body is written). Every
scenario that counts what a call left behind passes a PRIVATE `parent` (an `OwnedDir` of its own). Names in tests are
built at run time from `Owned.NameBase`, `Owned.Prefix` or the word in two pieces (`"teststo" + "delete"`, `O:170`),
never as bare-prefix literals (decision 3, S4 sc. 3). `Other` = the caller's token with its last digit changed. The
mounted `tools/fixtures/owner-vectors.json` is read through `RepositoryRoot.Path`; **a missing or unreadable vectors
file FAILS the test that needs it, never skips** (an uninitialised submodule is a broken setup, not a clean one).

0. **The runner's facts, measured before any S2 code** (`RunnerFactsTests.cs`; it asserts nothing about the helper,
   it RECORDS): on Windows, create one file under `Path.GetTempPath()` (`// testsToDelete-exempt: raw-temp — the
   measuring test must see the raw temp root once`) and print, through `TestContext.Current.AddWarning`, the file's
   owner SID (`Native.OwnerSidOf`'s call, inlined here until S2 exists), `WindowsIdentity.GetCurrent().User`,
   `.Owner`, whether the two SIDs are equal, whether the token is elevated (`new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)`),
   and `%TEMP%`'s final path against the profile; on Linux print `getuid()`, `/proc/1` visibility and the auxv tick
   rate. Run on the new `build-test-windows` job and on the Linux job; the output goes in the PR, and every Windows
   assertion below is written against it.
1. **A name carries the prefix, the owner, the token, what, and six random characters.** `Owned.NameFor("pg")`
   matches `^testsToDelete-[uw][0-9]{1,10}-mcp-pg-[A-Za-z0-9]{6}$` and starts with `Owned.NameBase`; two calls differ;
   `NameFor("a-b ")` is accepted; `NameFor(new string('x', Owned.Room))` is 63 characters. `FullName()` matches every
   `NameFor` result, and `NameBase + "pg-"` + an id of 6 and of 12 characters, and the `_` spelling in lower case; it
   does not match an id of 5 or of 13, one holding a `.` (`clxdyztb.njt`), or the same name under `Other`.
   `Classify`: the owner-form, legacy and none vectors of the mounted `owner-vectors.json` (`names`, n01–n16).
2. **A bad `what` throws before anything exists.** `""`, `"a/b"`, `"a\\b"`, `"-x"`, `"a\u0001b"`, `"a\u007fb"`,
   `"a\0b"`, `Room + 1` × `x` ⇒ `ArgumentException` naming the room, and the private parent's entry list is unchanged.
3. **The owner token.** `Compute(windows: false, uid: 1000, …)` = `u1000`; `Compute(true, null, "Alice", …)` =
   `w735577801`; every vector of the mounted `owner-vectors.json` `windows token` table (w01–w13) through `Windows`;
   `AsciiLower("SØREN")` = `sØREN`; env `TESTSTODELETE_OWNER=u42` ⇒ `u42`; `=` (empty) ⇒ the real token; `=root`,
   `=u`, `=u12345678901`, `=U1`, `="u1\n"`, `=" u1"` ⇒ throws naming the grammar (RED against a `$`-anchored regex:
   `u1\n` passes); `OwnerToken.This` matches `OwnerGrammar` and, off Windows, equals `"u" + Native.getuid()`.
4. **A folder and its claim, side by side, never inside.** `OwnedDir.Create("path Unicode пробел", parent)` makes a
   folder whose name starts with `NameBase + "path Unicode пробел-"`, empty, and a sibling `<folder>.owner` whose
   text `Parse`s to `Owner.RunRecord()` and ends in one `\n`.
5. **Disposal removes the folder and the claim, and is idempotent.** After `Dispose()` neither exists; a second
   `Dispose()` writes no warning; the instance is in `Unreclaimed()` between `Create` and `Dispose`, not after.
   **A disposal that fails stays visible:** make the folder unremovable (sc. 6's method), `Dispose()` ⇒ the
   instance is STILL in `Unreclaimed()` with `DisposeAttempted == true`; make it removable, `Dispose()` again ⇒ gone
   from the registry, folder and claim gone. RED first: against a registry that drops the entry on the first
   `Dispose()`, the failed instance is absent.
6. **Teardown never throws, and what it cannot remove stays prefixed AND claimed.** Windows: hold
   `File.Open(<dir>/held.txt, FileMode.Create, FileAccess.Write, FileShare.None)`; POSIX: `File.SetUnixFileMode(parent, UserRead | UserExecute)`,
   `Assert.Skip` as root. `Dispose()` returns; exactly one warning `owned: could not remove … left for the start-of-run
   sweep`; the folder still exists, its name matches `Pattern()`, its `.owner` still exists. Then restore and remove.
7. **Unwind: a test body that throws still leaves nothing**, and three folders from one scope, the third under the
   second, are all gone after `Dispose()` with no warning.
8. **A link inside is unlinked, never followed.** A sentinel `OwnedDir` holding `keep.txt`; a top-level directory link
   and one at depth 2 (`WorkspaceToolTests.cs:346-373`'s method; `Assert.Skip` when none can be made). After
   `Dispose()` the folder is gone and `keep.txt` still reads `must survive`.
9. **Claim first, create exclusively, never adopt.** (a) A claim already present for the first name the `nextName`
   seam returns ⇒ the call succeeds with the second name and the first claim is untouched. (b) **A squatted folder:**
   the first name's FOLDER already exists (unclaimed) ⇒ the `warn` seam got exactly one line containing `already exists
   under the name this run claimed`, the squatter's folder is untouched and holds its file, NO claim of ours is beside
   it, and the call returned the second name. (c) A squatted folder whose claim was REPLACED between our write and the
   collision (the `afterClaim` seam rewrites it) ⇒ that claim stays (`ClaimIfStill` refuses). (d) A `parent` that does not exist ⇒
   `InvalidOperationException` starting `owned: could not claim`, and nothing named `NameBase` appears anywhere.
   (e) **A write that fails after the exclusive create:** the `beforeClaimWrite` seam throws `IOException` ⇒ the call
   throws `could not claim`, and NO file named `NameBase` remains in the parent (the empty claim was removed); the seam
   instead deletes the empty claim and writes another text under the same name ⇒ the call throws, and THAT file still
   exists with its text (another identity, another content: not ours). RED against `Directory.CreateDirectory`: (b)
   adopts the squatter; RED for (e): an empty claim is left behind.
10. **The record round-trips, carries what the OS gives, and refuses everything else.** `Parse(RunRecord().Format() + "\r\n")`
    equals `RunRecord()`; `Owner.This.Host` matches `^[^@:\s]+@(linux|macos|windows)\z`, starts with
    `HostName(Dns.GetHostName())`, and under WSL (`WSL_DISTRO_NAME` set) holds `~` + the distribution; the `hostnames`
    vectors (h01–…) through `HostName`; the `record` vectors (r-…) through `Parse` (valid ⇔ not null); every
    `grammar` vector (g01–g14, i01–i29 — the LEGACY grammar, `<host>@<os>:<pid>[:<start>]`) through `Parse` ⇒ null,
    valid or not: the merged reader never reads the old form; on Linux `Start`
    and `Ns` are not null, `Ns` equals the inode `readlink /proc/self/ns/pid` prints (read by the test through
    `LinkTarget`), `ClockTicksPerSecond()` equals the value `getconf CLK_TCK` prints (spawned ONCE by the test — a test
    may, the helper may not), and **`Start` is within 2 s of `Process.GetCurrentProcess().StartTime.ToUniversalTime()`
    as Unix seconds — the delta recorded in the PR**; on Windows `Start` and `Ns` are null and `Format()` has three
    fields. `ClockTicksFromAuxv` on hand-built vectors: `[17, 100, 0, 0]` ⇒ 100 (both widths, both endiannesses), no
    `AT_NULL` ⇒ null, odd length ⇒ null, rate 0 ⇒ null. `StartFromProcStat("1 (a b) S …")` with a command name holding
    spaces and parentheses ⇒ field 22 read after the LAST `)`; 19 fields ⇒ null; ticks null ⇒ null.
11. **`TESTSTODELETE_RUN`.** Through the `env` seam: unset ⇒ `This`; the text of `This.Format()` ⇒ a record equal to
    `This`; the same with an unmatchable pid ⇒ `This` and one warning; `Other`'s record of this process ⇒ `This` and
    one warning; `hands off` ⇒ `This` and one warning.
12. **`State` follows rule §3's order** (injected probe; `facts` explicit): null ⇒ Unknown; `NonCanonical` ⇒ Unknown;
    token `Other` ⇒ Unknown; `NameOwner = Other` ⇒ Unknown; `KernelMine = false` ⇒ Unknown; host `other@<tag>` ⇒
    Unknown; this host name with the other OS tag ⇒ Unknown; this host upper-cased ⇒ evaluated; on Linux `Ns` null
    ⇒ Unknown and `Ns + 1` ⇒ Unknown; pid 0, `long.MaxValue` ⇒ Unknown with the probe never called; `windows:1` ⇒
    Unknown; `Dead` ⇒ Gone, and `Dead` with `Hidepid = true` ⇒ Unknown; `Unknown`, a throwing probe ⇒ Unknown; alive
    with either start null ⇒ Live; 2 s apart either way ⇒ Live; 3 s and 10⁶ s apart ⇒ Unknown — never Gone. **Then
    the mounted vectors:** `scope` (s01–s15) — `nameOwner`, `recordOwner`, `callerOwner`, `kernelMine`, `hidepid` into
    `Facts` and the record; `ns` `same` = `Owner.This.Ns`, `other` = `Ns + 1`, `absent` = none, `leading-zero` = the
    text `0<ns>` (NonCanonical); `pid` `absent` / `present` / `refused` = an injected probe answering `Dead` / `Live` /
    `Unknown` — each `expected` matches (`s09`, `s10`, `s15` are Linux-reader cases: on Windows the record carries no
    namespace and they are asserted `Unknown` only where `Owner.This.Host` ends `@linux`); `verdicts` (v01–v26) —
    `host` `same` / `same-case` (`Owner.This.Host` case-swapped) / `foreign-name` / `foreign-os`; `pid` as above;
    `startDelta` added to the probe's start when `liveStart`; `pidText` and `ownerText` (`{HOST}`, `{NAME}` replaced)
    as the record text, behind `OwnerToken.This + ":"` — each `expected` matches, the `sides: ["node"]` ones included
    (this reader compares starts, like Node).
13. **The real probe and the real kernel owner.** `State(Owner.This, ProbeProcess)` ⇒ Live (through the start
    comparison on Linux); on Windows FIRST `ProbeProcess(Environment.ProcessId + 2)` ⇒ `Live` (the tag-bit aliasing,
    measured; a machine where it is `Dead` makes the unmatchable premise unsound — stop and report); then
    `UnmatchablePid.Next()` with the premise asserted before and after ⇒ `ProbeProcess` = `Dead` and `State` = Gone;
    `ProbeProcess(0)`, `(-1)`, `(2^53 + 1)`, on Linux `(int.MaxValue + 1L)`, on Windows `(uint.MaxValue + 1L)` ⇒
    `Unknown` with no native call; on Linux as a non-root user `ProbeProcess(1)` ⇒ `Unknown` (EPERM), `Assert.Skip` as
    root; `Scope.IsMine` of a file this test made ⇒ the MEASURED answer of scenario 0 (true where the owner SID is the
    user's, false where it is `Administrators` — asserted, not skipped); **and on Windows, whatever the runner's
    elevation, both outcomes through the fixtures:** `IsMine(OwnedByCaller(file))` ⇒ true and its `OwnerSidOf` equals
    `WindowsIdentity.GetCurrent().User`; `IsMine(ForeignToCaller(file))` ⇒ false (the `Administrators` SID or the
    out-of-profile path, the PR says which the runner took); RED first against an `IsMine` that checks the profile
    alone (`O:215-222`): the elevated runner's `Administrators`-owned file inside the profile reads true; `IsMine` of a symlink to that file ⇒ judged on
    the LINK's own owner and identity, and `Native.IdentityOf` of it reports `IsLink`; a `mine` seam answering false
    makes `ReadClaimText` of the same file `Unreadable("foreign-uid")`; a DIRECTORY named like a claim ⇒ `Unreadable`
    (the regular-file check; a FIFO needs `mkfifo`, which .NET cannot make without a child — not provoked, the
    directory covers the branch); a 513-byte claim ⇒ `Parse` null; `Native.IdentityOf`: a folder equals itself after
    `Directory.Move` (same `Dev`, `Ino`), differs from a re-created folder of the same name, a missing path ⇒ null, on
    Linux `Uid == getuid()` and a `stx_mask` without `STATX_INO` (an injected buffer through the pure parser) ⇒ throws.
14. **Elevated run (Windows, by hand, recorded in the PR; not a merge condition).** On the owner's machine, run the
    executable from an elevated prompt once and from a normal prompt once: the two answers of scenario 13's `IsMine`
    line, the sweep's report of a planted old own folder (`foreign-uid` and kept under one, collected under the other,
    where the measured owners differ), and whether `stat %u` of Git for Windows' bash reads the elevated entry as the
    caller's. The CI runner's answer is scenario 0's.

- **Commands.** `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_mcp.slnx -c Debug -m:4`;
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe --filter-namespace "Mcp.Tests.Leftovers"`;
  on Linux (WSL or CI) `./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests --filter-namespace "Mcp.Tests.Leftovers"`; the whole executable.
- **DoD.** Three commits, in *Order*'s sequence, each green on BOTH legs before the next is started: (1) scenario 0's
  output recorded in the PR and the Windows job green; (2) `Native`, identity, `Scope`, the probe and their tests —
  sc. 12, 13 (both Windows ownership outcomes included) — green on both legs, which is S3's starting condition; (3)
  the rest. Build at 0 warnings; every scenario observed RED, then GREEN, on the Windows job and on the Linux job
  (scenario 6, 10 and 13 run a different branch on each); the constants table of S1 in commit 3's message; the module
  has no caller yet — S3 and S4 adopt it.

## S3 — The start-of-run sweep, as an assembly fixture

- **Goal.** A run that was killed leaves `testsToDelete-<owner>-mcp-…` folders its `Dispose` never reached; the same
  user's NEXT run here collects them — only its own owner and token, only on the two keys, by identity, and never by
  age or by a name alone (rule §4). What is not its to remove, it names.
- **Files.** New `tests/Mcp.Tests/Leftovers/LeftoversSweep.cs`, `LeftoversSweepFixture.cs` (it carries
  `[assembly: AssemblyFixture(typeof(LeftoversSweepFixture))]`), `LeftoversSweepTests.cs`.

**The change.**

- `LeftoversSweep.Run(SweepOptions o) → SweepResult` with `SweepOptions(string Tmp, DateTimeOffset Now, TimeSpan MinAge,
  Func<long, Probe> Probe, Func<string, bool> Mine, SweepHooks? Hooks)` and
  `SweepResult(IReadOnlyList<string> Removed, IReadOnlyList<(string Path, string Reason)> Preserved, IReadOnlyList<string> Listed, IReadOnlyList<string> Errors)`;
  `MinAge` = 10 minutes (`S:39`). A port of `S:437-453`'s order without the worktree pass — this repository makes no
  worktree: **quarantines, folders, orphan claims**, each step in its own `try` (`S:421-429`); it never throws; probe
  answers are cached per pid; `Mine` defaults to `Scope.IsMine` (a seam: a second uid cannot be had in a test);
  `Hooks` (`AfterCapture`, `AfterVerdict`, `AfterRename`, `AfterRemove`, `AfterReserve`) are seams for a change between the capture,
  the verdict and the removal. The caller's owner is `OwnerToken.This`; the state is
  `Liveness.State(record, cached, new Facts(CallerOwner: owner, NameOwner: nameOwner, KernelMine: true))` (`S:444-445`).
  1. **Relation of a name** (`S:61-69`): `Owned.Classify` — owner form with token `mcp`: `own` when its owner is the
     caller's, else `foreign`; legacy form with token `mcp`: `legacy`; anything else: not this sweep's business, silent.
  2. **Folders** (`S:340-374`). Depth-1 entries of `Tmp` whose `Native.IdentityOf` says `IsDirectory && !IsLink` —
     a symlink or junction is never a candidate, whatever its name (`S:345`; `janitor.sh:538`), and is never removed
     even when its name's claim is `gone`. `foreign` ⇒ `Listed` `foreign: folder <name> — owner <o>`, nothing else. `legacy` ⇒ `Preserved`
     `legacy` and `Listed` `legacy: folder <name> — remove with: <command>` (`S:240-250`'s command for this platform)
     — **listed only, never collected** (rule §4 allows preserving more, `R:174-175`; this repository never produced
     a legacy name). A `.git` FILE ⇒ `other clone`. Otherwise **capture first** (`S:149-152`): `Native.IdentityOf(dir)`
     and the claim text through `Scope.ReadClaimText(claim, o.Mine)` — the sweep's `Mine` seam reaches the claim read
     too, never only the folder; then the decision table (`S:72-81`): age =
     `Now − LastWriteTimeUtc` `< MinAge` ⇒ `young`; `!Mine(dir)` ⇒ `foreign-uid` + `Listed` `foreign:`; no claim ⇒
     `unmarked` + `Listed` `unmarked: folder <name>` — **never collected, however old** (`R:75-76`); unreadable ⇒
     `unreadable` (or `foreign-uid`); `State` ⇒ `Gone` remove, `Live` ⇒ `live`, `Unknown` ⇒ `foreign host` when the
     host differs, else `unknown`.
  3. **Remove by identity** (`S:204-218`): `Hooks.AfterVerdict`; re-check identity and claim text unchanged, else
     `replaced`; `Directory.Move(dir, quarantine)` with `quarantine = Path.Combine(parent, "." + name + ".teststodelete-quarantine-" + Environment.ProcessId + "-" + 4 random bytes as hex)`
     (same parent: atomic, never across a file system; a leading `.`: no prefix match reaches it); `Hooks.AfterRename`;
     identity at the quarantine and the claim at the ORIGINAL path unchanged, else restore (below) and `replaced`
     (`quarantine-conflict` when the restore failed); `UnlinkTopLevelLinks(quarantine)`; `Removal.Quietly(quarantine)`
     — `false` ⇒ an `Errors` entry; `Hooks.AfterRemove`; then delete the claim ONLY while its identity and text are still the ones captured
     before the rename — a claim replaced in that window is kept and reported `replaced` (round 3; sc. 5e). **Restore** (`S:169-197`): Windows `Directory.Move` back (a
     rename onto an existing directory fails there, so it is exclusive); Linux `Native.MakeDirectoryExclusively(original)`
     as a mode-700 placeholder, `Hooks.AfterReserve`, `Native.RenameOnto(quarantine, original)` (`rename(2)` onto an
     empty directory is atomic; `Directory.Move` refuses an existing target), on failure remove the placeholder only
     while it is still a real, empty directory of the caller's.
  4. **Orphan claims** (`S:376-404`): entries `*.owner` of `Tmp` that are REGULAR files (`IsRegular && !IsLink` —
     `EnumerateFiles` returns symlinks too, `S:379`), whose folder is absent and whose age ≥ `MinAge`; relation as above (`foreign` ⇒ `Listed foreign: claim …`; `legacy` ⇒ `Listed legacy: claim …`); `own` ⇒ the same
     decision with `Mine(claim)` and the parsed record — `Gone` ⇒ the claim is deleted; `live`, `unknown`, `foreign host`,
     `unreadable` keep it. Age only selects: a young orphan is a creation in progress (S2 claims first).
  5. **Quarantines first** (`S:406-419`): entries matching `^\.(.+)\.teststodelete-quarantine-(\d+)-([0-9a-f]+)$`
     (case-insensitive) that are real directories, whose inner name's relation is not "other", that are `Mine`, whose
     renamer `Probe` says `Dead`, and whose `ChangeTime` AND `WriteTime` (from `Native.IdentityOf`) are both past the
     window ⇒ restored under the inner name when free, `Listed` `notice: restored <name> from a quarantine a killed
     sweep left`; taken ⇒ `Preserved` `quarantine-conflict` AND `Listed`
     `quarantine-conflict: folder <quarantine> — its name <name> is taken; remove by hand with: <command>` (the
     platform's command for the quarantine folder alone). **Every later run retries the restore; while the name is
     taken the quarantine stays, listed, and the sweep never removes it** (`R:185-187`: restore or list — a quarantine
     is a folder this sweep judged `gone` once, but the re-check that authorises its removal cannot run under a name
     it no longer holds).
  It never touches a name `Classify` does not put in this repository's scope: not another token, not the bare prefix,
  not the old `mcp-…` names (below).
- `LeftoversSweepFixture` — `public sealed class`, public parameterless constructor (xUnit v3 creates it **before any
  test of the assembly runs**): calls `Run` over `Owned.TempRoot()` with `DateTimeOffset.UtcNow`, `MinAge`,
  `Liveness.ProbeProcess`, `Scope.IsMine` inside `try/catch`, keeps `public SweepResult LastRun`, and — only when
  `Removed + Preserved + Listed + Errors > 0` — writes ONE line `mcp tests: leftovers — removed N, preserved M, listed K`
  plus one line per `Listed` and per `Errors` entry through a `report` seam (`internal LeftoversSweepFixture(Func<SweepResult> sweep, Action<string> report)`;
  the public constructor passes the real sweep and `Console.Error.WriteLine` — no test is running yet). A throwing
  sweep is one reported line; **the constructor never throws** (`R:193`). S4 adds the end-of-run audit to this class.
- **Old names are not swept.** The 17 old prefixes (`mcp-daily-log` … `mcp-workspace`) are not prefixed names at all:
  `mcp-` is not the prefix, and other software makes `mcp-…` temp folders too. A machine that still holds them deletes
  them once by hand, after looking (S4 § *Hand clean-up*).

**Scenarios** (`LeftoversSweepTests.cs`; RED first: the tests before `LeftoversSweep` exists, each observed failing).
Every scenario passes `Tmp` = a private `OwnedDir` — never the real temp root. Dead owners are `UnmatchablePid`
(premise asserted); "old" is `Directory.SetLastWriteTimeUtc(dir, now − 11 min)` (claims with `File.SetLastWriteTimeUtc`);
own names are `Owned.NameBase + "<what>-abc123"`; a claim's record is `new Owner(OwnerToken.This, Owner.This.Host, <pid>, Owner.This.Start, Owner.This.Ns).Format()`
unless stated (on Linux the namespace must be this reader's or the verdict is `unknown` by design — sc. 2 asserts that too).

1. **A killed run's folder is collected.** The state a kill leaves, built by hand: an old own folder holding a file and
   a top-level link (S2 sc. 8's method) to a sentinel `OwnedDir` outside `Tmp`, claimed by an unmatchable pid ⇒ `Run`
   removes the folder and its claim, `Removed` names the folder, no quarantine remains, and the sentinel's `keep.txt`
   still exists. RED before the sweep exists; it is the leak class this story closes.
2. **Two keys — a live, foreign, unknown or unmarked folder is never reclaimed, however old.** In one `Tmp`, all old
   except where stated: claimed by THIS process ⇒ `live`; claimed by `Other` with this host and pid ⇒ `unknown`
   (the record's owner is not the caller's); NAMED under `Other` and claimed by a dead pid ⇒ not in `Preserved`, one
   `Listed` line `foreign: folder …`; claimed by `other@<tag>:…` ⇒ `foreign host`; this host with the other OS tag ⇒
   `foreign host`; on Linux, claimed by a dead pid with NO namespace ⇒ `unknown` and with `Ns + 1` ⇒ `unknown`; claim
   text `hands off` ⇒ `unreadable`; a 513-byte claim ⇒ `unreadable`; the OLD grammar `<host>@<os>:<dead pid>` ⇒ `unreadable`;
   `Mine` seam false for one folder ⇒ `foreign-uid` and a `foreign:` line, and `Mine` false for the CLAIM only (the
   seam answers by path) ⇒ `foreign-uid` too; claimed by THIS process's pid with a start 3 s off ⇒ `unknown`; a dead
   pid with ANY start, 3 s off included ⇒ removed (`O:548`: dead is answered before the starts are compared); YOUNG
   and dead-claimed ⇒ `young`; `.git` file and no claim ⇒ `other clone`; **unmarked and old ⇒ `unmarked`, an
   `unmarked:` line, and the folder still exists** (RED against E1's rule, which removed it); **a SYMLINK (Windows: a
   junction; `Assert.Skip` when none can be made) under an own name, old, with a sibling claim of a dead pid ⇒ not in
   `Removed`, not in `Preserved`, the link still there, its target folder and the target's `keep.txt` intact, and the
   claim untouched**; dead-claimed ⇒ removed. Each preserved entry carries exactly its reason; every preserved folder
   still exists afterwards.
3. **Anchored and scoped.** Old, dead-claimed or unmarked: `"x-" + NameBase + "a-abc123"`, `"teststo" + "deletemcp-a-abc123"`,
   `Owned.Prefix + OwnerToken.This + "-conventions-a-abc123"`, `Owned.Prefix + OwnerToken.This + "-mcpx-a-abc123"`,
   `"mcp-workspace-abc123"` all survive untouched and appear in no list; `Owned.Prefix + "mcp-a-abc123"` (legacy) survives
   and gets one `legacy: folder … — remove with:` line naming the folder AND its `.owner`; `NameBase.ToUpperInvariant() + "A-XYZ123"`
   and `NameBase.ToLowerInvariant().Replace('-', '_') + "a_xyz123"` with a dead claim are removed (case-insensitive,
   both separators).
4. **Orphan claims are judged by owner, never by age alone.** Four EQUALLY old orphan claims with no folder: claimed by
   THIS process ⇒ `live`; by an unmatchable pid ⇒ removed; by `other@<tag>` ⇒ `foreign host`; holding `hands off` ⇒
   `unreadable`. Only the `gone` one disappears. A young dead-claimed orphan ⇒ kept, unlisted; an old `Other`-named one
   ⇒ a `foreign: claim …` line, kept. RED against an age-only rule: three of the four vanish.
5. **Removal is by identity.** (a) Through `Hooks.AfterVerdict` the test MOVES the judged folder aside, creates a
   new folder under the old name, and only then deletes the moved one (an inode freed first is reused by the next
   `mkdir` on ext4, which would make the two identities equal) ⇒ `replaced`, the new folder untouched. (b) Through
   `Hooks.AfterRename` the test rewrites the claim ⇒ the folder is restored under its name, `replaced`, nothing
   removed. (c) A quarantine left by a "killed sweep" (built by hand in its OWN `Tmp`:
   `.<own name>.teststodelete-quarantine-<unmatchable pid>-deadbeef`; its `ChangeTime` cannot be set from .NET, so the
   window is passed by `Now = DateTimeOffset.UtcNow + 11 min` in `SweepOptions`) ⇒ restored, `notice: restored …`;
   the same with the original name TAKEN ⇒ `quarantine-conflict` in `Preserved` and a `quarantine-conflict:` line
   naming the command, both untouched, and a SECOND `Run` with the name freed ⇒ restored (the retry); a quarantine
   whose renamer is THIS process ⇒ untouched, unlisted. (d) After every removal no `.*.teststodelete-quarantine-*`
   entry remains in `Tmp`. (e) Through `Hooks.AfterRemove` the test deletes the claim and writes
   another text under the same name ⇒ that claim is kept, `replaced` is reported, nothing else is touched; RED first
   with an unconditional claim delete (round 3).
6. **The sweep never fails the run.** `Run` on a `Tmp` that does not exist ⇒ one `Errors` entry, no throw; a folder the
   `Mine` seam throws on ⇒ one `Errors` entry and the next folder still judged; the internal fixture constructor with a
   sweep that throws ⇒ completes, the `report` seam got the error line, `LastRun` is empty.
7. **Silence when there is nothing.** An empty `Tmp` ⇒ an empty result, and the fixture's `report` seam got nothing.
8. **The fixture ran before this test.** A test class taking `LeftoversSweepFixture fixture` asserts `fixture.LastRun`
   is not null — the sweep ran at the start of THIS run, over the real temp root.
9. **Windows proves COLLECTION with the real keys, whatever the runner's elevation** (no `Mine` seam: `Scope.IsMine`
   itself). In a `Tmp` under the profile: an old own folder and its dead-pid claim, both passed through
   `OwnedByCaller` (S2) ⇒ `Removed` names it, folder and claim gone; a second old own folder and claim through
   `ForeignToCaller` (its own `Tmp` where the fallback is the out-of-profile path) ⇒ `foreign-uid`, a `foreign:` line,
   both still there. The PR records which branch each fixture took on the runner. RED first: against an `IsMine`
   that checks the profile alone, the second is removed on an elevated runner; against the measured-only sc. 13, an
   elevated runner never removes anything and the leg proves preservation only.

- **Commands.** As S2, `--filter-class "*LeftoversSweepTests"`; then the whole executable twice in a row — the second
  run prints no `leftovers` line on a clean machine.
- **DoD.** Every scenario observed RED, then GREEN, on Windows and Linux; two consecutive full runs print nothing from
  the sweep; the sweep's wall time on a full run is under one second (record it).

## S4 — The guard, the migration of 21 sites, and the end-of-run audit

- **Goal.** No test here creates a temp path except through `Mcp.Tests.Leftovers` (or under a valid inline
  exemption), a guard keeps it so — red on every class of rule §7, on a token that is unregistered, of the owner
  grammar or another repository's, and on a scan of nothing — and a run that leaves a folder it claimed is a red run.
- **Files.** New `tests/Mcp.Tests/Leftovers/LeftoversGuard.cs`, `LeftoversGuardTests.cs`, `LeftoversAudit.cs`,
  `LeftoversAuditTests.cs`, `LeftoversJanitorTests.cs` (the live cross-check), `LeftoversSelfTest.cs` (the audit's
  teeth); `LeftoversSweepFixture.cs` (gains `DisposeAsync`); the 11 files of the migration table; `Owned.cs` (one
  exemption comment — harness setup, added BEFORE scenario 1's RED run); `.github/workflows/ci.yml` (the self-test
  step); new `research/module_tests.md`; `research/architecture.md`.

**The guard.** `LeftoversGuard.Scan(string repoRoot) → GuardReport(int FilesScanned, IReadOnlyList<Finding> Findings)`,
`Finding(string File, int Line, string Class, string Text)`; pure apart from reading files. A port of the shape of
`G:` mapped to this repository's languages.

- **Root.** `RepositoryRoot.Path` (S2). One implementation, not two.
- **Discovery** (a directory named `bin`, `obj`, `node_modules`, `.git`, `.claude`, `.http-artifacts` or `logs` is
  never entered — `.claude/rules/shared` is another repository): `tests/**/*.cs` (every class); `src/**/*.cs` (the
  classes `legacy-name` and `owner-blind` and the token constant ONLY — product code makes no test leftover, and a
  product temp path is not this rule's; today `src/` holds neither word, facts); `.github/workflows/*.yml` and
  `*.yaml`; `.github/scripts/**/*.{mjs,js,cjs,sh,ps1,py}`; `http/**/*.{mjs,js,cjs}`; every `*.sh`, `*.ps1` and
  `*.py`. **Excluded by name**, a list a test pins to exactly one entry: `tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs`
  (it carries the violations as fixture strings; `G:72-74`). `LeftoversGuard.cs` itself is SCANNED: every detector
  word in it — `CreateTemp` + `Subdirectory`, `GetTemp` + `Path`, `GetTemp` + `FileName`, `mkd` + `temp`, the marker
  `testsToDe` + `lete-exempt:`, the prefix word — is spelled in two pieces, as `G:103` does, and a test pins that the
  guard over its own file reports nothing. Zero files ⇒ `FilesScanned == 0`, reported as `scanned nothing under
  <root> — discovery broke; the repository is not clean`.
- **Comments.** A line whose trimmed text starts with `//` or `#` is skipped for matching but read for exemptions.
  **Block comments** `/* … */` are stripped from `.cs`, `.mjs`, `.js` and `.cjs` text BEFORE matching: each character
  inside one becomes a space, every newline is kept, so line numbers do not move; a `/*` inside a string literal
  opens nothing (the stripper walks string literals). Exemption markers are read from the ORIGINAL text.
- **Classes** (rule §7; a hit outside a valid exemption is a finding). The prefix word is spelled in two pieces inside
  the guard (`"teststo" + "delete"`, `G:103`), so the guard's own detectors are not findings:
  - `raw-temp`: `CreateTempSubdirectory`, `GetTempPath2?`, `GetTempFileName` (the API name, no `(` required — `G:248-256`),
    `GetEnvironmentVariable\(\s*"(TEMP|TMP|TMPDIR)"`, `\bmkdtemp(Sync)?\s*\(`, `\btmpdir\s*\(`,
    `process\.env\.(TEMP|TMP|TMPDIR)\b`, `\btemp_dir\s*\(`; in `.sh` and workflows also `\bmktemp\b`; in `.ps1` also
    `New-TemporaryFile|\$env:TE?MP\b`. `RUNNER_TEMP` is never a finding (`R:250`).
  - `raw-worktree`: `["']worktree["']\s*,\s*["']add["']`, `\bworktree\s+add\b`.
  - `docker-run`: `\bdocker\s+run\b` on a line without `--rm`. **Stricter than the rule on purpose:** the §5 carve-out
    is NOT recognised — this repository starts no container, and recognising it means parsing workflow YAML, a
    dependency this repository does not have. A container that needs the carve-out re-opens this guard. Recorded as a
    deviation.
  - `docker-name`: `--name[ =]+(\S+)`, `docker\s+(volume|network)\s+create\s+(\S+)`, `WithName\(\s*"([^"]*)"`,
    `docker\s+build\b.*\s(-t|--tag)[ =]+(\S+)`, `docker\s+compose\b.*\s(-p|--project-name)[ =]+(\S+)` and
    `COMPOSE_PROJECT_NAME["']?\s*[:=]\s*["']?(\S+)` — the captured name (quotes stripped) not matching the OWNER form
    `^teststodelete[-_][uw][0-9]{1,10}[-_]` (case-insensitive; `G:101`). A value that is wholly a shell expansion
    (`"${x}"`, `$(f a)`) is dynamic, not a finding (`G:283-290`).
  - `legacy-name` (`G:107-110`, `G:377-382`): a literal `teststodelete[-_]([a-z0-9]+)` (case-insensitive) whose second
    segment is not of the owner grammar — except `exempt`, `quarantine` and an environment name `TESTSTODELETE_[A-Z]+` —
    and a prefix built from the token: `teststodelete[-_]\{\s*RepoToken\b` (a C# interpolation) or `\$\{?\s*(REPO_TOKEN|RepoToken|repoToken)\b`.
    Exemptible.
  - `owner-blind` — **no exemption of any kind** (`R:255`, `R:260`). In `.cs` (decision 3): any literal
    `teststodelete[-_]` (case-insensitive) NOT followed by `[uw][0-9]{1,10}[-_]`, with the same three carve-outs as
    `legacy-name`, in every `.cs` file except `tests/Mcp.Tests/Leftovers/Owned.cs` (the helper's own file: `Prefix`,
    `Pattern`, `LegacyPattern` live there) — so `Owned.cs` is the ONE place the bare prefix is written and a constant
    elsewhere cannot carry it to a sweep; and, the same intent one step further, **a reference to `Owned.Prefix` or
    `Owned.LegacyPattern` in any `.cs` file outside `tests/Mcp.Tests/Leftovers/`** (the regex `\bOwned\.(Prefix|LegacyPattern)\b`
    on a non-comment line) is a finding of the same class — a test builds names from `Owned.NameBase`, never from the
    bare prefix. In scripts and workflows (`.sh`, `.ps1`, `.mjs`, `.js`, `.cjs`, `.py`, `.yml`): **the SHIPPED
    classifier, started through the mount, not a port** — `LeftoversGuard` runs
    `node --input-type=module -e "<ten lines>" -- <file>…` where the inline module imports `classifyLines` from
    `<root>/.claude/rules/shared/tools/lib/sweeper-scan.mjs` (the one implementation the guard, the machine scan and
    the rule checks share; it needs no `yaml`), reads each file named on the command line, and prints one JSON line
    `{ file, line }` per verdict `broad`; each such line is an `owner-blind` finding. `node` missing, a non-zero exit
    or unparsable output ⇒ one finding of class `owner-blind` naming the cause — the script half is never silently
    skipped; `node` ≥ 20 is on every runner and every developer machine (`low-priority.mjs`). So scripts keep exactly
    the shipped tracer's semantics (literal + same-file binding, its limits included), measured on this branch by
    scenario 3's script cases, which are the shipped tool's own answers.
  - `shared-store`: the four shapes of `G:260-265`, verbatim and case-sensitive (`DROP DATABASE|SCHEMA|TABLE`,
    `TRUNCATE [TABLE]` against a name not in the owner form, `FLUSHALL|FLUSHDB`, `deleteMany({})`) — the prose word
    `truncated` never matches.
  - `ryuk-off`: `TESTCONTAINERS_RYUK_DISABLED\s*[:=]\s*["']?true` (case-insensitive) — no exemption accepted.
- **Exemptions — inline only** (`R:259-263`; `G:217-241`): marker `(?://|#|--|/\*|\*|<!--)\s*testsToDelete-exempt:(.*)$`
  on the finding's line or the line directly above; the rest parsed by `^\s*([^\s—]*)\s*(?:—\s*(.*?))?\s*(?:\*/|-->)?\s*$`.
  Valid when the class is one of `raw-temp`, `raw-worktree`, `docker-run`, `docker-name`, `legacy-name`, `shared-store`
  (`G:75`) and the reason is a reason — ≥ 10 characters, ≥ 3 words, not matching
  `^(see above|see below|needed|required|necessary|obvious|todo|tbd|n/?a|because|why|reason|it is needed)\b`
  (case-insensitive; `tools/ownership-check.mjs:83-91`) — and, for `shared-store`, contains `lock:`. Anything else after
  the marker is a finding of class `exemption` (no class, unknown class, `ryuk-off`, `owner-blind`, no reason, a placeholder).
- **The token is registered and is THIS repository's** (`R:30-36`; `G:695-723`): `LeftoversGuard.TokenFindings(string ruleText, string token, string identity)`
  — `token` must match `^[a-z0-9]{2,12}\z` and NOT the owner grammar; the rule must carry a `## Tokens` heading followed
  by a GFM table whose header is exactly `| token | family | repository |`; the token must be the first cell (backticks
  stripped) of exactly one row; no token twice; the row's `repository` cell must equal `identity` (trimmed,
  case-insensitive), and an empty `identity` is a finding (fail closed). The real check reads
  `<root>/.claude/rules/shared/common/test-leftovers.md` with `OwnedToken.RepoToken` and `OwnedToken.RepoRow`; **a
  missing file is a finding** (an uninitialised submodule registers nothing — run `git submodule update --init .claude/rules/shared`).
  **One constant** (`R:63-65`; `G:731-732`): over every tracked `.cs` (`src/` and `tests/`), `.mjs`, `.js`, `.cjs`,
  `.sh`, `.ps1`, `.py` file, a non-comment line matching `\bRepoToken\b\s*(?::\s*\w+\s*)?=\s*["']([^"'\n]*)["']` must
  occur exactly once — in `tests/Mcp.Tests/Leftovers/OwnedToken.cs` — and its value must be `token`; a second
  declaration, or another value, is a `token` finding.

**The migration.** Each of the 11 classes becomes `public sealed class X : IDisposable` with
`private readonly OwnedScope _owned = new();` and `public void Dispose() => _owned.Dispose();` — xUnit v3 creates one
instance per test (per theory row) and disposes it after the test, **whether the test passed or threw**; that is this
language's `t.after`. Every factory in the facts table becomes an instance method (it reads `_owned`); helpers that
make nothing (`Sink`, `Relative`, `At`, `Event`, `Days`, `Usage`, `Invoke`, `Numbered`) stay `static`. `what` is the
old prefix without its `mcp-`:

| File | Becomes |
|---|---|
| `DailyRunFileSinkTests.cs:92` | `private string TempRoot() => _owned.Dir("daily-log");` |
| `HealthTests.cs:61`, `:67` | `_owned.Dir("health-broken")`, `_owned.Dir("health")` |
| `LogRetentionTests.cs:68`, `:117` | `_owned.Dir("retention-empty")`, `_owned.Dir("retention")` |
| `SpoolFixtureTests.cs:46` | `_owned.Dir("spool-fixture")` |
| `SpoolUsageSinkTests.cs:133`, `:157`, `:175` | `_owned.Dir("spool-broken")`; `Directory = _owned.Dir("spool-fault") + "\0x"`; `_owned.Dir("spool")` |
| `SurfaceFingerprintTests.cs:133` | `_owned.Dir("fingerprint")` |
| `SurfaceParityTests.cs:119`, `:149` | `_owned.Dir("parity-descriptions")`, `_owned.Dir("parity")` |
| `TelemetryCorrelationTests.cs:119` | `_owned.Dir("correlation")` |
| `ToolDescriptionCatalogTests.cs:102`, `:113` | `Path.Combine(_owned.Dir("descriptions"), "nope")`, `_owned.Dir("descriptions")` |
| `ToolSurfaceTests.cs:138` | `_owned.Dir("surface")` |
| `WorkspaceToolTests.cs:285`, `:301` | `_owned.Dir("outside")` |
| `WorkspaceToolTests.cs:402` | `var home = _owned.Dir("workspace"); var root = Path.Combine(home, "root");` then `Directory.CreateDirectory(Path.Combine(root, "sub"))` as today — the workspace root is a child of an OWNED folder (rule §1: it dies with its container), so `:259-260`'s `outside.txt` lands in `home`, is removed with it, and each theory row has its own |
| `WorkspaceToolTests.cs:243`, `:276` | `Owned.TempRoot()` in place of `Path.GetTempPath()` — the meaning is unchanged (any absolute directory) |
| `Owned.cs` (`TempRoot`) | the line above `Path.GetTempPath()`: `// testsToDelete-exempt: raw-temp — the helper is the one place a test learns the temp root` — **harness setup, not migration**: it lands before scenario 1's RED run, so that RED counts only the 11 files below |

Assertions do not change, with one exception that is the point: `WorkspaceToolTests.The_provider_reports_which_workspace_it_is_scoped_to`
(`:387-393`) still compares with `root`, which is now `home/root`.

**The end-of-run audit.** Two checks, one verdict:

- `LeftoversAudit.ClaimedBy(string tmp, Owner owner) → IReadOnlyList<string>`: every depth-1 folder of `tmp` whose name
  matches `Owned.Pattern()` and whose sibling claim (through `Scope.ReadClaimText`) parses to exactly `owner` (token,
  host, pid, start, ns), plus every such claim whose folder is missing.
- `LeftoversAudit.Unreclaimed() → IReadOnlyList<(string Path, bool DisposeAttempted)>`: `OwnedDir.Unreclaimed()` —
  every folder created and never disposed, AND every folder whose disposal was tried and failed, **wherever its
  `parent` was**.

`LeftoversSweepFixture` implements `IAsyncDisposable`; `DisposeAsync` — after the last test of the assembly — first
takes the `Unreclaimed()` snapshot, disposes each of them once more (a handle released since lets this one succeed),
then computes `ClaimedBy(Owned.TempRoot(), Owner.RunRecord())` and removes each entry with `Removal.Quietly`, and,
when any list was not empty, throws ONE `InvalidOperationException` whose message names each path under its own
words — `mcp tests: this run left <n> folder(s) it claimed — never disposed: <paths>; disposed but not removed:
<paths>; claimed in the temp root: <paths>` (an empty group is omitted). A green run's teardown leaves nothing it
claimed; this is the whole-suite form of the epic's goal sentence, and it catches the two leaks the guard cannot see:
a helper call nobody disposes, and a disposal that gave up. In an elevated Windows run the claim scan is blind (S2,
*Elevated runs*); the registry check is not.

**The janitor cross-check, automated** (`LeftoversJanitorTests.cs`; the contract has TWO implementations, so the
family's testing rule wants a LIVE check, not a by-hand one). On Linux only (on Windows `bash` on the path may be
WSL's, which reads every Windows claim as foreign, `J:18-21` — `Assert.Skip` there with that sentence; the Windows
half stays by hand, sc. 9): in the REAL temp root (`Owned.TempRoot()` — the janitor sweeps nothing else), plant one
folder `Owned.NameBase + "janitor-" + <id>` with a sibling claim
`new Owner(OwnerToken.This, Owner.This.Host, UnmatchablePid.Next(), Owner.This.Start, Owner.This.Ns).Format() + "\n"`
(premise asserted; this reader's namespace, else the janitor says `unknown`, `janitor.sh:649`) and a second one named
under `Other` with the same claim text but `Other`'s token; age all four entries to 11 minutes
(`SetLastWriteTimeUtc`); run `bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` with
`JANITOR_MIN_AGE_MINUTES=10`, cwd `RepositoryRoot.Path`, stdout and stderr captured, inside `try/finally` that removes
both plants. Assert: exit 0; stdout holds a `would run:` line naming the first folder; no `would run:` line names the
second; stderr holds a `foreign:` line naming the second; no `skipped:` line names either. A missing `bash` or
`janitor.sh` FAILS the test. If the first is not listed, the two implementations' records disagree: stop and report to
the conventions owner, never adjust the C# format to pass.

**The audit's teeth, automated** (`LeftoversSelfTest.cs` + one CI step, two cases). The class runs ONLY when
`MCP_LEFTOVERS_SELFTEST` is set (`Assert.Skip` otherwise, with the sentence that says so); the normal run never sets
it. `Leaves_one_owned_dir_undisposed` (value `1`): `OwnedDir.Create("selftest")` and return without disposing.
`Disposes_but_the_removal_fails` (value `failed-dispose`): `OwnedDir.Create("selftest-failed")`, make it unremovable —
Linux: a subfolder holding a file, then `File.SetUnixFileMode(subfolder, UserRead | UserExecute)` (0o555: the file
inside cannot be unlinked; `Assert.Skip` as root); Windows: a held `FileShare.None` handle on a file inside — call
`Dispose()` (one warning, the entry stays `DisposeAttempted`), then make it removable again (restore the mode;
release the handle) and return. The audit, at the end of the run, names it under *disposed but not removed*, removes
it now, and throws; the temp root is clean afterwards. The step, AFTER the normal `Test` step:

```yaml
      - name: The audit fails a run that leaves a claimed folder
        run: |
          set -o pipefail
          for case in "1:never disposed:selftest" "failed-dispose:disposed but not removed:selftest-failed"; do
            IFS=: read -r value words what <<< "$case"
            if MCP_LEFTOVERS_SELFTEST="$value" ./tests/Mcp.Tests/bin/Release/net10.0/Mcp.Tests --filter-class "*LeftoversSelfTest" 2>&1 | tee selftest.log; then
              echo "the end-of-run audit did not fail the run ($value)"; exit 1; fi
            grep -q "mcp tests: this run left" selftest.log || { echo "the run failed, but not with the audit's message ($value)"; exit 1; }
            grep -E "$words: .*testsToDelete-[uw][0-9]{1,10}-mcp-$what-[A-Za-z0-9]{6}" selftest.log >/dev/null \
              || { echo "the audit's message does not name the folder under '$words' ($value)"; exit 1; }
            left=$(find "${TMPDIR:-/tmp}" -maxdepth 1 -iname "teststodelete-*-mcp-$what-*") \
              || { echo "the temp root could not be listed"; exit 1; }
            [ -z "$left" ] || { echo "the audit left the folder behind: $left"; exit 1; }
          done
```

— green only on a NON-zero exit per case that carries the audit's own message with the case's own words (`never
disposed:` / `disposed but not removed:`) before the folder's name, and an empty listing of the temp root
`Owned.TempRoot` resolves to on the runner (`Path.GetTempPath()` = `$TMPDIR` or `/tmp`); a listing error is red,
never ignored (round 3).
If xUnit v3 does not fail the run on an assembly-fixture cleanup exception, this step is red on its first run: stop and
report — do not turn the audit into a warning silently.

**CI.** The `build-test` submodule step and the `build-test-windows` job are S2's; this story adds the self-test step
above. All jobs green is the DoD.

**Docs.** `research/architecture.md` § CI (`:238-251`): one paragraph — the helper (`tests/Mcp.Tests/Leftovers`, names
`testsToDelete-<owner>-mcp-<what>-<id>`, a sibling `.owner` claimed first), the start-of-run sweep (assembly fixture,
two keys, owner liveness, identity, 10 minutes), the end-of-run audit, the guard and its eight classes, the CI
submodule step, and the host-wide janitor `bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` (Git for
Windows' bash on Windows, never WSL's; forced dry-run there; `would run:` on stdout, exit 0/3/1/2 — `J:14-68`); the
test count at `:40` updated; the module map names `research/module_tests.md`.

**`research/module_tests.md` — new, created in this story.** The shared `common/knowledge-base.md` requires it by
name in every repository, and none exists here yet. The facts it carries, each true at merge time:

- **Where the harness is** — `tests/Mcp.Tests` (xUnit v3 on Microsoft Testing Platform), and inside it
  `tests/Mcp.Tests/Leftovers/` (helper, token, record, liveness, scope, native calls, sweep, audit, guard).
- **How it is run** — the exact commands, identical to CI's `build-test` job: `dotnet build dew_flow_mcp.slnx -c
  Release` and the test executable (`./tests/Mcp.Tests/bin/Release/net10.0/Mcp.Tests`), never `dotnet test`; locally
  through `node .claude/rules/shared/tools/low-priority.mjs`; `--filter-namespace "Mcp.Tests.Leftovers"`; the janitor.
- **The leftovers flows, each `covered` or `not covered`** with the test that covers it: the runner's facts (S2 sc.
  0), the helper (S2 sc. 1–9), the token and record (S2 sc. 3, 10, 11), liveness and scope (S2 sc. 12–13), the
  start-of-run sweep (S3 sc. 1–8), the end-of-run audit (S4 sc. 7, 7a, and the CI self-test step), the guard (S4 sc.
  1, 3–6), the janitor reading this helper's claim (S4 *cross-check*, Linux CI; Windows by hand, sc. 9), an elevated
  run on the owner's machine (S2 sc. 14, by hand).
- **What it does not prove** — a SIGKILL of the real executable (the kill is built by hand, S3 sc. 1); macOS (no CI
  leg; the helper refuses there); the §5 container carve-out (not recognised, the recorded deviation); the shipped
  tracer's own limits over scripts (the header of `tools/lib/prefix-bindings.mjs`); the guard reads text, so a temp
  path built by reflection or in a generated file is invisible; the audit runs only when the executable reaches
  `DisposeAsync`; the janitor on Windows (forced dry-run, by hand only).
- **When it runs** — every pull request (`build-test`, `build-test-windows`), and the janitor by hand on a machine that
  stopped running these tests.

**Hand clean-up**, once per machine, recorded in the PR and not code: list the temp root's folders named exactly
`mcp-<one of the 17 old prefixes>` + 8 characters + `.` + 3 (Windows) or + 6 characters (Linux), and `outside.txt`
containing `secret`; look at the list; delete only those. **The record — per machine, each path removed or kept with
a reason — is merge condition (d) of *Order*.**

**Scenarios** (RED first: scenario 1 is written and run BEFORE any migration — and AFTER the harness setup: the
`Owned.cs` exemption comment is in place first, so `Owned.TempRoot()`'s own `Path.GetTempPath()` is not counted).

1. **This repository passes the guard.** `Scan(RepositoryRoot.Path)` ⇒ `FilesScanned > 0` and no findings; the
   failure message lists every finding as `file:line — class — text`. **RED before the migration, with the `Owned.cs`
   exemption already in place: exactly 21 `raw-temp` findings in 11 files** (the 19 creation sites and
   `WorkspaceToolTests.cs:243`, `:276`) — the list goes in the PR. A 22nd finding naming `Owned.cs` means the setup
   step was skipped; it is not the RED. A finding of class `owner-blind` or `legacy-name` in a test of S2 or S3 means a
   name was written as a literal: build it from pieces.
2. **The escape target stays inside an owned folder.** New test in `WorkspaceToolTests`:
   `The_escape_target_is_written_inside_an_owned_folder_never_into_the_temp_root` — `var (_, root) = Build();` then the
   folder name of `Directory.GetParent(root)` matches `Owned.Pattern()`. **RED before the migration** with the real
   symptom (the parent is the temp root itself), GREEN after.
3. **Break-it per class**, each on a tree written at run time into a private `OwnedDir` (files written there, never
   committed; `<prefix>` below is the word assembled from pieces at run time — this file is excluded from the scan,
   but a literal here would travel into every copy of it): `Directory.CreateTempSubdirectory("x")` in `tests/X/A.cs` ⇒ 1 finding naming line
   and class; `Path.GetTempPath` without `(` ⇒ 1; `Path.GetTempFileName()` ⇒ 1; `Environment.GetEnvironmentVariable("TMPDIR")`
   ⇒ 1; `RUNNER_TEMP` ⇒ 0; `mktemp -d` in `a.sh` ⇒ 1; `$env:TEMP` in `b.ps1` ⇒ 1; `"worktree", "add"` ⇒ 1
   (`raw-worktree`); `git worktree add x` in `a.sh` ⇒ 1; `docker run -d img` in a workflow ⇒ 1 (`docker-run`);
   `docker run --rm --name <owner form> img` ⇒ 0; `docker run --rm --name cache img` ⇒ 1 (`docker-name`);
   `--name <legacy form>` ⇒ 1 `docker-name` (and 1 `legacy-name`); `docker volume create cache` ⇒ 1; `WithName("pg")`
   ⇒ 1; `WithName("<owner form>")` ⇒ 0; `docker compose -p cache up` ⇒ 1; `COMPOSE_PROJECT_NAME: cache` ⇒ 1;
   `--name "${name}"` ⇒ 0; `TRUNCATE TABLE users` ⇒ 1 (`shared-store`); the same under `-- testsToDelete-exempt:
   shared-store — pg:5432 users; reuse intentional; lock: pg_advisory_lock(42)` ⇒ 0, and minus `lock:` ⇒ 1
   (`exemption`); `// truncated` ⇒ 0; `TESTCONTAINERS_RYUK_DISABLED: "true"` ⇒ 1 even under a well-formed exemption;
   a `Path.GetTempPath()` under `// testsToDelete-exempt: raw-temp — the one place a test learns the temp root` ⇒ 0;
   `// testsToDelete-exempt: raw-temp — needed` ⇒ 1 (`exemption`); `// testsToDelete-exempt: nonsense — a perfectly
   fine reason here` ⇒ 1; a commented-out `// Directory.CreateTempSubdirectory("x")` ⇒ 0; a file under `tests/X/bin/`
   ⇒ not scanned. **`legacy-name`:** `"<prefix>-mcp-x-abc123"` in a `.cs` ⇒ 1 `legacy-name` AND 1 `owner-blind`;
   the same under `// testsToDelete-exempt: legacy-name — a fixture of the form before the owner` ⇒ the `legacy-name`
   finding gone, the `owner-blind` one stays; `$"<prefix>-{RepoToken}-"` ⇒ 1 `legacy-name`; `"<prefix>-exempt:"`,
   `".x.<prefix lower>-quarantine-1-ab"`, `"TESTSTODELETE_OWNER"` ⇒ 0. **`owner-blind`:** in a `.cs` outside the
   helper file `"<prefix>-"` ⇒ 1, `"<prefix lower>_"` ⇒ 1, `"<prefix>-u1000-"` ⇒ 0, `"<prefix>-" + x` ⇒ 1, a
   `const string P = "<prefix>-"` ⇒ 1 (the shape of a constant carrying the prefix), the same literal in a file named
   `tests/Mcp.Tests/Leftovers/Owned.cs` of the tree ⇒ 0; `var p = Owned.Prefix + what;` in `tests/X/A.cs` ⇒ 1 and in
   `tests/Mcp.Tests/Leftovers/X.cs` ⇒ 0; `Owned.LegacyPattern.IsMatch(n)` in `src/Y.cs` ⇒ 1; under
   `// testsToDelete-exempt: owner-blind — any reason at all here` ⇒ still 1 plus 1 `exemption`; **scripts, judged by
   the shipped classifier through `node`:** in `a.sh` `rm -rf "$TMPDIR"/<prefix>-*` ⇒ 1, `find … -name '<prefix lower>[-_]*'`
   ⇒ 1, `<prefix>-u*` ⇒ 1, `<prefix>-"$OWNER"-*` ⇒ 0, `<prefix>-${OWNER}-*` ⇒ 0, `<prefix>-conventions-*` ⇒ 0
   (a literal first segment), `P='<prefix>-'` on line 1 and `rm -rf "$P"*` on line 3 ⇒ 1 reported at line 3,
   `const P = "<prefix>-"` and `glob(P + "*")` in `a.mjs` ⇒ 1 at the use, `$P = "<prefix>-"` and `Remove-Item "$P*"`
   in `b.ps1` ⇒ 1, `P = "<prefix>-"` and `glob.glob(P + "*")` in `c.py` ⇒ 1, a workflow `run: rm -rf /tmp/<prefix>-*`
   ⇒ 1; `echo "<prefix>-u1000-mcp-x-abc123"` ⇒ 0 (a name, not a pattern) — each expected count is what
   `node .claude/rules/shared/tools/sweeper-scan.mjs --root <that tree>` prints for the same file, recorded once; and
   with `PATH` emptied of `node` (a seam naming the executable) ⇒ 1 `owner-blind` finding naming `node`, never 0.
   **Block comments:** `/* Path.GetTempPath() */` ⇒ 0; opened on line 1, closed on line 3 with `Path.GetTempPath()`
   on line 2 ⇒ 0; `*/ var t = Path.GetTempPath();` ⇒ 1 at ITS line; `var glob = "src/**/*.cs"; var t = Path.GetTempPath();`
   ⇒ 1; `/* testsToDelete-exempt: raw-temp — the one place a test learns the temp root */` above ⇒ 0. **The guard
   over itself:** `Scan` of a tree holding only a copy of `LeftoversGuard.cs` at its path ⇒ 0 findings and
   `FilesScanned == 1`. RED first: every break-it is written and run against a STUB `Scan` that returns
   `FilesScanned = 1` and no findings — the guard is new, so every "⇒ 1" is observed at 0 before its class exists.
4. **A scan of nothing is red, not clean.** An empty tree ⇒ `FilesScanned == 0`; a root that does not exist ⇒
   `FilesScanned == 0` and no throw.
5. **The token is registered and is ours.** On the real mount, `TokenFindings(rule, OwnedToken.RepoToken, OwnedToken.RepoRow)`
   ⇒ none, and the one-constant check over the real repository ⇒ none (the one declaration is in `OwnedToken.cs`). Break-its on rule texts written in the test:
   `nosuchtoken`, `Mcp`, `x`, `u1000`, `w1` ⇒ one finding each; no `## Tokens` ⇒ one; a two-column table ⇒ one; `mcp`
   listed twice ⇒ one; `mcp` only in the `repository` column ⇒ one; the `mcp` row's repository `the embedding sidecar`
   ⇒ one naming both; identity `""` ⇒ one; the rule file missing ⇒ one. The one-constant check on a tree with
   `RepoToken = "mcp"` in two files ⇒ one; with `RepoToken = "mcpx"` ⇒ one.
6. **The exclusion list is exactly one file** — pinned.
7. **The audit names only what THIS run claimed.** In a private `tmp`: a folder claimed by `Owner.RunRecord()` ⇒
   listed; one claimed by a dead pid, one by `Other`, one by `other@<tag>`, one unclaimed, one of another token ⇒ not
   listed; an orphan claim of this run ⇒ listed.
7a. **The audit sees an undisposed folder under ANY parent, and a failed disposal.** RED first: an
   `OwnedDir.Create("custom", parent: <a private OwnedDir NOT at the temp root's depth 1>)` left undisposed ⇒
   `Unreclaimed()` names its path with `DisposeAttempted == false` while `ClaimedBy(Owned.TempRoot(), …)` does not;
   after `Dispose()` it is gone from both. A second one made unremovable (S2 sc. 6's method) and disposed ⇒ named with
   `DisposeAttempted == true`; made removable and disposed again ⇒ gone. The audit's message for a hand-built pair
   carries both groups under their words. The test disposes everything it made before it ends.
8. **The audit has teeth** — the CI self-test step above, RED first: the step is added BEFORE `LeftoversSelfTest`
   exists (the filtered run then finds no test and exits zero: the step is red), then the self-test lands and the
   step is green on a non-zero exit per case with the audit's message naming one `testsToDelete-<owner>-mcp-selftest-…`
   folder under `never disposed:` and one `…-mcp-selftest-failed-…` under `disposed but not removed:` in the job log,
   and the temp root holds none afterwards. The second case is RED against the old registry (the entry dropped on the
   first `Dispose()`): the run exits zero. Locally: `MCP_LEFTOVERS_SELFTEST=1` and `=failed-dispose` with
   `--filter-class "*LeftoversSelfTest"` exit non-zero; without the variable the class skips and the normal run is green.
9. **No leftover after a full run, and the janitor reads THIS helper's claim on Windows** (by hand, recorded in the
   PR; the Linux half is `LeftoversJanitorTests`, automated above). Before the migration, one full run leaves N new
   `mcp-…` folders and `outside.txt` in the temp root (count them); after it, zero new entries of either, and `bash
   .claude/rules/shared/tools/janitor.sh --token mcp --dry-run`, run from this repository, prints no `would run:` line
   and exits 0 (Windows: its forced-dry-run notice first, `J:22-24`; Linux too, on the dev VM). Then, on Windows under
   Git for Windows' bash, the plant of the automated check by hand: one folder `Owned.NameBase + "janitor-" + <id>`
   with the C# helper's claim for an unmatchable pid (premise asserted), one under `Other`, both aged 11 minutes;
   `JANITOR_MIN_AGE_MINUTES=10 bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` ⇒ one `would run:`
   line naming the first, a `foreign:` line for the second, no `skipped:` for either, exit 0 — or, where scenario 0
   measured `Administrators` as the owner, the `foreign:` / `-unsafe-root` lines the janitor prints for that case
   (`research/security/test-leftovers-merged-core.md:239-243`), recorded as the Windows answer. Then remove what was
   planted. All lines go in the PR. A disagreement of the two records: stop and report to the conventions owner, never
   adjust the C# format to pass.

- **Commands.** As S2, `--filter-class "*LeftoversGuardTests"` (RED, then GREEN), `--filter-class "*WorkspaceToolTests"`,
  the whole executable on Windows and Linux; `dotnet format dew_flow_mcp.slnx --verify-no-changes --no-restore` in
  Linux or WSL; the `plans` steps before the push: `node .claude/rules/shared/tools/pin-check.mjs`,
  `plan-lifecycle.mjs`, `gate-snippet-check.mjs`, `build-flags-check.mjs`, `node .github/scripts/branch-protection.mjs --selftest`.
- **DoD.** The `Owned.cs` exemption in place before scenario 1's RED; scenarios 1, 2 and 7a observed RED with the
  counts above, then GREEN; every break-it observed at 0 against the stub, then red (the block-comment, `const P`,
  `Owned.Prefix`, script and `node`-missing cases included); the guard over its own file 0; the self-test step red
  before `LeftoversSelfTest`, then green on a non-zero exit; `LeftoversJanitorTests` green on the Linux job; scenario
  9 recorded with the Windows listing; CI green on all five jobs; `research/module_tests.md` created with every flow
  marked; `architecture.md` updated; the merge condition of *Order* met and quoted in the PR. **Promotion rides in the
  producer PR's last commit** (this repository has no other epic, and the shared planning rule's "parent and stories
  promote together" is the parent's to do in its own repository): the status line becomes
  `> Status: **IMPLEMENTED, <YYYY-MM-DD>.**` with the deviations recorded — the carve-out not recognised, legacy names
  listed only, `.cs` `owner-blind` by literal, macOS not built, and whatever the measured Windows owner forced — the
  links fixed for `research/`, `git mv todo/PLAN_test_leftovers_E3.md research/PLAN_test_leftovers_E3.md` LAST, its
  `todo/README.md` row moved to `research/README.md`, and `plan-lifecycle.mjs` clean on the squash.

## Growth

Each run creates about 70 folders of a few KB under the temp root, plus one sibling claim each; every one is removed
by its test's `Dispose`, and the end-of-run audit makes a run that leaves one red. A killed run leaves at most those
~70 (< 1 MB); the same user's next run collects them once 10 minutes old and their owner is `gone`, and the shared
janitor collects them on any machine under the same two keys. A killed SWEEP leaves at most one quarantine, restored
or listed by the next run. Nothing here grows without an owner.

## What the other epics take from here

The parent copies this helper's SHAPE into the C# tests of E2, E6 and E7 (E5 widens its own existing helper).
**E2.S3, E6 and E7.S3 wait for E3's MERGED producer stage** — the squash-merged pull request, not a story on a branch.
The boundary, so the copies need not rediscover it:

| Item | Built here | What a copy does |
|---|---|---|
| The API shape (`Owned`, `OwnerToken`, `Owner`, `Liveness`, `Scope`, `Native`, `OwnedDir`, `OwnedScope`, `Removal`) | yes | every file but `OwnedToken.cs` copied byte-identical save the namespace line; `OwnedToken.cs` rewritten with its own `RepoToken` and `RepoRow` from its own row of the `## Tokens` table — never a copy of this one's (`O:42`'s shape) |
| The container label — `--label testsToDelete.owner=<record>` on `run` / `volume create` / `network create`, the same string a Testcontainers builder adds (`.WithLabel("testsToDelete.owner", Owner.RunRecord().Format())`), beside an owner-form `WithName`; a daemon object has no kernel owner, so the label's owner segment is the scope key (`R:130`, `R:137-138`) | **not built here** — nothing starts a container | E2's smoke, E6's Testcontainers fixture and E7 add it to their sweep (a `docker ps --filter label=…` inventory, judged by `Owner.Parse` + `Liveness.State`, removal by captured id) and to their guard's `docker-name` class |
| The owner token: real uid, or the SHA-256 Windows form with ASCII-only lower-casing; `TESTSTODELETE_OWNER` validated | yes, vectors pinned | same bytes; the vectors file is the proof |
| Claim first (`FileMode.CreateNew`), then an EXCLUSIVE `mkdir` through P/Invoke; an own id, never `CreateTempSubdirectory` | yes, measured above | same — the Windows id and the missing exclusive create are the reasons |
| The record with the Linux start from `/proc` + `AT_CLKTCK` and the pid namespace; no start elsewhere | yes, measured against `Process.StartTime` (S2 sc. 10) | same formula — two helpers on one machine must agree within 2 s |
| `Dns.GetHostName()` with the WSL `~distro` suffix, never `Environment.MachineName` | yes | same — the janitor reads `hostname` and `WSL_DISTRO_NAME` |
| The Windows kernel owner by BOTH keys (profile AND owner SID), fail closed; elevated runs kept | yes (decision 2) | same — the reference checks the profile alone; the family's C# is stricter |
| Identity by `statx` (Linux) and `FileIdInfo` (Windows); removal through a quarantine rename, restore when free | yes | same `Native.cs`; a copy that makes repositories clears the read-only attribute before `Directory.Delete` |
| The probe: `kill(pid, 0)` / `OpenProcess`, never `Process.GetProcessById` | yes | same |
| Per-test disposal through `IDisposable` + `OwnedScope`; assembly-fixture sweep and end-of-run audit with the registry | yes | same, plus the kinds that repository creates (containers, databases) |
| `FullName` accepting rule §1's 6–12-character id; legacy names listed, never collected | yes | same — write 6, accept 6–12 |
| Guard: eight classes; `owner-blind` in `.cs` by literal plus the `Owned.Prefix` reference rule (decision 3), the shipped classifier through `node` for scripts; the carve-out NOT recognised; the guard scans itself clean | all | a repository that starts containers implements the §5 carve-out in full; the `.cs` literal rule and the `node` call are the family's |
| The live janitor cross-check and the audit self-test step | yes (Linux) | same two tests; a copy with containers adds a labelled plant |
| macOS | not built (no leg, no machine) | a copy with a Mac developer measures Darwin's `lstat` first |

## Plan-gate round 1 (2026-10-17)

The coai plan gate's first round over this document; judged by Opus. Accepted (applied above), each marked where the
released contract overturned it:

1. S4 sc. 9 plants a folder with a claim in the C# helper's own format, aged 11 minutes, and records
   `janitor.sh --token mcp --dry-run` listing it, on Windows (Git Bash) and Linux — **holds**; the format is now the
   merged record with the owner token and, on Linux, the namespace, and a second plant under another owner must be
   listed `foreign:`.
2. S4 creates `research/module_tests.md` — holds.
3. The `Owned.cs` exemption comment is harness setup, added before sc. 1's RED run, so the RED is exactly 21
   findings in 11 files — holds (`Owned.cs` is also the one file the `.cs` `owner-blind` rule does not read).
4. S1 re-checks S2's constants against the released rule before S2 merges — **superseded:** S1 is done by PR #57 and
   S2 is written against the released text; the constants table is S2's own DoD, in the producer PR.
5. The end-of-run audit also checks a process-wide registry of created-but-undisposed instances — holds.
6. The guard strips `/* … */` block comments before matching, string literals respected — holds.
7. `Owned.FullName` accepts rule §1's id of 6–12, though the helper writes 6 — holds, with the owner segment added.

Rejected:

- An xUnit self-test that a throwing assembly-fixture dispose turns the exit code red — already an explicit RED
  observation in S4 sc. 8 (stop and report if it does not).
- Separate CI step wiring for the guard — the guard is a test in the executable; a finding already fails `build-test`.
- Treating a zombie process as `gone` — transient, and keeping it is the safe side; liveness belongs to the contract,
  not to this helper.
- A rollback note — a `git revert` of the producer merge is the normal flow, and is within the barrier's rollback rule
  here (`R:291-293`): this repository has no owner-blind sweeper to restore, so a revert disables a producer and
  nothing else.

Also reflected from the contract, settled: `janitor.sh --token <t>` exists in the release (`J:26`; S4 docs and sc. 9).
Of the rejections, the first is now overturned by round 2 (A4): the teeth are a CI step, not a by-hand revert.

## Plan-gate round 2 (2026-10-31)

The coai plan gate over the re-split, plus the Opus judge; all accepted and applied above, none rejected. The owner's
standing answer: macOS stays "not built; the helper refuses with a clear message".

- A0 a claim write that fails after the exclusive create removes the claim only under its own identity or content
  (S2 step 2, sc. 9e). A1 a taken quarantine is retried by every run, listed with its command, never removed (S3 step 5,
  sc. 5c). A2 the hand clean-up record is merge condition (d). A3 the janitor cross-check is a Linux CI test
  (`LeftoversJanitorTests`). A4 the audit's teeth are a CI step under `MCP_LEFTOVERS_SELFTEST=1` (sc. 8). A5 promotion
  to `research/` with `IMPLEMENTED` and deviations in the producer PR (S4 DoD). A6 `src/**/*.cs` scanned for the
  literal classes and the token constant.
- B1 the guard spells its detector words and the marker in pieces and scans itself clean (sc. 3). B2 `\z` and `[0-9]`
  everywhere, the line end stripped by `EndsWith`, `u1\n` refused (sc. 3). B3 `RepositoryRoot.cs` and the submodule
  step moved into S2; a missing vectors file fails. B4 S3 sc. 2: this pid with a start 3 s off ⇒ `unknown`, a dead pid
  with any start ⇒ removed. B5 sc. 5c passes the window through `Now`. B6 the Windows probe with `SYNCHRONIZE` and
  `WaitForSingleObject` — it matches libuv's `uv__kill` as stated. B7 pid bounds per OS, the tag-bit aliasing measured
  first (sc. 13). B8 `CreateFileW` values in `Native`. B9 scripts judged by the SHIPPED classifier through `node`
  (`tools/lib/sweeper-scan.mjs` `classifyLines`), `.py` added — the one-level re-implementation is gone. B10 a
  `windows-latest` job; scenario 0 measures the runner's file owner before S2 code and every Windows assertion is the
  measured answer; `Dispose` never consults `IsMine`; `ReadClaimText` has its `mine` seam; `OwnedToken.cs` holds the
  token and row; the `grammar`, `scope` and `verdicts` vectors run through `Parse` and `State` (sc. 10, 12); the
  container label is in the boundary table as "not built here". B11 the owner SID from `GetSecurityInfo` on the
  `OPEN_REPARSE_POINT` handle, null ⇒ false, namespaces named; candidates are real directories and regular files; a
  symlink case in S3 sc. 2. B12 `0x1C0`. B13 the `stx_mask` check, `static partial`, marshalling, musl unmeasured. B14
  the collision by `HResult` / identity. B15 `o.Mine` reaches the claim read; orphan claims are regular files. B16 sc.
  5a moves aside before it deletes. B17 `Owned.Prefix` / `Owned.LegacyPattern` outside `Leftovers/` is a finding. B18
  sc. 3 RED against a stub `Scan`. The fail-closed edges are one bullet of S2.


Round 3 (2026-10-31; GPT alone — Gemini did not receive the 1167-line plan whole): `proceed` with three findings. Accepted: the self-test step is green only on the audit's own message and an empty, listable temp root (S4); the claim is deleted after the quarantine only under the identity and text captured before the rename (S3 step 3, sc. 5e). Rejected: "the audit can delete another live test's folder" — the audit runs in the assembly fixture's dispose, after every test of the assembly has finished, and takes only claims whose record is this process's own pid and start.

**Cadence consultation (2026-10-31; GPT), three findings, all taken.** C1: the Windows leg proved preservation only —
on an elevated runner every own folder is `foreign-uid`; now two test fixtures (`OwnedByCaller`, by `SetOwner` to the
caller's own SID, no privilege needed; `ForeignToCaller`, the `Administrators` SID where the token may own it, else
an out-of-profile path) make both outcomes on any machine, S2 sc. 13 asserts them through the real `IsMine` and S3
sc. 9 asserts the sweep REMOVES the first and keeps the second; after S4 the Windows job runs the full executable,
Linux-only premises as named skips. C2: a failed disposal was invisible to the audit (the registry entry went before
the removal was tried); now the entry stays until folder and claim are gone, `Unreclaimed()` returns both kinds, the
audit names them under distinct words, and the CI self-test has a second case, `failed-dispose`. C3: S2 lands as
three commits, each green on both legs, S3 waiting on the second (*Order*, S2 DoD); the PR still merges as one squash.

## Judge's notes on the parent's E3 entry

Opus, 2026-10-16, against `origin/main` b352881 of this repository and the E1 branch of the shared rules; the
*Resolution* column re-read against `115bbf8` on 2026-10-31.

| # | The entry says | What is so | Resolution here |
|---|---|---|---|
| P1 | 19 sites | 19 creation sites, confirmed — plus 2 `Path.GetTempPath()` sites the rule's `raw-temp` class names | the guard's RED is 21 (S4 sc. 1) |
| P2 | (nothing) | `WorkspaceToolTests.cs:259-260` leaks a fixed-name `outside.txt` into the shared temp root, written concurrently by two theory rows; found on a machine | S4 migration row and sc. 2 |
| P3 | helper, guard, proof | no start-of-run sweep and no owner marker, which rule §3–§4 require of every repository that creates a resource kind | S2 (token, claim, liveness, scope), S3 (sweep) |
| P4 | `LogRetentionTests.cs:79` ⇒ dispose must tolerate an open handle | the note says POSIX does NOT refuse; Windows does | S2 sc. 6 provokes the refusal per OS |
| P5 | the guard must see the creation under `"\0x"` | the composed path is never created; a line-based guard sees the call regardless | no special case; noted in the facts |
| P6 | (nothing) | `CreateTempSubdirectory` on Windows makes an id with a `.` (measured); .NET has no exclusive `mkdir`, no inode, no uid, no `kill(pid, 0)` | S2 claims first with `FileMode.CreateNew` and takes the rest through `Native.cs` — now the rule's own order (`R:65-75`), not a deviation; recorded for E2/E6/E7 |
| P7 | (nothing) | CI's `build-test` does not fetch the submodule the token check reads | S4 CI step |
| P8 | depends on E1 | the release turns `Pin check` red here until the pin moves | **superseded by owner decision 1:** the pin bump is PR #57, merged alone; the producer stage waits for R6's records and the barrier checklist instead |
| P9 | RED: a test that creates through the old path and asserts it gone after a failure | in C# that is either reflection over eleven private factories or a compile error, not a red assertion | RED = the guard (21), the `outside.txt` test, the audit's teeth by revert |
| P10 | copied by shape into E2, E5, E6, E7 | E5's own entry widens coai's existing `TempDir.For` instead | the boundary table names E2, E6, E7, and waits for the MERGED producer stage |
| P11 | `testsToDelete-mcp-…` | the released form carries the owner segment; the parent's E3 entry still shows the E1 form | every name here is `testsToDelete-<owner>-mcp-…`; the parent's entry is the remainder plan's to correct (R6) |

**Sign-off: yes**, on S2–S4 as written; S1 is done. Three judgements are mine and are recorded as deviations for the
promotion: the guard does not recognise the §5 container carve-out (stricter than the rule; nothing here starts a
container); the `.cs` `owner-blind` class is a literal rule, with the shipped classifier judging scripts (decision 3);
legacy names are listed and never collected (the rule allows preserving more; this repository never produced one).
macOS is not built, and the facts say what was checked: no CI leg, no machine, and Darwin's `lstat` symbol differs by
architecture — nobody measured it.
