using PartyIconsEvolved.Nameplates;
using Xunit;

namespace PartyIconsEvolved.Tests;

public class NameShortenerTests
{
    [Theory]
    [InlineData("Firstname Lastname", 0, "Firstname Lastname")]
    [InlineData("Firstname Lastname", -1, "Firstname Lastname")]
    [InlineData("Firstname Lastname", 18, "Firstname Lastname")]
    [InlineData("Firstname Lastname", 40, "Firstname Lastname")]
    [InlineData("Firstname Lastname", 12, "Firstname La")]
    [InlineData("Firstname Lastname", 5, "First")]
    [InlineData("Firstname L.", 11, "Firstname L")]
    [InlineData("F. L.", 2, "F.")]
    public void CutsToTheLimit(string name, int limit, string expected)
    {
        Assert.Equal(expected, NameShortener.Shorten(name, limit, addEllipsis: false));
    }

    [Fact]
    public void DoesNotLeaveADanglingSpace()
    {
        // The tenth character is the space between the names.
        Assert.Equal("Firstname", NameShortener.Shorten("Firstname Lastname", 10, addEllipsis: false));
        Assert.Equal("Firstname…", NameShortener.Shorten("Firstname Lastname", 10, addEllipsis: true));
    }

    [Fact]
    public void EllipsisIsOnlyAddedWhenSomethingWasCut()
    {
        Assert.Equal("First…", NameShortener.Shorten("Firstname Lastname", 5, addEllipsis: true));
        Assert.Equal("Firstname L.", NameShortener.Shorten("Firstname L.", 12, addEllipsis: true));
    }

    [Fact]
    public void CountsCharactersNotCodeUnits()
    {
        // Each of these is a surrogate pair, i.e. two UTF-16 code units.
        const string name = "𝓐𝓑𝓒𝓓";
        Assert.Equal(4, NameShortener.Length(name));
        Assert.Equal("𝓐𝓑", NameShortener.Shorten(name, 2, addEllipsis: false));
        Assert.Equal(name, NameShortener.Shorten(name, 4, addEllipsis: false));
    }
}
