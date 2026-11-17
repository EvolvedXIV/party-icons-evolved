using System.Linq;
using PartyIconsEvolved.Game;
using PartyIconsEvolved.Roles;
using Xunit;

namespace PartyIconsEvolved.Tests;

public class RoleAssignmentsTests
{
    private static PlayerKey Player(string name) => new(name, 1);

    private static (PlayerKey Key, RoleCategory Category)[] Party(params (string Name, RoleCategory Category)[] members) =>
        members.Select(m => (Player(m.Name), m.Category)).ToArray();

    private static readonly (PlayerKey Key, RoleCategory Category)[] StandardParty = Party(
        ("Tank A", RoleCategory.Tank),
        ("Healer A", RoleCategory.Healer),
        ("Melee A", RoleCategory.Melee),
        ("Ranged A", RoleCategory.Ranged),
        ("Tank B", RoleCategory.Tank),
        ("Healer B", RoleCategory.Healer),
        ("Melee B", RoleCategory.Melee),
        ("Ranged B", RoleCategory.Ranged));

    [Fact]
    public void AutoAssignsByJobInPartyOrder()
    {
        var roles = new RoleAssignments();
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.MT, roles.GetRole(Player("Tank A")));
        Assert.Equal(RoleId.OT, roles.GetRole(Player("Tank B")));
        Assert.Equal(RoleId.H1, roles.GetRole(Player("Healer A")));
        Assert.Equal(RoleId.H2, roles.GetRole(Player("Healer B")));
        Assert.Equal(RoleId.M1, roles.GetRole(Player("Melee A")));
        Assert.Equal(RoleId.M2, roles.GetRole(Player("Melee B")));
        Assert.Equal(RoleId.R1, roles.GetRole(Player("Ranged A")));
        Assert.Equal(RoleId.R2, roles.GetRole(Player("Ranged B")));
        Assert.False(roles.IsManual(Player("Tank A")));
    }

    [Fact]
    public void NothingIsAssignedWithoutAutoAssign()
    {
        var roles = new RoleAssignments();
        roles.Resolve(StandardParty, autoAssign: false);

        Assert.All(StandardParty, member => Assert.False(roles.TryGetRole(member.Key, out _)));
    }

    [Fact]
    public void ExtraMeleeSpillIntoRangedSlots()
    {
        var roles = new RoleAssignments();
        roles.Resolve(
            Party(
                ("Melee A", RoleCategory.Melee),
                ("Melee B", RoleCategory.Melee),
                ("Melee C", RoleCategory.Melee),
                ("Ranged A", RoleCategory.Ranged)),
            autoAssign: true);

        Assert.Equal(RoleId.M1, roles.GetRole(Player("Melee A")));
        Assert.Equal(RoleId.M2, roles.GetRole(Player("Melee B")));
        Assert.Equal(RoleId.R1, roles.GetRole(Player("Melee C")));
        Assert.Equal(RoleId.R2, roles.GetRole(Player("Ranged A")));
    }

    [Fact]
    public void MembersWithoutACombatJobGetNoRole()
    {
        var roles = new RoleAssignments();
        roles.Resolve(Party(("Crafter", RoleCategory.None), ("Tank A", RoleCategory.Tank)), autoAssign: true);

        Assert.False(roles.TryGetRole(Player("Crafter"), out _));
        Assert.Equal(RoleId.MT, roles.GetRole(Player("Tank A")));
    }

    [Fact]
    public void ManualAssignmentWinsAndOthersMoveOutOfTheWay()
    {
        var roles = new RoleAssignments();
        roles.Resolve(StandardParty, autoAssign: true);

        // The second melee on the list calls M1.
        roles.SetManual(Player("Melee B"), RoleId.M1);
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.M1, roles.GetRole(Player("Melee B")));
        Assert.True(roles.IsManual(Player("Melee B")));
        Assert.Equal(RoleId.M2, roles.GetRole(Player("Melee A")));
        Assert.False(roles.IsManual(Player("Melee A")));
    }

    [Fact]
    public void ManualAssignmentIsNotLimitedByJob()
    {
        var roles = new RoleAssignments();
        roles.SetManual(Player("Ranged A"), RoleId.MT);
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.MT, roles.GetRole(Player("Ranged A")));
        Assert.Equal(RoleId.OT, roles.GetRole(Player("Tank A")));
        // Only two tank slots exist, so the second tank is left without a role.
        Assert.False(roles.TryGetRole(Player("Tank B"), out _));
    }

    [Fact]
    public void TakingAManuallyHeldRoleSwapsTheTwoPlayers()
    {
        var roles = new RoleAssignments();
        roles.SetManual(Player("Healer A"), RoleId.H1);
        roles.Resolve(StandardParty, autoAssign: true);
        roles.SetManual(Player("Healer B"), RoleId.H2);
        roles.Resolve(StandardParty, autoAssign: true);

        roles.SetManual(Player("Healer B"), RoleId.H1);
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.H1, roles.GetRole(Player("Healer B")));
        Assert.Equal(RoleId.H2, roles.GetRole(Player("Healer A")));
    }

    [Fact]
    public void RolesAreNeverDuplicatedWithinTheParty()
    {
        var roles = new RoleAssignments();
        roles.Resolve(StandardParty, autoAssign: true);

        roles.SetManual(Player("Tank B"), RoleId.MT);
        roles.Resolve(StandardParty, autoAssign: true);
        roles.SetManual(Player("Ranged B"), RoleId.M1);
        roles.Resolve(StandardParty, autoAssign: true);
        roles.SetManual(Player("Melee A"), RoleId.R2);
        roles.Resolve(StandardParty, autoAssign: true);

        var assigned = StandardParty.Select(m => roles.GetRole(m.Key)).Where(r => r != RoleId.None).ToList();
        Assert.Equal(assigned.Count, assigned.Distinct().Count());
        Assert.Equal(8, assigned.Count);
    }

    [Fact]
    public void ClearingReturnsThePlayerToAutomatic()
    {
        var roles = new RoleAssignments();
        roles.SetManual(Player("Melee B"), RoleId.M1);
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.True(roles.ClearManual(Player("Melee B")));
        Assert.False(roles.ClearManual(Player("Melee B")));
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.M1, roles.GetRole(Player("Melee A")));
        Assert.Equal(RoleId.M2, roles.GetRole(Player("Melee B")));
        Assert.False(roles.HasManual);
    }

    [Fact]
    public void SomeoneWhoLeftDoesNotBlockTheirOldSlot()
    {
        var roles = new RoleAssignments();
        roles.SetManual(Player("Gone"), RoleId.MT);
        roles.Resolve(StandardParty, autoAssign: true);

        Assert.Equal(RoleId.MT, roles.GetRole(Player("Tank A")));
        Assert.Equal(RoleId.OT, roles.GetRole(Player("Tank B")));
    }

    [Fact]
    public void ManualAssignmentsStillShowWithAutoAssignOff()
    {
        var roles = new RoleAssignments();
        roles.SetManual(Player("Tank B"), RoleId.MT);
        roles.Resolve(StandardParty, autoAssign: false);

        Assert.Equal(RoleId.MT, roles.GetRole(Player("Tank B")));
        Assert.False(roles.TryGetRole(Player("Tank A"), out _));
    }

    [Fact]
    public void PlayersOnDifferentWorldsAreDistinct()
    {
        var roles = new RoleAssignments();
        roles.SetManual(new PlayerKey("Same Name", 1), RoleId.MT);
        roles.Resolve([], autoAssign: true);

        Assert.Equal(RoleId.MT, roles.GetRole(new PlayerKey("Same Name", 1)));
        Assert.Equal(RoleId.None, roles.GetRole(new PlayerKey("Same Name", 2)));
    }
}
