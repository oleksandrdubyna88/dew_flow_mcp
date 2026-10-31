# PLAN — Test leftovers, E3: one C# helper, nineteen sites, a sweep and a guard for `tests/Mcp.Tests`

> Status: **plan only, nothing implemented yet, 2026-10-31 — re-split against release 115bbf8; plan only.** Stories of
> epic E3 of the parent plan `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E3). Scope: a new
> `tests/Mcp.Tests/Leftovers/` folder (helper, owner token and record, liveness, the kernel-owner and identity reads,
> start-of-run sweep, end-of-run audit, guard), the 11 test files that create temp folders today, one CI step, a new
> `research/module_tests.md`, and `research/architecture.md` § CI. The pin bump (the old S1) is PR #57, which merges
> alone (owner decision 1), and is not a story here.
>
> Split by Opus, 2026-10-16, against the E1 branch; re-split 2026-10-31 against the released contract
> (`dew_flow_conventions` `115bbf8`) and the owner's three decisions of 2026-10-31. Models per story below. No
> cornerstone: E3 is not one (parent, E3 entry).

**Committed together with this document** (one commit, the shared `common/planning-docs.md` § *A plan split into epics
and stories*): its row in [todo/README.md](README.md) under *Currently open*:

```
| [PLAN_test_leftovers_E3.md](PLAN_test_leftovers_E3.md) | plan only, 2026-10-31 — re-split against release 115bbf8; the producer PR waits for R6's sweep-only records and the barrier checklist | Stories of epic E3 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`: every temp folder `tests/Mcp.Tests` makes is named `testsToDelete-<owner>-mcp-…`, claimed first, removed by the test that made it, collected by the same user's next run when a run was killed, and kept so by a guard |
```

**Done when** (from the parent, made concrete): `tests/Mcp.Tests` creates nothing it does not remove, and everything it
creates is named `testsToDelete-<owner>-mcp-<what>-<id>` by one helper, claimed before it exists (S2, S4); a killed
run's folders are collected at the start of the same user's next run, only on the two keys — scope and a `gone` owner
(S3); a guard over `tests/`, scripts and CI is red on every class of the rule's §7, on an unregistered or misassigned
token and on a scan of nothing (S4); the three stories land as ONE producer pull request, merged only under the
condition in *Order*.

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
  succeeds on an existing folder), **no inode or file-id API, no uid read and no `kill(pid, 0)`**: S2 takes the five
  OS calls the contract needs through one P/Invoke file, `Native.cs`, and nothing else in the folder calls the OS by hand.
- **No Docker, no worktrees, no databases** anywhere in `tests/`, `src/` or `.github/` (`is-docker` appears only in
  `http/package-lock.json` as a transitive dependency of the contract suite). The scripts are
  `.github/scripts/branch-protection.mjs`, `http/httpyac.config.js` and the two workflows; none makes a temp path. The
  guard still implements every class (rule DoD: *red on every class of §7*).
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
- **Reusable neighbours.** `LocalCpuBudgetTests.cs:20-27` finds the repository root (the folder holding `*.slnx`
  above `AppContext.BaseDirectory`) — S4 extracts it rather than copying it. `RecordingLogger.cs` is the in-repo
  pattern for a captured sink. `WorkspaceToolTests.cs:346-373` already makes a directory link or a junction, or skips.

## Order

| Story | Model (brief · code · review) | Depends on | Lands |
|---|---|---|---|
| S1 — the pin on release `115bbf8` | — | — | **PR #57**, merged alone (owner decision 1); not built here |
| S2 — the owner token and record, the C# owned-resource helper, liveness, the kernel-owner and identity reads | Opus · Sonnet · Opus | PR #57 merged | first commit of the producer branch |
| S3 — the start-of-run sweep, as an assembly fixture | Opus · Sonnet · Opus | S2 | second |
| S4 — the guard, the migration of 21 sites, the end-of-run audit | Opus · Sonnet · Opus | S2, S3 | last |

**One branch, one pull request, squash-merged.** S2–S4 are commits on `feat/test-leftovers-e3`; each is reviewed as a
unit; none merges alone — the guard's test over this repository is red until the migration lands, and a helper that
emits the owner form without its sweep is half the promise. **The merge condition** (rule *The barrier*; remainder plan
R6): the producer pull request merges only after (a) PR #57 is merged, (b) `dew_flow_conventions ·
todo/PLAN_test_leftovers_merged_core_release.md` R6 records every consumer's sweep-only stage — this family's seven and
the private sibling's — as landed or empty (this repository's: empty, measured above), and (c) the per-machine
*Barrier checklist* of `research/PLAN_test_leftovers_merged_core.md` is recorded for every machine that runs these tests
(the Ubuntu dev VM is recorded open; the owner's Windows machine is not tested at the time of writing). The stories may
be written, gated, built and reviewed before that; the merge waits. If the released text differs from what is cited
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
- **Files.** New, all in namespace `Mcp.Tests.Leftovers`, under `tests/Mcp.Tests/Leftovers/`: `Owned.cs`,
  `OwnerToken.cs`, `Owner.cs`, `Liveness.cs`, `Scope.cs`, `Native.cs`, `OwnedDir.cs`, `UnmatchablePid.cs`; tests
  `OwnedTests.cs`, `OwnerTests.cs`, `LivenessTests.cs`, `ScopeTests.cs`; one property in `tests/Mcp.Tests/Mcp.Tests.csproj`
  (*Analyzers*). Nothing else outside the folder changes.

**The change.** A block comment at the top of `Owned.cs`, in the voice of `O:1-34`: what the helper owns, the order
claim → register → create and why, why teardown never throws, why the owner lives beside the folder and never inside
it, and the two keys with three answers.

```csharp
public static class Owned
{
    public const string Prefix = "testsToDelete-";                 // rule §1 — the ONE bare-prefix literal in .cs outside the guard's fixtures (decision 3)
    public const string RepoToken = "mcp";                         // rule §1 — the ONLY place the token is written (the guard counts it)
    public const string RepoRow = "the MCP server";                // the `repository` column of the `mcp` row (R:310): the guard's identity check
    public const int MaxName = 63, IdLength = 6, StartToleranceSeconds = 2, MaxClaimBytes = 512, MaxDraws = 3;   // rule §1, §3; O:48-53
    public static readonly Regex OwnerGrammar = new("^[uw][0-9]{1,10}$");
    public static string NameBase => Prefix + OwnerToken.This + "-" + RepoToken + "-";   // "testsToDelete-u1000-mcp-"
    public static int Room => MaxName - NameBase.Length - 1 - IdLength;                  // the room for `what` under THIS owner (O:132)
    public static Regex Pattern(string? owner = null);   // ^teststodelete[-_]<owner>[-_]mcp[-_], IgnoreCase | CultureInvariant (O:147-150)
    public static readonly Regex LegacyPattern;          // ^teststodelete[-_]mcp[-_] — listed, never collected (O:152)
    public static Regex FullName(string? owner = null);  // Pattern + ".+[-_][A-Za-z0-9]{6,12}$"
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
internal static class Native                                      // every call to the OS the contract needs and .NET lacks — nothing else P/Invokes
{
    // Linux (libc): getuid; kill(pid, 0); mkdir(path, 0700); statx(AT_FDCWD, path, AT_SYMLINK_NOFOLLOW, STATX_BASIC_STATS, out buffer); rename(from, to)
    // Windows (kernel32): CreateDirectoryW; CreateFileW (FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, access 0, share all);
    //   GetFileInformationByHandleEx (FileIdInfo = 18, FileBasicInfo = 0); GetFinalPathNameByHandleW; OpenProcess (PROCESS_QUERY_LIMITED_INFORMATION); GetExitCodeProcess
    internal static Identity? IdentityOf(string path);            // the entry ITSELF, a link never followed; null when it is not there
    internal static bool MakeDirectoryExclusively(string path);   // false = already exists (EEXIST / ERROR_ALREADY_EXISTS); any other failure throws
    internal static void RenameOnto(string from, string to);      // Linux rename(2); Windows Directory.Move (exclusive by itself)
}

public sealed class OwnedDir : IDisposable                        // one folder; `using var dir = OwnedDir.Create("x");`
{
    public string Path { get; }
    public static OwnedDir Create(string what, string? parent = null, Action<string>? warn = null);
    internal static OwnedDir Create(string what, string? parent, Action<string>? warn, Func<string> nextName, Action<string>? afterClaim = null);   // test seams: the names to try; a step between the claim and the create
    public void Dispose();                                        // Removal.Quietly(Path, warn); idempotent; never throws
    internal static IReadOnlyList<OwnedDir> Undisposed();         // the process-wide registry: created, not yet disposed (S4 audit)
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
  the helper then refuses to create anything (no claim, no folder); empty is unset. Windows → `Windows(Environment.UserName)`
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
- **`Owner.Parse`** (`O:397-406`): longer than 512 bytes ⇒ null; strip ONE trailing `\r?\n`; match
  `^([uw][0-9]{1,10}):([^@:\s]+@(linux|macos|windows)):(\d+)(?::(\d+)(?::(\d+))?)?$` (`O:57`); anything else — empty,
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
     `written = Owner.RunRecord().Format() + "\n"` (UTF-8, no BOM). An `IOException` while `File.Exists(claim)` ⇒ the
     name is somebody else's: next draw. Any other failure ⇒ `InvalidOperationException("owned: could not claim <dir>: <cause>")`
     — nothing was created. **No `Directory.Exists` pre-check**: it is a race, and the exclusive create answers it.
  3. `Native.MakeDirectoryExclusively(dir)` — Linux `mkdir(dir, 0700)` (only the caller's processes read a scratch
     folder; no test here runs a child as another user), Windows `CreateDirectoryW`. `false` (the folder already
     exists under the claimed name) ⇒ ONE warning `owned: <dir> already exists under the name this run claimed — it is
     not this run's and is left alone; drawing another name`, then `Removal.ClaimIfStill(dir, written)`, then the next
     draw. Any other failure ⇒ remove the own claim (inside its own `try`), throw
     `InvalidOperationException("owned: could not create <dir>: <cause>", inner)`. Never `Directory.CreateDirectory`.
  4. Return the `OwnedDir` and add it to the registry. After the draws: throw `owned: no free name for <what> in <parent> after 3 draws`.
  The caller registers the cleanup on the same statement (`using var` or `OwnedScope.Dir`); the window inside `Create`
  is covered by step 3's own catch. Nothing is ever written inside `dir`.
- **The registry of undisposed instances.** One process-wide `ConcurrentDictionary<OwnedDir, byte>`; the FIRST
  `Dispose()` removes the entry (before the removal is tried). `OwnedScope` holds `OwnedDir`s, so its folders are in
  the registry too. `Undisposed()` is a snapshot. It is what lets S4's audit see a folder made under a CUSTOM `parent`.
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
  `Native.IdentityOf(path)?.Uid == Native.getuid()` (`O:223-227`). **Windows, decision 2, both keys:** (1) the entry's
  final path (`GetFinalPathNameByHandleW` on a `CreateFileW` handle opened with `FILE_FLAG_OPEN_REPARSE_POINT`, so a
  junction resolves to itself and its PARENTS resolve) is strictly inside the final path of
  `Environment.GetFolderPath(SpecialFolder.UserProfile)`, compared ordinal-ignore-case by path segments (`O:204-207`);
  AND (2) `new FileInfo(path).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier))`
  equals `WindowsIdentity.GetCurrent().User`. Either read throwing, or either key false ⇒ `false`. A `DirectoryInfo`
  for a directory. Nothing else in the repository asks who owns a file.
- **Elevated runs (Windows).** A process elevated by UAC creates files owned by `BUILTIN\Administrators`, not by the
  user's SID, so key (2) fails for the run's OWN files: `IsMine` is false, the sweep lists this run's old folders as
  `foreign-uid` and never removes them, and the audit's claim scan sees nothing (its registry check still does).
  Teardown by exact name is unaffected. Kept, by decision 2: a person removes an elevated run's leftovers after
  looking. Whether Git for Windows' `stat %u` (`J:133-135`) reads such entries as the caller's is NOT verified — the
  janitor is forced to `--dry-run` on Windows anyway (`J:22-24`). Scenario 14 records what an elevated run does.
- **`Scope.ReadClaimText(file, mine)`** — the ONE safe read (`O:470-491`): `Native.IdentityOf(file)` null ⇒ `Missing`;
  not a regular file (a FIFO would block, a link is not followed) ⇒ `Unreadable`; `!mine(file)` ⇒
  `Unreadable("foreign-uid")`; else `FileStream(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)`
  reading at most 513 bytes ⇒ `Text`; any throw ⇒ `Unreadable`. `Parse` of a text over 512 bytes is null.
- **`Native.IdentityOf(path)`** — Linux: `statx(AT_FDCWD = -100, path, AT_SYMLINK_NOFOLLOW = 0x100, STATX_BASIC_STATS = 0x7ff, buffer)`
  into a 256-byte buffer with the kernel's fixed layout (`stx_mask` u32 @0, `stx_nlink` u32 @16, `stx_uid` u32 @20,
  `stx_mode` u16 @28, `stx_ino` u64 @32, `stx_ctime` @96 and `stx_mtime` @112 as `{ i64 sec, u32 nsec }`,
  `stx_dev_major` u32 @136, `stx_dev_minor` u32 @140); `Dev` = major << 32 | minor; type from `stx_mode & 0xF000`
  (`0x8000` regular, `0x4000` directory, `0xA000` link); errno `ENOENT` ⇒ null, any other ⇒ throw. `statx` is
  exported by glibc ≥ 2.28 and musl ≥ 1.1.24 — the Ubuntu runner, WSL and every supported .NET 10 Linux. Windows:
  `CreateFileW` + `GetFileInformationByHandleEx(FileIdInfo)` (`VolumeSerialNumber` u64, `FileId` 128-bit — ReFS needs
  the wide one) and `(FileBasicInfo)` for `ChangeTime` / `LastWriteTime` and the attributes; `ERROR_FILE_NOT_FOUND` /
  `ERROR_PATH_NOT_FOUND` ⇒ null. `Uid` is null on Windows. **macOS: not built** — no CI leg, no developer machine, and
  Darwin's `lstat` layout differs by symbol (`lstat$INODE64` on x86-64); `Owner.This` throws
  `PlatformNotSupportedException` there until someone measures it, and the facts of this document say so.
- **`Liveness.ProbeProcess(pid)`** — the reference's probe (`O:332-342`), never `Process.GetProcessById` (its Linux
  answer under `hidepid` and its Windows access rules are .NET's, not the contract's): `pid < 1` or `> 2^53` ⇒
  `Unknown`; Windows and `pid == 1` ⇒ `Unknown`. Linux: `Native.kill(pid, 0)` — `0` ⇒ `Live`; errno `ESRCH` (3) ⇒
  `Dead`; `EPERM` or anything else ⇒ `Unknown`. Windows: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, false, pid)`
  — null with `ERROR_INVALID_PARAMETER` (87) ⇒ `Dead`; null with anything else (access denied) ⇒ `Unknown`; a handle
  whose `GetExitCodeProcess` is not `STILL_ACTIVE` (259) ⇒ `Dead`, else `Live`; the handle closed — the same shape as
  libuv's `uv_kill(pid, 0)` behind the reference's `process.kill`. Then, Linux and `Live` only: `Start` =
  `StartFromProcStat(/proc/<pid>/stat, btime, ClockTicksPerSecond())` (null when unreadable). No `HasExited`, no
  `Process.Start`, nothing that spawns.
- **`Liveness.State(owner, probe, facts)`** — rule §3's order exactly (`O:531-552`): `owner is null` ⇒ `Unknown`;
  `NonCanonical` ⇒ `Unknown`; `owner.Token != (facts.CallerOwner ?? OwnerToken.This)` ⇒ `Unknown`; `facts.NameOwner`
  given and `!= owner.Token` ⇒ `Unknown`; `facts.KernelMine == false` ⇒ `Unknown`; `owner.Host` ≠ `Owner.This.Host`
  (ordinal, case-insensitive) ⇒ `Unknown`; this host ends `@linux` and (`owner.Ns is null` or `!= Owner.This.Ns`) ⇒
  `Unknown`; `Pid < 1` or `> 2^53` ⇒ `Unknown`; `Pid == 1` and host ends `@windows` ⇒ `Unknown`; `probe(Pid)` throws
  ⇒ `Unknown`; `Dead` ⇒ `(facts.Hidepid ?? HidepidActive()) ? Unknown : Gone` (the ONLY `Gone`); not `Live` ⇒
  `Unknown`; either start null ⇒ `Live`; `|Δ| <= StartToleranceSeconds` ⇒ `Live`; otherwise `Unknown`.
- **`UnmatchablePid`** — a port of `dew_flow_conventions · tools/lib/unmatchable-pid.mjs:32-49`: `Next(i = 0)` =
  Windows `777770 + 4·i` (≡ 2 mod 4), Linux `pid_max + 1 + i` (`/proc/sys/kernel/pid_max`, else 4194304);
  `AssertNoSuchProcess(pid)` asserts the premise through `ProbeProcess` (`Dead`). **A test never takes a dead owner
  from a process it started and killed** — the OS hands that pid to the next process (measured by E1 on Windows, one
  run in fifteen).
- **Analyzers.** `File.SetUnixFileMode` is `[UnsupportedOSPlatform("windows")]`; the ACL and `WindowsIdentity` APIs
  are `[SupportedOSPlatform("windows")]`: every such call sits under `OperatingSystem.IsWindows()` / `IsLinux()` or
  `CA1416` breaks the build. `Native.cs` uses `[LibraryImport]` with `SetLastError = true` and
  `Marshal.GetLastPInvokeError()`; its generated marshalling is unsafe code, so `Mcp.Tests.csproj` gains
  `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — the test project only, the one change outside the folder. Every
  async call in a test takes `TestContext.Current.CancellationToken`.

