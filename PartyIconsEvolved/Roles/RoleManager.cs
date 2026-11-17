using System;
using System.Collections.Generic;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Game;

namespace PartyIconsEvolved.Roles;

/// <summary>
/// Owns who has which role. Explicit assignments (chat, right-click menu, settings window) always win; everyone
/// else in the party is filled in from their job when auto-assignment is enabled.
/// </summary>
internal sealed class RoleManager : IDisposable
{
    private readonly PluginConfiguration config;
    private readonly PartyWatcher party;
    private readonly RoleAssignments assignments = new();

    /// <summary>Raised whenever the role of any player may have changed.</summary>
    public event Action? Changed;

    public RoleManager(PluginConfiguration config, PartyWatcher party)
    {
        this.config = config;
        this.party = party;

        party.Changed += Recalculate;
        party.LeftParty += OnLeftParty;
        Recalculate();
    }

    public void Dispose()
    {
        party.Changed -= Recalculate;
        party.LeftParty -= OnLeftParty;
    }

    public bool TryGetRole(PlayerKey key, out RoleId role) => assignments.TryGetRole(key, out role);

    public RoleId GetRole(PlayerKey key) => assignments.GetRole(key);

    /// <summary>Whether the player's role was set explicitly rather than derived from their job.</summary>
    public bool IsManual(PlayerKey key) => assignments.IsManual(key);

    /// <summary>Explicitly gives a role to a player; <see cref="RoleId.None"/> returns them to automatic.</summary>
    public void Assign(PlayerKey key, RoleId role)
    {
        if (!key.IsValid) {
            return;
        }

        if (role == RoleId.None) {
            Clear(key);
            return;
        }

        assignments.SetManual(key, role);
        Recalculate();
    }

    /// <summary>Removes an explicit assignment, returning the player to automatic assignment.</summary>
    public void Clear(PlayerKey key)
    {
        if (assignments.ClearManual(key)) {
            Recalculate();
        }
    }

    /// <summary>Removes every explicit assignment.</summary>
    public void Reset()
    {
        assignments.ClearAllManual();
        Recalculate();
    }

    public void Recalculate()
    {
        assignments.Resolve(EnumerateParty(), config.AutoAssignRoles);
        Changed?.Invoke();
    }

    private IEnumerable<(PlayerKey Key, RoleCategory Category)> EnumerateParty()
    {
        foreach (var member in party.Members) {
            yield return (member.Key, JobData.GetCategory(member.JobId));
        }
    }

    private void OnLeftParty()
    {
        if (assignments.HasManual) {
            Service.Log.Debug("Left the party, clearing role assignments");
            Reset();
        }
    }
}
