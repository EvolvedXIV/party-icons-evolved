using System;
using Lumina.Excel.Sheets;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Roles;

namespace PartyIconsEvolved.Game;

/// <summary>An icon to draw for a job, with the metrics needed to line it up with text.</summary>
/// <param name="IconId">Game icon id.</param>
/// <param name="Scale">Scale needed for this icon set to match the size of the others.</param>
/// <param name="Padding">Transparent border inside the 32px icon, in pixels.</param>
internal readonly record struct JobIcon(uint IconId, float Scale, float Padding);

/// <summary>Job lookups: role category (from the game's own data) and icon ids.</summary>
internal static class JobData
{
    private static RoleCategory[] categories = [];
    private static string[] abbreviations = [];

    // Icon sets that are not a simple "base + job id". Indexed by job id - 1.
    private static readonly uint[] Glowing =
    [
        62301, 62302, 62303, 62304, 62305, 62306, 62307, 62310, 62311, 62312,
        62313, 62314, 62315, 62316, 62317, 62318, 62319, 62320, 62401, 62402,
        62403, 62404, 62405, 62406, 62407, 62308, 62408, 62409, 62309, 62410,
        62411, 62412, 62413, 62414, 62415, 62416, 62417, 62418, 62419, 62420,
        62421, 62422, 62423,
    ];

    // Offsets of each job within one of the flat, single-colour icon sets.
    private static readonly ushort[] FlatOffsets =
    [
        22, 23, 24, 25, 26, 28, 29, 31, 32, 33,
        34, 35, 36, 37, 38, 39, 40, 41, 79, 80,
        81, 82, 83, 84, 85, 30, 86, 87, 121, 122,
        125, 123, 124, 127, 128, 129, 130, 131, 132, 133,
        185, 186, 154,
    ];

    private const uint FlatGrey = 91000;
    private const uint FlatOrange = 92500;
    private const uint FlatRed = 93000;
    private const uint FlatBlue = 94000;
    private const uint FlatGreen = 94500;

    public static void Initialize()
    {
        var sheet = Service.DataManager.GetExcelSheet<ClassJob>();

        var max = 0u;
        foreach (var row in sheet) {
            max = Math.Max(max, row.RowId);
        }

        categories = new RoleCategory[max + 1];
        abbreviations = new string[max + 1];
        foreach (var row in sheet) {
            // ClassJob.Role: 1 tank, 2 melee DPS, 3 ranged DPS (physical and magical), 4 healer.
            categories[row.RowId] = row.Role switch
            {
                1 => RoleCategory.Tank,
                2 => RoleCategory.Melee,
                3 => RoleCategory.Ranged,
                4 => RoleCategory.Healer,
                _ => RoleCategory.None,
            };
            abbreviations[row.RowId] = row.Abbreviation.ExtractText();
        }
    }

    public static RoleCategory GetCategory(uint jobId) =>
        jobId < (uint)categories.Length ? categories[jobId] : RoleCategory.None;

    public static string GetAbbreviation(uint jobId) =>
        jobId < (uint)abbreviations.Length ? abbreviations[jobId] ?? string.Empty : string.Empty;

    /// <summary>Gets the icon for a job in the given style. Fails for job 0 (no job).</summary>
    public static bool TryGetIcon(JobIconStyle style, uint jobId, out JobIcon icon)
    {
        if (jobId == 0 || jobId >= (uint)categories.Length) {
            icon = default;
            return false;
        }

        var index = (int)jobId - 1;
        switch (style) {
            case JobIconStyle.Embossed:
                icon = new JobIcon(62000 + jobId, 1f, 2.5f);
                return true;

            case JobIconStyle.Glowing when index < Glowing.Length:
                icon = new JobIcon(Glowing[index], 1f, 2.5f);
                return true;

            case JobIconStyle.RoleColored when index < FlatOffsets.Length:
                var set = GetCategory(jobId) switch
                {
                    RoleCategory.Tank => FlatBlue,
                    RoleCategory.Healer => FlatGreen,
                    RoleCategory.Melee => FlatRed,
                    RoleCategory.Ranged => FlatOrange,
                    _ => FlatGrey,
                };
                icon = new JobIcon(set + FlatOffsets[index], 1.45f, 6.5f);
                return true;

            // Framed, and the fallback for jobs newer than the tables above.
            default:
                icon = new JobIcon(62100 + jobId, 1f, 0.5f);
                return true;
        }
    }
}