**Scenarios** (RED first — write the tests against the signatures above before the types exist and record the build
failure, then stub the types and record each scenario failing on its assertion before the body is written). Every
scenario that counts what a call left behind passes a PRIVATE `parent` (an `OwnedDir` of its own). Names in tests are
built at run time from `Owned.NameBase`, `Owned.Prefix` or the word in two pieces (`"teststo" + "delete"`, `O:170`),
never as bare-prefix literals (decision 3, S4 sc. 3). `Other` = the caller's token with its last digit changed.

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
   `=u`, `=u12345678901`, `=U1` ⇒ throws naming the grammar; `OwnerToken.This` matches `OwnerGrammar` and, off
   Windows, equals `"u" + Native.getuid()`.
4. **A folder and its claim, side by side, never inside.** `OwnedDir.Create("path Unicode пробел", parent)` makes a
   folder whose name starts with `NameBase + "path Unicode пробел-"`, empty, and a sibling `<folder>.owner` whose
   text `Parse`s to `Owner.RunRecord()` and ends in one `\n`.
5. **Disposal removes the folder and the claim, and is idempotent.** After `Dispose()` neither exists; a second
   `Dispose()` writes no warning; the instance is in `Undisposed()` between `Create` and `Dispose`, not after.
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
   RED against `Directory.CreateDirectory`: (b) adopts the squatter.
