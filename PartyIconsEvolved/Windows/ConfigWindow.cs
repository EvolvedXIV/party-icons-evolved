using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Config;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;
using PartyIconsEvolved.Roles;

namespace PartyIconsEvolved.Windows;

internal sealed class ConfigWindow : Window
{
    private static readonly (NameColorMode Value, string Name)[] NameColorModes =
    [
        (NameColorMode.GameDefault, "Game default"),
        (NameColorMode.Role, "Role colour"),
        (NameColorMode.Custom, "Custom"),
    ];

    private static readonly (JobIconStyle Value, string Name)[] JobIconStyles =
    [
        (JobIconStyle.Framed, "Framed (party list)"),
        (JobIconStyle.Embossed, "Embossed"),
        (JobIconStyle.Glowing, "Glowing"),
        (JobIconStyle.RoleColored, "Role coloured"),
    ];

    private static readonly (JobIconPosition Value, string Name)[] JobIconPositions =
    [
        (JobIconPosition.Left, "Left of the name"),
        (JobIconPosition.Right, "Right of the name"),
        (JobIconPosition.Above, "Above the name"),
        (JobIconPosition.Below, "Below the name"),
    ];

    private static readonly (RoleIndicatorStyle Value, string Name)[] RoleIndicatorStyles =
    [
        (RoleIndicatorStyle.Boxed, "Boxed letters"),
        (RoleIndicatorStyle.Plain, "Plain text"),
    ];

    private static readonly (RoleIndicatorPosition Value, string Name)[] RoleIndicatorPositions =
    [
        (RoleIndicatorPosition.BeforeName, "Before the name"),
        (RoleIndicatorPosition.AfterName, "After the name"),
    ];

    private static readonly (RoleNaming Value, string Name)[] RoleNamings =
    [
        (RoleNaming.Western, "MT  OT  H1  H2  M1  M2  R1  R2"),
        (RoleNaming.Eastern, "MT  ST  H1  H2  D1  D2  D3  D4"),
    ];

    // One job of each role, to show what an icon style looks like: PLD, WHM, DRG, BLM.
    private static readonly uint[] SampleJobs = [19, 24, 22, 25];

    private readonly Plugin plugin;
    private readonly PluginConfiguration config;
    private readonly Dictionary<RoleCategory, Vector4> defaultRoleColors = new();
    private bool unsaved;

