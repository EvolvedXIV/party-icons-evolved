---
name: dalamud-plugin
description: Build, change or debug a Dalamud plugin for FINAL FANTASY XIV (C#, .NET 10, API level 15) - project and manifest setup, Dalamud services, ImGui windows, config, commands, nameplates, context menus, chat, native UI nodes via FFXIVClientStructs, repo.json and GitHub releases, and building and testing in a Linux cloud container. Use whenever the work touches Dalamud, XIVLauncher plugins, FFXIVClientStructs, Lumina or a repo.json plugin repository.
---

# Writing Dalamud plugins

Knowledge from building `ffxivplugins/party-icons-evolved` (this repository) against **Dalamud 15.0.4,
API level 15, Dalamud.NET.Sdk 15.0.0, .NET SDK 10.0.115**, in a Linux cloud container. Everything here
compiled and ran unless marked **(unverified)**. v1.0.0 of this plugin was reported working in game by the
user; anything that only exists from v1.0.1 on is marked **(not yet seen in game)**.

The source of this repository is the best worked example. Read it before writing similar code:

| Need | Look at |
|---|---|
| Entry point, service wiring, command | `PartyIconsEvolved/Plugin.cs`, `Service.cs` |
| Settings window (tabs, combos, sliders, colour pickers, tables) | `PartyIconsEvolved/Windows/ConfigWindow.cs` |
| Saved config | `PartyIconsEvolved/Configuration/Configuration.cs` |
| Nameplate text and native nodes | `PartyIconsEvolved/Nameplates/` |
| Context menu, chat listener | `PartyIconsEvolved/Roles/RoleContextMenu.cs`, `ChatRoleListener.cs` |
| Party/job data via ClientStructs + Lumina | `PartyIconsEvolved/Game/` |
| Tests that run without Dalamud | `PartyIconsEvolved.Tests/` |
| CI, release, repo.json | `.github/workflows/`, `scripts/make_repo_json.py`, `repo.json` |

Reference files next to this one:

- `reference/project-skeleton.md` – csproj, manifest, Service/Plugin/Config/Window skeletons, test project.
- `reference/api-notes.md` – verified signatures and patterns: nameplates, native nodes, context menu, chat,
  SeString, Lumina sheets, party data, game config.
- `reference/release.md` – repo.json format, generator script, build and release workflows.
- `tools/xmldoc.py`, `tools/api.sh` – offline API lookup (below).

## 1. Look APIs up locally first, the web last

Dalamud's API changes between levels and memory of it is unreliable (several names below were not what one
would guess). Before writing a call, check it against the assemblies that will actually be compiled against:

1. **XML docs** in `/root/.xlcore/dalamud/Hooks/dev/` (or `$DALAMUD_HOME`): `Dalamud.xml` (~14k members),
   `FFXIVClientStructs.xml` (~69k), `Lumina.xml`, `Lumina.Excel.xml`.
   ```bash
   python3 -I .claude/skills/dalamud-plugin/tools/xmldoc.py Dalamud.xml 'Services\.INamePlateGui' 'NamePlateUpdateHandler\.Name'
   ```
2. **Exact signatures, struct layouts, enum values** – including the **ImGui bindings, which ship no XML docs**:
   ```bash
   .claude/skills/dalamud-plugin/tools/api.sh Dalamud.dll 'Services\.IChatGui$'
   .claude/skills/dalamud-plugin/tools/api.sh FFXIVClientStructs.dll 'AddonNamePlate\+NamePlateObject$'
   .claude/skills/dalamud-plugin/tools/api.sh Dalamud.Bindings.ImGui.dll '^Dalamud\.Bindings\.ImGui\.ImGui$' | grep ' Combo('
   ```
   `api.sh` builds a small MetadataLoadContext reflection dumper on first use (reflection-only, the dumped
   assembly is not executed). Nested types use `+`.
3. The reference plugin `ffxivplugins/xivPartyIcons` (if attached) for nameplate/node techniques.
4. Only then the web (goatcorp/Dalamud, aers/FFXIVClientStructs sources). Docs sites may describe a
   different API level than the one installed; trust the local assemblies.

Do **not** try `dotnet tool install -g ilspycmd`: in auto mode the install was denied by the permission
classifier. The reflection dumper covers the same need.

## 2. Getting a working toolchain in the cloud container

Nothing is preinstalled. What worked (and is saved as the setup script of the "Damalud Plugins" (sic) environment,
so a fresh session may already have it – check `dotnet --version` and the Dalamud folder first):

```bash
apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0   # Ubuntu 24.04 archive
mkdir -p ~/.xlcore/dalamud/Hooks/dev
curl -sSfL -o /tmp/dalamud.zip https://goatcorp.github.io/dalamud-distrib/latest.zip
unzip -q -o /tmp/dalamud.zip -d ~/.xlcore/dalamud/Hooks/dev
curl -sSL https://goatcorp.github.io/dalamud-distrib/version    # {"AssemblyVersion":"15.0.4.0"}
```

