using System;
using System.Numerics;
using Dalamud.Configuration;
using PartyIconsEvolved.Roles;

namespace PartyIconsEvolved.Configuration;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled = true;

    // Which nameplates are touched.
    public bool IncludeSelf = true;
    public bool IncludeSelfWhenSolo;
    public bool OnlyInDuties;

    // Name.
    public bool ShowName = true;
    public int MaxNameLength;
    public bool AddEllipsis;
    public NameColorMode NameColor = NameColorMode.GameDefault;
    public Vector4 CustomNameColor = new(1f, 1f, 1f, 1f);
    public bool HideTitle;
    public bool HideFreeCompanyTag;
    public float PlateScale = 1f;

    // Job icon.
    public bool ShowJobIcon = true;
    public JobIconStyle JobIconStyle = JobIconStyle.Framed;
    public JobIconPosition JobIconPosition = JobIconPosition.Left;
    public float JobIconScale = 1f;
    public float JobIconOffsetX;
    public float JobIconOffsetY;
    public bool HideStatusIcon;

    // Role indicator.
    public bool ShowRoleIndicator = true;
    public RoleIndicatorStyle RoleIndicatorStyle = RoleIndicatorStyle.Boxed;
    public RoleIndicatorPosition RoleIndicatorPosition = RoleIndicatorPosition.BeforeName;
    public RoleNaming RoleNaming = RoleNaming.Western;
    public float RoleIndicatorScale = 1f;
    public float RoleIndicatorOffsetX;
    public float RoleIndicatorOffsetY;

    // Role colours. Null means "use the game's own UI colour", which matches xivPartyIcons.
    public Vector4? TankColor;
    public Vector4? HealerColor;
    public Vector4? MeleeColor;
    public Vector4? RangedColor;

    // Role assignment.
    public bool AutoAssignRoles = true;
    public bool AssignFromChat = true;
    public bool ChatAliases = true;
    public bool NotifyChatAssignment = true;
    public bool ContextMenu = true;

    /// <summary>
    /// Whether the role indicator has its own size or position. It is then drawn by a separate node instead of
    /// being part of the name text, which cannot be scaled or moved piece by piece.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool RoleIndicatorFloats =>
        MathF.Abs(RoleIndicatorScale - 1f) > 0.001f || RoleIndicatorOffsetX != 0f || RoleIndicatorOffsetY != 0f;

    public Vector4? GetRoleColor(RoleCategory category) => category switch
    {
        RoleCategory.Tank => TankColor,
        RoleCategory.Healer => HealerColor,
        RoleCategory.Melee => MeleeColor,
        RoleCategory.Ranged => RangedColor,
        _ => null,
    };

    public void SetRoleColor(RoleCategory category, Vector4? color)
    {
        switch (category) {
            case RoleCategory.Tank: TankColor = color; break;
            case RoleCategory.Healer: HealerColor = color; break;
            case RoleCategory.Melee: MeleeColor = color; break;
            case RoleCategory.Ranged: RangedColor = color; break;
        }
    }

    /// <summary>Clamps values that may have been hand-edited or come from an older version.</summary>
    public void Sanitize()
    {
        MaxNameLength = Math.Clamp(MaxNameLength, 0, 64);
        PlateScale = Math.Clamp(PlateScale, 0.5f, 2.5f);
        JobIconScale = Math.Clamp(JobIconScale, 0.25f, 4f);
        JobIconOffsetX = Math.Clamp(JobIconOffsetX, -150f, 150f);
        JobIconOffsetY = Math.Clamp(JobIconOffsetY, -150f, 150f);
        RoleIndicatorScale = Math.Clamp(RoleIndicatorScale, 0.25f, 4f);
        RoleIndicatorOffsetX = Math.Clamp(RoleIndicatorOffsetX, -150f, 150f);
        RoleIndicatorOffsetY = Math.Clamp(RoleIndicatorOffsetY, -150f, 150f);
    }

    public static PluginConfiguration Load()
    {
        PluginConfiguration? config = null;
        try {
            config = Service.PluginInterface.GetPluginConfig() as PluginConfiguration;
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not read the configuration, starting from defaults");
        }

        config ??= new PluginConfiguration();
        config.Sanitize();
        return config;
    }

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
