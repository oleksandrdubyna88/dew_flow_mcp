# Security

## Reporting a vulnerability

Use **GitHub's private vulnerability reporting** on this repository (Security → Report a vulnerability).
It is private by construction, which a public issue is not.

Please include what you did, what happened, and what you expected. A proof of concept helps; a working
exploit is not required and you are not expected to build one.

**Do not open a public issue for a vulnerability**, and please do not test against a server you do not own.

## What this server is exposed to

Worth stating plainly, because the deployment shape decides most of the answer:

- **The HTTP transport does not authenticate yet.** It is safe on `localhost` and it is not safe on a LAN
  or any routable interface. Do not bind it to one. Authentication is
  [tracked work](todo/PLAN_mcp_product.md), not an oversight, and this note exists so nobody discovers it
  by deploying first.
- **The tools read the filesystem**, sandboxed to `--root`. Absolute paths and `..` traversal are
  rejected lexically. Symlink and junction escapes are rejected by resolving the REAL path of the
  request, then requiring it to lie under the root resolved the same way (a link whose target stays
  inside the root still reads). The resolver works root-down like `realpath(3)`: it takes one segment
  at a time, and when a segment is a link it reads that link's immediate target and puts the target's
  own segments in front of the rest, so the directories INSIDE a target are resolved as well. `.` is
  skipped and `..` goes to the parent of the path resolved so far, never above the filesystem root.
  Once a segment does not exist, nothing below it does: the rest is appended as written, and a `..`
  in that rest is REFUSED (the kernel answers "not found" for `missing/..`, and "not a directory"
  for `file/..`, which is refused as well). One budget of link
  expansions covers the whole resolution: 40, or 32 on macOS, whose kernel stops a chain there with `ELOOP` (a resolver that allowed
  more would accept a chain that the later open then fails; Windows allows 63, so 40 stays the stricter value there). Plain directories
  cost nothing, so deep paths are fine, and a
  link loop reaches the budget at once. A missing, empty or null path, a path with a NUL, or one
  longer than 32 KiB, is refused before any walking (the reader refuses a null or empty path itself,
  not only the tool provider in front of it). Over the budget, over the length cap, or any error while resolving, the read is
  REFUSED. The read then opens exactly the resolved path that was checked, not the spelling it was
  asked with. **Not covered:** a link swapped between the check and the open. The open is by path,
  with no no-follow flag, so a party who can write inside `--root` while a read runs can race it.
  Make `--root` a directory only trusted parties can write to. **Also out of scope:** hard links and
  bind mounts (a hard link is not a link to the resolver; git cannot create either, and on Linux
  `fs.protected_hardlinks` limits who may make a hard link to a file they do not own). On
  case-insensitive filesystems the containment compare is exact-case, so a link target spelled in a
  different case from the root is refused, not read: a false refusal that fails closed.
  Reads are capped in both lines and bytes so one call cannot pull an unbounded payload. `--root` is
  the security boundary: point it at the workspace you mean, never at a home directory or a drive root.
- **Past defects in this guard (dated, so the claim is not read as always true).**
  Until 2026-08-19 the guard was lexical only, and a junction inside the root read straight through
  it; the real-path check and its escape tests closed that. Until 2026-11-01 the resolver kept only
  the final spelling of a link's target and did not resolve the directories inside it. A link inside
  the root whose target ran through a second link of the root (`alias` to `<root>/l2/secret.txt`,
  with `l2` pointing outside the root) passed the prefix check, and the read followed `l2` out of the
  root. It was reproduced on Linux with a symlink to a file and a symlink to a folder below `l2`.
  The same flaw refused valid reads when a link target ran through a symlinked ancestor of the root
  (for example `/var` to `/private/var` on macOS). The single root-down resolver above fixed both, and
  the escape tests (`WorkspaceToolTests`) now cover chained links, relative links, loops, the budget
  and a deep path.
  Also 2026-11-01, found in review of that fix before it shipped: a `..` after a MISSING segment was
  popped textually and the segments after it were never checked for links, so `a` to
  `nonexistent/../evil` (with `evil` linked to a file outside the root) and `a2` to `zz/..` read
  the outside file. The kernel answers "not found" there, so the resolver now refuses a `..` after a
  missing segment, or after an existing file. The same review closed a slow path (a 200,000-segment request took 36 s), a NUL in
  the request (a tool failure, now a refusal) and a Windows link target `\x` that was read against
  the process's drive instead of the link's.
- **There are no editing tools here.** The public surface reads. That is a product boundary and also a
  security property, and it is asserted by a test.
- **Telemetry is opt-in.** Without `--spool <path>` nothing is written. When it is on, call arguments are
  recorded to a local file under a byte budget — so a directory you point it at inherits whatever
  sensitivity the arguments carry.

## Supported versions

Pre-1.0 in practice — see [VERSIONING.md](VERSIONING.md). Fixes land on `main`; there are no maintained
release branches, so the supported version is the current one.

## What we will do

Acknowledge the report, tell you whether we can reproduce it, and say what the fix is and when it lands.
If we decide something is not a vulnerability, we will say why rather than close it silently. Credit in the
release note if you want it, and no credit if you would rather not.
