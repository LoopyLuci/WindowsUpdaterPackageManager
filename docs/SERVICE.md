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
- Plugin loading is timeout-protected by default to avoid startup hangs.
  - Default per-plugin timeout: 10s.
  - Override with `WUPM_PLUGIN_LOAD_TIMEOUT_SECONDS` if a plugin needs longer initialization.
- Plugin assembly resolution falls back to `AppContext.BaseDirectory` for dependencies.

## Plugin execution

- `IPlugin.ExecuteAsync(command, args)` is the standard execution contract.
- `/plugins/{name}/execute` accepts `{ "command": "...", "args": "..." }` and returns `{ command, args, output }`.
- Plugins should return `null` for unknown commands and throw only for unrecoverable errors.

## Conditional API tests

- Live endpoint tests in `WupmApiEndpointTests` are skipped by default.
- Run them only when:
  - `WUPM_API_TESTS=1`
  - port 5002 is available for binding
- Example: `WUPM_API_TESTS=1 dotnet test -c Release --filter "FullyQualifiedName~WupmApiEndpointTests"`

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
- **Plugin load timeout**: if plugins timeout at 10s, set `WUPM_PLUGIN_LOAD_TIMEOUT_SECONDS` to a higher value.
- **Accidental publish artifacts in git**: `.gitignore` ignores `publish/` and common zip outputs; remove any accidentally committed archives before push.
