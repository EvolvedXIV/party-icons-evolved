# Party Icons Evolved

A Dalamud plugin for FINAL FANTASY XIV. Build and test commands are in `README.md`.

For Dalamud API knowledge, toolchain setup, gotchas and the release process, use the `dalamud-plugin` skill in
[EvolveXIV/dalamud_reference](https://github.com/EvolveXIV/dalamud_reference): attach that repository to the
session (it loads the skill automatically), or clone it and read `.claude/skills/dalamud-plugin/SKILL.md`.
Record what you learn here in that skill, not in this repository.

`scripts/make_repo_json.py` and `.github/workflows/` have template copies in that skill (`templates/`, with the
name replaced by `MyPlugin`). Keep both in step when changing either.

Releases update this plugin's entry in `repo.json` in
[EvolveXIV/releases](https://github.com/EvolveXIV/releases); the workflow needs the `RELEASES_TOKEN` secret.
