using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Roles;
using Xunit;

namespace PartyIconsEvolved.Tests;

public class RoleParsingTests
{
    [Theory]
    [InlineData("MT", RoleId.MT)]
    [InlineData("OT", RoleId.OT)]
    [InlineData("H1", RoleId.H1)]
    [InlineData("H2", RoleId.H2)]
    [InlineData("M1", RoleId.M1)]
    [InlineData("M2", RoleId.M2)]
    [InlineData("R1", RoleId.R1)]
    [InlineData("R2", RoleId.R2)]
    [InlineData("m1", RoleId.M1)]
    [InlineData("  r2 ", RoleId.R2)]
    public void MessageThatIsOnlyARoleIsRecognised(string message, RoleId expected)
    {
        Assert.True(RoleExtensions.TryParseRole(message, allowAliases: false, out var role));
        Assert.Equal(expected, role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("M3")]
    [InlineData("M1?")]
    [InlineData("I'm M1")]
    [InlineData("M1 M2")]
    [InlineData("M 1")]
    [InlineData("MT pulls")]
    [InlineData("mt/ot")]
    public void AnythingElseIsIgnored(string message)
    {
        Assert.False(RoleExtensions.TryParseRole(message, allowAliases: true, out var role));
        Assert.Equal(RoleId.None, role);
    }

    [Theory]
    [InlineData("ST", RoleId.OT)]
    [InlineData("t1", RoleId.MT)]
    [InlineData("T2", RoleId.OT)]
    [InlineData("D1", RoleId.M1)]
    [InlineData("D2", RoleId.M2)]
    [InlineData("D3", RoleId.R1)]
    [InlineData("d4", RoleId.R2)]
    public void AliasesAreOnlyAcceptedWhenEnabled(string message, RoleId expected)
    {
        Assert.True(RoleExtensions.TryParseRole(message, allowAliases: true, out var role));
        Assert.Equal(expected, role);

        Assert.False(RoleExtensions.TryParseRole(message, allowAliases: false, out _));
    }

    [Fact]
    public void LabelsFollowTheNamingScheme()
    {
        Assert.Equal("OT", RoleId.OT.Label(RoleNaming.Western));
        Assert.Equal("ST", RoleId.OT.Label(RoleNaming.Eastern));
        Assert.Equal("R2", RoleId.R2.Label(RoleNaming.Western));
        Assert.Equal("D4", RoleId.R2.Label(RoleNaming.Eastern));
        Assert.Equal("MT", RoleId.MT.Label(RoleNaming.Eastern));
        Assert.Equal(string.Empty, RoleId.None.Label(RoleNaming.Western));
    }

    [Fact]
    public void EveryLabelParsesBackToItsRole()
    {
        foreach (var naming in new[] { RoleNaming.Western, RoleNaming.Eastern }) {
            foreach (var role in RoleExtensions.AllRoles) {
                Assert.True(RoleExtensions.TryParseRole(role.Label(naming), allowAliases: true, out var parsed));
                Assert.Equal(role, parsed);
            }
        }
    }
}