10. **The record round-trips, carries what the OS gives, and refuses everything else.** `Parse(RunRecord().Format() + "\r\n")`
    equals `RunRecord()`; `Owner.This.Host` matches `^[^@:\s]+@(linux|macos|windows)$`, starts with
    `HostName(Dns.GetHostName())`, and under WSL (`WSL_DISTRO_NAME` set) holds `~` + the distribution; the `hostnames`
    vectors (h01–…) through `HostName`; the `record` vectors (r-…) through `Parse` (valid ⇔ not null); on Linux `Start`
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
    with either start null ⇒ Live; 2 s apart either way ⇒ Live; 3 s and 10⁶ s apart ⇒ Unknown — never Gone.
13. **The real probe and the real kernel owner.** `State(Owner.This, ProbeProcess)` ⇒ Live (through the start
    comparison on Linux); `UnmatchablePid.Next()` with the premise asserted ⇒ `ProbeProcess` = `Dead` and `State` =
    Gone; on Linux as a non-root user `ProbeProcess(1)` ⇒ `Unknown` (EPERM), `Assert.Skip` as root; `Scope.IsMine` of a
    file this test made ⇒ true (Windows: `Assert.Skip` with the message `elevated: the owner is Administrators` when
    `GetOwner` of that file is not the user SID — scenario 14); a `mine` seam answering false makes `ReadClaimText` of
    the same file `Unreadable("foreign-uid")`; a DIRECTORY named like a claim ⇒ `Unreadable` (the regular-file check;
    a FIFO needs `mkfifo`, which .NET cannot make without a child — not provoked, the directory covers the branch); a
    513-byte claim ⇒ `Parse` null; `Native.IdentityOf`: a folder equals itself
    after `Directory.Move` (same `Dev`, `Ino`), differs from a re-created folder of the same name, a symlink reports
    `IsLink` and its own identity, a missing path ⇒ null, on Linux `Uid == getuid()`.
