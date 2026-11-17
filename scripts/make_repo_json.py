#!/usr/bin/env python3
"""Writes repo.json, the custom plugin repository that Dalamud reads.

Usage: make_repo_json.py <packaged manifest> <repo.json>

The packaged manifest is the PartyIconsEvolved.json that DalamudPackager puts next to latest.zip. Everything in
repo.json is derived from it, so the plugin's .json and .csproj stay the only places to edit.
"""
import json
import sys
import time

ZIP_NAME = "PartyIconsEvolved.zip"


def main() -> None:
    manifest_path, repo_json_path = sys.argv[1], sys.argv[2]

    with open(manifest_path, encoding="utf-8-sig") as file:
        manifest = json.load(file)

    version = manifest["AssemblyVersion"]
    download = f"{manifest['RepoUrl']}/releases/download/v{version}/{ZIP_NAME}"

    # Keep the timestamp when the version did not change, so that regenerating is a no-op.
    last_update = int(time.time())
    try:
        with open(repo_json_path, encoding="utf-8-sig") as file:
            previous = json.load(file)[0]
        if previous.get("AssemblyVersion") == version:
            last_update = previous.get("LastUpdate", last_update)
    except (OSError, ValueError, LookupError, AttributeError):
        pass

    entry = {
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
        "DownloadCount": 0,
        "LastUpdate": last_update,
        "DownloadLinkInstall": download,
        "DownloadLinkUpdate": download,
        "DownloadLinkTesting": download,
    }

    with open(repo_json_path, "w", encoding="utf-8", newline="\n") as file:
        json.dump([entry], file, indent=2, ensure_ascii=False)
        file.write("\n")


if __name__ == "__main__":
    main()