    public ConfigWindow(Plugin plugin) : base("Party Icons Evolved###PartyIconsEvolvedConfig")
    {
        this.plugin = plugin;
        config = plugin.Configuration;

        Size = new Vector2(520, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        var theme = Service.GameConfig.TryGet(SystemConfigOption.ColorThemeType, out uint themeValue) ? themeValue : 0;
        var colors = Service.DataManager.GetExcelSheet<UIColor>();
        foreach (var category in RoleExtensions.AllCategories) {
            defaultRoleColors[category] = colors.GetRowOrDefault(category.DefaultUiColor()) is { } row
                ? FromRgba(theme switch
                {
                    1 => row.Light,
                    2 => row.ClassicFF,
                    3 => row.ClearBlue,
                    _ => row.Dark,
                })
                : Vector4.One;
        }
    }

    public override void OnClose()
    {
        plugin.Nameplates.Preview = false;
        SaveIfNeeded(force: true);
    }

    public override void Draw()
    {
        var changed = false;

        changed |= ImGui.Checkbox("Enabled", ref config.Enabled);

        ImGui.SameLine(0, 24 * ImGui.GetIO().FontGlobalScale);
        var preview = plugin.Nameplates.Preview;
        if (ImGui.Checkbox("Preview on everyone nearby", ref preview)) {
            plugin.Nameplates.Preview = preview;
        }

        HelpMarker("Applies your settings to every player nameplate, including your own, while this window is open. " +
                   "Handy for trying things out without a party.");

        ImGui.Spacing();

        using (var tabs = ImRaii.TabBar("##tabs")) {
            if (tabs) {
                using (var tab = ImRaii.TabItem("Nameplate")) {
                    if (tab) {
                        using var child = ImRaii.Child("##nameplate");
                        changed |= DrawNameSection();
                        changed |= DrawScopeSection();
                    }
                }

                using (var tab = ImRaii.TabItem("Job icon")) {
                    if (tab) {
                        using var child = ImRaii.Child("##jobIcon");
                        changed |= DrawJobIconSection();
                    }
                }

                using (var tab = ImRaii.TabItem("Role indicator")) {
                    if (tab) {
                        using var child = ImRaii.Child("##indicator");
                        changed |= DrawIndicatorSection();
                    }
                }

                using (var tab = ImRaii.TabItem("Role assignment")) {
                    if (tab) {
                        using var child = ImRaii.Child("##assignment");
                        changed |= DrawAssignmentSection();
                        DrawPartySection();
                    }
                }
            }
        }

        if (changed) {
            config.Sanitize();
            plugin.OnConfigurationChanged();
            unsaved = true;
        }

        SaveIfNeeded(force: false);
    }

    private bool DrawNameSection()
    {
        var changed = false;

        SectionHeader("Name");

        changed |= ImGui.Checkbox("Show name", ref config.ShowName);

        using (ImRaii.Disabled(!config.ShowName)) {
            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderInt(
                "Max characters", ref config.MaxNameLength, 0, 32, config.MaxNameLength == 0 ? "No limit" : "%d");
            HelpMarker(
                "Cuts the name off after this many characters.\n\n" +
                "This is applied on top of the game's own setting, so the name is first abbreviated the way you " +
                "chose under Character Configuration > Display Name Settings > Display Type.\n\n" +
                $"Party: {NameDisplayType(UiConfigOption.NamePlateNameTypeParty)}\n" +
                $"Own: {NameDisplayType(UiConfigOption.NamePlateNameTypeSelf)}");

            using (ImRaii.Disabled(config.MaxNameLength == 0)) {
                changed |= ImGui.Checkbox("Add \"…\" to shortened names", ref config.AddEllipsis);
            }

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= EnumCombo("Name colour", ref config.NameColor, NameColorModes);
            if (config.NameColor == NameColorMode.Custom) {
                ImGui.SameLine();
                changed |= ImGui.ColorEdit4(
                    "##customNameColor", ref config.CustomNameColor,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
            }
        }

        changed |= ImGui.Checkbox("Hide title", ref config.HideTitle);
        ImGui.SameLine(0, 24 * ImGui.GetIO().FontGlobalScale);
        changed |= ImGui.Checkbox("Hide Free Company tag", ref config.HideFreeCompanyTag);

        ImGui.SetNextItemWidth(ItemWidth);
        changed |= ImGui.SliderFloat("Nameplate size", ref config.PlateScale, 0.5f, 2.5f, "%.2fx");
        HelpMarker("Scales the name, role indicator and job icon together. Ctrl+click to type a value.");

        return changed;
    }

    private bool DrawJobIconSection()
    {
        var changed = false;

        SectionHeader("Job icon");

        changed |= ImGui.Checkbox("Show job icon", ref config.ShowJobIcon);

        using (ImRaii.Disabled(!config.ShowJobIcon)) {
            ImGui.SetNextItemWidth(ItemWidth);
            changed |= EnumCombo("Style", ref config.JobIconStyle, JobIconStyles);

            ImGui.SameLine();
            DrawSampleIcons();

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= EnumCombo("Position", ref config.JobIconPosition, JobIconPositions);

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Size", ref config.JobIconScale, 0.25f, 4f, "%.2fx");

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Horizontal offset", ref config.JobIconOffsetX, -100f, 100f, "%.0f");

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Vertical offset", ref config.JobIconOffsetY, -100f, 100f, "%.0f");
            HelpMarker("Fine-tunes the position. Positive values move the icon right and down.");
        }

        changed |= ImGui.Checkbox("Hide the game's own status icon", ref config.HideStatusIcon);
        HelpMarker("The small icon the game shows left of the name (party leader, new adventurer, " +
                   "the game's own job icon option and so on).");

        return changed;
    }

    private bool DrawScopeSection()
    {
        var changed = false;

        SectionHeader("Applies to");

        ImGui.TextDisabled("Party members are always included. Other players are never touched.");

        changed |= ImGui.Checkbox("My own nameplate", ref config.IncludeSelf);
        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!config.IncludeSelf)) {
            changed |= ImGui.Checkbox("Also when I am not in a party", ref config.IncludeSelfWhenSolo);
        }

        changed |= ImGui.Checkbox("Only inside duties", ref config.OnlyInDuties);

        return changed;
    }

