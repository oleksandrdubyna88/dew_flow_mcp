# PLAN — Test leftovers, E3: one C# helper, nineteen sites, a sweep and a guard for `tests/Mcp.Tests`

> Status: **plan only, nothing implemented yet, 2026-10-16.** Stories of epic E3 of the parent plan
> `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E3). Scope: the `.claude/rules/shared` pin,
> a new `tests/Mcp.Tests/Leftovers/` folder (helper, owner liveness, start-of-run sweep, end-of-run audit, guard), the
> 11 test files that create temp folders today, one CI step, a new `research/module_tests.md`, and
> `research/architecture.md` § CI.
>
> Split by Opus, 2026-10-16. Models per story below. No cornerstone: E3 is not one (parent, E3 entry).

**Committed together with this document** (one commit, the shared `common/planning-docs.md` § *A plan split into epics
and stories*): its row in [todo/README.md](README.md) under *Currently open*:

```
| [PLAN_test_leftovers_E3.md](PLAN_test_leftovers_E3.md) | plan only, 2026-10-16 — waits for the shared rules' E1 release | Stories of epic E3 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`: every temp folder `tests/Mcp.Tests` makes is named `testsToDelete-mcp-…`, claimed, removed by the test that made it, collected by the next run when a run was killed, and kept so by a guard |
```

**Done when** (from the parent, made concrete): `tests/Mcp.Tests` creates nothing it does not remove, and everything it
creates is named `testsToDelete-mcp-<what>-<id>` by one helper (S2, S4); a killed run's folders are collected at the
start of the next run, only when their owner is provably gone (S3); a guard over `tests/`, scripts and CI is red on
every class of the rule's §7, on an unregistered token and on a scan of nothing (S4); the pin is on the release that
carries the contract (S1).

## The contract this document encodes

The rule as E1 ships it: `dew_flow_conventions · common/test-leftovers.md`, read on branch `feat/test-leftovers-e1`
@ `95c99c6` — **not on `main` and not released on 2026-10-16** (the `release` tip is `e29883f`, the sha this
repository pins). Sections cited as *rule §n*:

- §1 (`:15-39`) — `testsToDelete-<repo>-<what>-<id>`; `<repo>` in ONE constant, registered in the `## Tokens` table
  (`:188-205`; the row `mcp` is at `:201`); `<what>` any file-system characters but separators, NUL and control
  characters, never a leading `-`; `<id>` 6–12 of `[A-Za-z0-9]`; whole name ≤ 63; every match
  `^teststodelete[-_]` case-insensitive and anchored; what lives INSIDE an owned folder needs no name of its own.
- §2 (`:41-53`) — create → register cleanup on the next statement → claim, or **claim first, then create**; a claim
  that cannot be written ⇒ remove and throw; teardown by exact name, never throws, five tries 100 ms apart, on giving
  up leaves the resource prefixed AND claimed with ONE warning line.
- §3 (`:55-82`) — owner `<host>@<os>:<pid>[:<start>]`, start = integer Unix seconds UTC from the OS's record, only
  where no process is spawned; a folder's owner is the sibling `<folder>.owner`; liveness is three-valued
  (`live`/`gone`/`unknown`), and **only "no process with that pid" is `gone`**; starts more than 2 s apart ⇒ `unknown`.
- §4 (`:84-103`) — the start-of-run sweep: own token only; age ≥ 10 minutes selects; removes only on `gone`; unmarked
  new-form ⇒ `gone` once old; never fails the run.
- §7 (`:140-158`) — the guard's classes (`raw-temp`, `raw-worktree`, `docker-run`, `docker-name`, `shared-store`,
  `ryuk-off`), red on a scan of nothing, and the inline exemption grammar.

The reference implementation is Node: `dew_flow_conventions · tools/lib/owned.mjs` (helper, `:41-337`),
`tools/lib/leftovers-sweep.mjs` (sweep), `tools/leftovers-check.mjs` (guard), `tools/lib/unmatchable-pid.mjs`. This
epic writes the family's **first C# helper by shape, not by file**: same names where C# allows, same order, same
three-valued answers — and two deliberate differences, both measured or forced by .NET (S2, *Why claim first*).

## Facts the split rests on (read on `origin/main` b352881, 2026-10-16)

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

  About **70 folders per full run** (one per factory call), each a few KB; none removed today.
- **Two more `raw-temp` sites the parent did not count**: `WorkspaceToolTests.cs:243` (`new WorkspaceRoot(Path.GetTempPath())`
  for a constructor that must throw) and `:276` (`Path.GetTempPath()` as an absolute path the tool must refuse). They
  create nothing, but rule §7's `raw-temp` class names `GetTempPath`, so the guard's RED over this repository lists
  **21** findings, not 19.
