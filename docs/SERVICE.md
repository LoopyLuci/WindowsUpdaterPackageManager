# Windows Service Wrapper

WUPM can run as a Windows service via `sc.exe create` or `nssm install`. This document covers exact requirements, failure modes, and elevation behavior.

## Requirements

- Windows 10/11 Pro or Enterprise recommended.
- .NET 10 runtime for self-contained service deployment.
- Service actions require **administrator rights**:
  - install, start, stop, uninstall
  - privileged Windows Update scans
  - offline image servicing

## Elevation behavior

- If the process is **not elevated**:
  - `/service/install` returns an error and the GUI Settings tab shows a clear elevation message.
  - `/service/status` returns `{ installed: false, elevated: false, message: "Not running as administrator; service operations require elevation" }`.
- If the process **is elevated**:
  - install/start/stop/uninstall are allowed.
  - GUI and MCP callers must still handle `UnauthorizedAccessException` from Windows APIs.

## Non-admin fallback

- Cache prune, marketplace search, audit history, and plugin toggle do **not** require admin.
- Service install/uninstall buttons in Settings are disabled when elevation is missing.

## Troubleshooting

- **Access denied**: rerun GUI/MCP/API from an elevated shell.
- **Service already exists**: uninstall first or use a unique service name.
- **Port conflicts**: use distinct ports:
  - MCP HTTP: 5000
  - GUI control: 5003
  - WupmApi: 5002