14. **Elevated run (Windows, by hand, recorded in the PR; not a merge condition).** Run the executable from an
    elevated prompt once: scenario 13 skips with its message, the sweep's report of a planted old own folder says
    `foreign-uid`, the folder still exists; from a normal prompt the same plant is collected. Both observations go in
    the PR, with whether `stat %u` of Git for Windows' bash reads the elevated entry as the caller's.

- **Commands.** `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_mcp.slnx -c Debug -m:4`;
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe --filter-namespace "Mcp.Tests.Leftovers"`;
  on Linux (WSL or CI) `./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests --filter-namespace "Mcp.Tests.Leftovers"`; the whole executable.
- **DoD.** Build at 0 warnings; every scenario observed RED, then GREEN, on Windows and on the Linux CI runner
  (scenario 6, 10 and 13 run a different branch on each); the constants table of S1 in the commit message; the module
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
  `Hooks` (`AfterCapture`, `AfterVerdict`, `AfterRename`, `AfterReserve`) are seams for a change between the capture,
  the verdict and the removal. The caller's owner is `OwnerToken.This`; the state is
  `Liveness.State(record, cached, new Facts(CallerOwner: owner, NameOwner: nameOwner, KernelMine: true))` (`S:444-445`).
  1. **Relation of a name** (`S:61-69`): `Owned.Classify` — owner form with token `mcp`: `own` when its owner is the
     caller's, else `foreign`; legacy form with token `mcp`: `legacy`; anything else: not this sweep's business, silent.
  2. **Folders** (`S:340-374`). Depth-1 entries of `Tmp` that are REAL directories (`ReparsePoint` not set — a link is
     never judged). `foreign` ⇒ `Listed` `foreign: folder <name> — owner <o>`, nothing else. `legacy` ⇒ `Preserved`
     `legacy` and `Listed` `legacy: folder <name> — remove with: <command>` (`S:240-250`'s command for this platform)
     — **listed only, never collected** (rule §4 allows preserving more, `R:174-175`; this repository never produced
     a legacy name). A `.git` FILE ⇒ `other clone`. Otherwise **capture first** (`S:149-152`): `Native.IdentityOf(dir)`
     and the claim text through `Scope.ReadClaimText`; then the decision table (`S:72-81`): age =
     `Now − LastWriteTimeUtc` `< MinAge` ⇒ `young`; `!Mine(dir)` ⇒ `foreign-uid` + `Listed` `foreign:`; no claim ⇒
     `unmarked` + `Listed` `unmarked: folder <name>` — **never collected, however old** (`R:75-76`); unreadable ⇒
     `unreadable` (or `foreign-uid`); `State` ⇒ `Gone` remove, `Live` ⇒ `live`, `Unknown` ⇒ `foreign host` when the
     host differs, else `unknown`.
  3. **Remove by identity** (`S:204-218`): `Hooks.AfterVerdict`; re-check identity and claim text unchanged, else
     `replaced`; `Directory.Move(dir, quarantine)` with `quarantine = Path.Combine(parent, "." + name + ".teststodelete-quarantine-" + Environment.ProcessId + "-" + 4 random bytes as hex)`
     (same parent: atomic, never across a file system; a leading `.`: no prefix match reaches it); `Hooks.AfterRename`;
     identity at the quarantine and the claim at the ORIGINAL path unchanged, else restore (below) and `replaced`
     (`quarantine-conflict` when the restore failed); `UnlinkTopLevelLinks(quarantine)`; `Removal.Quietly(quarantine)`
     — `false` ⇒ an `Errors` entry; then delete the claim. **Restore** (`S:169-197`): Windows `Directory.Move` back (a
     rename onto an existing directory fails there, so it is exclusive); Linux `Native.MakeDirectoryExclusively(original)`
     as a mode-700 placeholder, `Hooks.AfterReserve`, `Native.RenameOnto(quarantine, original)` (`rename(2)` onto an
     empty directory is atomic; `Directory.Move` refuses an existing target), on failure remove the placeholder only
     while it is still a real, empty directory of the caller's.
  4. **Orphan claims** (`S:376-404`): files `*.owner` of `Tmp` whose folder is absent and whose age ≥ `MinAge`;
     relation as above (`foreign` ⇒ `Listed foreign: claim …`; `legacy` ⇒ `Listed legacy: claim …`); `own` ⇒ the same
     decision with `Mine(claim)` and the parsed record — `Gone` ⇒ the claim is deleted; `live`, `unknown`, `foreign host`,
     `unreadable` keep it. Age only selects: a young orphan is a creation in progress (S2 claims first).
  5. **Quarantines first** (`S:406-419`): entries matching `^\.(.+)\.teststodelete-quarantine-(\d+)-([0-9a-f]+)$`
     (case-insensitive) that are real directories, whose inner name's relation is not "other", that are `Mine`, whose
     renamer `Probe` says `Dead`, and whose `ChangeTime` AND `WriteTime` (from `Native.IdentityOf`) are both past the
     window ⇒ restored under the inner name when free, `Listed` `notice: restored <name> from a quarantine a killed
     sweep left`; taken ⇒ `Preserved` `quarantine-conflict`.
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
   `Mine` seam false for one folder ⇒ `foreign-uid` and a `foreign:` line; a dead pid with a start 3 s off ⇒ `unknown`;
   YOUNG and dead-claimed ⇒ `young`; `.git` file and no claim ⇒ `other clone`; **unmarked and old ⇒ `unmarked`, an
   `unmarked:` line, and the folder still exists** (RED against E1's rule, which removed it); dead-claimed ⇒ removed. Each
   preserved entry carries exactly its reason; every preserved folder still exists afterwards.
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
5. **Removal is by identity.** (a) Through `Hooks.AfterVerdict` the test deletes the judged folder and creates a new one
   under the same name ⇒ `replaced`, the new folder untouched. (b) Through `Hooks.AfterRename` the test rewrites the
   claim ⇒ the folder is restored under its name, `replaced`, nothing removed. (c) A quarantine left by a "killed sweep"
   (built by hand: `.<own name>.teststodelete-quarantine-<unmatchable pid>-deadbeef`, both stamps past the window) ⇒
   restored, `notice: restored …`; the same with the original name TAKEN ⇒ `quarantine-conflict`, both untouched; a
   quarantine whose renamer is THIS process ⇒ untouched, unlisted. (d) After every removal no `.*.teststodelete-quarantine-*`
   entry remains in `Tmp`.
6. **The sweep never fails the run.** `Run` on a `Tmp` that does not exist ⇒ one `Errors` entry, no throw; a folder the
   `Mine` seam throws on ⇒ one `Errors` entry and the next folder still judged; the internal fixture constructor with a
   sweep that throws ⇒ completes, the `report` seam got the error line, `LastRun` is empty.
7. **Silence when there is nothing.** An empty `Tmp` ⇒ an empty result, and the fixture's `report` seam got nothing.
8. **The fixture ran before this test.** A test class taking `LeftoversSweepFixture fixture` asserts `fixture.LastRun`
   is not null — the sweep ran at the start of THIS run, over the real temp root.

- **Commands.** As S2, `--filter-class "*LeftoversSweepTests"`; then the whole executable twice in a row — the second
  run prints no `leftovers` line on a clean machine.
- **DoD.** Every scenario observed RED, then GREEN, on Windows and Linux; two consecutive full runs print nothing from
  the sweep; the sweep's wall time on a full run is under one second (record it).

## S4 — The guard, the migration of 21 sites, and the end-of-run audit

- **Goal.** No test here creates a temp path except through `Mcp.Tests.Leftovers` (or under a valid inline
  exemption), a guard keeps it so — red on every class of rule §7, on a token that is unregistered, of the owner
  grammar or another repository's, and on a scan of nothing — and a run that leaves a folder it claimed is a red run.
- **Files.** New `tests/Mcp.Tests/Leftovers/LeftoversGuard.cs`, `LeftoversGuardTests.cs`, `LeftoversAudit.cs`,
  `LeftoversAuditTests.cs`; new `tests/Mcp.Tests/RepositoryRoot.cs`; `LocalCpuBudgetTests.cs` (uses it);
  `LeftoversSweepFixture.cs` (gains `DisposeAsync`); the 11 files of the migration table; `Owned.cs` (one exemption
  comment — harness setup, added BEFORE scenario 1's RED run); `.github/workflows/ci.yml`; new
  `research/module_tests.md`; `research/architecture.md`.

**The guard.** `LeftoversGuard.Scan(string repoRoot) → GuardReport(int FilesScanned, IReadOnlyList<Finding> Findings)`,
`Finding(string File, int Line, string Class, string Text)`; pure apart from reading files. A port of the shape of
`G:` mapped to this repository's languages.

- **Root.** `RepositoryRoot.Path` — `LocalCpuBudgetTests.cs:20-27`'s `FindRoot` moved into
  `internal static class RepositoryRoot`; `LocalCpuBudgetTests` reads it from there. One implementation, not two.
- **Discovery** (a directory named `bin`, `obj`, `node_modules`, `.git`, `.claude`, `.http-artifacts` or `logs` is
  never entered — `.claude/rules/shared` is another repository): `tests/**/*.cs`; `.github/workflows/*.yml` and
  `*.yaml`; `.github/scripts/**/*.{mjs,js,cjs,sh,ps1}`; `http/**/*.{mjs,js,cjs}`; every `*.sh` and `*.ps1`. **Excluded
  by name**, a list a test pins to exactly one entry: `tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs` (it carries
  the violations as fixture strings; `G:72-74`). Zero files ⇒ `FilesScanned == 0`, reported as `scanned nothing under
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
    elsewhere cannot carry it to a sweep. In scripts and workflows (`.sh`, `.ps1`, `.mjs`, `.js`, `.cjs`, `.yml`): a
    pattern, glob or filter that matches the prefix with no owner segment right after it — the prefix word, a
    separator (`-`, `_`, `[-_]`, `[-_]{1}`, `(-|_)`), then neither an owner start (`[uw]` followed by a digit, `\d`,
    `[0-9]`, `$`, `{`, `[uw]|`, `[uw]`) nor a literal first segment — written literally, or through a variable the SAME
    file assigns a value holding the prefix: shell `NAME=`, `export NAME=`, PowerShell `$NAME =`, JS `const|let|var NAME =`
    with a literal value holding the prefix word, every use `$NAME`, `${NAME}`, `${NAME<op>…}`, `$env:NAME`, bare `NAME`
    substituted and the line judged again. **Deviation, recorded:** one level of binding — no alias chains, defaults,
    path joins or overflow of the shipped tracer (`tools/lib/prefix-bindings.mjs`); the per-machine barrier scan is
    the shipped tracer over the same files and found nothing here at `b352881`; a script that needs more re-opens this
    guard.
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
  — `token` must match `^[a-z0-9]{2,12}$` and NOT the owner grammar; the rule must carry a `## Tokens` heading followed
  by a GFM table whose header is exactly `| token | family | repository |`; the token must be the first cell (backticks
  stripped) of exactly one row; no token twice; the row's `repository` cell must equal `identity` (trimmed,
  case-insensitive), and an empty `identity` is a finding (fail closed). The real check reads
  `<root>/.claude/rules/shared/common/test-leftovers.md` with `Owned.RepoToken` and `Owned.RepoRow`; **a missing file
  is a finding** (an uninitialised submodule registers nothing — run `git submodule update --init .claude/rules/shared`).
  **One constant** (`R:63-65`; `G:731-732`): over every tracked `.cs`, `.mjs`, `.js`, `.cjs`, `.sh`, `.ps1` file (the
  discovery set plus `tests/Mcp.Tests/Leftovers/`), a non-comment line matching `\bRepoToken\b\s*(?::\s*\w+\s*)?=\s*["']([^"'\n]*)["']`
  must occur exactly once and its value must be `token`; a second declaration, or another value, is a `token` finding.

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
- `LeftoversAudit.Undisposed() → IReadOnlyList<string>`: the paths of `OwnedDir.Undisposed()` — every folder created
  and never disposed, **wherever its `parent` was**.