- `builds.dotnet.microsoft.com`, `dot.net`, `aka.ms` and `kamori.goats.dev` were **blocked** by the proxy (403).
  `goatcorp.github.io` had to be **added to the environment's allowed hosts** (it now is). NuGet works.
- NuGet has **no usable Dalamud reference package** (`Dalamud` there is a stale 14.0.3; `Dalamud.NET.Sdk` and
  `DalamudPackager` are real). The SDK finds Dalamud via `DalamudLibPath`, which on Linux is
  `$HOME/.xlcore/dalamud/Hooks/dev/`, overridable with `DALAMUD_HOME`. Missing folder → build error
  "Dalamud installation not found".
- The SDK targets `net10.0-windows`; on Linux set `<EnableWindowsTargeting>true</EnableWindowsTargeting>`
  in the csproj (or pass `-p:EnableWindowsTargeting=true`).

## 3. Build and test

```bash
dotnet build PartyIconsEvolved.sln -c Release          # plugin + tests; 0 warnings expected
dotnet test PartyIconsEvolved.Tests -c Release         # do NOT add --no-build (see gotchas)
```

Output: `PartyIconsEvolved/bin/x64/Release/PartyIconsEvolved.dll`, and DalamudPackager's
`.../Release/PartyIconsEvolved/latest.zip` + generated manifest `.../Release/PartyIconsEvolved/PartyIconsEvolved.json`
(that manifest gains `AssemblyVersion` and `DalamudApiLevel`; `repo.json` is generated from it).

The game itself cannot run in the container. Verify what can be verified here:
compile, unit-test pure logic, and round-trip SeString bytes in a scratch console app
(see `reference/api-notes.md` → SeString). Everything that touches game memory needs the user to try it in
game; say so explicitly and list what to look at.

**Testing pattern:** keep game-free logic (parsing, assignment rules, string shortening) in files that only
use the BCL, and compile them into a plain `net10.0` xUnit project with `<Compile Include="../Plugin/File.cs" Link=.../>`.
No Dalamud, no Windows needed. See `PartyIconsEvolved.Tests/PartyIconsEvolved.Tests.csproj`.

## 4. Plugin anatomy (API 15)

- **Entry point:** `public sealed class Plugin : IDalamudPlugin` with ctor `(IDalamudPluginInterface pi)`;
  first line `pi.Create<Service>();` which fills `[PluginService] public static T X { get; private set; } = null!;`
  properties. Dispose everything you subscribed, in reverse order.
- **Services used and working:** `IDalamudPluginInterface`, `IPluginLog`, `IChatGui`, `IClientState`,
  `ICommandManager`, `ICondition`, `IDataManager`, `IFramework`, `IGameGui`, `IGameConfig`, `ITextureProvider`,
  `IToastGui`, `IContextMenu`, `IAddonLifecycle`, `INamePlateGui`, `IPlayerState`.
- **`IPlayerState`** gives the local player: `IsLoaded`, `CharacterName`, `HomeWorld` (RowRef → `.RowId`),
  `EntityId`, `ContentId`, `ClassJob`. This plugin never needed `ClientState.LocalPlayer`.
- **Windows:** `Dalamud.Interface.Windowing.Window` subclass + `WindowSystem`; hook
  `UiBuilder.Draw += windowSystem.Draw`, `UiBuilder.OpenConfigUi += window.Toggle`,
  `UiBuilder.OpenMainUi += window.Toggle`. ImGui is `Dalamud.Bindings.ImGui` (not ImGuiNET);
  use `Dalamud.Interface.Utility.Raii.ImRaii` scopes (`TabBar`, `TabItem`, `Child`, `Combo`, `Disabled`,
  `PushIndent`, `PushId`, `Table`, `Tooltip`). Colours: `Dalamud.Interface.Colors.ImGuiColors.DalamudViolet` etc.
- **Config:** class implementing `Dalamud.Configuration.IPluginConfiguration` (`int Version`), public fields are
  serialised (Newtonsoft); `pi.GetPluginConfig() as T ?? new T()`, `pi.SavePluginConfig(this)`.
  Use `[Newtonsoft.Json.JsonIgnore]` on computed properties. Don't save on every frame of a slider drag:
  mark dirty and save when `!ImGui.IsAnyItemActive()` and on window close / plugin dispose.
- **Commands:** `CommandManager.AddHandler("/x", new CommandInfo(OnCommand) { HelpMessage = "..." })`,
  `RemoveHandler` in Dispose. Handler signature `(string command, string arguments)`.
