using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;
using PartyIconsEvolved.Nameplates;
using PartyIconsEvolved.Roles;
using PartyIconsEvolved.Windows;

namespace PartyIconsEvolved;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/pie";

    private readonly WindowSystem windowSystem = new("PartyIconsEvolved");
    private readonly ConfigWindow configWindow;
    private readonly ChatRoleListener chatListener;
    private readonly RoleContextMenu contextMenu;

    internal PluginConfiguration Configuration { get; }
    internal PartyWatcher Party { get; }
    internal RoleManager Roles { get; }
    internal NameplateText Text { get; }
    internal NameplateController Nameplates { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        JobData.Initialize();
        Configuration = PluginConfiguration.Load();

        Party = new PartyWatcher();
        Roles = new RoleManager(Configuration, Party);
        Text = new NameplateText(Configuration);
        Nameplates = new NameplateController(Configuration, Party, Roles, Text);
        chatListener = new ChatRoleListener(Configuration, Roles);
        contextMenu = new RoleContextMenu(Configuration, Party, Roles);

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;

        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Party Icons Evolved settings. \"/pie reset\" clears all role assignments.",
        });
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler(Command);

        Service.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        Service.PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();

        configWindow.SaveIfNeeded(force: true);

        contextMenu.Dispose();
        chatListener.Dispose();
        Nameplates.Dispose();
        Roles.Dispose();
        Party.Dispose();
    }

    /// <summary>Makes a changed setting take effect immediately.</summary>
    internal void OnConfigurationChanged()
    {
        Text.Invalidate();

        // Also raises Roles.Changed, which redraws the nameplates.
        Roles.Recalculate();
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant()) {
            case "":
            case "config":
                configWindow.Toggle();
                break;

            case "reset":
                Roles.Reset();
                Service.ChatGui.Print("Role assignments were reset.", "Party Icons Evolved");
                break;

            default:
                Service.ChatGui.Print($"Unknown argument. Usage: {Command} [config|reset]", "Party Icons Evolved");
                break;
        }
    }
}