`LeftoversSweepFixture` implements `IAsyncDisposable`; `DisposeAsync` — after the last test of the assembly — first
disposes every instance `Undisposed()` names, then computes `ClaimedBy(Owned.TempRoot(), Owner.RunRecord())` and
removes each entry with `Removal.Quietly`, and, when either list was not empty, throws
`InvalidOperationException("mcp tests: this run left <n> folder(s) it claimed — <paths>; a test made scratch outside a disposed scope")`.
A green run's teardown leaves nothing it claimed; this is the whole-suite form of the epic's goal sentence, and it
catches the one leak the guard cannot see. In an elevated Windows run the claim scan is blind (S2, *Elevated runs*);
the registry check is not.

**CI.** `build-test` gains, before `Build`, the step the other two jobs already have:

```yaml
      - name: Fetch the shared rules
        run: git submodule update --init --depth 1 .claude/rules/shared
```

— the guard's token check reads the mounted rule and fails closed without it.

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
- **The leftovers flows, each `covered` or `not covered`** with the test that covers it: the helper (S2 sc. 1–9),
  the token and record (S2 sc. 3, 10, 11), liveness and scope (S2 sc. 12–13), the start-of-run sweep (S3 sc. 1–8),
  the end-of-run audit (S4 sc. 7, 7a, 8), the guard (S4 sc. 1, 3–6), the janitor reading this helper's claim (S4 sc.
  9, by hand — `not covered` in CI, with why), an elevated run (S2 sc. 14, by hand).