- **Per-frame work:** `IFramework.Update += (IFramework f) => ...` (framework thread; game structs are safe here).
- **Addon hooks without signatures:** `IAddonLifecycle.RegisterListener(AddonEvent.PreDraw, "NamePlate", handler)`,
  handler `(AddonEvent type, AddonArgs args)`, addon pointer `args.Addon.Address`. Also `PreFinalize` for cleanup.
  No `Hook<T>`/signature scanning was needed for this plugin – prefer lifecycle events and Dalamud services.
- **ClientStructs:** `GroupManager.Instance()->MainGroup` (party), `AgentHUD.Instance()->PartyMembers`
  (party list order), `(Character*)player.Address` for `CharacterData.ClassJob`, `HomeWorld`,
  `((GameObject*)addr)->NameString`. Project needs `AllowUnsafeBlocks` (the SDK sets it).

Details and verified signatures: `reference/api-notes.md`.

## 5. Gotchas hit while building (and the fixes)

| Symptom | Cause / fix |
|---|---|
| `error CS0117: 'ImGui' does not contain a definition for 'SeparatorText'` | Not in these bindings. Use `ImGui.Spacing(); ImGui.TextColored(ImGuiColors.DalamudViolet, title); ImGui.Separator();`. Check any "newer ImGui" call with `api.sh` first. |
| CI: `The argument .../bin/Release/net10.0/X.Tests.dll is invalid` | Building via the `.sln` (x64 platform) puts the test DLL in `bin/x64/Release/...`, but `dotnet test <project> --no-build` looks in `bin/Release/...`. Drop `--no-build`. |
| `dotnet new sln` produced `X.slnx` | .NET 10 default. Use `dotnet new sln --format sln` for a classic `.sln`. |
| Build: "Dalamud installation not found at ..." | Dalamud distrib not extracted to `~/.xlcore/dalamud/Hooks/dev` / `DALAMUD_HOME`. |
| Windows targeting error on Linux | `<EnableWindowsTargeting>true</EnableWindowsTargeting>`. |
| Chat handler from older examples (`(XivChatType type, int ts, ref SeString sender, ref SeString msg, ref bool handled)`) | In API 15 `IChatGui.ChatMessage` is `OnHandleableChatMessageDelegate`: `void (IHandleableChatMessage message)` with `LogKind`, `Sender`, `Message` (SeString), `Timestamp`, `PreventOriginal()`. (xivPartyIcons carries the old signature commented out next to the new one.) |
| Own chat messages have no sender link | `message.Sender` has a `PlayerPayload` only for other players. Fall back to `IPlayerState.CharacterName`/`HomeWorld`. |
| Debug-printing game text shows garbage | `ReadOnlySeString.ToString()` gives raw text; use `.ToMacroString()` to see `<colortype(37)>` etc. |
| `repo.json` / release zip 404 for Dalamud | Dalamud downloads anonymously; a **private** GitHub repo returns 404 for both raw files and release assets. The repo must be public. |
| First push to an empty repo made the `claude/...` branch the default | GitHub picks the first pushed branch. Push `main` first, or ask the user to change the default branch in Settings. |
| `fatal: expected 'acknowledgments', received 'packfile'` on push | Harmless warning from the proxy; the push succeeded. |
| `latest.zip` ends up inside the build output folder | That's DalamudPackager's output, `bin/x64/Release/<InternalName>/latest.zip`; copy/rename it for releases. |

More API-level pitfalls (nameplate field removal semantics, NameIconId placeholder, node flag syncing,
text node font copying) are in `reference/api-notes.md`.

## 6. Shipping to users

Users install through a custom repository: `/xlsettings` → Experimental → Custom Plugin Repositories →
add `https://raw.githubusercontent.com/<owner>/<repo>/main/repo.json` → `/xlplugins`.

This repo automates it (verified twice, v1.0.0.0 and v1.0.1.0):

1. Bump `<Version>` in the plugin csproj, commit, push to `main`.
2. Run the **Release** workflow (`workflow_dispatch`; trigger with `mcp__github__actions_run_trigger`,
   method `run_workflow`, `workflow_id: release.yml`, `ref: main`).
3. It builds, tests, creates GitHub release `v<version>` with `<InternalName>.zip`, regenerates `repo.json`
   with `scripts/make_repo_json.py` and **pushes a commit to main** – fetch/fast-forward before your next push.

Full files: `reference/release.md`.

## 7. Working conventions that served well

- Read the reference implementation before inventing node/geometry code; copy its lifecycle
  (wait one PreDraw before touching addon nodes, recreate after PreFinalize, destroy on Dispose).
- Wrap every per-frame / per-event handler body in try/catch with `Service.Log.Error(e, "...")`: an exception
  inside a game callback can take features down for the session.
- Leave the game's state exactly as found on Dispose (reset scales, hide/free nodes, `RequestRedraw()`).
- When something can't be verified without the game, ship it but tell the user precisely what to check.
