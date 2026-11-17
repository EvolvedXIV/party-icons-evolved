namespace PartyIconsEvolved.Configuration;

public enum NameColorMode
{
    /// <summary>Leave the colour the game picked.</summary>
    GameDefault,

    /// <summary>Use the colour of the player's role (tank, healer, melee, ranged).</summary>
    Role,

    /// <summary>Use <see cref="PluginConfiguration.CustomNameColor"/>.</summary>
    Custom,
}

public enum JobIconStyle
{
    /// <summary>Framed icons with a role-coloured backdrop, as used by the party list.</summary>
    Framed,

    /// <summary>Plain gold/silver embossed icons.</summary>
    Embossed,

    /// <summary>Glowing gold/silver icons.</summary>
    Glowing,

    /// <summary>Flat icons tinted by role (blue, green, red, orange).</summary>
    RoleColored,
}

public enum JobIconPosition
{
    Left,
    Right,
    Above,
    Below,
}

public enum RoleIndicatorStyle
{
    /// <summary>The game's boxed letter glyphs.</summary>
    Boxed,

    /// <summary>Regular text.</summary>
    Plain,
}

public enum RoleIndicatorPosition
{
    BeforeName,
    AfterName,
}

public enum RoleNaming
{
    /// <summary>MT OT H1 H2 M1 M2 R1 R2.</summary>
    Western,

    /// <summary>MT ST H1 H2 D1 D2 D3 D4.</summary>
    Eastern,
}
