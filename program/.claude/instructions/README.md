# program/ — Claude instructions index

Reference docs for the `BotReport2026` C# WinForms app (the compiled
implementation of the SBR report generator described in the repo-root
[CLAUDE.md](../../CLAUDE.md)). Read these before making changes under
`program/BotReport2026/`.

- [01-architecture.md](01-architecture.md) — project layout, control flow, how to add a rule.
- [02-detection-rules.md](02-detection-rules.md) — exact logic for Rules 101, 202, 203, 206, 208, 209, 212, 301.
- [03-data-formats.md](03-data-formats.md) — RSP/Transaction Report/lookup file column mappings and parsing behavior.
- [04-configuration.md](04-configuration.md) — `AppConfig` fields, defaults, `config.json`, fixed repo-relative paths.
- [05-build-and-run.md](05-build-and-run.md) — dotnet build/run/publish commands.
- [06-ui-workflow.md](06-ui-workflow.md) — the three-tab UI and the end-to-end user flow.

These describe the current implementation as read from source (accurate as of
2026-08-02). If the code changes, update the relevant doc in the same change —
these are not auto-generated.

For end-user documentation (how to run the app monthly, not how it's built),
see [../../.claude/usermanuals/](../../.claude/usermanuals/).
