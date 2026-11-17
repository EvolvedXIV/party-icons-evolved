using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using PartyIconsEvolved.Configuration;
using PartyIconsEvolved.Roles;
using LuminaSeStringBuilder = Lumina.Text.SeStringBuilder;

namespace PartyIconsEvolved.Nameplates;

/// <summary>
/// Composes the text of a nameplate: the (optionally shortened and recoloured) name and the role indicator.
/// Everything is expressed as game text macros, so it is rendered by the game's own nameplate text node.
/// </summary>
internal sealed class NameplateText
{
    /// <summary>Keeps a plate alive when it has no visible text at all.</summary>
    private const string Placeholder = "　";

    /// <summary>UIColor row used for the outline of the role indicator (black), as in xivPartyIcons.</summary>
    private const uint IndicatorEdgeUiColor = 51;

    /// <summary>Width of one full-width character cell at nameplate size, the same as one boxed letter.</summary>
    public const float CellWidth = 18f;

    /// <summary>Approximate width of one regular letter at nameplate size.</summary>
    private const float PlainLetterWidth = 10f;

    private readonly PluginConfiguration config;
    private readonly Dictionary<(RoleId Role, RoleCategory NameColor), (Payload[] Prefix, Payload[] Suffix)> wraps = new();
    private readonly Dictionary<RoleId, Payload[]> indicators = new();
    private readonly Dictionary<RoleId, byte[]> floatingIndicators = new();

    public NameplateText(PluginConfiguration config)
    {
        this.config = config;
    }

    /// <summary>Drops everything derived from the configuration. Call after any setting changed.</summary>
    public void Invalidate()
    {
        wraps.Clear();
        indicators.Clear();
        floatingIndicators.Clear();
        Version++;
    }

    /// <summary>Changes whenever previously built text may be out of date.</summary>
    public int Version { get; private set; }

    /// <summary>Applies the configured name and role indicator to a nameplate that is being updated.</summary>
    public void Apply(INamePlateUpdateHandler handler, RoleId role, RoleCategory jobCategory)
    {
        var indicatorRole = config.ShowRoleIndicator ? role : RoleId.None;

        if (!config.ShowName) {
            handler.NameParts.Text = indicatorRole != RoleId.None
                ? ToSeString(GetIndicator(indicatorRole))
                : new SeString(new TextPayload(Placeholder));
            return;
        }

        if (config.MaxNameLength > 0 && Shorten(handler.Name, config.MaxNameLength, config.AddEllipsis) is { } name) {
            handler.NameParts.Text = name;
        }

        // RoleCategory.None doubles as "leave the name colour alone" for the cache key.
        var nameColor = config.NameColor switch
        {
            NameColorMode.Role => role != RoleId.None ? role.Category() : jobCategory,
            _ => RoleCategory.None,
        };

        if (indicatorRole == RoleId.None && nameColor == RoleCategory.None && config.NameColor != NameColorMode.Custom) {
            return;
        }

        var (prefix, suffix) = GetWrap(indicatorRole, nameColor);
        handler.NameParts.TextWrap = (ToSeString(prefix), ToSeString(suffix));
    }

    /// <summary>
    /// The coloured role indicator as null-terminated game text, for the separate indicator node.
    /// </summary>
    public byte[] GetFloatingIndicator(RoleId role)
    {
        if (!floatingIndicators.TryGetValue(role, out var bytes)) {
            var builder = new LuminaSeStringBuilder();
            AppendColoredIndicator(builder, role);
            var text = builder.ToArray();
            bytes = new byte[text.Length + 1];
            text.CopyTo(bytes, 0);
            floatingIndicators[role] = bytes;
        }

        return bytes;
    }

    /// <summary>
    /// How many blank character cells to leave in the name text for a floating indicator, so that the name and
    /// the job icon make room for it at its configured size.
    /// </summary>
    public int FloatingIndicatorCells(RoleId role)
    {
        var letters = role.Label(config.RoleNaming).Length;
        var letterWidth = config.RoleIndicatorStyle == RoleIndicatorStyle.Boxed ? CellWidth : PlainLetterWidth;
        var width = letters * letterWidth * config.RoleIndicatorScale;
        return Math.Max(1, (int)MathF.Round(width / CellWidth));
    }

    /// <summary>The indicator as plain characters (boxed glyphs or letters), without colour.</summary>
    public string IndicatorText(RoleId role)
    {
        var label = role.Label(config.RoleNaming);
        return config.RoleIndicatorStyle == RoleIndicatorStyle.Boxed ? ToBoxed(label) : label;
    }