- **A leak that is not a folder.** `WorkspaceToolTests.cs:259-260` writes `outside.txt` into
  `Directory.GetParent(root)` — and `root` is a direct child of the temp root, so the file lands **in the shared temp
  root itself**, under a fixed name, never removed; the two `[InlineData]` rows of that theory (`:254-255`) write the
  same file concurrently. Observed on a development machine 2026-10-16: `%TEMP%\outside.txt`, 6 bytes, content
  `secret`, last written 2026-10-14 — exactly what that test writes. (No `mcp-…` folder from these tests was in that
  machine's temp root.)
- **`"\0x"` is not a risk for the guard.** `SpoolUsageSinkTests.cs:157` appends a NUL to the scratch path so the
  writer faults; the composed path is never created (the test asserts the fault, `:164-167`). The only resource is the
  base folder the same line creates, and a line-based guard sees that call whatever is concatenated after it. After
  S4 the line calls the helper.
- **The POSIX note at `LogRetentionTests.cs:79` says the opposite of what the parent reads into it.** On POSIX an open
  handle does NOT stop `Directory.Delete(recursive)`; on **Windows** it does (that test skips off Windows, `:93-98`).
  The conclusion stands — teardown must survive a refused delete and never throw — but the refusal is a Windows event,
  so the never-throw scenario provokes it per OS (S2 scenario 6).
- **`Directory.CreateTempSubdirectory` cannot be the helper's primitive.** Measured 2026-10-16 on .NET 10.0.12,
  Windows: `CreateTempSubdirectory("e3probe-")` made `e3probe-clxdyztb.njt` — prefix + `Path.GetRandomFileName()`, an
  id of 8 + `.` + 3 that breaks rule §1's `[A-Za-z0-9]{6,12}`. On Unix it is `mkdtemp`'s six. .NET has no atomic
  exclusive `mkdir` (`Directory.CreateDirectory` succeeds on an existing folder), so S2 generates the id and claims
  first with `FileMode.CreateNew` on the sibling — the atomic primitive .NET does have.
- **No Docker, no worktrees, no databases** anywhere in `tests/`, `src/` or `.github/` (`is-docker` appears only in
  `http/package-lock.json` as a transitive dependency of the contract suite). `.github/scripts/branch-protection.mjs`
  and `http/` create no temp path. The guard still implements every class (rule DoD: *red on every class of §7*).
- **Out of scope, on purpose:** `SpoolFixtureTests.cs:77-79` writes `mcp-spool-v0-correlated.jsonl` into
  `AppContext.BaseDirectory` — a fixed-name file in the build output, overwritten by every run, git-ignored, and kept
  there deliberately for a human to copy (`:29-31`). Not a temp resource; the guard does not flag it.
  `src/ServiceDefaults/McpLogging.cs:191` (`Directory.Delete` in the log retention) is product code.
- **How tests run here.** xUnit v3 on Microsoft Testing Platform (`tests/Mcp.Tests/Mcp.Tests.csproj:7`,
  `global.json` `test.runner`); the executable, never `dotnet test` (`CLAUDE.md:31-34`); locally through
  `node .claude/rules/shared/tools/low-priority.mjs` with at most 8 threads (`tests/Mcp.Tests/xunit.runner.json`,
  copied only off CI, `Mcp.Tests.csproj:39`). `TreatWarningsAsErrors` (`Directory.Build.props`), so analyzer warnings
  — `CA1416` platform checks, xUnit's `xUnit1051` cancellation-token rule — are build errors. Test classes are
  `public sealed`, in namespace `Mcp.Tests`; none has a fixture or a collection today. There is **no
  `research/module_tests.md`**: tests are documented in `research/architecture.md` § CI (`:238-251`) and the project
  table (`:40`, "123 tests").
- **CI** (`.github/workflows/ci.yml`): `build-test` (`:13-42`) runs on `ubuntu-latest` in Release and **does not fetch
  the `.claude/rules/shared` submodule**; `contract` (`:54`) and `plans` (`:92-93`) do. `plans` runs `pin-check.mjs`
  (`:110-111`), which compares the pin with the tip of the ref `.gitmodules` tracks — `branch = release`. So **the day
  E1 is released, every pull request here goes red on `Pin check` until S1 lands.** `Formatted` (`:33-34`) runs
  `dotnet format --verify-no-changes`; on a Windows checkout it reports ENDOFLINE noise from `core.autocrlf`
  (`:29-32`), so run it in Linux or WSL, or rely on CI.
- **Reusable neighbours.** `LocalCpuBudgetTests.cs:20-27` finds the repository root (the folder holding `*.slnx`
  above `AppContext.BaseDirectory`) — S4 extracts it rather than copying it. `RecordingLogger.cs` is the in-repo
  pattern for a captured sink. The C# liveness shape the parent names is
  `dew_flow_benchmark · src/Bench.Infrastructure/Process/WorkerLiveness.cs` (`GetProcessById`; `ArgumentException`
  ⇒ no such process; `Win32Exception` ⇒ refused, not answered).

## Order

| Story | Model (brief · code · review) | Depends on | Lands |
|---|---|---|---|
| S1 — the pin on the E1 release | Opus (this) · main session or Sonnet · Opus | **E1 released** | first, the day the release exists |
| S2 — the C# owned-resource helper and owner liveness | Opus · Sonnet · Opus | S1 | second |
| S3 — the start-of-run sweep, as an assembly fixture | Opus · Sonnet · Opus | S2 | third |
| S4 — the guard, the migration of 21 sites, the end-of-run audit | Opus · Sonnet · Opus | S2, S3 | last |

S2 may be coded while S1 waits, against the rule text cited above; it **merges only after S1**, and if the released
text differs from the sections cited here, stop and report — do not resolve the difference in code. S4 is one story
for the reason E1's S4 was: the guard's test over this repository is red until the migration lands, and two merges
would leave `main` red between them.

## S1 — The pin on the E1 release

- **Goal.** `.claude/rules/shared` points at the release that carries the contract, so `Pin check` is green again and
  the mount offers what S2–S4 read: the `## Tokens` table with the `mcp` row, and `tools/janitor.sh`.
- **Files.** `.claude/rules/shared` (the gitlink only).
- **The change.**
  1. `git -C .claude/rules/shared fetch origin release` and `git -C .claude/rules/shared checkout <the release sha>`
     — the sha `git ls-remote https://github.com/oleksandrdubyna88/dew_flow_conventions.git refs/heads/release`
     prints, never `main`'s.
  2. Read the released `common/test-leftovers.md` and confirm, one by one: the `## Tokens` table has the header
     `| token | family | repository |` and a row whose first cell is `` `mcp` ``; rule §1–§4 and §7 say what *The
     contract this document encodes* cites (the 63-character limit, the owner shape, the sibling `.owner`, the 2-second
     tolerance, the 10-minute age, the exemption grammar); `tools/janitor.sh` exists and takes `--token <t>` (sweeps
     only `testsToDelete-<t>-…`, combinable with `--dry-run`; S4 sc. 9 uses it). A difference is a stop: report it
     to the epic's owner; S2–S4 are briefed against the cited text.
  3. `git add .claude/rules/shared` and one commit:
     `chore(rules): pin shared conventions to release <short sha> — test leftovers: owner, start-of-run sweep, janitor, guard grammar`.
  4. **Re-check S2's constants against the released text, before S2 merges.** With the pin on the release, compare
     each constant S2 writes with the released rule: `RepoToken` = the `## Tokens` row (`mcp`); `Prefix` and the
     `Owned.Pattern` / `Owned.FullName` spelling = rule §1 (prefix, both separators, case-insensitive, id 6–12 of
     `[A-Za-z0-9]`, 63 characters); `LeftoversSweep.MinAge` = rule §4 (10 minutes); `StartToleranceSeconds` and the
     `Owner` format and parse expression = rule §3 (`<host>@<os>:<pid>[:<start>]`, host compared case-insensitively).
     The comparison is a table in S1's PR. A mismatch updates S2 (its constants, scenarios and this document) FIRST —
     S2 does not merge with a constant the released rule does not say.
- **Scenarios.** No behaviour changes here, so no new test; the checks below are the evidence, each recorded in the PR.
- **Commands.** `node .claude/rules/shared/tools/pin-check.mjs` (green after the bump); `node
  .claude/rules/shared/tools/plan-lifecycle.mjs`; `node .claude/rules/shared/tools/gate-snippet-check.mjs`; `node
  .claude/rules/shared/tools/build-flags-check.mjs`; `node .github/scripts/branch-protection.mjs --selftest`;
  `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_mcp.slnx -c Debug -m:4`; the test executable
  (the release may carry other rule changes — every `plans` step must stay green).
- **DoD.** The pin equals the `release` tip; all five `plans` steps green locally and on CI; the facts of step 2 and
  the constants table of step 4 recorded in the PR; nothing else in the diff.

## S2 — The C# owned-resource helper and owner liveness

- **Goal.** One place through which every test here makes a scratch folder: the name from one constant (rule §1), the
  claim beside it (§3), the cleanup registered by construction, exact teardown that never throws (§2), and the
  three-valued liveness answer S3's sweep trusts (§3).
- **Files.** New, all in namespace `Mcp.Tests.Leftovers`, under `tests/Mcp.Tests/Leftovers/`: `Owned.cs`,
  `Liveness.cs`, `OwnedDir.cs`, `UnmatchablePid.cs`; tests `OwnedTests.cs`, `LivenessTests.cs`. Nothing outside the
  folder changes.

**The change.** A block comment at the top of `Owned.cs`, in the voice of the comments around it in this repository:
what the helper owns, why the claim is written BEFORE the folder here (below), why teardown never throws, why the
owner lives beside the folder and never inside it (a test that lists the folder must see exactly what it made).

```csharp
public static class Owned
{
    public const string Prefix = "testsToDelete-";                       // rule §1
    public const string RepoToken = "mcp";                               // rule §1 — the ONLY place the token is written
    public const string OwnedPrefix = Prefix + RepoToken + "-";          // "testsToDelete-mcp-"
    public const int MaxName = 63;                                       // rule §1
    public const int IdLength = 6;
    public const int StartToleranceSeconds = 2;                          // rule §3
    public static readonly Regex Pattern =                               // what the sweep, the audit and the guard match
        new("^teststodelete[-_]" + RepoToken + "[-_]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static readonly Regex FullName =                              // a whole name: rule §1's id is 6–12 of [A-Za-z0-9]
        new("^teststodelete[-_]" + RepoToken + "[-_].+[-_][A-Za-z0-9]{6,12}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string TempRoot();                       // Path.GetTempPath() — the one place a test learns the temp root
    public static string NameFor(string what);             // OwnedPrefix + what + "-" + IdLength chars of [A-Za-z0-9]
    public static void DefaultWarn(string line);           // see "Warnings"
}

public sealed record Owner(string Host, int Pid, long? Start)  // Host = "<hostname>@<linux|macos|windows>"
{
    public static Owner This { get; }                      // computed once
    public string Format();                                // $"{Host}:{Pid}" + (Start is null ? "" : $":{Start}")
    public static Owner? Parse(string text);               // null when the text is not exactly the form (= unreadable)
}

public enum OwnerState { Live, Gone, Unknown }
public sealed record Probe(bool? Alive, long? Start);     // Alive: true | false (no such process) | null (refused/failed)

public static class Liveness
{
    public static Probe ProbeProcess(int pid);                          // the real query, never spawns a process
    public static Probe FromException(Exception error);                 // the pure exception map (below)
    public static OwnerState State(Owner? owner, Func<int, Probe> probe); // rule §3's order
}

public sealed class OwnedDir : IDisposable                // one folder; `using var dir = OwnedDir.Create("x");`
{
    public string Path { get; }
    public static OwnedDir Create(string what, string? parent = null, Action<string>? warn = null);
    internal static OwnedDir Create(string what, string? parent, Action<string>? warn, Func<string> nextName); // test seam: the names to try
    public void Dispose();                                // Removal.Quietly(Path, warn); idempotent; never throws
    internal static IReadOnlyList<OwnedDir> Undisposed(); // the process-wide registry: created, not yet disposed (S4 audit)
}

public sealed class OwnedScope : IDisposable              // many folders, one Dispose — a test class's field
{
    public OwnedScope(Action<string>? warn = null);
    public string Dir(string what, string? parent = null); // OwnedDir.Create + kept; returns its path
    public void Dispose();                                // every folder, last-made-first; never throws
}

internal static class Removal
{
    internal static bool Quietly(string dir, Action<string> warn); // folder, then its sibling claim; never throws
}
```

- **`what`** (rule §1): non-empty; no `/`, `\`, NUL, other control character (`< 0x20`, `0x7f`); not starting with
  `-`; MAY hold `-`, spaces and non-ASCII; `OwnedPrefix.Length + what.Length + 1 + IdLength <= MaxName` — here
  `what` ≤ 38. Invalid ⇒ `ArgumentException` naming the value, before anything exists.
- **The id**: `RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 6)`
  (.NET 8+, unbiased) — never `Path.GetRandomFileName()` (it carries a `.`), never `CreateTempSubdirectory`. The
  helper WRITES 6; every reader of a whole name (`Owned.FullName`) ACCEPTS rule §1's 6–12, so a name made by another
  implementation of the contract with a longer id still reads as one. Selection by the sweep, the audit and the guard
  stays the anchored prefix `Owned.Pattern` (rule §4); `FullName` never narrows it.
- **`Owner.This`**: `Host` = `Dns.GetHostName()` with every run of `@`, `:` or whitespace replaced by `-`, then `@` and
  the tag `windows` | `linux` | `macos` from `OperatingSystem.IsWindows()/IsLinux()/IsMacOS()` — any other platform
  throws `PlatformNotSupportedException` (so the helper refuses to create: no claim, no folder). **Not**
  `Environment.MachineName`: on Windows that is the NetBIOS name, cut at 15 characters, while the janitor reads
  `hostname` (`dew_flow_conventions · tools/janitor.sh:33`) — a long host name would then read as foreign and its
  leftovers would never be collected. `Pid` = `Environment.ProcessId`. `Start` = `Process.GetCurrentProcess().StartTime`
  → `ToUniversalTime()` → `new DateTimeOffset(…).ToUnixTimeSeconds()` — a native read on all three OSes (rule §3:
  *a native process API*), so C# writes the three-field form everywhere, unlike Node on Windows. If that read throws,
  `Start` is `null` (the two-field form); it never throws out of `This`.
- **`Owner.Parse`**: strip ONE trailing `\r?\n`; match `^([^@:\s]+@(linux|macos|windows)):(\d+)(?::(\d+))?$`
  (`dew_flow_conventions · tools/lib/owned.mjs:51`); anything else — empty, `hands off`, `devbox:1:2`,
  `devbox@beos:1:2`, `devbox@linux:x:2`, `devbox@linux:1:2:3`, a pid beyond `int` — ⇒ `null`.
- **`OwnedDir.Create` — why claim first.** The order is rule §2's second form, *claim first, then create*, because .NET
  offers no exclusive `mkdir` but does offer an exclusive file create:
  1. `parent ??= Owned.TempRoot()`; `name = Owned.NameFor(what)`; `dir = Path.Combine(parent, name)`;
     `claim = dir + ".owner"`.
  2. If `Directory.Exists(dir) || File.Exists(dir)`: take a new name (someone's unclaimed folder of that name).
  3. Write the claim: `new FileStream(claim, FileMode.CreateNew, FileAccess.Write, FileShare.None)`, content
     `Owner.This.Format() + "\n"` (UTF-8, no BOM). An `IOException` while `File.Exists(claim)` ⇒ the name is taken,
     take a new one. Any other failure ⇒ throw `InvalidOperationException("owned: could not claim <dir>: <cause>")` —
     nothing was created.
  4. `Directory.CreateDirectory(dir)`. On failure: delete the claim (inside its own `try`), then throw
     `InvalidOperationException("owned: could not create <dir>: <cause>", inner)`.
  5. Return the `OwnedDir`. Steps 2–3 retry at most five names; then throw.
  The caller registers the cleanup on the same statement (`using var` or `OwnedScope.Dir`). Nothing is ever written
  inside `dir`.
- **The registry of undisposed instances.** Step 5 also adds the new `OwnedDir` to one process-wide
  `ConcurrentDictionary<OwnedDir, byte>`; the FIRST `Dispose()` removes it (before the removal is tried, so a removal
  that gives up is reported by its own warning, not twice). `OwnedScope` holds `OwnedDir`s, so its folders are in the
  registry too. `Undisposed()` is a snapshot. It is what lets S4's audit see a folder made under a CUSTOM `parent`
  (outside the temp root), which a scan of the temp root's claims cannot.
- **`Removal.Quietly(dir, warn)`**: up to five tries, 100 ms apart (`Thread.Sleep`), each: if the folder exists, first
  remove every TOP-LEVEL entry whose `Attributes` carry `FileAttributes.ReparsePoint` as a link (a `DirectoryInfo` ⇒
  `Directory.Delete(link)` non-recursive, which removes a junction or a directory symlink and never its target; a
  `FileInfo` ⇒ `File.Delete`), then `Directory.Delete(dir, recursive: true)`. Then the sibling claim the same way
  (`File.Delete`, five tries). Every exception is caught. Giving up ⇒ ONE warning line
  `owned: could not remove <path> (<exception type>: <message>) — left for the start-of-run sweep` and `false`. **The
  folder goes first**: when it cannot be removed its claim stays, so the leftover stays prefixed and claimed (rule
  §2). A path already gone is success.
- **Warnings.** `Owned.DefaultWarn(line)`: inside a test (`TestContext.Current.Test is not null`) ⇒
  `TestContext.Current.AddWarning(line)` — xUnit v3 attaches it to the test result, the test still passes; outside a
  test ⇒ `Console.Error.WriteLine(line)`. This is test-harness output, not a host log (the family's Serilog rule
  governs hosts), and it is the same channel the Node helper uses. Every type takes an `Action<string>? warn` seam so
  tests capture lines instead of reading the console.
- **`OwnedScope`**: a `List<OwnedDir>` behind a lock (xUnit may call a scope from one test only, but a test may use
  `Task.WhenAll`); `Dispose` disposes in reverse order, each in its own `try`, and is idempotent.
- **`Liveness.ProbeProcess(pid)`**: `pid <= 0` ⇒ `Probe(false, null)`. `using var p = Process.GetProcessById(pid)`; any
  exception ⇒ `FromException(e)`. Then the start: `p.StartTime` as above; a `Win32Exception` (access denied — another
  user's process), `NotSupportedException` or any other exception ⇒ `Probe(true, null)` — alive, start unreadable;
  except `InvalidOperationException` (it exited between the two calls) ⇒ `Probe(false, null)`. No `HasExited`, no
  `Process.Start`, nothing that spawns.
- **`Liveness.FromException(e)`** — the pure map, tested directly: `ArgumentException` (no process with that id) ⇒
  `Probe(false, null)`; `InvalidOperationException` (it has exited) ⇒ `Probe(false, null)`; anything else
  (`Win32Exception`, `UnauthorizedAccessException`, `PlatformNotSupportedException`, …) ⇒ `Probe(null, null)` —
  refused or failed, nothing known.
- **`Liveness.State(owner, probe)`** — rule §3's order exactly (`dew_flow_conventions · tools/lib/owned.mjs:234-247`):
  `owner is null` ⇒ `Unknown`; `owner.Host` ≠ `Owner.This.Host` (ordinal, case-insensitive) ⇒ `Unknown`;
  `probe(pid)` — a probe that throws ⇒ `Unknown`; `Alive == false` ⇒ `Gone` (the ONLY `Gone`); `Alive != true` ⇒
  `Unknown`; `owner.Start` or the probe's `Start` null ⇒ `Live`; `|Δ| <= StartToleranceSeconds` ⇒ `Live`; otherwise
  `Unknown` — a reused pid or a stepped clock, never proof of death.
- **`UnmatchablePid`** — a port of `dew_flow_conventions · tools/lib/unmatchable-pid.mjs:32-48`: `Next(i = 0)` = Windows
  `777770 + 4·i` (≡ 2 mod 4 — Windows pids are multiples of 4), Linux `pid_max + 1 + i` (`/proc/sys/kernel/pid_max`,
  else 4194304), macOS `100000 + i`; `AssertNoSuchProcess(pid)` asserts the premise. **A test never takes a dead
  owner from a process it started and killed** — the OS hands that pid to the next process (measured by E1 on Windows,
  one run in fifteen).
- **Analyzers.** `File.SetUnixFileMode` is `[UnsupportedOSPlatform("windows")]`: call it only under
  `if (!OperatingSystem.IsWindows())` or `CA1416` breaks the build. Every async call in a test takes
  `TestContext.Current.CancellationToken`.

**Scenarios** (`OwnedTests.cs`, `LivenessTests.cs`; RED first — write the tests against the signatures above before
the types exist and record the build failure, then stub the types and record each scenario failing on its assertion
before the body is written). Every scenario that counts what a call left behind passes a PRIVATE `parent` (an
`OwnedDir` of its own): the shared temp root is written by other tests at the same moment.

1. **A name carries the prefix, the token, what, and six random characters.** `Owned.NameFor("pg")` matches
   `^testsToDelete-mcp-pg-[A-Za-z0-9]{6}$`; two calls differ; `NameFor("a-b ")` (a separator, a trailing space) is
   accepted; `NameFor(new string('x', 38))` is 63 characters. `Owned.FullName` matches every `NameFor` result, and
   `testsToDelete-mcp-pg-` + an id of 6 and of 12 `[A-Za-z0-9]` characters (and the lowercase `_` spelling); it does
   not match an id of 5 or of 13 characters, or one holding a `.` (`clxdyztb.njt`).
2. **A bad `what` throws before anything exists.** `""`, `"a/b"`, `"a\\b"`, `"-x"`, `"a\u0001b"`, `"a\u007fb"`,
   `"a\0b"`, 39 × `x` ⇒ `ArgumentException`, and the private parent's entry list is unchanged.
3. **A folder and its claim, side by side, never inside.** `OwnedDir.Create("path Unicode пробел", parent)` makes a
   folder whose name starts with `testsToDelete-mcp-path Unicode пробел-`, empty, and a sibling `<folder>.owner` that
   `Owner.Parse`s to `Owner.This`.
4. **Disposal removes the folder and the claim, and is idempotent.** After `Dispose()` neither exists; a second
   `Dispose()` writes no warning. Between `Create` and `Dispose` the instance is in `OwnedDir.Undisposed()`; after the
   first `Dispose` it is not (also when the removal gave up, scenario 6).
5. **Unwind: a test body that throws still leaves nothing.** `var scope = new OwnedScope(); string path = ""; try {
   path = scope.Dir("unwind", parent); throw new InvalidOperationException("the failing body"); } catch
   (InvalidOperationException) { } finally { scope.Dispose(); }` ⇒ neither `path` nor `path + ".owner"` exists, and the
   parent is empty. A second case: three folders from one scope, the third made with the second as its `parent` ⇒
   after `Dispose()` all three and their claims are gone and the `warn` seam got nothing.
6. **Teardown never throws, and what it cannot remove stays prefixed AND claimed.** Windows: hold
   `File.Open(<dir>/held.txt, FileMode.Create, FileAccess.Write, FileShare.None)` inside the folder; POSIX: create
   the folder under a private parent, then `File.SetUnixFileMode(parent, UserRead | UserExecute)`, and
   `Assert.Skip` when `Environment.UserName == "root"` (root ignores it). `Dispose()` returns; the `warn` seam got
   exactly one line starting `owned: could not remove` and ending `left for the start-of-run sweep`; the folder still
   exists, its name still matches `Owned.Pattern`, and `<folder>.owner` still exists. Then release the handle or
   restore the mode and remove both.
7. **A link inside is unlinked, never followed.** A sentinel `OwnedDir` holding `keep.txt`; inside the folder under
   test a top-level directory link to the sentinel AND a second one at depth 2 (`sub/link`) — `Directory.CreateSymbolicLink`,
   falling back on Windows to a junction (`cmd.exe /c mklink /J`, the fallback `WorkspaceToolTests.cs:346-373`
   already uses; `Assert.Skip` when neither can be made). After `Dispose()` the folder is gone and the sentinel's
   `keep.txt` still reads `must survive`.
8. **A claim that cannot be written creates nothing.** A claim file already present for the generated name is a
   taken name (seen through the internal `nextName` seam returning a fixed name first, then a fresh one): the call
   succeeds with the second name and leaves the first claim untouched. A
   `parent` that does not exist ⇒ `InvalidOperationException` starting `owned: could not claim`, and nothing named
   `testsToDelete-mcp-` appears anywhere.
9. **The owner round-trips and refuses everything else.** `Owner.Parse(Owner.This.Format() + "\r\n") == Owner.This`;
   `Owner.This.Start` is not null on Windows and Linux, and is within 1 s of the Unix seconds of
   `Process.GetCurrentProcess().StartTime` read independently in the test; `new Owner("devbox@windows", 37560, null).Format() == "devbox@windows:37560"`;
   `Parse("devbox@linux:4711:1792053744")` ⇒ `("devbox@linux", 4711, 1792053744)`; the bad texts of *Owner.Parse* ⇒
   `null`; `Owner.This.Host` matches `^[^@:\s]+@(linux|macos|windows)$` and starts with `Dns.GetHostName()`.
10. **`State` follows rule §3's order** (injected probe): `null` ⇒ Unknown; host `other@<tag>` ⇒ Unknown; this host
    name with the other OS tag ⇒ Unknown; this host in upper case ⇒ evaluated; `Probe(false, …)` ⇒ Gone;
    `Probe(null, …)`, a probe that throws ⇒ Unknown; alive with either start null ⇒ Live; starts 2 s apart either way ⇒
    Live; 3 s apart either way, and 10⁶ s apart ⇒ Unknown — never Gone.
11. **The real probe.** `State(Owner.This, Liveness.ProbeProcess)` ⇒ Live (through the start comparison);
    `UnmatchablePid.Next()` with the premise asserted before and after ⇒ `ProbeProcess` = `Probe(false, null)` and
    `State` = Gone; `FromException(new ArgumentException())` and `(new InvalidOperationException())` ⇒ `Alive ==
    false`; `(new Win32Exception(5))`, `(new UnauthorizedAccessException())`, `(new IOException())` ⇒ `Alive == null`.

- **Commands.** `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_mcp.slnx -c Debug -m:4`;
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests.exe --filter-namespace "Mcp.Tests.Leftovers"`;
  the whole executable; on Linux (WSL or CI) `./tests/Mcp.Tests/bin/Debug/net10.0/Mcp.Tests --filter-namespace "Mcp.Tests.Leftovers"`.
