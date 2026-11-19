# Project skeleton (verified: builds with 0 warnings on Linux, loads in game)

Layout used by this repo:

```
MyPlugin.sln                      # dotnet new sln --format sln   (.NET 10 default would be .slnx)
MyPlugin/
  MyPlugin.csproj
  MyPlugin.json                   # manifest source; DalamudPackager completes it
  packages.lock.json              # generated (the SDK turns on lock files) – commit it
  Plugin.cs  Service.cs  Configuration/  Windows/ ...
MyPlugin.Tests/                   # optional, game-free logic only
.github/workflows/build.yml  .github/workflows/release.yml
scripts/make_repo_json.py         # updates our entry in EvolveXIV/releases/repo.json
.gitignore                        # .vs/ .idea/ obj/ bin/ *.user
```

## csproj

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
    <PropertyGroup>
        <Version>1.0.0.0</Version>                       <!-- becomes AssemblyVersion in repo.json -->
        <Description>One line.</Description>
        <PackageProjectUrl>https://github.com/OWNER/REPO</PackageProjectUrl>
        <IsPackable>false</IsPackable>
        <!-- Lets the Windows-targeted project build on Linux (cloud container, CI). -->
        <EnableWindowsTargeting>true</EnableWindowsTargeting>
    </PropertyGroup>
</Project>
```

The SDK already sets: `net10.0-windows`, `LangVersion 14`, x64, `Nullable enable`, `AllowUnsafeBlocks`,
no TFM in output path (→ `bin/x64/Release/`), lock files, and references (not copied) to Dalamud,
Dalamud.Bindings.ImGui/ImPlot/ImGuizmo, FFXIVClientStructs, InteropGenerator.Runtime, Newtonsoft.Json, Lumina,
Lumina.Excel, Serilog, Microsoft.Extensions.ObjectPool, plus DalamudPackager. Opt out per library with
`<Use_Dalamud_ImGui>false</Use_Dalamud_ImGui>` etc. Don't add PackageReferences for these.

## Manifest `MyPlugin.json`

Named after the assembly (`<AssemblyName>.json`, as in this repo). DalamudPackager adds `AssemblyVersion`, `DalamudApiLevel`,
`LoadRequiredState`, `LoadSync`, `CanUnloadAsync`, `LoadPriority`, `AcceptsFeedback` to the packaged copy.

```json
{
  "Author": "owner",
  "Name": "My Plugin",
  "Punchline": "Short tagline.",
  "Description": "Longer text, \n for new lines.",
  "InternalName": "MyPlugin",
  "RepoUrl": "https://github.com/OWNER/REPO",
  "ApplicableVersion": "any",
  "Tags": ["UI"]
}
```

## Service.cs

```csharp
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

internal sealed class Service
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    // ... add what you use: IChatGui, IClientState, ICondition, IDataManager, IGameGui, IGameConfig,
    //     ITextureProvider, IToastGui, IContextMenu, IAddonLifecycle, INamePlateGui
}
```

## Plugin.cs

```csharp
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/myplugin";
    private readonly WindowSystem windowSystem = new("MyPlugin");
    private readonly ConfigWindow configWindow;
    internal PluginConfiguration Configuration { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();              // fills the [PluginService] statics
        Configuration = PluginConfiguration.Load();

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;   // cog in /xlplugins
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;     // "Open" button

        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Open settings." });
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler(Command);
        Service.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        Service.PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        // dispose features in reverse order of creation
    }

    private void OnCommand(string command, string arguments) => configWindow.Toggle();
}
```

## Configuration

```csharp
using Dalamud.Configuration;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled = true;                  // public fields are saved (Newtonsoft)
    public Vector4? CustomColor;                 // nullable = "use default"

    [Newtonsoft.Json.JsonIgnore]
    public bool Derived => CustomColor is not null;

    public static PluginConfiguration Load()
    {
        PluginConfiguration? config = null;
        try { config = Service.PluginInterface.GetPluginConfig() as PluginConfiguration; }
        catch (Exception e) { Service.Log.Error(e, "Could not read the configuration"); }
        config ??= new PluginConfiguration();
        // clamp values here: hand edits and older versions happen
        return config;
    }

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
```

## Window

```csharp
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

internal sealed class ConfigWindow : Window
{
    public ConfigWindow(Plugin plugin) : base("My Plugin###MyPluginConfig")   // ### = stable id
    {
        Size = new Vector2(520, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(440, 360),
                                                      MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
    }

    public override void OnClose() { /* save if dirty */ }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("##tabs");
        if (!tabs) return;
        using (var tab = ImRaii.TabItem("General")) {
            if (tab) {
                using var child = ImRaii.Child("##general");          // scrolls independently
                // ImGui.Checkbox("Enabled", ref config.Enabled);
                // using (ImRaii.Disabled(!config.Enabled)) { ImGui.SliderFloat("Size", ref config.Scale, 0.25f, 4f, "%.2fx"); }
            }
        }
    }
}
```

Enum combo helper, help markers ("(?)" + tooltip), section headers (replacement for the missing
`SeparatorText`), per-row tables and icon previews: `PartyIconsEvolved/Windows/ConfigWindow.cs`.
Scale fixed pixel sizes by `ImGui.GetIO().FontGlobalScale`.

## Test project (no Dalamud, runs anywhere)

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <LangVersion>14.0</LangVersion>
        <IsPackable>false</IsPackable>
        <Platforms>x64</Platforms>
    </PropertyGroup>
    <ItemGroup>
        <!-- Only files that use nothing but the BCL. -->
        <Compile Include="../MyPlugin/Logic/Parser.cs" Link="Plugin/Parser.cs" />
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
        <PackageReference Include="xunit" Version="2.9.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    </ItemGroup>
</Project>
```

Linked files are compiled into the test assembly itself, so their `internal` types are usable from tests
without `InternalsVisibleTo`. Design for it: keep e.g. role-assignment rules in a class that takes plain data
(`RoleAssignments.Resolve(IEnumerable<(PlayerKey, RoleCategory)>, bool)`) and a thin game-facing wrapper
(`RoleManager`) that feeds it. `Microsoft.NET.Test.Sdk 17.12.0` prints a harmless
"VSTEST_ARTIFACTS_DIRECTORY ... outdated" notice.
