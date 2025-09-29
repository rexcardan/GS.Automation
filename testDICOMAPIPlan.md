# Test DICOM API Plan

This plan adds settings management and a Spectre.Console settings UI to configure and validate connectivity to the DICOMAnon Web API using `DAClient.IsClientConnected()`.

## Goals

- Persist application settings: IP address, API key, and port.
- Provide an interactive Spectre.Console UI to view/update settings.
- Validate inputs and test API connectivity via `DAClient.IsClientConnected()`.
- Expose CLI flags for non-interactive configuration and testing.
- Align settings consumption with `DAClientSyncService` used by the export workflow.

## Architecture

- Settings Model: `AppSettings` including `DICOMAnonIPAddress`, `DICOMAnonAPIKey`, `DICOMAnonPort`.
- Settings Service: `IAppSettingsService` with load/save/update semantics.
- Persistence: JSON at a predictable location (e.g., `./settings.json` by default; override via env var or CLI flag).
- Spectre UI: interactive menu to edit values, save, and run connection test.
- Connectivity Test: construct `DAClient` with current settings and call `IsClientConnected()`.

## Data Model

- `AppSettings`
  - `string DICOMAnonIPAddress`
  - `string DICOMAnonAPIKey`
  - `int DICOMAnonPort`
  - (Existing fields maintained; only add missing items as needed.)
- Backward compatibility: if `DICOMAnonPort` missing in an existing file, default to `13997` and save upon next write.

## Settings Persistence

- Default path: `settings.json` in app working directory.
- API: `Load()`, `Save(AppSettings)`, `Get()`, `Update(Action<AppSettings>)`.
- Validation on save: ensure IP/host is non-empty, port in range 1–65535; allow empty API key but warn.
- Simple schema versioning: include a `Version` field to allow future migrations.

## Spectre.Console Settings UI

- Entry command: `settings`.
- Menu options:
  - View current settings
  - Edit IP address
  - Edit API key
  - Edit port
  - Test connection (uses current in-memory values)
  - Save
  - Reset to defaults (confirm)
  - Exit
- UX details:
  - Use `TextPrompt<string>` for IP and API key; `TextPrompt<int>` for port with validators.
  - Show connection test result with clear success/failure markup and details if available.
  - Indicate unsaved changes and prompt to save on exit.

## CLI (Non-Interactive) Options

- Command: `settings` with flags:
  - `--ip <value>`
  - `--key <value>`
  - `--port <value>`
  - `--test` (test connectivity and exit with appropriate code)
  - `--save` (persist supplied values)
- Behavior:
  - If any of `--ip/--key/--port` provided with `--save`, update and write file.
  - If `--test`, instantiate `DAClient` with effective values and print status; return non-zero on failure.

## Connectivity Test Implementation

- Build `DAClient` with `AppSettings.DICOMAnonIPAddress`, `AppSettings.DICOMAnonPort`, `AppSettings.DICOMAnonAPIKey`.
- Call `IsClientConnected()` and present result.
- Optional extra diagnostics on failure: echo endpoint/port and suggest checking firewall or credentials.

## Integration with Existing Plans

- `DAClientSyncService` must consume `DICOMAnonPort` from settings (not a hard-coded constant).
- Selection and worklist export flows rely on `IAppSettingsService` for consistent settings.
- The settings UI becomes the centralized place to configure IP/key/port before selection/export.

## Implementation Steps

1) Extend/Confirm `AppSettings` model with `DICOMAnonPort`.
2) Implement/extend `IAppSettingsService` and `AppSettingsService` for JSON persistence.
3) Add Spectre `settings` command with interactive menu and validators.
4) Add non-interactive flags (`--ip/--key/--port/--test/--save`).
5) Implement connectivity test using `DAClient.IsClientConnected()`.
6) Update `DAClientSyncService` to use `DICOMAnonPort` from settings.
7) Documentation: quick start for configuring and testing connectivity.

## Validation

- Unit tests: settings load/save, defaulting/migration for missing port, validation helpers.
- Manual tests: interactive edits, save prompts, connectivity success/failure scenarios, CLI flags.

## Pseudo-code Snippets

```csharp
// Load settings
var s = settingsService.Load();

// Edit example
var ip = AnsiConsole.Ask<string>("Enter IP address:", s.DICOMAnonIPAddress);
var port = AnsiConsole.Prompt(
    new TextPrompt<int>("Enter port:").Validate(p => p is >=1 and <= 65535 ? ValidationResult.Success() : ValidationResult.Error("Port must be 1-65535"))
).DefaultValue(s.DICOMAnonPort);

// Test connection
var client = new DAClient(ip, port, s.DICOMAnonAPIKey);
var ok = client.IsClientConnected();
AnsiConsole.MarkupLine(ok ? "[green]Connection OK[/]" : "[red]Connection failed[/]");
```

## Milestones

1) Settings model + persistence + tests.
2) Interactive Spectre settings UI.
3) CLI flags and connectivity test.
4) Update DAClientSyncService to use settings port.
5) Docs and manual validation.

