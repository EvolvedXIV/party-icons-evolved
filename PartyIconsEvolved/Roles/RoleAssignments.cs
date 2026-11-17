using System.Collections.Generic;
using PartyIconsEvolved.Game;

namespace PartyIconsEvolved.Roles;

/// <summary>
/// The role bookkeeping itself, free of any game access: explicit ("manual") assignments made by chat, the
/// right-click menu or the settings window, and the resolved view in which the rest of the party is filled in
/// from their jobs.
/// </summary>
internal sealed class RoleAssignments
{
    private readonly Dictionary<PlayerKey, RoleId> manual = new();
    private readonly Dictionary<PlayerKey, RoleId> resolved = new();

    public bool HasManual => manual.Count != 0;

    public bool TryGetRole(PlayerKey key, out RoleId role) => resolved.TryGetValue(key, out role);

    public RoleId GetRole(PlayerKey key) => resolved.GetValueOrDefault(key);

    public bool IsManual(PlayerKey key) => manual.ContainsKey(key);

    /// <summary>
    /// Explicitly gives <paramref name="role"/> to a player. If someone else was explicitly holding it, they take
    /// over whatever role this player had, so two people can trade places with a single action.
    /// Call <see cref="Resolve"/> afterwards.
    /// </summary>
    public void SetManual(PlayerKey key, RoleId role)
    {
        var previous = resolved.GetValueOrDefault(key);

        PlayerKey? displaced = null;
        foreach (var (other, otherRole) in manual) {
            if (otherRole == role && other != key) {
                displaced = other;
                break;
            }
        }

        if (displaced is { } displacedKey) {
            if (previous != RoleId.None && previous != role) {
                manual[displacedKey] = previous;
            }
            else {
                manual.Remove(displacedKey);
            }
        }

        manual[key] = role;
    }

    /// <summary>Removes an explicit assignment. Returns false if there was none.</summary>
    public bool ClearManual(PlayerKey key) => manual.Remove(key);

    public void ClearAllManual() => manual.Clear();

    /// <summary>
    /// Rebuilds the resolved roles. Explicit assignments always apply. With <paramref name="autoAssign"/>, the
    /// remaining members get the first free role that suits their job, in the order given (party list order).
    /// </summary>
    public void Resolve(IEnumerable<(PlayerKey Key, RoleCategory Category)> members, bool autoAssign)
    {
        resolved.Clear();

        foreach (var (key, role) in manual) {
            resolved[key] = role;
        }

        if (!autoAssign) {
            return;
        }

        var pending = new List<(PlayerKey Key, RoleCategory Category)>();
        var taken = new HashSet<RoleId>();

        // Only explicit assignments of people who are actually here block a slot.
        foreach (var member in members) {
            if (manual.TryGetValue(member.Key, out var role)) {
                taken.Add(role);
            }
            else if (member.Key.IsValid) {
                pending.Add(member);
            }
        }

        foreach (var (key, category) in pending) {
            if (resolved.ContainsKey(key)) {
                continue;
            }

            foreach (var candidate in category.PreferredRoles()) {
                if (taken.Add(candidate)) {
                    resolved[key] = candidate;
                    break;
                }
            }
        }
    }
}
