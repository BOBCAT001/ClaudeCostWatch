# ClaudeCostWatch

An unofficial Windows system tray app that monitors [Claude Code](https://claude.ai/code) costs in real time by reading its local JSONL usage logs.

> **Not affiliated with or endorsed by Anthropic.**

## Features

- **Live cost tracking** — today / this week / this month, visible on hover
- **Per-project breakdown** — popup table with daily, weekly, and monthly cost per project
- **Task logging** — track cost for a specific task (Jira ticket, GitHub issue, etc.) and write a Markdown usage log, updated live every 30 seconds
- **Pro/Max plan detection** — reads `~/.claude/.credentials.json` and labels costs as API-equivalent estimates when on a subscription plan
- **Auto-refreshed pricing** — pulls model prices from [LiteLLM](https://github.com/BerriAI/litellm), cached locally and refreshed daily
- **Start with Windows** — optional autostart via registry

## Requirements

- Windows 10 or 11 (x64)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) installed and used at least once (logs are read from `~\.claude\projects\`)
- No .NET runtime required — self-contained single executable

## Installation

1. Go to the [Releases](../../releases/latest) page and download `ClaudeCostWatch.exe`
2. Run it — it will appear in your system tray

> **Windows SmartScreen** will likely warn you the first time ("Windows protected your PC"). Click **More info → Run anyway**. This is expected for unsigned executables distributed outside the Microsoft Store.

## Usage

Hover the tray icon to see your current costs. Right-click for the context menu.

### Context menu

| Item | Description |
|------|-------------|
  | **Project breakdown** | Opens a popup table showing Today / Week / Month cost per Claude Code project |
  | **Reports...** | Opens the historical reports window with tabs for cost by day, week, project, and task |
  | **Start logging...** | Prompts for a task ID and begins tracking incremental cost for that task |
  | **Stop logging [ID]** | Finalises the current task log entry and writes a summary to the log file |
  | **Open log file** | Opens `usage_log.md` in your default editor |
  | **Set log folder...** | Choose the folder where `usage_log.md` is written |
  | **Open Claude JSONL folder** | Opens `~\.claude\projects\` in Explorer to browse raw Claude Code log files |
  | **Refresh now** | Forces an immediate rescan of all JSONL log files |
  | **Refresh pricing** | Re-fetches model pricing data from LiteLLM and rescans logs |
  | **Edit model rates...** | Opens a dialog to override per-model pricing; overrides take precedence over LiteLLM rates |
  | **Set daily limit...** | Opens a dialog to set a daily spending threshold; a balloon alert fires when it is exceeded |
  | **Clear daily limit** | Removes the daily spending threshold (greyed out when no limit is set) |
  | **Start with Windows** | Toggles the autostart registry entry so ClaudeCostWatch launches at login |
  | **Visit Project Homepage** | Opens the project page in your default browser |
  | **Close Application** | Stops logging if active, removes the tray icon, and exits |
  
### Usage logging

Start logging before you begin a task, enter a task ID (e.g. `PROJ-123` or `#42`), then stop when you're done. A Markdown file (`usage_log.md`) in your chosen log folder records each session with a per-project cost breakdown.

### Pro / Max plans

If you use a Claude Pro or Max subscription, costs are **API-equivalent estimates only** and do not reflect your actual subscription fee. The tray tooltip and breakdown window label them accordingly with a `~` prefix.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8).

```
git clone <repo-url>
cd ClaudeCostWatch
dotnet publish src/ClaudeCostWatch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:DebugType=none -o artifacts/
```

The output is a single `artifacts/ClaudeCostWatch.exe`.

## Disclaimer

Cost estimates are calculated from [LiteLLM pricing data](https://github.com/BerriAI/litellm/blob/main/model_prices_and_context_window.json) and may not match your actual Anthropic bill. This project is not affiliated with or endorsed by Anthropic.
