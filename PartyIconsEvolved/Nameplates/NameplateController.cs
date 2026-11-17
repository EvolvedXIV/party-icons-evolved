using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;
using PartyIconsEvolved.Roles;

namespace PartyIconsEvolved.Nameplates;

/// <summary>
/// Drives the nameplates of party members. Text is changed through Dalamud's nameplate service while the game is
/// filling in nameplate data; the job icon is a native image node placed once the game has laid the plate out.
/// </summary>
internal sealed unsafe class NameplateController : IDisposable
{
    private const string AddonName = "NamePlate";

    /// <summary>Node id of our icon inside each nameplate component ("PIE1").</summary>
    private const uint IconNodeId = 0x50494531;

    /// <summary>
    /// A blank game icon. Showing it makes the game position its own name icon node even when it has nothing to
    /// show there, which gives us a reliable anchor next to the name.
    /// </summary>
    private const int BlankIconId = 61696;

    // Geometry of the name container, in its own (unscaled) coordinates.
    private const float PlateCenterX = 144f;
    private const float PlateBottomY = 107f;
    private const float IconHalf = IconNode.Size / 2f;
    private const float LineHeight = 18f;

    // Distance between the game's name icon node and where an icon should sit to hug the name.
    private const float AnchorGapBesideName = -6f;
    private const float AnchorGapBesideNativeIcon = -28f;

    private enum Stage
    {
        /// <summary>Waiting for the addon to draw once, so that it has finished setting itself up.</summary>
        WaitingForDraw,
        WaitingForNodes,
        Ready,
        Failed,
    }

    private readonly PluginConfiguration config;
    private readonly PartyWatcher party;
    private readonly RoleManager roles;
    private readonly NameplateText text;

    private PlateState[] plates = [];
    private Stage stage = Stage.WaitingForDraw;
    private bool preview;

