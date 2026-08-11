# WUPM Repository Schema Releases

## 1.0

- Initial stable schema.
- Fields: `schemaVersion`, `generatedAt`, `repositoryUrl`, `packages[]`.
- Package manifest requires: `id`, `version`, `sha256`, `created`.

## 2.0 (planned)

- Introduces `deltas[]`, `drivers[]`, `categories[]`, and `minimumClientVersion`.
- Backward compatibility rule: clients MUST reject `schemaVersion` `> supported maximum` and SHOULD offer upgrade guidance.

## Migration rules

1. Additive changes require minor `schemaVersion` bump and `index.schema.json` update.
2. Removal/rename requires major bump and documented migration path in this file.
3. Unknown `schemaVersion` values are rejected by WUPM parser.
4. Repository authors MUST validate against `index.schema.json` before publishing.
