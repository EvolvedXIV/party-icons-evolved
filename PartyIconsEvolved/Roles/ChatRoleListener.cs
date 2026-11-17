using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;

namespace PartyIconsEvolved.Roles;

/// <summary>
/// Assigns a role to a party member when they send a party chat message that is nothing but that role, e.g. "M1".
/// </summary>
internal sealed class ChatRoleListener : IDisposable
{
    private readonly PluginConfiguration config;
    private readonly RoleManager roles;

    public ChatRoleListener(PluginConfiguration config, RoleManager roles)
    {
        this.config = config;
        this.roles = roles;

        Service.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Service.ChatGui.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!config.Enabled || !config.AssignFromChat) {
            return;
        }

        if (message.LogKind is not (XivChatType.Party or XivChatType.CrossParty)) {
            return;
        }

        try {
            if (!RoleExtensions.TryParseRole(message.Message.TextValue, config.ChatAliases, out var role)) {
                return;
            }

            // The game only attaches a player link to messages from other people.
            PlayerKey key = default;
            foreach (var payload in message.Sender.Payloads) {
                if (payload is PlayerPayload player) {
                    key = new PlayerKey(player.PlayerName, player.World.RowId);
                    break;
                }
            }

            if (!key.IsValid) {
                key = new PlayerKey(Service.PlayerState.CharacterName, Service.PlayerState.HomeWorld.RowId);
            }

            if (!key.IsValid || (roles.IsManual(key) && roles.GetRole(key) == role)) {
                return;
            }

            roles.Assign(key, role);

            if (config.NotifyChatAssignment) {
                Service.ToastGui.ShowQuest(
                    $"{key.Name}: {role.Label(config.RoleNaming)}",
                    new QuestToastOptions { DisplayCheckmark = true });
            }
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not process a chat message for role assignment");
        }
    }
}