- **What it does not prove** — a SIGKILL of the real executable (the kill is built by hand, S3 sc. 1); macOS (no CI
  leg; the helper refuses there); the §5 container carve-out (not recognised, the recorded deviation); a script whose
  prefix reaches a sweep through more than one binding (the recorded deviation; the barrier scan sees it); the guard
  reads text, so a temp path built by reflection or in a generated file is invisible; the audit runs only when the
  executable reaches `DisposeAsync`.
- **When it runs** — every pull request (`build-test`), and the janitor by hand on a machine that stopped running these tests.

**Hand clean-up**, once per machine, recorded in the PR and not code: list the temp root's folders named exactly
`mcp-<one of the 17 old prefixes>` + 8 characters + `.` + 3 (Windows) or + 6 characters (Linux), and `outside.txt`
containing `secret`; look at the list; delete only those.

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
   `tests/Mcp.Tests/Leftovers/Owned.cs` of the tree ⇒ 0; under `// testsToDelete-exempt: owner-blind — any reason at
   all here` ⇒ still 1 plus 1 `exemption`; in `a.sh` `rm -rf "$TMPDIR"/<prefix>-*` ⇒ 1, `find … -name '<prefix lower>[-_]*'`
   ⇒ 1, `<prefix>-u*` ⇒ 1, `<prefix>-"$OWNER"-*` ⇒ 0, `<prefix>-${OWNER}-*` ⇒ 0, `<prefix>-conventions-*` ⇒ 0
   (a literal first segment), `P='<prefix>-'` on line 1 and `rm -rf "$P"*` on line 3 ⇒ 1 reported at line 3,
   `const P = "<prefix>-"` and `glob(P + "*")` in `a.mjs` ⇒ 1 at the use, `$P = "<prefix>-"` and `Remove-Item "$P*"`
   in `b.ps1` ⇒ 1, a workflow `run: rm -rf /tmp/<prefix>-*` ⇒ 1; `echo "<prefix>-u1000-mcp-x-abc123"` ⇒ 0 (a name,
   not a pattern). **Block comments:** `/* Path.GetTempPath() */` ⇒ 0; opened on line 1, closed on line 3 with
   `Path.GetTempPath()` on line 2 ⇒ 0; `*/ var t = Path.GetTempPath();` ⇒ 1 at ITS line; `var glob = "src/**/*.cs"; var t = Path.GetTempPath();`
   ⇒ 1; `/* testsToDelete-exempt: raw-temp — the one place a test learns the temp root */` above ⇒ 0. RED first:
   written before the stripper and the two new classes exist, the multi-line and the `const P` cases observed giving the
   wrong count.
