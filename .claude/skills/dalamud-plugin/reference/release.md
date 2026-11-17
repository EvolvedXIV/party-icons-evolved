# Releasing: repo.json, GitHub releases, CI (verified: v1.0.0.0 and v1.0.1.0 shipped this way)

## How users install

`/xlsettings` → Experimental → Custom Plugin Repositories → add
`https://raw.githubusercontent.com/OWNER/REPO/main/repo.json`, tick Enabled, save → `/xlplugins` → search.
The repository **must be public**: Dalamud fetches `repo.json` and the zip anonymously, and private repos
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
- Don't hand-edit: `scripts/make_repo_json.py <packaged manifest> repo.json` generates it from
  `bin/x64/Release/<InternalName>/<InternalName>.json` and keeps `LastUpdate` when the version didn't change.
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

Manually triggered. Needs `permissions: contents: write` and `GH_TOKEN: ${{ github.token }}` for `gh`.
It refuses to run if the tag already exists (forces a version bump), creates the release, then commits the
regenerated `repo.json` back to the branch as `github-actions[bot]` – so **fetch before pushing again**.
The repo's copy is the source of truth; core steps, simplified (the real file reads the version from the
packaged manifest into a step output `tag` instead of `$VERSION`):

```yaml
      - name: Publish release
        run: |
          cp MyPlugin/bin/x64/Release/MyPlugin/latest.zip MyPlugin.zip
          gh release create "v$VERSION" MyPlugin.zip --title "v$VERSION" --generate-notes --target "$GITHUB_SHA"
      - name: Point repo.json at the release
        run: |
          python3 scripts/make_repo_json.py MyPlugin/bin/x64/Release/MyPlugin/MyPlugin.json repo.json
          if ! git diff --quiet -- repo.json; then
            git config user.name "github-actions[bot]"
            git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
            git commit -m "Update repo.json for v$VERSION" -- repo.json
            git push origin "HEAD:${GITHUB_REF_NAME}"
          fi
```

From a Claude session: there is no `gh` CLI for this; trigger with the GitHub MCP tool
(`actions_run_trigger`, method `run_workflow`, `workflow_id: release.yml`, `ref: main`), poll with
`actions_list` (`list_workflow_jobs`), confirm with `get_latest_release`, then
`git fetch origin main && git merge --ff-only FETCH_HEAD`. A run takes about 30–60 s once started.

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
