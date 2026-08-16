# `telemetry/v0` — the spool line this server emits

> Status: **as implemented, 2026-08-15.** The schema is OWNED by `dew_flow_benchmark`, which ingests
> it; this document records what the emitter here actually writes, so the two halves can be compared
> without reading each other's code. Emitter: [`src/Mcp.Telemetry`](../src/Mcp.Telemetry).
>
> Design record: [PLAN_usage_telemetry.md](PLAN_usage_telemetry.md).

## Where it lands

```
{spoolRoot}/{yyyy-MM-dd}/{app}-{HH-mm-ss}-{pid}.jsonl
```

UTC throughout, a folder per day and **a file per run** — the same shape as the logging rule, for the
same reason: the question asked of a spool is always "hand me what this host produced", and a file
shared across runs cannot be handed over while it is being written.

One JSON object per line. A half-written file loses one call, never the file.

Turned on per host by naming a directory (`--spool <path>` on the standalone host). Unset ⇒
`NullUsageSink` stays and nothing is written: telemetry is something an operator switches on, not
something that appears because a package was referenced.

## One line

```jsonc
{
  "schema": "telemetry/v0",
  "at": "2026-08-15T09:30:01+00:00",
  "emitter": { "app": "mcp-stdio", "pid": 1234, "machine": "WORKSTATION" },
  "caller": {
    "clientName":    { "captured": true,  "value": "claude-code", "reason": "" },
    "clientVersion": { "captured": true,  "value": "2.0.0",       "reason": "" },
    "model":         { "captured": false, "value": "",            "reason": "the MCP protocol carries no model identity for the caller" },
    "transport": "stdio"
  },
  "correlation": {
    "leg":   { "captured": true, "value": "cell-17", "reason": "" },
    "phase": { "captured": true, "value": "Verify",  "reason": "" }
  },
  "tool": "rt_read_local_file",
  "scope": "D:/work/repo",
  "argumentsJson": "{\"path\":\"a.txt\"}",
  "argumentsTruncatedBytes": 0,
  "outcome": "answered",
  "error": "",
  "responseChars": 42,
  "responseBody": "lines 1-3 of 3\nalpha",
  "responseTruncatedBytes": 0,
  "tokens": { "captured": false, "value": 0, "reason": "this surface does not count tokens" },
  "serverMs": 13.4
}
```

`correlation` arrived **2026-08-16, additively within v0** — no version bump, because the consumer was
built defaulting an absent object to unattributed and documents that it must. Every line an emitter
wrote before that date still reads, as unattributed, which is what it truthfully is.

## The six things a consumer must not get wrong

1. **`outcome` has three values** — `answered` · `refused` · `error` — spelled out on the wire rather
   than taken from an enum's `ToString`, because they are a published vocabulary. A refusal is a guard
   that worked (a path outside the sandbox, a missing argument); an error is something that broke.
   Merging them makes both uncountable. On the MCP wire both still set `isError`: the protocol has one
   error state, and this server does not invent a second.
2. **`captured` ships even when true.** A consumer must never infer "unknown" from an absent field.
   `{captured: false}` carries the `reason` and an empty `value` — rendering that as `0` or as the
   popular answer turns a gap in instrumentation into a claim about the caller.
3. **`model` is `captured: false` for every real session, and that is correct.** No MCP revision tells
   a server which model drives a session. It is populated only when the caller is the in-process
   bridge and the host named its model (`AddLocalLlmToolBridge("qwen3-coder")`) — which is how a
   benchmark leg attributes its own traffic.
4. **`responseChars` is always exact; `responseBody` may be cut.** Sizes are never budgeted, payloads
   always are (4 KB each by default), and `*TruncatedBytes` says precisely how much went. The budget
   is applied at emit, so retention is a property of the schema rather than of a later clean-up job.
5. **`serverMs` is server-side processing, not the caller's latency.** It excludes transport and
   excludes any wait for an accelerator — that belongs in the consumer's infrastructure-wait bucket,
   and folding it in here would make a busy card read as a slow tool.
6. **`correlation` is what the CALLER declared, never what this server inferred.** An MCP server has no
   idea what a harness leg is; the value comes from `--correlation <leg[/phase]>` and is stamped
   unchanged, case included. Two consequences a consumer must hold:
   - **Unattributed is the normal reading.** Every real session declares nothing, so `leg` is
     `captured: false` with the reason `"the caller declared no leg"` — deliberately the SAME string a
     consumer substitutes for a line that carries no `correlation` object at all, so "predates the
     field" and "declared nothing" do not read as two different facts.
   - **It is honest only for a process serving ONE unit of work.** The flag is refused at startup on
     the HTTP transport: one value stamped across concurrent callers would invent an attribution, and
     an invented one cannot be told from a correct one by any report downstream.

## What is not in v0

- **Tokens.** No tool on this surface counts them; the field exists so a surface that does (an
  embedding or reranking tool) can fill it without a schema change, and reads as *not captured* until
  one does.
- **Authentication identity.** The HTTP transport does not authenticate yet
  ([todo/PLAN_mcp_product.md](../todo/PLAN_mcp_product.md) Phase 2), so there is no authenticated
  principal to record. When there is, it joins `caller`.
- **A version negotiation.** v0 is stamped, not negotiated. A consumer meeting a version it does not
  know must refuse that line by name rather than guess at it.

## Backpressure and failure

The sink hands each record to a bounded channel and returns; a background writer does the IO. A tool
call is never blocked, delayed, or failed by telemetry.

When the channel is full the record is **dropped and counted** (`SpoolUsageSink.Dropped`, logged once
at shutdown). The obvious-looking `BoundedChannelFullMode.DropWrite` was tried first and is a trap: it
reports success while discarding the record, so the counter stays at zero — measured at capacity 1,
**500 records recorded, 2 written, 0 counted**. `Wait` paired with `TryWrite` (which refuses rather
than waits) is what makes "either written or counted" true.

A spool directory that cannot be written trips a breaker: one error log, the run's recording stops,
and every subsequent record counts as dropped. A failing disk will not start working because we logged
about it once per call.
