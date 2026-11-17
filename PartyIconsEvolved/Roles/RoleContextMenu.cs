using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;

namespace PartyIconsEvolved.Roles;

/// <summary>Adds a "Role Indicator" submenu to the right-click menu of party members.</summary>
internal sealed class RoleContextMenu : IDisposable
{
    private const string PartyListAddon = "_PartyList";

    private readonly PluginConfiguration config;
    private readonly PartyWatcher party;
    private readonly RoleManager roles;

    public RoleContextMenu(PluginConfiguration config, PartyWatcher party, RoleManager roles)
    {
        this.config = config;
        this.party = party;
        this.roles = roles;

        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose()
    {
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!config.Enabled || !config.ContextMenu) {
            return;
        }

        if (args is not { MenuType: ContextMenuType.Default, Target: MenuTargetDefault target }) {
            return;
        }

        if (!TryGetPlayer(target, out var key) || !IsEligible(args, key)) {
            return;
        }

        var current = roles.GetRole(key);
        args.AddMenuItem(new MenuItem
        {
            Name = current == RoleId.None
                ? "Role Indicator"
                : $"Role Indicator ({current.Label(config.RoleNaming)})",
            PrefixChar = 'R',
            PrefixColor = RoleCategory.Tank.DefaultUiColor(),
            IsSubmenu = true,
            OnClicked = clicked => clicked.OpenSubmenu(BuildRoleItems(key)),
        });
    }

    private List<MenuItem> BuildRoleItems(PlayerKey key)
    {
        var current = roles.GetRole(key);
        var items = new List<MenuItem>(RoleExtensions.AllRoles.Length + 1);

        foreach (var role in RoleExtensions.AllRoles) {
            var label = role.Label(config.RoleNaming);
            items.Add(new MenuItem
            {
                Name = role == current ? $"{label} (current)" : label,
                PrefixChar = label[0],
                PrefixColor = role.Category().DefaultUiColor(),
                Priority = items.Count,
                OnClicked = _ => roles.Assign(key, role),
            });
        }

        if (roles.IsManual(key)) {
            items.Add(new MenuItem
            {
                Name = "Automatic",
                Prefix = SeIconChar.BoxedLetterA,
                PrefixColor = RoleCategory.None.DefaultUiColor(),
                Priority = items.Count,
                OnClicked = _ => roles.Clear(key),
            });
        }

        return items;
    }

    /// <summary>Role indicators only make sense for yourself and the people in your party.</summary>
    private bool IsEligible(IMenuOpenedArgs args, PlayerKey key)
    {
        if (args.AddonName == PartyListAddon) {
            return true;
        }

        if (party.TryGetMember(key, out _)) {
            return true;
        }

        return key.Name == Service.PlayerState.CharacterName && key.WorldId == Service.PlayerState.HomeWorld.RowId;
    }

    private static bool TryGetPlayer(MenuTargetDefault target, out PlayerKey key)
    {
        if (target.TargetCharacter is { Name: { } characterName, HomeWorld.RowId: var characterWorld }) {
            key = new PlayerKey(characterName, characterWorld);
        }
        else if (target.TargetObject is IPlayerCharacter { Name.TextValue: { } objectName, HomeWorld.RowId: var objectWorld }) {
            key = new PlayerKey(objectName, objectWorld);
        }
        else if (target.TargetContentId != 0 && !string.IsNullOrEmpty(target.TargetName)) {
            key = new PlayerKey(target.TargetName, target.TargetHomeWorld.RowId);
        }
        else {
            key = default;
        }

        return key.IsValid;
    }
}
