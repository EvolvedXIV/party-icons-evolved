using System.Globalization;

namespace PartyIconsEvolved.Nameplates;

/// <summary>Cuts names down to a maximum number of characters.</summary>
internal static class NameShortener
{
    public const string Ellipsis = "…";

    /// <summary>Number of user-perceived characters in <paramref name="text"/>.</summary>
    public static int Length(string text) => new StringInfo(text).LengthInTextElements;

    /// <summary>Returns the first <paramref name="count"/> characters, never splitting a surrogate pair.</summary>
    public static string Take(string text, int count)
    {
        if (count <= 0) {
            return string.Empty;
        }

        var info = new StringInfo(text);
        return count >= info.LengthInTextElements ? text : info.SubstringByTextElements(0, count);
    }

    /// <summary>
    /// Shortens <paramref name="text"/> to at most <paramref name="maxLength"/> characters. A limit of zero or less
    /// means "no limit". Whitespace left dangling at the cut is removed.
    /// </summary>
    public static string Shorten(string text, int maxLength, bool addEllipsis)
    {
        if (maxLength <= 0 || Length(text) <= maxLength) {
            return text;
        }

        var cut = Take(text, maxLength).TrimEnd();
        return addEllipsis ? cut + Ellipsis : cut;
    }
}