4. **A scan of nothing is red, not clean.** An empty tree ⇒ `FilesScanned == 0`; a root that does not exist ⇒
   `FilesScanned == 0` and no throw.
5. **The token is registered and is ours.** On the real mount, `TokenFindings(rule, Owned.RepoToken, Owned.RepoRow)` ⇒
   none, and the one-constant check over the real repository ⇒ none. Break-its on rule texts written in the test:
   `nosuchtoken`, `Mcp`, `x`, `u1000`, `w1` ⇒ one finding each; no `## Tokens` ⇒ one; a two-column table ⇒ one; `mcp`
   listed twice ⇒ one; `mcp` only in the `repository` column ⇒ one; the `mcp` row's repository `the embedding sidecar`
   ⇒ one naming both; identity `""` ⇒ one; the rule file missing ⇒ one. The one-constant check on a tree with
   `RepoToken = "mcp"` in two files ⇒ one; with `RepoToken = "mcpx"` ⇒ one.
6. **The exclusion list is exactly one file** — pinned.
7. **The audit names only what THIS run claimed.** In a private `tmp`: a folder claimed by `Owner.RunRecord()` ⇒
   listed; one claimed by a dead pid, one by `Other`, one by `other@<tag>`, one unclaimed, one of another token ⇒ not
   listed; an orphan claim of this run ⇒ listed.
7a. **The audit sees an undisposed folder under ANY parent.** RED first: an `OwnedDir.Create("custom", parent: <a
   private OwnedDir NOT at the temp root's depth 1>)` left undisposed ⇒ `Undisposed()` names its path while
   `ClaimedBy(Owned.TempRoot(), …)` does not; after `Dispose()` it is gone from both. The test disposes everything it
   made before it ends.
8. **The audit has teeth** (observed by hand, recorded in the PR, then reverted): remove `: IDisposable` and
   `Dispose` from ONE migrated class (`ToolSurfaceTests`), run the executable ⇒ it exits non-zero with the audit's
   message naming three `testsToDelete-<owner>-mcp-surface-…` folders; restore ⇒ green, and the temp root holds none.
   If xUnit v3 does not fail the run on an assembly-fixture cleanup exception, stop and report — do not turn the audit
   into a warning silently.
