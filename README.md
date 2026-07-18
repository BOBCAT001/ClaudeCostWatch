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
| **Project breakdown** | Popup table showing daily / weekly / monthly cost per Claude Code project |
| **Start logging...** | Prompts for a task ID, then tracks incremental cost; updates the log every 30 s |
| **Stop logging [ID]** | Finalises the current log entry |
| **Open log file** | Opens the Markdown usage log in your default editor |
| **Set log folder...** | Choose where usage logs are saved |
| **Open Claude JSONL folder** | Browse the raw Claude Code log files |
| **Refresh now** | Re-read logs immediately |
| **Refresh pricing** | Re-fetch model prices from LiteLLM |
| **Start with Windows** | Toggle autostart |

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
