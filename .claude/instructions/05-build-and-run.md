# Build, Run, Publish

Target framework: `net8.0-windows` (WinForms, `OutputType=WinExe`). Solution file:
[BotReport2026.slnx](../BotReport2026.slnx). Project file:
[BotReport2026.csproj](../BotReport2026/BotReport2026.csproj). Current version:
`0.2.0` (`<Version>` in the csproj).

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

## Working folder

The published exe can live anywhere: all input, lookup and result folders hang off
the **working folder** shown at the top of the window, which the user browses to and
which is remembered in `workspace.json` beside the exe (see
[04-configuration.md](04-configuration.md) for the resolution order and the
sub-folder layout). A dev build still defaults to the repo checkout it was built
inside, so nothing changes when running from `bin/Debug/`.

## First run

On first launch (no `config.json` present), `ConfigManager.Load()` returns
`AppConfig` defaults. With no `workspace.json` either, the working folder is the
repo checkout when running from a dev build, otherwise `bot-report-files/` beside
the exe. `Workspace.EnsureFolders()` then creates `input-rsp-inbound/`,
`input-rsp-outbound/`, `input-transaction-report/`, `lookups/`, `mapper/`,
`SAE-lookups/` and `report-results/` inside it, so the user has somewhere to drop
files even on a fresh install. The reference files themselves (sanction list,
occupation mapper, SAE lookups) still have to be put there — switching to a folder
that has none of them offers to copy them from the folder previously in use.