9. **No leftover after a full run, and the janitor reads THIS helper's claim** (by hand, recorded in the PR, on
   Windows under Git for Windows' bash AND on Linux). Before the migration, one full run leaves N new `mcp-…` folders
   and `outside.txt` in the temp root (count them); after it, zero new entries of either, and `bash
   .claude/rules/shared/tools/janitor.sh --token mcp --dry-run`, run from this repository, prints no `would run:` line
   and exits 0 (Windows: its forced-dry-run notice first, `J:22-24`). Then plant one folder
   `Owned.NameBase + "janitor-" + <id>` in the temp root with a sibling claim in the C# helper's own format —
   `new Owner(OwnerToken.This, Owner.This.Host, UnmatchablePid.Next(), Owner.This.Start, Owner.This.Ns).Format()`,
   this user's token, this host (under WSL `host~distro`), this OS's tag, an unmatchable pid (premise asserted), and
   on Linux this reader's namespace (the janitor says `unknown` without it, `janitor.sh:649`) — age folder and claim
   to 11 minutes (`Directory.SetLastWriteTimeUtc` / `File.SetLastWriteTimeUtc`, or `touch -d`). Then
   `JANITOR_MIN_AGE_MINUTES=10 bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` ⇒ one `would run:`
   line on stdout naming that folder, no `skipped:` line for it, exit 0. Plant a second one claimed under `Other` ⇒ a
   `foreign:` line, no `would run:`. Then remove what was planted. All lines go in the PR. If the janitor does not list
   the first, the two implementations' records disagree: stop and report to the conventions owner, never adjust the
   C# format to pass.

- **Commands.** As S2, `--filter-class "*LeftoversGuardTests"` (RED, then GREEN), `--filter-class "*WorkspaceToolTests"`,
  the whole executable on Windows and Linux; `dotnet format dew_flow_mcp.slnx --verify-no-changes --no-restore` in
  Linux or WSL; the `plans` steps before the push: `node .claude/rules/shared/tools/pin-check.mjs`,
  `plan-lifecycle.mjs`, `gate-snippet-check.mjs`, `build-flags-check.mjs`, `node .github/scripts/branch-protection.mjs --selftest`.
- **DoD.** The `Owned.cs` exemption in place before scenario 1's RED; scenarios 1, 2 and 7a observed RED with the
  counts above, then GREEN; every break-it observed red (the block-comment, `const P` and binding cases included);
  scenario 8's teeth observed and reverted; scenario 9 recorded with both janitor listings; CI green on all four jobs
  with the new `build-test` step; `research/module_tests.md` created with every flow marked; `architecture.md`
  updated; the merge condition of *Order* met and quoted in the PR; this document's status line becomes
  `> Status: **E3 shipped, <YYYY-MM-DD>; promotes with its parent.**` — never the word *implemented* while it sits in
  `todo/` — and its `todo/README.md` row says the same. The parent and this document promote together, after the last epic.

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
| The API shape (`Owned`, `OwnerToken`, `Owner`, `Liveness`, `Scope`, `Native`, `OwnedDir`, `OwnedScope`, `Removal`) | yes | same names; its own `RepoToken` and `RepoRow` from its own row of the `## Tokens` table — never a copy of this one's |
| The owner token: real uid, or the SHA-256 Windows form with ASCII-only lower-casing; `TESTSTODELETE_OWNER` validated | yes, vectors pinned | same bytes; the vectors file is the proof |
| Claim first (`FileMode.CreateNew`), then an EXCLUSIVE `mkdir` through P/Invoke; an own id, never `CreateTempSubdirectory` | yes, measured above | same — the Windows id and the missing exclusive create are the reasons |
| The record with the Linux start from `/proc` + `AT_CLKTCK` and the pid namespace; no start elsewhere | yes, measured against `Process.StartTime` (S2 sc. 10) | same formula — two helpers on one machine must agree within 2 s |
| `Dns.GetHostName()` with the WSL `~distro` suffix, never `Environment.MachineName` | yes | same — the janitor reads `hostname` and `WSL_DISTRO_NAME` |
| The Windows kernel owner by BOTH keys (profile AND owner SID), fail closed; elevated runs kept | yes (decision 2) | same — the reference checks the profile alone; the family's C# is stricter |
| Identity by `statx` (Linux) and `FileIdInfo` (Windows); removal through a quarantine rename, restore when free | yes | same `Native.cs`; a copy that makes repositories clears the read-only attribute before `Directory.Delete` |
| The probe: `kill(pid, 0)` / `OpenProcess`, never `Process.GetProcessById` | yes | same |
| Per-test disposal through `IDisposable` + `OwnedScope`; assembly-fixture sweep and end-of-run audit with the registry | yes | same, plus the kinds that repository creates (containers, databases) |
| `FullName` accepting rule §1's 6–12-character id; legacy names listed, never collected | yes | same — write 6, accept 6–12 |
| Guard: eight classes; `owner-blind` in `.cs` by literal (decision 3), one-level binding in scripts; the carve-out NOT recognised | all | a repository that starts containers implements the §5 carve-out in full; the `.cs` literal rule is the family's |
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
container); the `.cs` `owner-blind` class is a literal rule and the script half traces one binding (decision 3; the
barrier scan is the full tracer); legacy names are listed and never collected (the rule allows preserving more; this
repository never produced one). macOS is not built, and the facts say what was checked: no CI leg, no machine, and
Darwin's `lstat` symbol differs by architecture — nobody measured it.
