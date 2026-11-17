using System;
using PartyIconsEvolved.Configuration;

namespace PartyIconsEvolved.Roles;

/// <summary>A raid-plan position.</summary>
public enum RoleId
{
    None = 0,
    MT,
    OT,
    H1,
    H2,
    M1,
    M2,
    R1,
    R2,
}

public enum RoleCategory
{
    None = 0,
    Tank,
    Healer,
    Melee,
    Ranged,
}

public static class RoleExtensions
{
    /// <summary>All assignable roles, in raid-plan order.</summary>
    public static readonly RoleId[] AllRoles =
        [RoleId.MT, RoleId.OT, RoleId.H1, RoleId.H2, RoleId.M1, RoleId.M2, RoleId.R1, RoleId.R2];

    public static readonly RoleCategory[] AllCategories =
        [RoleCategory.Tank, RoleCategory.Healer, RoleCategory.Melee, RoleCategory.Ranged];

    private static readonly RoleId[] TankRoles = [RoleId.MT, RoleId.OT];
    private static readonly RoleId[] HealerRoles = [RoleId.H1, RoleId.H2];
    private static readonly RoleId[] MeleeRoles = [RoleId.M1, RoleId.M2, RoleId.R1, RoleId.R2];
    private static readonly RoleId[] RangedRoles = [RoleId.R1, RoleId.R2, RoleId.M1, RoleId.M2];

    public static RoleCategory Category(this RoleId role) => role switch
    {
        RoleId.MT or RoleId.OT => RoleCategory.Tank,
        RoleId.H1 or RoleId.H2 => RoleCategory.Healer,
        RoleId.M1 or RoleId.M2 => RoleCategory.Melee,
        RoleId.R1 or RoleId.R2 => RoleCategory.Ranged,
        _ => RoleCategory.None,
    };

    /// <summary>The text shown for a role, e.g. "OT" or "ST" depending on the naming scheme.</summary>
    public static string Label(this RoleId role, RoleNaming naming)
    {
        if (naming == RoleNaming.Eastern) {
            switch (role) {
                case RoleId.OT: return "ST";
                case RoleId.M1: return "D1";
                case RoleId.M2: return "D2";
                case RoleId.R1: return "D3";
                case RoleId.R2: return "D4";
            }
        }

        return role == RoleId.None ? string.Empty : role.ToString();
    }

    /// <summary>
    /// Roles a job of this category may be auto-assigned, in order of preference. Melee and ranged spill over into
    /// each other's slots so that e.g. a third melee still gets an indicator.
    /// </summary>
    public static ReadOnlySpan<RoleId> PreferredRoles(this RoleCategory category) => category switch
    {
        RoleCategory.Tank => TankRoles,
        RoleCategory.Healer => HealerRoles,
        RoleCategory.Melee => MeleeRoles,
        RoleCategory.Ranged => RangedRoles,
        _ => [],
    };

    /// <summary>The game UI colour (UIColor row) used by default, identical to xivPartyIcons.</summary>
    public static ushort DefaultUiColor(this RoleCategory category) => category switch
    {
        RoleCategory.Tank => 37,
        RoleCategory.Healer => 42,
        RoleCategory.Melee => 524,
        RoleCategory.Ranged => 32,
        _ => 2,
    };

    public static string DisplayName(this RoleCategory category) => category switch
    {
        RoleCategory.Tank => "Tank",
        RoleCategory.Healer => "Healer",
        RoleCategory.Melee => "Melee",
        RoleCategory.Ranged => "Ranged",
        _ => "None",
    };

    /// <summary>
    /// Parses a chat message that consists of nothing but a role, e.g. "M1" or " ot ".
    /// </summary>
    public static bool TryParseRole(ReadOnlySpan<char> text, bool allowAliases, out RoleId role)
    {
        role = RoleId.None;
        text = text.Trim();
        if (text.Length != 2) {
            return false;
        }

        Span<char> upper = stackalloc char[2];
        upper[0] = char.ToUpperInvariant(text[0]);
        upper[1] = char.ToUpperInvariant(text[1]);

        role = upper switch
        {
            "MT" => RoleId.MT,
            "OT" => RoleId.OT,
            "H1" => RoleId.H1,
            "H2" => RoleId.H2,
            "M1" => RoleId.M1,
            "M2" => RoleId.M2,
            "R1" => RoleId.R1,
            "R2" => RoleId.R2,
            _ => RoleId.None,
        };

        if (role == RoleId.None && allowAliases) {
            role = upper switch
            {
                "ST" => RoleId.OT,
                "T1" => RoleId.MT,
                "T2" => RoleId.OT,
                "D1" => RoleId.M1,
                "D2" => RoleId.M2,
                "D3" => RoleId.R1,
                "D4" => RoleId.R2,
                _ => RoleId.None,
            };
        }

        return role != RoleId.None;
    }
}
