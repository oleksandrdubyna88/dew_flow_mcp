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
- **The tools read the filesystem**, sandboxed to `--root`. Absolute paths, `..` traversal and symlink
  escapes are rejected, and reads are capped in both lines and bytes so one call cannot pull an unbounded
  payload. `--root` is the security boundary: point it at the workspace you mean, never at a home
  directory or a drive root.
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