- **DoD.** Build at 0 warnings; every scenario observed RED, then GREEN, on Windows and on the Linux CI runner
  (scenario 6 runs a different branch on each); the module has no caller yet — S3 and S4 adopt it.

## S3 — The start-of-run sweep, as an assembly fixture

- **Goal.** A run that was killed leaves `testsToDelete-mcp-…` folders its `Dispose` never reached; this repository's
  NEXT run collects them — only its own token's, only when the owner is `gone` or the folder is unmarked and old, and
  never by age alone (rule §4).
- **Files.** New `tests/Mcp.Tests/Leftovers/LeftoversSweep.cs`, `LeftoversSweepFixture.cs` (it carries
  `[assembly: AssemblyFixture(typeof(LeftoversSweepFixture))]`), `LeftoversSweepTests.cs`.

**The change.**

- `LeftoversSweep.Run(string tmp, DateTimeOffset now, TimeSpan minAge, Func<int, Probe> probe) → SweepResult`, where
  `SweepResult(IReadOnlyList<string> Removed, IReadOnlyList<(string Path, string Reason)> Preserved,
  IReadOnlyList<string> Errors)`; `MinAge` = 10 minutes. It never throws: every failure is an `Errors` entry, and a
  failing step does not stop the next. Liveness answers are cached per pid for the call. A port of
  `dew_flow_conventions · tools/lib/leftovers-sweep.mjs:49-57` (decision), `:132-154` (folders), `:156-169` (orphan
  claims), without the worktree pass — this repository makes no worktree.
  1. **Folders.** Depth-1 directories of `tmp` whose NAME matches `Owned.Pattern`. A directory holding a `.git`
     **file** is some clone's linked worktree ⇒ preserved, reason `other clone`, never judged as an unmarked folder
     (rule §4, first bullet). Otherwise the decision table: age = `now − Directory.GetLastWriteTimeUtc` (a folder that
     vanished meanwhile is skipped) `< minAge` ⇒ preserve `young`; no sibling claim ⇒ remove (rule §4: unmarked new-form
     and old is nobody's — S2 never leaves a folder unclaimed); a claim that does not parse, or cannot be read ⇒
     preserve `unreadable`; else `Liveness.State`: `Gone` ⇒ remove, `Live` ⇒ preserve `live`, `Unknown` ⇒ preserve
     `foreign host` when the host differs, else `unknown`. **Remove** = `Removal.Quietly(dir, …)` (links first, then
     the folder, then the claim); `false` ⇒ an `Errors` entry.
  2. **Orphan claims.** Files of `tmp` whose name matches `Owned.Pattern` and ends in `.owner`, whose folder does not
     exist, and whose age is ≥ `minAge` ⇒ deleted (S2's claim-first order makes a young orphan a creation in
     progress — never touched).
  It never touches a name `Owned.Pattern` does not match: not another repository's token, not the bare prefix, not the
  old `mcp-…` names (below).
- `LeftoversSweepFixture` — `public sealed class`, public parameterless constructor (xUnit v3's requirement for an
  assembly fixture, which it creates **before any test of the assembly runs**): calls
  `Run(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess)` inside `try/catch`,
  keeps the result in `public SweepResult LastRun`, and — only when `Removed + Preserved + Errors > 0` — writes ONE
  line `mcp tests: leftovers — removed N, preserved M` plus one line per error through a `report` seam
  (`internal LeftoversSweepFixture(Func<SweepResult> sweep, Action<string> report)` for tests; the public constructor
  passes the real sweep and `Console.Error.WriteLine` — no test is running yet, so `TestContext.AddWarning` has no
  test to attach to). A throwing sweep is one reported line; **the constructor never throws** (rule §4: never fails
  the run). S4 adds the end-of-run audit to this class's `DisposeAsync`.
- **Old names are not swept.** The 17 old prefixes (`mcp-daily-log` … `mcp-workspace`) are not this sweep's business:
  `mcp-` is not a registered token, and other software on a machine makes `mcp-…` temp folders too. A machine that
  still holds them deletes them once by hand, after looking (S4 § *Hand clean-up*). This is rule §4's old-prefix
  clause applied with no window at all, on purpose.

**Scenarios** (`LeftoversSweepTests.cs`; RED first: the tests before `LeftoversSweep` exists, each observed failing).
Every scenario passes `tmp` = a private `OwnedDir` — never the real temp root. Dead owners are `UnmatchablePid`
(premise asserted); "old" is `Directory.SetLastWriteTimeUtc(dir, now - 11 min)` (claims likewise with
`File.SetLastWriteTimeUtc`).

1. **A killed run's folder is collected.** The state a kill leaves, built by hand: an old
   `testsToDelete-mcp-killed-abc123` holding a file and a top-level link (S2 scenario 7's method) to a sentinel
   `OwnedDir` outside `tmp`, with a sibling claim `<this host>@<os>:<unmatchable pid>:1792053744` ⇒ `Run` removes the
   folder and its claim, `Removed` names the folder, and the sentinel's `keep.txt` still exists. RED before the sweep
   exists; it is the leak class this story closes.
2. **Concurrent-owner survival — a live run's folder is never reclaimed, however old.** In one `tmp`, all old except
   where stated: claimed by THIS process ⇒ preserved `live`; claimed by `other@<tag>:1:1` ⇒ `foreign host`; claimed
   by this host name with the other OS tag ⇒ `foreign host`; claim text `hands off` ⇒ `unreadable`; claimed by this
   process's pid with a start 3 s off ⇒ `unknown`; YOUNG and claimed by a dead pid ⇒ `young`; holding a `.git` file
   and no claim ⇒ `other clone`; unmarked ⇒ removed; dead-claimed ⇒ removed. Each preserved entry carries exactly its
   reason, and every preserved folder still exists afterwards.
3. **Anchored and scoped.** Old, dead-claimed or unmarked: `x-testsToDelete-mcp-a-abc123`, `testsToDeletemcp-a-abc123`,
   `testsToDelete-conventions-a-abc123`, `testsToDelete-mcpx-a-abc123`, `mcp-workspace-abc123` all survive untouched
   and appear in no list; `TESTSTODELETE-MCP-a-xyz123` and `teststodelete_mcp_a_xyz123` with a dead claim are removed
   (case-insensitive, both separators).
4. **Orphan claims.** An old `testsToDelete-mcp-gone-abc123.owner` with no folder ⇒ removed; a young one ⇒ kept; an old
   `testsToDelete-other-x-abc123.owner` ⇒ kept.
5. **The sweep never fails the run.** `Run` on a `tmp` that does not exist ⇒ returns with one `Errors` entry, no throw;
   the internal fixture constructor with a sweep that throws ⇒ completes, the `report` seam got the error line, and
   `LastRun` is empty.
6. **Silence when there is nothing.** An empty `tmp` ⇒ an empty result, and the fixture's `report` seam got nothing.
7. **The fixture ran before this test.** A test class taking `LeftoversSweepFixture fixture` in its constructor (xUnit
   v3 injects assembly fixtures) asserts `fixture.LastRun` is not null — the sweep ran at the start of THIS run, over
   the real temp root.

- **Commands.** As S2, `--filter-class "*LeftoversSweepTests"`; then the whole executable twice in a row — the second
  run prints no `leftovers` line on a clean machine.
- **DoD.** Every scenario observed RED, then GREEN, on Windows and Linux; two consecutive full runs print nothing from
  the sweep; the sweep's wall time on a full run is under one second (one directory listing of the temp root —
  record it).

## S4 — The guard, the migration of 21 sites, and the end-of-run audit

- **Goal.** No test here creates a temp path except through `Mcp.Tests.Leftovers` (or under a valid inline
  exemption), a guard keeps it so — red on every class of rule §7, on an unregistered token and on a scan of nothing —
  and a run that leaves a folder it claimed is a red run.
- **Files.** New `tests/Mcp.Tests/Leftovers/LeftoversGuard.cs`, `LeftoversGuardTests.cs`, `LeftoversAudit.cs`,
  `LeftoversAuditTests.cs`; new `tests/Mcp.Tests/RepositoryRoot.cs`; `LocalCpuBudgetTests.cs` (uses it);
  `LeftoversSweepFixture.cs` (gains `DisposeAsync`); the 11 files of the migration table; `Owned.cs` (one exemption
  comment — harness setup, added BEFORE scenario 1's RED run); `.github/workflows/ci.yml`; new
  `research/module_tests.md`; `research/architecture.md`.

**The guard.** `LeftoversGuard.Scan(string repoRoot) → GuardReport(int FilesScanned, IReadOnlyList<Finding> Findings)`,
`Finding(string File, int Line, string Class, string Text)`; pure apart from reading files. A port of the shape of
`dew_flow_conventions · tools/leftovers-check.mjs`, mapped to this repository's languages.

- **Root.** `RepositoryRoot.Path` — `LocalCpuBudgetTests.cs:20-27`'s `FindRoot` moved into
  `internal static class RepositoryRoot` (the folder holding `*.slnx` above `AppContext.BaseDirectory`);
  `LocalCpuBudgetTests` reads it from there. One implementation, not two.
- **Discovery** (file-system enumeration from the root; a directory named `bin`, `obj`, `node_modules`, `.git`,
  `.claude`, `.http-artifacts` or `logs` is never entered — `.claude/rules/shared` is another repository): `tests/**/*.cs`;
  `.github/workflows/*.yml` and `*.yaml`; `.github/scripts/**/*.{mjs,js,cjs,sh,ps1}`; `http/**/*.{mjs,js,cjs}`; every
  `*.sh` and `*.ps1`. **Excluded by name**, a list a test pins to exactly one entry:
  `tests/Mcp.Tests/Leftovers/LeftoversGuardTests.cs` (it carries the violations as fixture strings). Zero files ⇒
  `FilesScanned == 0`, which the real-repository test reports as `scanned nothing under <root> — discovery broke; the
  repository is not clean`.
- **Comments.** A line whose trimmed text starts with `//` or `#` is skipped for matching (doc comments included —
  `LogRetentionTests.cs:79` mentions `Directory.Delete` in one) but read for exemptions. **Block comments** — `/* … */`,
  on one line or over many — are stripped from `.cs`, `.mjs`, `.js` and `.cjs` text BEFORE matching: each character
  inside one is replaced by a space and every newline is kept, so line numbers do not move; code after a closing `*/`
  on the same line is still matched. A `/*` inside a string literal (`"src/**/*.cs"`, a verbatim `@"…"`, a JS
  template) opens nothing — the stripper walks string literals, so a glob in a string cannot swallow the code after
  it. Exemption markers are read from the ORIGINAL text, so `/* testsToDelete-exempt: … */` still counts.
- **Classes** (rule §7; a hit outside a valid exemption is a finding):
  - `raw-temp`: `CreateTempSubdirectory\s*\(`, `GetTempPath2?\s*\(`, `GetTempFileName\s*\(`,
    `GetEnvironmentVariable\(\s*"(TEMP|TMP|TMPDIR)"`, `\bmkdtemp(Sync)?\s*\(`, `\btmpdir\s*\(`,
    `process\.env\.(TEMP|TMP|TMPDIR)\b`, `\btemp_dir\s*\(`; in `.sh` and workflows also `\bmktemp\b`; in `.ps1` also
    `New-TemporaryFile|\$env:TE?MP\b`.
  - `raw-worktree`: `["']worktree["']\s*,\s*["']add["']`, `\bworktree\s+add\b`.
  - `docker-run`: `\bdocker\s+run\b` on a line without `--rm`. **Stricter than the rule on purpose:** the §5 carve-out
    (no `--rm` under an always-run exact-name remover) is NOT recognised here — this repository starts no container,
    and recognising the carve-out means parsing workflow YAML, a dependency this repository does not have. A container
    that needs the carve-out re-opens this guard; until then any `docker run` without `--rm` is red. Recorded as a
    deviation.
  - `docker-name`: `--name[ =]+(\S+)`, `docker\s+(volume|network)\s+create\s+(\S+)`, `WithName\(\s*"([^"]*)"`, and
    `docker\s+build\b.*\s(-t|--tag)[ =]+(\S+)` — the captured name not matching `^teststodelete[-_]` (case-insensitive,
    quotes stripped). This repository builds no image, so every `docker build -t` is a test build.
  - `shared-store`: the four shapes of `dew_flow_conventions · tools/leftovers-check.mjs:131-144`, verbatim and
    case-sensitive (`DROP DATABASE|SCHEMA|TABLE`, `TRUNCATE [TABLE]` against a name not matching the prefix,
    `FLUSHALL|FLUSHDB`, `deleteMany({})`) — the prose word `truncated` never matches.
  - `ryuk-off`: `TESTCONTAINERS_RYUK_DISABLED\s*[:=]\s*["']?true` (case-insensitive) — no exemption accepted.
- **Exemptions — inline only** (rule §7): marker `(?://|#|--|/\*|\*|<!--)\s*testsToDelete-exempt:(.*)$` on the
  finding's line or the line directly above; the rest parsed by `^\s*([^\s—]*)\s*(?:—\s*(.*?))?\s*(?:\*/|-->)?\s*$`
  (`dew_flow_conventions · tools/leftovers-check.mjs:61`, `:105-115`). Valid when the class is one of `raw-temp`,
  `raw-worktree`, `docker-run`, `docker-name`, `shared-store`, and the reason is a reason — ≥ 10 characters, ≥ 3
  words, not matching `^(see above|see below|needed|required|necessary|obvious|todo|tbd|n/?a|because|why|reason|it is
  needed)\b` (case-insensitive; `dew_flow_conventions · tools/ownership-check.mjs:83-91`) — and, for `shared-store`,
  contains `lock:`. Anything else after the marker is a finding of class `exemption` (no class, unknown class,
  `ryuk-off`, no reason, a placeholder).
- **The token is registered** (rule §1): `LeftoversGuard.TokenFindings(string ruleText, string token)` — `token` must
  match `^[a-z0-9]{2,12}$`; the rule must carry a `## Tokens` heading followed by a GFM table whose header is exactly
  `| token | family | repository |`; the token must be the first cell (backticks stripped) of exactly one row; no
  token may appear twice. Any miss ⇒ one finding of class `token` naming the token and the rule path. The real check
  reads `<root>/.claude/rules/shared/common/test-leftovers.md`; **a missing file is a finding** (fail closed: an
  uninitialised submodule registers nothing — run `git submodule update --init .claude/rules/shared`).

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

- `LeftoversAudit.ClaimedBy(string tmp, Owner owner) → IReadOnlyList<string>`: every depth-1 folder of `tmp` matching
  `Owned.Pattern` whose sibling claim parses to exactly `owner` (host, pid and start), plus every such claim whose
  folder is missing.
- `LeftoversAudit.Undisposed() → IReadOnlyList<string>`: the paths of `OwnedDir.Undisposed()` (S2's process-wide
  registry) — every `OwnedDir`/`OwnedScope` folder created and never disposed, **wherever its `parent` was**, including
  a custom parent outside the temp root that the claim scan cannot see.

`LeftoversSweepFixture` implements `IAsyncDisposable`; `DisposeAsync` — which xUnit v3 calls after the last test of the
assembly — first disposes every instance `Undisposed()` names (each through its own `Dispose`, so the registry
empties), then computes `ClaimedBy(Owned.TempRoot(), Owner.This)` and removes each entry with `Removal.Quietly` (so
the machine is clean either way), and, when either list was not empty, throws
`InvalidOperationException("mcp tests: this run left <n> folder(s) it claimed — <paths>; a test made scratch outside a disposed scope")`
(the union of both lists, each path once). A green run's teardown leaves nothing it claimed, so this is the whole-suite
form of the epic's goal sentence, and it catches the one leak the guard cannot see — a helper call whose `OwnedDir`
nobody disposes, under any parent. A run that already failed
may add this line when a failed test left a file handle open on Windows; that is the same failure, named twice.

**CI.** `build-test` gains, before `Build`, the step the other two jobs already have:

```yaml
      - name: Fetch the shared rules
        run: git submodule update --init --depth 1 .claude/rules/shared
```

— the guard's token check reads the mounted rule and fails closed without it.

**Docs.** `research/architecture.md` § CI (`:238-251`): one paragraph — the helper (`tests/Mcp.Tests/Leftovers`, names
`testsToDelete-mcp-<what>-<id>`, a sibling `.owner`), the start-of-run sweep (assembly fixture, owner liveness, 10
minutes), the end-of-run audit, the guard and its classes, the CI submodule step, and the host-wide janitor
`bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` (Git for Windows' bash on Windows, never WSL's);
the test count at `:40` updated to the real number; the module map names `research/module_tests.md`.

**`research/module_tests.md` — new, created in this story.** The shared `common/knowledge-base.md` requires it by
name in every repository (the record `common/scenario-tests.md` point 7 describes), and none exists here yet. The
facts it carries, each true at merge time:

- **Where the harness is** — `tests/Mcp.Tests` (xUnit v3 on Microsoft Testing Platform), and inside it
  `tests/Mcp.Tests/Leftovers/` (helper, sweep, audit, guard).
- **How it is run** — the exact commands, identical to CI's `build-test` job: `dotnet build dew_flow_mcp.slnx -c
  Release` and the test executable (`./tests/Mcp.Tests/bin/Release/net10.0/Mcp.Tests`), never `dotnet test`; locally
  through `node .claude/rules/shared/tools/low-priority.mjs`; `--filter-namespace "Mcp.Tests.Leftovers"` for this
  epic's tests; the janitor command above.
- **The leftovers flows, each `covered` or `not covered`** with the test that covers it: the helper (name, claim
  beside, dispose, unwind, never-throw teardown, links unlinked — S2 sc. 1–8); owner liveness (S2 sc. 9–11); the
  start-of-run sweep (S3 sc. 1–7); the end-of-run audit (both checks, S4 sc. 7, 7a, 8); the guard (S4 sc. 1, 3–6);
  the janitor reading this helper's claim (S4 sc. 9, by hand — `not covered` in CI, with why).
- **What it does not prove** — a SIGKILL of the real executable (the kill is built by hand, S3 sc. 1); macOS (no CI
  leg); the §5 container carve-out (not recognised, the recorded deviation); the guard reads text, so a temp path
  built by reflection or in a generated file is invisible to it; the audit runs only when the executable reaches
  `DisposeAsync`.
- **When it runs** — every pull request (`build-test`), and the janitor by hand on a machine that stopped running
  these tests.

**Hand clean-up**, once per machine, recorded in the PR and not code: list the temp root's folders named exactly
`mcp-<one of the 17 old prefixes>` + 8 characters + `.` + 3 (Windows) or + 6 characters (Linux, macOS), and
`outside.txt` containing `secret`; look at the list; delete only those.

**Scenarios** (RED first: scenario 1 is written and run BEFORE any migration — and AFTER the harness setup: the
`Owned.cs` exemption comment is in place first, so `Owned.TempRoot()`'s own `Path.GetTempPath()` is not counted).

1. **This repository passes the guard.** `Scan(RepositoryRoot.Path)` ⇒ `FilesScanned > 0` and no findings; the
   failure message lists every finding as `file:line — class — text`. **RED before the migration, with the `Owned.cs`
   exemption already in place: exactly 21 `raw-temp` findings in 11 files** (the 19 creation sites and
   `WorkspaceToolTests.cs:243`, `:276`) — the list goes in the PR. A 22nd finding naming `Owned.cs` means the setup
   step was skipped: add the exemption and re-run; it is not the RED.
2. **The escape target stays inside an owned folder.** New test in `WorkspaceToolTests`:
   `The_escape_target_is_written_inside_an_owned_folder_never_into_the_temp_root` — `var (_, root) = Build();` then the
   folder name of `Directory.GetParent(root)` matches `Owned.Pattern`. **RED before the migration** with the real
   symptom (the parent is the temp root itself, the place `outside.txt` was found), GREEN after.
3. **Break-it per class**, each on a tree written at run time into a private `OwnedDir` (files written there, never
   committed): `Directory.CreateTempSubdirectory("x")` in `tests/X/A.cs` ⇒ 1 finding naming line and class;
   `Path.GetTempPath()` ⇒ 1; `Path.GetTempFileName()` ⇒ 1; `Environment.GetEnvironmentVariable("TMPDIR")` ⇒ 1;
   `mktemp -d` in `a.sh` ⇒ 1; `$env:TEMP` in `b.ps1` ⇒ 1; `"worktree", "add"` ⇒ 1 (`raw-worktree`); `git worktree add
   x` in `a.sh` ⇒ 1; `docker run -d img` in a workflow ⇒ 1 (`docker-run`); `docker run --rm --name
   testsToDelete-mcp-x-abc123 img` ⇒ 0; `docker run --rm --name cache img` ⇒ 1 (`docker-name`); `docker volume create
   cache` ⇒ 1; `WithName("pg")` ⇒ 1; `WithName("testsToDelete-mcp-pg-abc123")` ⇒ 0; `TRUNCATE TABLE users` ⇒ 1
   (`shared-store`); the same under `-- testsToDelete-exempt: shared-store — pg:5432 users; reuse intentional; lock:
   pg_advisory_lock(42)` ⇒ 0, and with that reason minus `lock:` ⇒ 1 (`exemption`); `// truncated` ⇒ 0;
   `TESTCONTAINERS_RYUK_DISABLED: "true"` ⇒ 1 even under a well-formed exemption; a `Path.GetTempPath()` under
   `// testsToDelete-exempt: raw-temp — the one place a test learns the temp root` ⇒ 0; `// testsToDelete-exempt:
   raw-temp — needed` ⇒ 1 (`exemption`); `// testsToDelete-exempt: nonsense — a perfectly fine reason here` ⇒ 1; a
   commented-out `// Directory.CreateTempSubdirectory("x")` ⇒ 0; a file under `tests/X/bin/` ⇒ not scanned.
   Block comments: `/* Path.GetTempPath() */` on one line ⇒ 0; a block opened on line 1 and closed on line 3 with
   `Path.GetTempPath()` on line 2 ⇒ 0; `*/ var t = Path.GetTempPath();` closing that block ⇒ 1, reported at ITS line
   (line numbers unmoved); `var glob = "src/**/*.cs"; var t = Path.GetTempPath();` ⇒ 1 (a `/*` inside a string opens
   no comment); `/* testsToDelete-exempt: raw-temp — the one place a test learns the temp root */` above a
   `Path.GetTempPath()` ⇒ 0. RED first: written before the stripper exists, the multi-line case is observed giving 1.
4. **A scan of nothing is red, not clean.** An empty tree ⇒ `FilesScanned == 0`; a root that does not exist ⇒
   `FilesScanned == 0` and no throw.
5. **The token is registered.** On the real mount, `TokenFindings(rule, Owned.RepoToken)` ⇒ none. Break-its on rule
   texts written in the test: `nosuchtoken`, `Mcp`, `x` ⇒ one finding each; no `## Tokens` ⇒ one; a two-column table
   ⇒ one; `mcp` listed twice ⇒ one; `mcp` only in the `repository` column ⇒ one; the rule file missing ⇒ one.
6. **The exclusion list is exactly one file** — pinned.
7. **The audit names only what THIS run claimed.** In a private `tmp`: a folder claimed by `Owner.This` ⇒ listed; one
   claimed by a dead pid, one by `other@<tag>`, one unclaimed, one of another token ⇒ not listed; an orphan claim of
   `Owner.This` ⇒ listed.
7a. **The audit sees an undisposed folder under ANY parent.** RED first (written before the registry exists, observed
   failing): an `OwnedDir.Create("custom", parent: <a private OwnedDir that is NOT under the temp root's depth 1>)` left
   undisposed ⇒ `LeftoversAudit.Undisposed()` names its path, while `ClaimedBy(Owned.TempRoot(), Owner.This)` does
   not; after its `Dispose()` ⇒ `Undisposed()` no longer names it. The test disposes everything it made before it
   ends, so the real audit at the end of the run is not affected.
8. **The audit has teeth** (observed by hand, recorded in the PR, then reverted): remove `: IDisposable` and
   `Dispose` from ONE migrated class (`ToolSurfaceTests`), run the executable ⇒ it exits non-zero with the audit's
   message naming three `testsToDelete-mcp-surface-…` folders; restore ⇒ green, and the temp root holds none of them.
   If xUnit v3 does not fail the run on an assembly-fixture cleanup exception, stop and report — do not turn the audit
   into a warning silently.
9. **No leftover after a full run** (by hand, recorded in the PR): before the migration, one full run leaves N new
   `mcp-…` folders and `outside.txt` in the temp root (count them); after it, zero new entries of either, and
   `bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run`, run from this repository, prints no
   `testsToDelete-mcp-` line.
   **The janitor reads THIS helper's claim** (by hand, recorded, on Windows under Git Bash AND on Linux): plant one
   folder `testsToDelete-mcp-janitor-<id>` in the temp root with a sibling claim whose text is the C# helper's own
   format — `new Owner(Owner.This.Host, UnmatchablePid.Next(), <a start>).Format()`, i.e. this host's DNS name, this
   OS's tag, an unmatchable pid (premise asserted) and a start — and age folder and claim to 11 minutes
   (`Directory.SetLastWriteTimeUtc` / `File.SetLastWriteTimeUtc`, or `touch -d`). Then
   `JANITOR_MIN_AGE_MINUTES=10 bash .claude/rules/shared/tools/janitor.sh --token mcp --dry-run` ⇒ it lists that folder
   for removal. Then remove folder and claim. Both observations — the Windows and the Linux listing line — go in the
   PR. If the janitor does not list it, the owner formats of the two implementations disagree: stop and report to
   E1, never adjust the C# format to pass.

- **Commands.** As S2, `--filter-class "*LeftoversGuardTests"` (RED, then GREEN), `--filter-class "*WorkspaceToolTests"`,
  the whole executable on Windows and Linux; `dotnet format dew_flow_mcp.slnx --verify-no-changes --no-restore` in
  Linux or WSL; `node .claude/rules/shared/tools/plan-lifecycle.mjs`.
- **DoD.** The `Owned.cs` exemption in place before scenario 1's RED; scenarios 1, 2 and 7a observed RED with the
  counts above, then GREEN; every break-it observed red (the block-comment cases included); scenario 8's teeth
  observed and reverted; scenario 9 recorded, the janitor's listing of the planted claim on Windows and on Linux
  included; CI green on all four jobs with the new `build-test` step; `research/module_tests.md` created with every
  flow marked; `architecture.md` updated; this document's status line becomes `> Status: **E3 shipped, <YYYY-MM-DD>; promotes with
  its parent.**` — never the word *implemented* while it sits in `todo/` — and its `todo/README.md` row says the same.
  The parent and this document promote together, after the last epic.

## Growth

Each run creates about 70 folders of a few KB under the temp root, plus one sibling claim each; every one is removed
by its test's `Dispose`, and the end-of-run audit makes a run that leaves one red. A killed run leaves at most those
~70 (< 1 MB); the next run's sweep removes them once 10 minutes old and their owner is gone, and the shared janitor
removes them on any machine. Nothing here grows without an owner.

## What the other epics take from here

The parent copies this helper's SHAPE into the C# tests of E2, E6 and E7 (E5 widens its own existing helper). The
boundary, so the copies need not rediscover it:

| Item | Built here | What a copy does |
|---|---|---|
| The API shape (`Owned`, `Owner`, `Liveness`, `OwnedDir`, `OwnedScope`, `Removal`) | yes | same names; its own `RepoToken` from its own row of the `## Tokens` table |
| Claim first, then create; an own id, never `CreateTempSubdirectory` | yes, measured above | same — the Windows id is the reason, not a preference |
| `Dns.GetHostName()`, never `Environment.MachineName` | yes | same — the janitor reads `hostname` |
| Per-test disposal through `IDisposable` + `OwnedScope` | yes | same; xUnit v3 only |
| Assembly-fixture sweep and end-of-run audit | yes | same, plus the kinds that repository creates (containers, databases) |
| The audit's second check: a process-wide registry of undisposed `OwnedDir`/`OwnedScope` instances | yes | same — it is the only check that sees a folder under a custom parent |
| `Owned.FullName` accepting rule §1's 6–12-character id | yes | same — write 6, accept 6–12 |
| Guard classes | all, with the carve-out NOT recognised | a repository that starts containers implements the §5 carve-out in full |
| Read-only files (git objects) inside a folder | not needed — no repositories are made here | a copy that makes repositories clears the read-only attribute before `Directory.Delete` |

## Plan-gate round 1 (2026-10-17)

The coai plan gate's first round over this document; judged by Opus. Accepted (applied above):

1. S4 sc. 9 plants a `testsToDelete-mcp-janitor-<id>` folder with a claim in the C# helper's own format (this host,
   the OS tag, an unmatchable pid, a start), aged 11 minutes, and records `janitor.sh --token mcp --dry-run` listing
   it, on Windows (Git Bash) and Linux (a duplicate finding of two reviewers, applied once).
2. S4 creates `research/module_tests.md` (harness location, exact commands, the flows covered / not covered, the
   limits) — the knowledge-base rule requires it by name.
3. The `Owned.cs` exemption comment is harness setup, added before sc. 1's RED run, so the RED is exactly 21
   findings in 11 files.
4. S1 re-checks S2's constants (token, prefix, min age, owner format) against the released rule before S2 merges; a
   mismatch updates S2 first.
5. The end-of-run audit also checks a process-wide registry of created-but-undisposed `OwnedDir`/`OwnedScope`
   instances, which covers folders under a custom parent (S2 registry, S4 audit, sc. 7a).
6. The guard strips `/* … */` block comments, multi-line included, before matching, string literals respected
   (S4 *Comments*, sc. 3).
7. `Owned.FullName` accepts rule §1's id of 6–12 `[A-Za-z0-9]`, though the helper writes 6 (S2, sc. 1).

Rejected:

- An xUnit self-test that a throwing assembly-fixture dispose turns the exit code red — already an explicit RED
  observation in S4 sc. 8 (stop and report if it does not).
- Separate CI step wiring for the guard — the guard is a test in the executable; a finding already fails `build-test`.
- Treating a zombie process as `gone` — transient, and keeping it is the safe side; liveness belongs to the E1
  contract, not to this helper.
- A rollback note — a `git revert` of the merge is the normal flow.

Also reflected from the E1 contract, settled: `janitor.sh --token <t>` exists in the release (S1 step 2, S4 docs and
sc. 9).

## Judge's notes on the parent's E3 entry

Opus, 2026-10-16, against `origin/main` b352881 of this repository and the E1 branch of the shared rules.

| # | The entry says | What is so | Resolution here |
|---|---|---|---|
| P1 | 19 sites | 19 creation sites, confirmed — plus 2 `Path.GetTempPath()` sites the rule's `raw-temp` class names | the guard's RED is 21 (S4 sc. 1) |
| P2 | (nothing) | `WorkspaceToolTests.cs:259-260` leaks a fixed-name `outside.txt` into the shared temp root, written concurrently by two theory rows; found on a machine | S4 migration row and sc. 2 |
| P3 | helper, guard, proof | no start-of-run sweep and no owner marker, which rule §3–§4 require of every repository that creates a resource kind | S2 (claim, liveness), S3 (sweep) |
| P4 | `LogRetentionTests.cs:79` ⇒ dispose must tolerate an open handle | the note says POSIX does NOT refuse; Windows does | S2 sc. 6 provokes the refusal per OS |
| P5 | the guard must see the creation under `"\0x"` | the composed path is never created; a line-based guard sees the call regardless | no special case; noted in the facts |
| P6 | (nothing) | `CreateTempSubdirectory` on Windows makes an id with a `.` (measured) and .NET has no exclusive `mkdir` | S2 claims first with `FileMode.CreateNew` and generates the id; recorded for E2/E6/E7 |
| P7 | (nothing) | CI's `build-test` does not fetch the submodule the token check reads | S4 CI step |
| P8 | depends on E1 | the release turns `Pin check` red here until the pin moves | S1 first, the day of the release |
| P9 | RED: a test that creates through the old path and asserts it gone after a failure | in C# that is either reflection over eleven private factories or a compile error, not a red assertion | RED = the guard (21), the `outside.txt` test, the audit's teeth by revert |
| P10 | copied by shape into E2, E5, E6, E7 | E5's own entry widens coai's existing `TempDir.For` instead | the boundary table names E2, E6, E7 |

**Sign-off: yes**, on S1–S4 as written. One judgement is mine and is recorded as a deviation for the promotion: the
guard does not recognise the §5 container carve-out (stricter than the rule; nothing here starts a container).
