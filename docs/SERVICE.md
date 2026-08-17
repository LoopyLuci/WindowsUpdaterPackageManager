# Windows Service Wrapper

WUPM can run as a Windows service via `sc.exe create` or `nssm install`. This document covers exact requirements, failure modes, and elevation behavior.

## Requirements

- Windows 10/11 Pro or Enterprise recommended.
- .NET 10 runtime for self-contained service deployment.
- Service actions require **administrator rights**:
  - install, start, stop, uninstall
  - privileged Windows Update scans
  - offline image servicing
  - `/windows-update` install/scan operations

## Elevation behavior

- If the process is **not elevated**:
  - `/service/install` returns an error and the GUI Settings tab shows a clear elevation message.
  - `/service/status` returns `{ installed: false, elevated: false, message: "Not running as administrator; service operations require elevation" }`.
  - `/windows-update` endpoints return 403/unauthorized when the caller is not elevated.
- If the process **is elevated**:
  - install/start/stop/uninstall are allowed.
  - GUI and MCP callers must still handle `UnauthorizedAccessException` from Windows APIs.

## Plugin loading

- `/plugins` returns plugin metadata from `PluginManager.Plugins`.
- Plugin loading is timeout-protected (10s per plugin) to avoid startup hangs.
- Plugin assembly resolution falls back to `AppContext.BaseDirectory` for dependencies.

## Non-admin fallback

- Cache prune, marketplace search, audit history, plugin toggle, and `/plugins` do **not** require admin.
- Service install/uninstall buttons in Settings are disabled when elevation is missing.

## Troubleshooting

- **Access denied**: rerun GUI/MCP/API from an elevated shell.
- **Service already exists**: uninstall first or use a unique service name.
- **Port conflicts**: use distinct ports:
  - MCP HTTP: 5000
  - GUI control: 5003
  - WupmApi: 5002
- **Plugin load timeout**: check `AppContext.BaseDirectory/plugins-debug.log` for plugin initialization errors.
