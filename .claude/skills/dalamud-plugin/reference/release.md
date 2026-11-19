# Releasing: repo.json, GitHub releases, CI

v1.0.0.0 and v1.0.1.0 shipped with a single-repo flow; since the move to a shared `EvolveXIV/releases`
repository the workflow updates `repo.json` there instead. That two-repo flow is **(not yet run)**.

## How users install

`/xlsettings` → Experimental → Custom Plugin Repositories → add
`https://raw.githubusercontent.com/EvolveXIV/releases/main/repo.json`, tick Enabled, save → `/xlplugins`.
Both the releases repo and the plugin repo (which hosts the zips) **must be public**: Dalamud fetches `repo.json` and the zip anonymously, and private repos
return 404 for both (checked with an unauthenticated request).

## repo.json

A JSON **array** with one object per plugin. Fields as used by Glamourer's repo.json (a well-known plugin
repository) and by this repo:

```json
[
  {
    "Author": "owner",
    "Name": "My Plugin",
    "Punchline": "...",
    "Description": "...",
    "Tags": ["UI"],
    "InternalName": "MyPlugin",
    "AssemblyVersion": "1.0.1.0",
    "TestingAssemblyVersion": "1.0.1.0",
    "RepoUrl": "https://github.com/OWNER/REPO",
    "ApplicableVersion": "any",
    "DalamudApiLevel": 15,
    "TestingDalamudApiLevel": 15,
    "IsHide": "False",
    "IsTestingExclusive": "False",
    "DownloadCount": 0,
    "LastUpdate": 1794884766,
    "DownloadLinkInstall": "https://github.com/OWNER/REPO/releases/download/v1.0.1.0/MyPlugin.zip",
    "DownloadLinkUpdate":  "https://github.com/OWNER/REPO/releases/download/v1.0.1.0/MyPlugin.zip",
    "DownloadLinkTesting": "https://github.com/OWNER/REPO/releases/download/v1.0.1.0/MyPlugin.zip",
    "IconUrl": "https://raw.githubusercontent.com/OWNER/REPO/main/images/icon.png"
  }
]
```

- `IsHide`/`IsTestingExclusive` are **strings** in the reference file; kept that way.
- `DalamudApiLevel` must match the Dalamud the user runs (15 here) or the plugin is shown as outdated.
- `IconUrl` is optional (this plugin has none yet).
- Don't hand-edit entries: `scripts/make_repo_json.py <packaged manifest> repo.json` rewrites the entry whose
  `InternalName` matches (appends it if missing) from `bin/x64/Release/<InternalName>/<InternalName>.json`.
  Other plugins' entries and extra fields such as `IconUrl` are kept; `LastUpdate` and `DownloadCount` are kept
  when the version didn't change, so rerunning is a byte-for-byte no-op (tested against the live file).
- Text edits (punchline, description) belong in the plugin manifest; a hand edit in `repo.json` is overwritten
  at the next release.
  The zip to publish is DalamudPackager's `bin/x64/Release/<InternalName>/latest.zip`, renamed to
  `<InternalName>.zip`.

## Build workflow (`.github/workflows/build.yml`)

```yaml
name: Build
on:
  push:
  pull_request:
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Download Dalamud
        run: |
          mkdir -p "$HOME/.xlcore/dalamud/Hooks/dev"
          curl -sSfL -o "$RUNNER_TEMP/dalamud.zip" https://goatcorp.github.io/dalamud-distrib/latest.zip
          unzip -q -o "$RUNNER_TEMP/dalamud.zip" -d "$HOME/.xlcore/dalamud/Hooks/dev"
      - name: Build
        run: dotnet build MyPlugin.sln -c Release
      - name: Test
        run: dotnet test MyPlugin.Tests -c Release      # NOT --no-build: solution build puts it under bin/x64
      - uses: actions/upload-artifact@v4
        with:
          name: MyPlugin
          path: MyPlugin/bin/x64/Release/MyPlugin/latest.zip
          if-no-files-found: error
```

GitHub-hosted Linux runners reach goatcorp.github.io fine. Actions logs a Node 20 deprecation warning for
checkout@v4/setup-dotnet@v4; harmless at the time of writing.

## Release workflow (`.github/workflows/release.yml`)

Manually triggered. Needs `permissions: contents: write` and `GH_TOKEN: ${{ github.token }}` for `gh`
(release on the plugin repo), plus a secret **`RELEASES_TOKEN`** – a fine-grained PAT or GitHub App token with
*Contents: Read and write* on `EvolveXIV/releases` – because the built-in `GITHUB_TOKEN` can only write to the
repository the workflow runs in. Claude cannot create repository secrets; the user has to.

Order matters so that a failure never leaves a half-published release:
1. fail fast if `RELEASES_TOKEN` is empty;
2. `actions/checkout` the plugin repo, then `actions/checkout` with `repository: EvolveXIV/releases`,
   `token: ${{ secrets.RELEASES_TOKEN }}`, `path: releases-repo` (after the first checkout, which would clean it);
3. build, test, refuse if tag `v<version>` exists;
4. `gh release create`;
5. in `releases-repo`: run the generator on `repo.json`, commit as `github-actions[bot]`, `git push origin HEAD`.

The repo's `.github/workflows/release.yml` is the source of truth.

From a Claude session: there is no `gh` CLI for this; trigger with the GitHub MCP tool
(`actions_run_trigger`, method `run_workflow`, `workflow_id: release.yml`, `ref: <default branch>`), poll with
`actions_list` (`list_workflow_jobs`), confirm with `get_latest_release` and by reading `repo.json` in
`EvolveXIV/releases`. The plugin repo no longer gets a bot commit, so no fetch is needed before your next push.
A run took about 30–60 s once started.

## Cloud environment setup script (saved on the "Damalud Plugins" environment)

```bash
#!/bin/bash
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq || true
apt-get install -y -qq dotnet-sdk-10.0 unzip curl || true
mkdir -p /root/.xlcore/dalamud/Hooks/dev
if curl -sSfL -o /tmp/dalamud.zip https://goatcorp.github.io/dalamud-distrib/latest.zip; then
  unzip -q -o /tmp/dalamud.zip -d /root/.xlcore/dalamud/Hooks/dev || true
fi
rm -f /tmp/dalamud.zip
exit 0
```
Requires `goatcorp.github.io` on the environment's allowed hosts. Ran successfully end to end inside a
session; whether it runs on a fresh container start was not observed.
