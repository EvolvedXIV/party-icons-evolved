namespace PartyIconsEvolved.Game;

/// <summary>
/// Identifies a player by name and home world. Unlike an entity or content id this is available everywhere a role
/// can be assigned from: chat, the context menu and nameplates.
/// </summary>
internal readonly record struct PlayerKey(string Name, uint WorldId)
{
    public bool IsValid => !string.IsNullOrEmpty(Name) && WorldId != 0;
}
