# Versioning and releases

**A tool schema is a contract with a machine that cannot ask what you meant.** A human reading a changed
parameter description re-reads the call site; an agent re-reads nothing. It keeps calling the tool the way
the old description taught it, and the calls stay syntactically valid — so the failure is not an error, it
is worse answers, arriving indefinitely, with nothing in any log saying why. That is the whole reason this
file exists.

## The number

`MAJOR.MINOR.PATCH`, SemVer, declared in `Directory.Build.props`. Builds carry the commit as build
metadata, so `--print-surface` reports something like:

```
1.0.0+d6e7de06792acea95b768341959cba86c0572e98
```

The suffix comes from the SDK's source-link stamp and is not part of the version's meaning — it answers
"exactly which build is this", which is a different question from "what does it promise".

## What counts as breaking, for a tool surface

The usual list applies (a tool removed, a required parameter added, a parameter's type changed), and it is
not the interesting half. **These are MAJOR too, and each one leaves every call compiling:**

| Change | Why it breaks a caller that cannot read a changelog |
|---|---|
| A parameter's **meaning** changes | The measured case: a limit documented as a payload knob was actually a recall setting. Redefining it silently re-tunes every agent that learned the old reading. |
| A default value changes | The caller that omitted the argument was relying on the old default. Omission is a choice. |
| A tool is **renamed** | An agent's learned routing is by name. A rename with an alias is MINOR; without one it is a removal. |
| A description is rewritten to describe **different behaviour** | Wording is the interface. Reworded prose that says the same thing is PATCH; prose that says something else is not. |

MINOR: a new tool, a new optional parameter, a description clarified without changing what it says the
tool does. PATCH: fixes, performance, prose that reads better and means the same.

**When it is genuinely unclear whether a rewrite changed the meaning, it is MAJOR.** The cost of being
wrong is asymmetric — a needless major version costs a conversation, a missed one costs a customer's agent
quietly calling the wrong thing.

## The surface fingerprint is the detector

Nobody should have to eyeball this. Every build can state its own surface, and the hashes are what make a
change visible:

```bash
dotnet run --project src/Mcp.Host -- --print-surface
curl localhost:5000/api/mcp/surface        # the same answer, from a running server
```

`toolsHash` covers the names and their schemas; `descriptionsHash` covers the served description text.
A release whose `toolsHash` moved and whose MAJOR did not is a release to stop and look at. A moved
`descriptionsHash` is the subtler one, because it is the case a type system cannot see at all — a diff of
description text is a diff of the contract.

Pin the fingerprint of a release and compare the next one against it. That is a CI assertion, not a
ceremony: `--print-surface` needs no port and exits, which is what makes it usable as one.

## Description sets are not versions

`--descriptions <dir> --description-set <name>` exists so a description can be measured as an experiment
arm without rebuilding the binary. **An arm is not a release.** Two servers on the same version can serve
different text on purpose, and `descriptionsHash` is how a measurement says which one produced a number.

When an arm wins and becomes the default, the compiled literal changes — and *that* is the release, versioned
by the table above.

## Releasing

1. Decide the bump against the table. If the surface moved, say so in the release note in the caller's
   terms — "`limit` now defaults to 20 instead of 5, which changes recall" — never in ours.
2. Set `<Version>` in `Directory.Build.props`. It is explicit rather than inherited: an unset version is
   silently `1.0.0` forever, which is a promise nobody made and nobody can rely on.
3. Tag the commit `v<version>`.
4. Record the release's `--print-surface` output. It is the evidence for the next comparison, and there is
   no other artefact that captures what a build actually advertised.

## Pre-1.0

The surface is one tool today and the three families in
[todo/PLAN_mcp_product.md](todo/PLAN_mcp_product.md) will replace it wholesale. Until that lands, the
version stays `1.x` in name only and **no compatibility is promised to anyone outside this product**.
Said plainly rather than implied by a low number: `0.x` would have said it better, and `1.0.0` was the
SDK's default rather than a claim — the first deliberate version is the one that ships the families.