    private bool DrawIndicatorSection()
    {
        var changed = false;

        SectionHeader("Role indicator");

        changed |= ImGui.Checkbox("Show role indicator", ref config.ShowRoleIndicator);

        using (ImRaii.Disabled(!config.ShowRoleIndicator)) {
            ImGui.SetNextItemWidth(ItemWidth);
            changed |= EnumCombo("Style##indicator", ref config.RoleIndicatorStyle, RoleIndicatorStyles);

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= EnumCombo("Position##indicator", ref config.RoleIndicatorPosition, RoleIndicatorPositions);

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat("Size##indicator", ref config.RoleIndicatorScale, 0.25f, 4f, "%.2fx");

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat(
                "Horizontal offset##indicator", ref config.RoleIndicatorOffsetX, -100f, 100f, "%.0f");

            ImGui.SetNextItemWidth(ItemWidth);
            changed |= ImGui.SliderFloat(
                "Vertical offset##indicator", ref config.RoleIndicatorOffsetY, -100f, 100f, "%.0f");
            HelpMarker("Fine-tunes the position. Positive values move the indicator right and down.\n\n" +
                       "At 1.00x with both offsets at 0 the indicator is simply part of the name text. With any " +
                       "other value it is drawn separately, and the name leaves a gap of matching width for it.");

            using (ImRaii.Disabled(!config.RoleIndicatorFloats)) {
                if (ImGui.Button("Reset size and offsets")) {
                    config.RoleIndicatorScale = 1f;
                    config.RoleIndicatorOffsetX = 0f;
                    config.RoleIndicatorOffsetY = 0f;
                    changed = true;
                }
            }
        }

        ImGui.SetNextItemWidth(ItemWidth);
        changed |= EnumCombo("Labels", ref config.RoleNaming, RoleNamings);

        SectionHeader("Colours");

        foreach (var category in RoleExtensions.AllCategories) {
            using var id = ImRaii.PushId((int)category);

            var custom = config.GetRoleColor(category);
            var color = custom ?? defaultRoleColors[category];

            if (ImGui.ColorEdit4("##color", ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha)) {
                config.SetRoleColor(category, color with { W = 1f });
                changed = true;
            }

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(category.DisplayName());

            ImGui.SameLine(110 * ImGui.GetIO().FontGlobalScale);
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(color, SampleIndicators(category));

            ImGui.SameLine(210 * ImGui.GetIO().FontGlobalScale);
            using (ImRaii.Disabled(custom is null)) {
                if (ImGui.Button("Default")) {
                    config.SetRoleColor(category, null);
                    changed = true;
                }
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("The defaults are the game's own UI colours and follow your colour theme.");
        if (config.NameColor == NameColorMode.Role) {
            ImGui.TextDisabled("These colours are also used for the name (Name colour: Role colour).");
        }

        return changed;
    }

    private bool DrawAssignmentSection()
    {
        var changed = false;

        SectionHeader("How roles are assigned");

        changed |= ImGui.Checkbox("Automatically, from job and party list order", ref config.AutoAssignRoles);
        HelpMarker("The first tank on the party list becomes MT, the second OT, and likewise for healers, melee " +
                   "and ranged. Roles set by chat or by hand always take priority.");

        changed |= ImGui.Checkbox("From party chat", ref config.AssignFromChat);
        HelpMarker("A party member who sends a message that contains only a role, for example \"M1\", " +
                   "is given that role.");

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!config.AssignFromChat)) {
            changed |= ImGui.Checkbox("Also accept ST, T1/T2 and D1-D4", ref config.ChatAliases);
            changed |= ImGui.Checkbox("Show a notification", ref config.NotifyChatAssignment);
        }

        changed |= ImGui.Checkbox("From the right-click menu", ref config.ContextMenu);
        HelpMarker("Adds \"Role Indicator\" to the menu you get when right-clicking a party member, " +
                   "in the world or on the party list.");

        return changed;
    }

