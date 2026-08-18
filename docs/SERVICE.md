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

## GUI plugin execution

- The Plugins tab exposes:
  - **Toggle Selected** — enable/disable a plugin via `/plugins/{name}/toggle`
  - **Execute Selected** — run a plugin command via `/plugins/{name}/execute` and shows the result in `StatusMessage`
- Input/output shape:
  - request: `{ "command": "hello", "args": "" }`
  - response: `{ "command": "hello", "args": "", "output": "..." }`

## Known limitation: in-memory API route tests

- `WupmApiEndpointTests` live tests are gated behind `WUPM_API_TESTS=1`.
- In-memory route tests using `Microsoft.AspNetCore.Mvc.Testing`/`WebApplicationFactory` or bare `WebApplication` hang during startup in this environment.
- The root cause is the same environment-specific ASP.NET Core test-host issue that required removing `WupmApiTests`.
- Workaround: validate `/plugins/{name}/execute` on a machine where the API can bind to port 5002, or rely on the existing plugin execution contract tests.

## Update distribution

- Repo layout uses GitHub release tags: `updates/{packageId}/{version}`.
- The release body is the manifest JSON. Assets are the packaged binaries.
- CLI: `wupm update push`, `wupm update pull`, `wupm self-update`.
- API: `POST /cli/execute` accepts `{ "command": "updates|self-update", "for": "...", "channel": "...", "repo": "..." }`.
- MCP: `updates_list` tool lists updates for a Windows version/channel.
- Manifest fields: `WindowsVersion`, `Architecture`, `PackageId`, `Version`, `Sha256`, `SourceUrl`, `PublishedAt`, `Channels`, `DisplayName`, `BuildNumber`.

## E2E validation

1. Create a test release in the GitHub repo with tag `updates/<packageId>/<version>`.
2. Run `wupm update push --source <pkg> --id <id> --version <ver> --for <winVer> --token <token>`.
3. Verify the release body contains valid manifest JSON.
4. Run `wupm update pull --for <winVer>` and confirm it surfaces the new update.
5. Run `wupm self-update --for <winVer>` and verify it stages an update binary.

## CI

- `.github/workflows/ci.yml` runs on push and PR.
- Steps: `dotnet build` and `dotnet test`.
- Workflow runs on `windows-latest` with `dotnet-version: 10.0.x`.

## Troubleshooting

- **Access denied**: rerun GUI/MCP/API from an elevated shell.
- **Service already exists**: uninstall first or use a unique service name.
- **Port conflicts**: use distinct ports:
  - MCP HTTP: 5000
  - GUI control: 5003
  - WupmApi: 5002
- **Plugin load timeout**: if plugins timeout at 10s, set `WUPM_PLUGIN_LOAD_TIMEOUT_SECONDS` to a higher value.
- **Accidental publish artifacts in git**: `.gitignore` ignores `publish/` and common zip outputs; remove any accidentally committed archives before push.
