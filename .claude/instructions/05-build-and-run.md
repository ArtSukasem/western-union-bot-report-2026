# Build, Run, Publish

Target framework: `net8.0-windows` (WinForms, `OutputType=WinExe`). Solution file:
[BotReport2026.slnx](../BotReport2026.slnx). Project file:
[BotReport2026.csproj](../BotReport2026/BotReport2026.csproj). Current version:
`0.1.0` (`<Version>` in the csproj).

## Build / run (dev)

```bash
dotnet build "program/BotReport2026/BotReport2026.csproj"
dotnet run --project "program/BotReport2026/BotReport2026.csproj"
```

Requires the Windows Desktop runtime (WinForms) — this will not build/run on
non-Windows platforms.

## Publish (self-contained win-x64)

Uses the checked-in profile
[FolderProfile.pubxml](../BotReport2026/Properties/PublishProfiles/FolderProfile.pubxml):
self-contained, `win-x64`, not single-file, no ReadyToRun. Output goes to
`bin\Release\net8.0-windows\publish\win-x64\`.

```bash
dotnet publish "program/BotReport2026/BotReport2026.csproj" -c Release -p:PublishProfile=FolderProfile
```

The `publish/` folder at `program/BotReport2026/publish/` (checked into this
working tree, alongside `EPPlus.dll`, `Newtonsoft.Json.dll`, etc.) is an example
of that publish output. `Releases/` at the repo root holds zipped release builds
(e.g. `Bot Report v1.0_20260616.zip`).

## Repo-root path dependency

The app assumes it's running from inside the checked-out repo — see
[04-configuration.md](04-configuration.md) for how `MainForm.RepoRoot` walks up
from the build output directory to find the input folders, `lookups/`, `mapper/`, and
`report-results/`. **Do not** run the published exe from a location outside the
repo (or a copy that doesn't preserve the same relative folder depth) without
first checking that path math still resolves correctly — it isn't overridable
via config or command-line args.

## First run

On first launch (no `config.json` present), `ConfigManager.Load()` returns
`AppConfig` defaults. `MainForm.EnsureFolders()` creates `input-rsp-inbound/`,
`input-rsp-outbound/`, `input-transaction-report/`, `lookups/`, `mapper/`, and
`report-results/` if missing, so the user has somewhere to drop files even on a
fresh checkout.
