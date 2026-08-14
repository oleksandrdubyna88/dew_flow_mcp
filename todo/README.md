# todo/

Plans for work that is **not finished**. Documentation of the system as it *is* belongs in `research/`.

The test is one question: **is someone still supposed to build this?** If yes it lives here; once it ships,
the plan moves to `research/` with its status changed to `IMPLEMENTED <date>` and its deviations recorded.

Every plan starts with a status line on line 2–3 and carries: the symptom or goal before any solution,
references to real code as `file.cs:line` (verified, not guessed), a build order, a test plan, and a
Definition of Done.

## Currently open

| plan | status | scope |
|---|---|---|
| [PLAN_mcp_product.md](PLAN_mcp_product.md) | inversion and both transports built and parity-tested; the tool set is one placeholder | the three tool families, auth, usage metering, public-repository hygiene |

## This repository is public

Two consequences that shape every plan here:

- It must not know that retrieval exists. `IToolProvider` is declared here and implemented outside; the
  arrow points inward only, and a test in the RAG repo checks it from the other side too.
- Editing tools stay out. The public surface reads; the write path lives in the private product.

## Sibling repositories

- `dew_flow_rag_qln` — the retrieval product that implements these tools (private)
- `dew_flow_sidecar_rust` — the embedding engine underneath (public)