    private void DrawPartySection()
    {
        SectionHeader("Current party");

        var members = plugin.Party.Members;
        if (members.Count == 0) {
            ImGui.TextDisabled("Not logged in.");
            return;
        }

        using (var table = ImRaii.Table("##party", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) {
            if (table) {
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 50 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.WidthFixed, 130 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableHeadersRow();

                for (var i = 0; i < members.Count; i++) {
                    var member = members[i];
                    using var id = ImRaii.PushId(i);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(member.Key.Name);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(JobData.GetAbbreviation(member.JobId));

                    ImGui.TableNextColumn();
                    DrawRoleCombo(member.Key);
                }
            }
        }

        if (ImGui.Button("Reset to automatic")) {
            plugin.Roles.Reset();
        }

        HelpMarker("Forgets every role that was set by chat or by hand. Also available as /pie reset.\n" +
                   "This happens on its own when you leave the party.");
    }

    private void DrawRoleCombo(PlayerKey key)
    {
        var roles = plugin.Roles;
        var current = roles.GetRole(key);
        var manual = roles.IsManual(key);

        var preview = current == RoleId.None ? "-" : current.Label(config.RoleNaming);
        if (!manual && current != RoleId.None) {
            preview += " (auto)";
        }

        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo("##role", preview);
        if (!combo) {
            return;
        }

        if (ImGui.Selectable("Automatic", !manual)) {
            roles.Clear(key);
        }

        foreach (var role in RoleExtensions.AllRoles) {
            if (ImGui.Selectable(role.Label(config.RoleNaming), manual && role == current)) {
                roles.Assign(key, role);
            }
        }
    }

    private void DrawSampleIcons()
    {
        var size = new Vector2(ImGui.GetFrameHeight());
        for (var i = 0; i < SampleJobs.Length; i++) {
            if (!JobData.TryGetIcon(config.JobIconStyle, SampleJobs[i], out var icon)) {
                continue;
            }

            if (i > 0) {
                ImGui.SameLine(0, 2);
            }

            var texture = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(icon.IconId)).GetWrapOrEmpty();
            ImGui.Image(texture.Handle, size);
        }
    }

    private string SampleIndicators(RoleCategory category)
    {
        var (first, second) = category switch
        {
            RoleCategory.Tank => (RoleId.MT, RoleId.OT),
            RoleCategory.Healer => (RoleId.H1, RoleId.H2),
            RoleCategory.Melee => (RoleId.M1, RoleId.M2),
            _ => (RoleId.R1, RoleId.R2),
        };

        return $"{plugin.Text.IndicatorText(first)} {plugin.Text.IndicatorText(second)}";
    }

    internal void SaveIfNeeded(bool force)
    {
        // Wait until a slider is released rather than writing the file on every frame of a drag.
        if (unsaved && (force || !ImGui.IsAnyItemActive())) {
            config.Save();
            unsaved = false;
        }
    }

    private static float ItemWidth => 220 * ImGui.GetIO().FontGlobalScale;

    private static string NameDisplayType(UiConfigOption option)
    {
        if (!Service.GameConfig.TryGet(option, out uint value)) {
            return "unknown";
        }

        return value switch
        {
            0 => "Full Name",
            1 => "Surname Abbreviated",
            2 => "Forename Abbreviated",
            3 => "Initials",
            _ => "unknown",
        };
    }

    private static Vector4 FromRgba(uint rgba) => new(
        ((rgba >> 24) & 0xFF) / 255f,
        ((rgba >> 16) & 0xFF) / 255f,
        ((rgba >> 8) & 0xFF) / 255f,
        1f);

    private static bool EnumCombo<T>(string label, ref T value, (T Value, string Name)[] options) where T : struct, Enum
    {
        var preview = string.Empty;
        foreach (var (optionValue, name) in options) {
            if (EqualityComparer<T>.Default.Equals(optionValue, value)) {
                preview = name;
                break;
            }
        }

        using var combo = ImRaii.Combo(label, preview);
        if (!combo) {
            return false;
        }

        var changed = false;
        foreach (var (optionValue, name) in options) {
            var selected = EqualityComparer<T>.Default.Equals(optionValue, value);
            if (ImGui.Selectable(name, selected) && !selected) {
                value = optionValue;
                changed = true;
            }
        }

        return changed;
    }

    private static void SectionHeader(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudViolet, title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) {
            using var tooltip = ImRaii.Tooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
    }
}
