# Contributing

**This repository does not accept outside pull requests.** It is proprietary (see [LICENSE](LICENSE)), and
merging a contribution without a signed agreement would leave part of a work we sell owned by someone else.
Turning that away at the door is more honest than reviewing a patch we could not merge.

## What is welcome

**Issues.** A bug report, a tool whose description misled your agent, a schema that does not say what it
means — those are useful precisely because we cannot reproduce them from here. A tool description is a
measured artefact in this product, so "the model consistently misread this parameter" is a defect report,
not a preference.

**Security reports**, through [SECURITY.md](SECURITY.md) rather than an issue.

## If you have a commercial agreement

Work follows the conventions in [.claude/rules/](.claude/rules/), which are not advisory:

- **Plans first** for non-trivial work, in `todo/`, promoted to `research/` when they ship.
- **A bug fix starts with a failing test** that names the guarantee, observed failing for the real symptom
  before the fix exists. A test that goes red for a setup error proves nothing.
- **Reuse before writing.** A second implementation of a capability is a defect from the moment it
  compiles, because the two will drift and nothing will notice.
- **`research/` is documentation of what runs.** If a sentence there describes something that does not, it
  is a bug in that file.

Run the tests as an executable, never `dotnet test` — see [README.md](README.md#build-and-test).

## The one boundary that is not negotiable

**No editing tools, and no knowledge of retrieval.** This repository reads; the write path stays in the
private product, and `IToolProvider` is declared here and implemented outside so the dependency arrow only
ever points inward. Both are checked by tests rather than by review. A change that crosses either is not a
change to review — it is a change to the product's shape, and it belongs in a plan first.
