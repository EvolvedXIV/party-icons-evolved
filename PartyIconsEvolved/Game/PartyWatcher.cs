using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PartyIconsEvolved.Game;

/// <summary>A member of the current party (or the local player when solo).</summary>
internal sealed record PartyMemberInfo(PlayerKey Key, ulong ContentId, uint EntityId, uint JobId, bool IsSelf);

/// <summary>
/// Watches the party for changes in members, jobs and party list order, and keeps a snapshot sorted the way the
/// party list shows it.
/// </summary>
internal sealed unsafe class PartyWatcher : IDisposable
{
    private const int MaxMembers = 8;

    private readonly record struct Slot(ulong ContentId, byte Job, byte Order);

    private readonly Slot[] signature = new Slot[MaxMembers];
    private readonly Dictionary<ulong, byte> lastKnownJob = new();
    private List<PartyMemberInfo> members = [];
    private int signatureCount = -1;

    /// <summary>Raised on the framework thread whenever <see cref="Members"/> changed.</summary>
    public event Action? Changed;

    /// <summary>Raised when the player stops being in a party.</summary>
    public event Action? LeftParty;

    /// <summary>Party members in party list order. Contains only the local player when solo.</summary>
    public IReadOnlyList<PartyMemberInfo> Members => members;

    /// <summary>Whether the player is in a party with at least one other member in this instance.</summary>
    public bool InParty { get; private set; }

    public PartyWatcher()
    {
        Service.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
    }

    public bool TryGetMember(PlayerKey key, out PartyMemberInfo member)
    {
        foreach (var candidate in members) {
            if (candidate.Key == key) {
                member = candidate;
                return true;
            }
        }

        member = null!;
        return false;
    }

    public static bool IsPartyMember(uint entityId)
    {
        var groupManager = GroupManager.Instance();
        return groupManager != null && groupManager->MainGroup.IsEntityIdInParty(entityId);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try {
            Update();
        }
        catch (Exception e) {
            Service.Log.Error(e, "Party update failed");
        }
    }

    private void Update()
    {
        if (!Service.PlayerState.IsLoaded) {
            if (signatureCount != 0) {
                signatureCount = 0;
                members = [];
                InParty = false;
                lastKnownJob.Clear();
                Changed?.Invoke();
            }

            return;
        }

        Span<Slot> current = stackalloc Slot[MaxMembers];
        var count = 0;

        var groupManager = GroupManager.Instance();
        var groupCount = groupManager == null ? 0 : Math.Min((int)groupManager->MainGroup.MemberCount, MaxMembers);

        if (groupCount == 0) {
            current[count++] = new Slot(Service.PlayerState.ContentId, (byte)Service.PlayerState.ClassJob.RowId, 0);
        }
        else {
            var partyMembers = groupManager->MainGroup.PartyMembers;
            for (var i = 0; i < groupCount; i++) {
                ref var member = ref partyMembers[i];
                current[count++] = new Slot(
                    member.ContentId,
                    EffectiveJob(member.ContentId, member.ClassJob),
                    GetPartyListOrder(member.ContentId, i));
            }
        }

        var wasInParty = InParty;
        InParty = groupCount > 0;

        if (count != signatureCount || !current[..count].SequenceEqual(signature.AsSpan(0, count))) {
            current[..count].CopyTo(signature);
            signatureCount = count;
            Rebuild(groupManager, groupCount);
            Changed?.Invoke();
        }

        // A cross-world party is not in the group manager outside of instances, so the party "emptying" there
        // does not mean it was left.
        if (wasInParty && !InParty && !Service.Condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance]) {
            lastKnownJob.Clear();
            LeftParty?.Invoke();
        }
    }

    /// <summary>
    /// Members who are out of range temporarily report job 0; keep showing what they were last seen as.
    /// </summary>
    private byte EffectiveJob(ulong contentId, byte job)
    {
        if (job != 0) {
            lastKnownJob[contentId] = job;
            return job;
        }

        return lastKnownJob.GetValueOrDefault(contentId);
    }

    private static byte GetPartyListOrder(ulong contentId, int fallback)
    {
        var hud = AgentHUD.Instance();
        if (hud != null && contentId != 0) {
            var hudMembers = hud->PartyMembers;
            var hudCount = Math.Min((int)hud->PartyMemberCount, hudMembers.Length);
            for (var i = 0; i < hudCount; i++) {
                if (hudMembers[i].ContentId == contentId) {
                    return hudMembers[i].Index;
                }
            }
        }

        // Not on the party list (yet): keep group order, after everyone who is.
        return (byte)(100 + fallback);
    }

    private void Rebuild(GroupManager* groupManager, int groupCount)
    {
        var list = new List<(byte Order, PartyMemberInfo Info)>(Math.Max(groupCount, 1));
        var selfContentId = Service.PlayerState.ContentId;

        if (groupCount == 0) {
            var key = new PlayerKey(Service.PlayerState.CharacterName, Service.PlayerState.HomeWorld.RowId);
            list.Add((0, new PartyMemberInfo(
                key, selfContentId, Service.PlayerState.EntityId, Service.PlayerState.ClassJob.RowId, true)));
        }
        else {
            var partyMembers = groupManager->MainGroup.PartyMembers;
            for (var i = 0; i < groupCount; i++) {
                ref var member = ref partyMembers[i];
                var key = new PlayerKey(member.NameString, member.HomeWorld);
                list.Add((signature[i].Order, new PartyMemberInfo(
                    key, member.ContentId, member.EntityId, signature[i].Job, member.ContentId == selfContentId)));
            }

            list.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        }

        var result = new List<PartyMemberInfo>(list.Count);
        foreach (var (_, info) in list) {
            result.Add(info);
        }

        members = result;
    }
}
