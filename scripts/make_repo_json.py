#!/usr/bin/env python3
"""Updates this plugin's entry in a Dalamud custom plugin repository file (repo.json).

Usage: make_repo_json.py <packaged manifest> <repo.json>

The packaged manifest is the PartyIconsEvolved.json that DalamudPackager puts next to latest.zip. The plugin's
entry in repo.json is derived from it, so the plugin's .json and .csproj stay the only places to edit.

repo.json lives in the shared EvolveXIV/releases repository and may list several plugins: only the entry with the
same InternalName is touched (or appended if missing). Fields of that entry that this script does not generate,
such as IconUrl, are kept. Running it again for an unchanged version leaves the file byte-for-byte the same.
"""
import json
import sys
import time

ZIP_NAME = "PartyIconsEvolved.zip"


def build_entry(manifest: dict, previous: dict) -> dict:
    version = manifest["AssemblyVersion"]
    download = f"{manifest['RepoUrl']}/releases/download/v{version}/{ZIP_NAME}"

    # Keep the timestamp when the version did not change, so that regenerating is a no-op.
    last_update = int(time.time())
    if previous.get("AssemblyVersion") == version and "LastUpdate" in previous:
        last_update = previous["LastUpdate"]

    generated = {
        "Author": manifest["Author"],
        "Name": manifest["Name"],
        "Punchline": manifest["Punchline"],
        "Description": manifest["Description"],
        "Tags": manifest.get("Tags", []),
        "InternalName": manifest["InternalName"],
        "AssemblyVersion": version,
        "TestingAssemblyVersion": version,
        "RepoUrl": manifest["RepoUrl"],
        "ApplicableVersion": manifest.get("ApplicableVersion", "any"),
        "DalamudApiLevel": manifest["DalamudApiLevel"],
        "TestingDalamudApiLevel": manifest["DalamudApiLevel"],
        "IsHide": "False",
        "IsTestingExclusive": "False",
        "DownloadCount": previous.get("DownloadCount", 0),
        "LastUpdate": last_update,
        "DownloadLinkInstall": download,
        "DownloadLinkUpdate": download,
        "DownloadLinkTesting": download,
    }

    # Generated fields first, in a fixed order; then anything extra the entry already had (e.g. IconUrl).
    entry = dict(generated)
    for key, value in previous.items():
        if key not in entry:
            entry[key] = value
    return entry


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)

    manifest_path, repo_json_path = sys.argv[1], sys.argv[2]

    with open(manifest_path, encoding="utf-8-sig") as file:
        manifest = json.load(file)

    try:
        with open(repo_json_path, encoding="utf-8-sig") as file:
            entries = json.load(file)
    except FileNotFoundError:
        entries = []

    if not isinstance(entries, list):
        sys.exit(f"{repo_json_path} must contain a JSON array")

    name = manifest["InternalName"]
    index = next(
        (i for i, e in enumerate(entries) if isinstance(e, dict) and e.get("InternalName") == name), None)

    if index is None:
        entries.append(build_entry(manifest, {}))
    else:
        entries[index] = build_entry(manifest, entries[index])

    with open(repo_json_path, "w", encoding="utf-8", newline="\n") as file:
        json.dump(entries, file, indent=2, ensure_ascii=False)
        file.write("\n")


if __name__ == "__main__":
    main()
