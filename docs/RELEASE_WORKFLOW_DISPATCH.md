# Release workflow dispatch

GitHub’s workflow-dispatch/parse cache is stale for `.github/workflows/release.yml`,
so automated CLI dispatch (`gh workflow run ...`) is temporarily blocked with:
`Unrecognized named-value: 'secrets' at line 53`.

Workaround:
- Use the GitHub web UI: Actions → Release → Run workflow → select `v0.4.0`
- Or push a new tag once the cache clears

The workflow file itself validates cleanly with `python -c "import yaml; yaml.safe_load(...)"`.