    /// <summary>
    /// Shortens the text of a name while keeping any styling payloads it carries. Returns null if the name already
    /// fits, so that the game's own string can be left untouched.
    /// </summary>
    internal static SeString? Shorten(SeString name, int maxLength, bool addEllipsis)
    {
        var total = 0;
        foreach (var payload in name.Payloads) {
            if (payload is TextPayload { Text: { } text }) {
                total += NameShortener.Length(text);
            }
        }

        if (maxLength <= 0 || total <= maxLength) {
            return null;
        }

        var result = new List<Payload>(name.Payloads.Count);
        TextPayload? lastText = null;
        var remaining = maxLength;

        foreach (var payload in name.Payloads) {
            if (payload is not TextPayload { Text: { } text }) {
                // Colour pushes/pops and the like must stay balanced, so they are always kept.
                result.Add(payload);
                continue;
            }

            if (remaining <= 0) {
                continue;
            }

            var part = NameShortener.Take(text, remaining);
            remaining -= NameShortener.Length(part);
            lastText = new TextPayload(part);
            result.Add(lastText);
        }

        if (lastText != null) {
            var trimmed = (lastText.Text ?? string.Empty).TrimEnd();
            lastText.Text = addEllipsis ? trimmed + NameShortener.Ellipsis : trimmed;
        }

        return new SeString(result);
    }

    private (Payload[] Prefix, Payload[] Suffix) GetWrap(RoleId role, RoleCategory nameColor)
    {
        if (wraps.TryGetValue((role, nameColor), out var wrap)) {
            return wrap;
        }

        var colorName = config.NameColor == NameColorMode.Custom || nameColor != RoleCategory.None;

        var prefix = new LuminaSeStringBuilder();
        if (role != RoleId.None && config.RoleIndicatorPosition == RoleIndicatorPosition.BeforeName) {
            AppendIndicator(prefix, role);
            prefix.Append(" ");
        }

        if (colorName) {
            PushNameColor(prefix, nameColor);
        }

        var suffix = new LuminaSeStringBuilder();
        if (colorName) {
            PopNameColor(suffix, nameColor);
        }

        if (role != RoleId.None && config.RoleIndicatorPosition == RoleIndicatorPosition.AfterName) {
            suffix.Append(" ");
            AppendIndicator(suffix, role);
        }

        wrap = (ToPayloads(prefix), ToPayloads(suffix));
        wraps[(role, nameColor)] = wrap;
        return wrap;
    }

    private Payload[] GetIndicator(RoleId role)
    {
        if (!indicators.TryGetValue(role, out var payloads)) {
            var builder = new LuminaSeStringBuilder();
            AppendIndicator(builder, role);
            payloads = ToPayloads(builder);
            indicators[role] = payloads;
        }

        return payloads;
    }

    /// <summary>
    /// Appends the indicator as it appears in the name text: the indicator itself, or blank space of the right
    /// width when the indicator is drawn separately on top of it.
    /// </summary>
    private void AppendIndicator(LuminaSeStringBuilder builder, RoleId role)
    {
        if (config.RoleIndicatorFloats) {
            builder.Append(new string(Placeholder[0], FloatingIndicatorCells(role)));
        }
        else {
            AppendColoredIndicator(builder, role);
        }
    }

    private void AppendColoredIndicator(LuminaSeStringBuilder builder, RoleId role)
    {
        var category = role.Category();
        builder.PushEdgeColorType(IndicatorEdgeUiColor);
        PushRoleColor(builder, category);
        builder.Append(IndicatorText(role));
        PopRoleColor(builder, category);
        builder.PopEdgeColorType();
    }

    private void PushNameColor(LuminaSeStringBuilder builder, RoleCategory category)
    {
        if (config.NameColor == NameColorMode.Custom) {
            PushRgb(builder, config.CustomNameColor);
        }
        else {
            PushRoleColor(builder, category);
        }
    }

    private void PopNameColor(LuminaSeStringBuilder builder, RoleCategory category)
    {
        if (config.NameColor == NameColorMode.Custom) {
            builder.PopColor();
        }
        else {
            PopRoleColor(builder, category);
        }
    }

    private void PushRoleColor(LuminaSeStringBuilder builder, RoleCategory category)
    {
        if (config.GetRoleColor(category) is { } custom) {
            PushRgb(builder, custom);
        }
        else {
            builder.PushColorType(category.DefaultUiColor());
        }
    }

    private void PopRoleColor(LuminaSeStringBuilder builder, RoleCategory category)
    {
        if (config.GetRoleColor(category) is not null) {
            builder.PopColor();
        }
        else {
            builder.PopColorType();
        }
    }

    private static void PushRgb(LuminaSeStringBuilder builder, Vector4 color)
    {
        builder.PushColorRgba(ToByte(color.X), ToByte(color.Y), ToByte(color.Z), 255);
    }

    private static byte ToByte(float channel) => (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);

    private static Payload[] ToPayloads(LuminaSeStringBuilder builder)
    {
        var bytes = builder.ToArray();
        return bytes.Length == 0 ? [] : SeString.Parse(bytes).Payloads.ToArray();
    }

    /// <summary>Wraps cached payloads in a new string, so that nothing downstream can modify the cache.</summary>
    private static SeString ToSeString(Payload[] payloads) => new(new List<Payload>(payloads));

    /// <summary>Maps letters and digits to the game font's boxed glyphs.</summary>
    private static string ToBoxed(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text) {
            builder.Append(ch switch
            {
                >= '0' and <= '9' => (char)(ch + 0xE05F),
                >= 'A' and <= 'Z' => (char)(ch + 0xE030),
                >= 'a' and <= 'z' => (char)(ch + 0xE010),
                _ => ch,
            });
        }

        return builder.ToString();
    }
}