    public NameplateController(PluginConfiguration config, PartyWatcher party, RoleManager roles, NameplateText text)
    {
        this.config = config;
        this.party = party;
        this.roles = roles;
        this.text = text;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnPreFinalize);
        Service.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
        Service.NamePlateGui.OnPostNamePlateUpdate += OnPostNamePlateUpdate;
        roles.Changed += Refresh;
    }

    /// <summary>Applies the configuration to every nearby player, so settings can be tried out anywhere.</summary>
    public bool Preview
    {
        get => preview;
        set {
            if (preview != value) {
                preview = value;
                Refresh();
            }
        }
    }

    public void Dispose()
    {
        roles.Changed -= Refresh;
        Service.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        Service.NamePlateGui.OnPostNamePlateUpdate -= OnPostNamePlateUpdate;
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, AddonName, OnPreDraw);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, OnPreFinalize);

        Teardown((AddonNamePlate*)Service.GameGui.GetAddonByName(AddonName).Address);

        // Have the game write its own text back now that we are no longer listening.
        Service.NamePlateGui.RequestRedraw();
    }

    /// <summary>Asks the game to rebuild all nameplates, picking up changed settings or roles.</summary>
    public void Refresh() => Service.NamePlateGui.RequestRedraw();

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        Teardown((AddonNamePlate*)args.Addon.Address);
        stage = Stage.WaitingForDraw;
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        switch (stage) {
            case Stage.WaitingForDraw:
                stage = Stage.WaitingForNodes;
                return;

            case Stage.WaitingForNodes:
                var addon = (AddonNamePlate*)args.Addon.Address;
                try {
                    if (CreateNodes(addon)) {
                        stage = Stage.Ready;
                        Refresh();
                    }
                }
                catch (Exception e) {
                    Service.Log.Error(e, "Could not add job icon nodes to the nameplates; nameplates are left untouched");
                    DestroyNodes(addon);
                    plates = [];
                    stage = Stage.Failed;
                }

                return;

            case Stage.Failed:
                return;
        }

        var isPvP = Service.ClientState.IsPvP;

        foreach (var plate in plates) {
            if (!plate.Modified) {
                continue;
            }

            var obj = plate.Object;
            if (isPvP ||
                obj->NamePlateKind != UIObjectKind.PlayerCharacter ||
                (obj->RootComponentNode->NodeFlags & NodeFlags.Visible) == 0) {
                Reset(plate);
                continue;
            }

            if (plate.IconVisible) {
                SyncIconFlags(plate);
            }

            if (plate.NeedsCollisionUpdate) {
                UpdateCollision(plate);
            }
        }
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (stage != Stage.Ready) {
            return;
        }

        foreach (var handler in handlers) {
            var index = handler.NamePlateIndex;
            if ((uint)index >= (uint)plates.Length) {
                continue;
            }

            var plate = plates[index];
            try {
                if (handler.NamePlateKind == NamePlateKind.PlayerCharacter && TryStyle(handler, out var layout)) {
                    plate.Layout = layout;
                    plate.HasPendingLayout = true;
                    plate.Modified = true;
                }
                else if (plate.Modified) {
                    Reset(plate);
                }
            }
            catch (Exception e) {
                Service.Log.Error(e, "Nameplate update failed");
                Reset(plate);
            }
        }
    }

    private void OnPostNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (stage != Stage.Ready) {
            return;
        }

        foreach (var plate in plates) {
            if (plate.HasPendingLayout) {
                ApplyLayout(plate);
            }
        }
    }

    /// <summary>
    /// Decides whether a player nameplate is ours and, if so, rewrites its text. Returns false to leave the plate
    /// exactly as the game made it.
    /// </summary>
    private bool TryStyle(INamePlateUpdateHandler handler, out PlateLayout layout)
    {
        layout = default;

        if (!config.Enabled || Service.ClientState.IsPvP) {
            return false;
        }

        if (handler.PlayerCharacter is not { } player) {
            return false;
        }

        var entityId = player.EntityId;
        var isSelf = entityId == Service.PlayerState.EntityId;
        var isPartyMember = !isSelf && PartyWatcher.IsPartyMember(entityId);

        if (!preview) {
            if (config.OnlyInDuties && !Service.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95)) {
                return false;
            }

            var applies = isSelf
                ? config.IncludeSelf && (party.InParty || config.IncludeSelfWhenSolo)
                : isPartyMember;
            if (!applies) {
                return false;
            }
        }

        var character = (Character*)player.Address;
        uint jobId = character->CharacterData.ClassJob;
        var category = JobData.GetCategory(jobId);

        var role = RoleId.None;
        if (config.ShowRoleIndicator || config.NameColor == NameColorMode.Role) {
            var key = new PlayerKey(((GameObject*)player.Address)->NameString, character->HomeWorld);
            if (!roles.TryGetRole(key, out role) && preview && !isSelf && !isPartyMember) {
                // Bystanders have no role; show a plausible one so the preview is representative.
                var preferred = category.PreferredRoles();
                role = preferred.Length > 0 ? preferred[0] : RoleId.None;
            }
        }

        text.Apply(handler, role, category);

        if (config.HideTitle) {
            handler.DisplayTitle = false;
        }

        if (config.HideFreeCompanyTag) {
            handler.RemoveFreeCompanyTag();
        }

        layout.ShowIcon = config.ShowJobIcon && JobData.TryGetIcon(config.JobIconStyle, jobId, out layout.Icon);

        var nativeIcon = handler.NameIconId;
        layout.HasNativeIcon = nativeIcon > 0 && nativeIcon != BlankIconId;
        if (layout.HasNativeIcon && config.HideStatusIcon) {
            layout.HasNativeIcon = false;
            handler.NameIconId = layout.ShowIcon ? BlankIconId : -1;
        }
        else if (!layout.HasNativeIcon && layout.ShowIcon) {
            handler.NameIconId = BlankIconId;
        }

        if (handler.DisplayTitle && handler.GetFieldAsSpan(NamePlateStringField.Title).Length > 0) {
            layout.TitleLine = handler.IsPrefixTitle ? -1 : 1;
        }

        return true;
    }

    /// <summary>Positions the job icon and applies the plate scale, after the game has laid out the plate.</summary>
    private void ApplyLayout(PlateState plate)
    {
        plate.HasPendingLayout = false;

        var obj = plate.Object;
        var node = plate.Icon;
        var layout = plate.Layout;

        if (layout.ShowIcon) {
            var padding = layout.Icon.Padding;
            var scale = layout.Icon.Scale * config.JobIconScale;

            // The game keeps its own name icon vertically centred on the name, directly to the left of it.
            var anchor = obj->NameIcon;
            var lineCenterY = anchor->Y + IconHalf;
            float x, y;

            switch (config.JobIconPosition) {
                case JobIconPosition.Right:
                    // Mirror image of the left position around the centre of the plate.
                    node->OriginX = padding;
                    node->OriginY = IconHalf;
                    x = (2 * PlateCenterX) - (anchor->X + IconNode.Size + AnchorGapBesideName) - padding;
                    y = anchor->Y;
                    break;

                case JobIconPosition.Above:
                    node->OriginX = IconHalf;
                    node->OriginY = IconNode.Size - padding;
                    x = PlateCenterX - IconHalf;
                    y = lineCenterY - (LineHeight / 2f) - (layout.TitleLine < 0 ? LineHeight : 0f) - IconNode.Size + padding;
                    break;

                case JobIconPosition.Below:
                    node->OriginX = IconHalf;
                    node->OriginY = padding;
                    x = PlateCenterX - IconHalf;
                    y = lineCenterY + (LineHeight / 2f) + (layout.TitleLine > 0 ? LineHeight : 0f) - padding;
                    break;

                default:
                    // Scale away from the name, so the gap to the text stays the same at any size.
                    node->OriginX = IconNode.Size - padding;
                    node->OriginY = IconHalf;
                    x = anchor->X + (layout.HasNativeIcon ? AnchorGapBesideNativeIcon : AnchorGapBesideName) + padding;
                    y = anchor->Y;
                    break;
            }

            node->SetScale(scale, scale);
            node->SetPositionFloat(x + config.JobIconOffsetX, y + config.JobIconOffsetY);

            if (plate.LoadedIconId != layout.Icon.IconId) {
                node->LoadIconTexture(layout.Icon.IconId, 0);
                plate.LoadedIconId = layout.Icon.IconId;
            }

            plate.IconVisible = true;
            SyncIconFlags(plate);
        }
        else {
            node->ToggleVisibility(false);
            plate.IconVisible = false;
        }

        var container = obj->NameContainer;
        if (MathF.Abs(config.PlateScale - 1f) < 0.001f) {
            if (plate.ScaleModified) {
                container->OriginX = 0;
                container->OriginY = 0;
                container->SetScale(1f, 1f);
                plate.ScaleModified = false;
                plate.NeedsCollisionUpdate = true;
            }
        }
        else {
            container->OriginX = PlateCenterX;
            container->OriginY = PlateBottomY;
            container->SetScale(config.PlateScale, config.PlateScale);
            plate.ScaleModified = true;
            plate.NeedsCollisionUpdate = true;
        }
    }

    /// <summary>
    /// The icon follows the name text: visible only while the name is, and depth-sorted the same way.
    /// </summary>
    private static void SyncIconFlags(PlateState plate)
    {
        const NodeFlags synced = NodeFlags.UseDepthBasedPriority | NodeFlags.Visible;
        var nameFlags = plate.Object->NameText->NodeFlags;
        plate.Icon->NodeFlags ^= (plate.Icon->NodeFlags ^ nameFlags) & synced;
    }

    /// <summary>Keeps the clickable area of the plate in step with its scale.</summary>
    private static void UpdateCollision(PlateState plate)
    {
        plate.NeedsCollisionUpdate = false;

        var collision = plate.Object->NameplateCollision;
        if (plate.ScaleModified) {
            var scale = plate.Object->NameContainer->ScaleX;
            collision->OriginX = collision->Width / 2f;
            collision->OriginY = collision->Height;
            collision->SetScale(scale, scale);
            plate.CollisionModified = true;
        }
        else if (plate.CollisionModified) {
            ResetCollision(plate);
        }
    }

    private static void ResetCollision(PlateState plate)
    {
        var collision = plate.Object->NameplateCollision;
        collision->OriginX = 0;
        collision->OriginY = 0;
        collision->SetScale(1f, 1f);
        plate.CollisionModified = false;
    }

    /// <summary>Returns the nodes of a plate to how the game expects them.</summary>
    private static void Reset(PlateState plate)
    {
        if (plate.ScaleModified) {
            var container = plate.Object->NameContainer;
            container->OriginX = 0;
            container->OriginY = 0;
            container->SetScale(1f, 1f);
            plate.ScaleModified = false;
        }

        if (plate.CollisionModified) {
            ResetCollision(plate);
        }

        plate.Icon->ToggleVisibility(false);
        plate.IconVisible = false;
        plate.NeedsCollisionUpdate = false;
        plate.HasPendingLayout = false;
        plate.Modified = false;
    }

    private void Teardown(AddonNamePlate* addon)
    {
        if (addon != null) {
            foreach (var plate in plates) {
                Reset(plate);
            }

            DestroyNodes(addon);
        }

        plates = [];
    }

    /// <summary>
    /// Adds an icon node to every nameplate. Returns false if the addon is not fully loaded yet, in which case it is
    /// safe to simply call this again on a later frame.
    /// </summary>
    private bool CreateNodes(AddonNamePlate* addon)
    {
        var objects = addon->NamePlateObjectArray;
        if (objects == null) {
            return false;
        }

        var created = new PlateState[AddonNamePlate.NumNamePlateObjects];
        for (var i = 0; i < created.Length; i++) {
            var obj = &objects[i];
            if (!TryGetUldManager(obj, out var uldManager) || uldManager->LoadedState != AtkLoadState.Loaded) {
                return false;
            }

            // A node may be left over if nameplates were only partially set up on an earlier attempt.
            var node = IconNode.Find(uldManager, IconNodeId);
            if (node == null) {
                node = IconNode.Create(IconNodeId);
                if (node == null) {
                    throw new InvalidOperationException($"Could not allocate the icon node for nameplate {i}");
                }

                if (!IconNode.Link(node, obj->NameContainer, uldManager)) {
                    IconNode.Free(node);
                    throw new InvalidOperationException($"Nameplate {i} has no nodes to attach the icon to");
                }
            }

            created[i] = new PlateState { Object = obj, Icon = node };
        }

        plates = created;
        return true;
    }

    private static void DestroyNodes(AddonNamePlate* addon)
    {
        var objects = addon->NamePlateObjectArray;
        if (objects == null) {
            return;
        }

        for (var i = 0; i < AddonNamePlate.NumNamePlateObjects; i++) {
            if (!TryGetUldManager(&objects[i], out var uldManager)) {
                continue;
            }

            var node = IconNode.Find(uldManager, IconNodeId);
            if (node != null) {
                IconNode.Destroy(node, uldManager);
            }
        }
    }

    private static bool TryGetUldManager(AddonNamePlate.NamePlateObject* obj, out AtkUldManager* uldManager)
    {
        uldManager = null;

        var container = obj->NameContainer;
        if (container == null || container->ParentNode == null) {
            return false;
        }

        var componentNode = container->ParentNode->GetAsAtkComponentNode();
        if (componentNode == null || componentNode->Component == null) {
            return false;
        }

        uldManager = &componentNode->Component->UldManager;
        return true;
    }
}
