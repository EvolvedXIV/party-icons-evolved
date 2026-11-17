using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PartyIconsEvolved.Game;

namespace PartyIconsEvolved.Nameplates;

/// <summary>Per-nameplate bookkeeping: the game's nameplate object, our icon node and what we changed.</summary>
internal sealed unsafe class PlateState
{
    public AddonNamePlate.NamePlateObject* Object;
    public AtkImageNode* Icon;

    /// <summary>We changed something on this plate that has to be undone when it stops being ours.</summary>
    public bool Modified;

    /// <summary>Set during the data update, consumed once the game has laid the plate out.</summary>
    public bool HasPendingLayout;
    public PlateLayout Layout;

    public bool IconVisible;
    public uint LoadedIconId;
    public bool ScaleModified;
    public bool CollisionModified;
    public bool NeedsCollisionUpdate;
}

/// <summary>What to draw on a plate besides its text.</summary>
internal struct PlateLayout
{
    public bool ShowIcon;
    public JobIcon Icon;

    /// <summary>The game is showing its own icon (online status etc.) next to the name.</summary>
    public bool HasNativeIcon;

    /// <summary>-1 if the title is on the line above the name, 1 if below, 0 if there is no title.</summary>
    public int TitleLine;
}
