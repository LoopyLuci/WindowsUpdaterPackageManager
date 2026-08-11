WUPM telemetry is opt-in. No data is sent unless the user explicitly enables it.

## Opt-in

Set `WUPM_TELEMETRY_ENABLED=true` to enable local structured logging only. Remote sinks are disabled by default; if enabled, they must be configured explicitly by the user or administrator.

## Data collected when enabled

- Operation result: success/failure counts
- Package IDs and versions installed or rolled back
- API endpoint execution counts
- No personal files, file paths, or credentials are logged

## Disabling telemetry

Unset or set `WUPM_TELEMETRY_ENABLED=false` to disable telemetry.
