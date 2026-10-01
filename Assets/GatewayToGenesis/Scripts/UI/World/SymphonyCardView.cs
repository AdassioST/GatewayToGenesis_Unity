using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One card as a Symphony shows it: the card, how it is sounded, and who brought it.</summary>
public struct CardEntry
{
    public CombatCard card;
    /// <summary>Sounded as a Major Note (an Awakened Legend's leitmotif).</summary>
    public bool major;
    /// <summary>The binding of the section or legend that voices it (a caster's card with no Root of its own takes it).</summary>
    public SpellBinding voiceRoot;
    /// <summary>"Grave Warden", "Aurelian's grimoire", "the fallback".</summary>
    public string source;
}

/// <summary>
/// A Symphony card drawn the way the Sonata website's Spell Builder draws a spell (src/features/spells/SpellCard.tsx,
/// read through <see cref="CardFace"/>): the whole card in its Root's colours, the rank (the chord) in the top corner
/// and the Root's note opposite it, the chord drawn on the circle of fifths as its art, then the name, the suit line
/// (the Root, then each Minor Note), the practice, the catchline, and the Purpose stamp. The game adds what a hand needs:
/// the cost in Beats on an orb over the art, and who brought the card. Its back is "The reading" (<see cref="CardFace.Reading"/>).
/// Built in code (UGUI); the round marks are sprites made here, once.
/// </summary>
public static class SymphonyCardView
{
    public const float Width = 300f, Height = 440f;
    private const float Art = 180f;

    private static readonly Color Parchment = new Color(0.96f, 0.91f, 0.83f);
    private static readonly Color Dim = new Color(0.78f, 0.72f, 0.66f);

    private static Sprite _disc, _glow;

    /// <summary>A hard-edged disc (nodes, the cost orb).</summary>
    private static Sprite Disc => _disc != null ? _disc : _disc = Round(64, d => Mathf.Clamp01((1f - d) * 32f));

    /// <summary>A soft radial glow (the light the Root throws over the face).</summary>
    private static Sprite Glow => _glow != null ? _glow : _glow = Round(128, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));

    private static Sprite Round(int size, Func<float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(d)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>
    /// Builds the card under <paramref name="parent"/> (Width x Height), its face or its back; <paramref name="onClick"/>
    /// (null: none) is called when it is touched (the window turns it).
    /// </summary>
    public static RectTransform Build(Transform parent, CardEntry entry, TooltipTheme theme, bool back, Action onClick = null)
    {
        var card = entry.card;
        var root = CardFace.Root(card, entry.voiceRoot);
        var (bright, deep, ink) = CardFace.Palette(root);

        var rect = CodeUI.Panel(parent, card?.name ?? "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.sizeDelta = new Vector2(Width, Height);
        var rim = rect.gameObject.AddComponent<Image>();
        rim.color = new Color(bright.r, bright.g, bright.b, 0.75f);
        rim.raycastTarget = onClick != null;
        if (onClick != null) rect.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());

        var body = CodeUI.Solid(rect, "Body", Color.Lerp(deep, ink, 0.35f));
        body.rectTransform.offsetMin = new Vector2(2f, 2f);
        body.rectTransform.offsetMax = new Vector2(-2f, -2f);
        var inner = CodeUI.Solid(rect, "Inner rule", new Color(bright.r, bright.g, bright.b, 0.18f));
        inner.rectTransform.offsetMin = new Vector2(9f, 9f);
        inner.rectTransform.offsetMax = new Vector2(-9f, -9f);
        var innerFill = CodeUI.Solid(rect, "Inner", Color.Lerp(deep, ink, 0.45f));
        innerFill.rectTransform.offsetMin = new Vector2(10f, 10f);
        innerFill.rectTransform.offsetMax = new Vector2(-10f, -10f);

        if (card == null) return rect;
        TooltipTrigger.Ensure(rect.gameObject).SetCustom(card.name, CardFace.Reading(card));
        if (back) Back(rect, entry, theme, bright);
        else Face(rect, entry, theme, root, bright, deep, ink);
        return rect;
    }

    // ===== THE FACE =====

    private static void Face(RectTransform rect, CardEntry entry, TooltipTheme theme, SpellBinding root, Color bright, Color deep, Color ink)
    {
        var card = entry.card;
        // The corner: the rank (the chord), and the Root's note.
        var rank = Text(rect, "Rank", CardFace.Rank(card, entry.major).ToUpperInvariant(), 12f, bright, FontStyles.Bold, theme);
        rank.characterSpacing = 8f;
        Top(rank.rectTransform, 22f, -18f, -60f, 18f);
        var note = Text(rect, "Note", root == SpellBinding.Unattuned ? "·" : CardFace.Note(root), 22f, Parchment, FontStyles.Normal, theme);
        note.alignment = TextAlignmentOptions.TopRight;
        Top(note.rectTransform, Width - 60f, -14f, -22f, 26f);

        // The art: the chord on the circle of fifths.
        var art = CodeUI.Panel(rect, "Art", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        art.sizeDelta = new Vector2(Art, Art);
        art.anchoredPosition = new Vector2(0f, -40f - Art / 2f);
        Sigil(art, card, root, bright, theme);

        // The cost, in Beats, where a hand needs to see it.
        var orb = Image(rect, "Cost", Disc, bright);
        Top(orb.rectTransform, 18f, -40f, 0f, 38f, 38f);
        var cost = Text(orb.rectTransform, "Beats", card.TemporalBeats.ToString(), 20f, ink, FontStyles.Bold, theme);
        cost.alignment = TextAlignmentOptions.Center;
        CodeUI.Stretch(cost.rectTransform);

        // The name, the suit line, the practice, the catchline.
        float y = -40f - Art - 6f;
        var name = Text(rect, "Name", card.name, 25f, Parchment, FontStyles.Bold, theme);
        // One line, as the website's card holds it: a long name shrinks rather than wrapping into the suit line.
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.enableAutoSizing = true;
        name.fontSizeMin = 15f;
        name.fontSizeMax = 25f;
        Top(name.rectTransform, 22f, y, -22f, 32f);
        y -= 34f;
        var minors = card.minors ?? new System.Collections.Generic.List<SpellBinding>();
        string suit = root == SpellBinding.Unattuned
            ? TooltipText.Paint(CardFace.SuitLine(card, entry.voiceRoot).ToUpperInvariant(), Hex(bright))
            : TooltipText.Paint(HarmonicCircle.Name(root).ToUpperInvariant(), Hex(bright)) +
              string.Concat(minors.Where(m => m != root).Distinct().Select(m => $"  {TooltipText.Paint(TooltipText.Symbol("•"), Hex(CardFace.Palette(m).bright))} {TooltipText.Paint(HarmonicCircle.Name(m).ToUpperInvariant(), Hex(Dim))}"));
        var suitLine = Text(rect, "Suit", suit, 13f, bright, FontStyles.Bold, theme);
        suitLine.characterSpacing = 4f;
        Top(suitLine.rectTransform, 22f, y, -22f, 18f);
        y -= 19f;
        var practice = Text(rect, "Practice", CardFace.Practice(card), 12.5f, Dim, FontStyles.Italic, theme);
        Top(practice.rectTransform, 22f, y, -22f, 17f);
        y -= 22f;
        var line = Text(rect, "Effect", string.Join("\n", card.effects.Where(e => e != null).Select(SymphonyText.Effect)), 16f, Parchment, FontStyles.Normal, theme);
        line.overflowMode = TextOverflowModes.Ellipsis;
        Top(line.rectTransform, 22f, y, -22f, Height + y - 78f);

        // The stamps: the Purpose (in its thread's colour), and who brought the card.
        var purposeColor = CardFace.Palette(CardFace.PurposeBinding(card.purpose)).bright;
        Chip(rect, card.purpose.ToString().ToUpperInvariant(), purposeColor, theme, 22f, 20f);
        if (!string.IsNullOrEmpty(entry.source))
        {
            var from = Text(rect, "Source", entry.source, 11f, Dim, FontStyles.Italic, theme);
            from.alignment = TextAlignmentOptions.BottomLeft;
            from.textWrappingMode = TextWrappingModes.NoWrap;
            from.overflowMode = TextOverflowModes.Ellipsis;
            var r = from.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(1f, 0f);
            r.offsetMin = new Vector2(22f, 52f);
            r.offsetMax = new Vector2(-22f, 68f);
        }
    }

    /// <summary>
    /// The mark (the website's spellSigil): the twelve stations of the circle of fifths, C at twelve o'clock; the six
    /// fifths between the seven threads drawn along the ring and the tritone from B back to F left open; the five rests
    /// with their notes; the Root lit with its note, each Minor Note a smaller node with a chord line back to it.
    /// </summary>
    private static void Sigil(RectTransform art, CombatCard card, SpellBinding root, Color bright, TooltipTheme theme)
    {
        float r = Art * 0.34f;
        Vector2 At(float station, float radius) { float a = station / 12f * Mathf.PI * 2f - Mathf.PI / 2f; return new Vector2(Mathf.Cos(a) * radius, -Mathf.Sin(a) * radius); }

        var glow = Image(art, "Glow", Glow, new Color(bright.r, bright.g, bright.b, root == SpellBinding.Unattuned ? 0.12f : 0.34f));
        Centered(glow.rectTransform, Vector2.zero, Art * 0.95f);

        // The ring: each fifth between neighbours, and the open tritone.
        var order = CardFace.FifthsOrder;
        for (int i = 0; i + 1 < order.Count; i++)
        {
            float from = CardFace.Station(order[i]), to = CardFace.Station(order[i + 1]);
            if (to < from) to += 12f;
            for (float s = from; s < to - 0.001f; s += 0.25f)
                Line(art, At(s, r), At(Mathf.Min(to, s + 0.25f), r), 1.2f, new Color(bright.r, bright.g, bright.b, 0.35f));
        }
        foreach (var (station, noteName) in CardFace.Rests())
        {
            var dot = Image(art, "Rest", Disc, new Color(Dim.r, Dim.g, Dim.b, 0.35f));
            Centered(dot.rectTransform, At(station, r), 4f);
            var label = Text(art, "Rest note", noteName.Replace("b", "<size=80%>b</size>"), 11f, new Color(Dim.r, Dim.g, Dim.b, 0.5f), FontStyles.Normal, theme);
            label.alignment = TextAlignmentOptions.Center;
            Centered(label.rectTransform, At(station, r + 17f), 26f);
        }

        var minors = (card.minors ?? new System.Collections.Generic.List<SpellBinding>()).Where(m => m != SpellBinding.Unattuned && m != root).Distinct().ToList();
        if (root != SpellBinding.Unattuned)
            foreach (var m in minors) Line(art, At(CardFace.Station(root), r), At(CardFace.Station(m), r), 2.2f, bright);
        foreach (var b in order)
        {
            bool isRoot = b == root, isMinor = minors.Contains(b);
            var colour = isRoot || isMinor ? (isRoot ? bright : CardFace.Palette(b).bright) : new Color(bright.r, bright.g, bright.b, 0.45f);
            var node = Image(art, b.ToString(), Disc, colour);
            Centered(node.rectTransform, At(CardFace.Station(b), r), isRoot ? 19f : isMinor ? 12f : 5f);
            if (!isRoot && !isMinor) continue;
            var label = Text(art, "Note", CardFace.Note(b), isRoot ? 20f : 16f, Parchment, FontStyles.Normal, theme);
            label.alignment = TextAlignmentOptions.Center;
            Centered(label.rectTransform, At(CardFace.Station(b), r + 22f), 30f);
        }
        // Steel has no Root: its mark is the ring alone, unlit, with the kind at its heart.
        if (root == SpellBinding.Unattuned)
        {
            var kind = Text(art, "Kind", card.kind == CardKind.Command ? "ORDER" : card.kind == CardKind.Instinct ? "INSTINCT" : "STEEL", 13f, new Color(bright.r, bright.g, bright.b, 0.8f), FontStyles.Bold, theme);
            kind.characterSpacing = 10f;
            kind.alignment = TextAlignmentOptions.Center;
            Centered(kind.rectTransform, Vector2.zero, 120f);
        }
    }

    // ===== THE BACK =====

    private static void Back(RectTransform rect, CardEntry entry, TooltipTheme theme, Color bright)
    {
        var card = entry.card;
        var title = Text(rect, "Title", "THE READING", 12f, bright, FontStyles.Bold, theme);
        title.characterSpacing = 8f;
        Top(title.rectTransform, 22f, -18f, -22f, 18f);
        var name = Text(rect, "Name", card.name, 21f, Parchment, FontStyles.Bold, theme);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.enableAutoSizing = true;
        name.fontSizeMin = 13f;
        name.fontSizeMax = 21f;
        Top(name.rectTransform, 22f, -40f, -22f, 28f);
        // The Purpose and the reading flow as one text, so a Purpose that wraps pushes the reading down instead of under it.
        var purposeColor = CardFace.Palette(CardFace.PurposeBinding(card.purpose)).bright;
        string role = $"<size=104%>{TooltipText.Paint(card.purpose.ToString().ToUpperInvariant(), Hex(purposeColor))}  " +
                      $"{TooltipText.Paint($"<i>{CardFace.Role(card.purpose)}</i>", Hex(Dim))}</size>\n" +
                      $"<size=92%>{TooltipText.Paint(CardFace.What(card.purpose), Hex(Dim))}</size>\n<size=45%> </size>\n";
        var reading = Text(rect, "Reading", role + CardFace.Reading(card), 12.5f, Parchment, FontStyles.Normal, theme);
        reading.overflowMode = TextOverflowModes.Ellipsis;
        reading.lineSpacing = -6f;
        Top(reading.rectTransform, 22f, -72f, -22f, Height - 72f - 24f);
    }

    // ===== PIECES =====

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, FontStyles style, TooltipTheme theme)
    {
        var label = CodeUI.Label(parent, name, text, size, color, style, theme);
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static Image Image(Transform parent, string name, Sprite sprite, Color color)
    {
        var image = CodeUI.Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Places a rect from the card's top-left: x from the left, y down from the top (negative), right inset (negative), height.</summary>
    private static void Top(RectTransform r, float x, float y, float right, float height, float width = 0f)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = width > 0f ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = width > 0f ? new Vector2(width, height) : new Vector2(right - x, height);
    }

    private static void Centered(RectTransform r, Vector2 at, float size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = at;
        r.sizeDelta = new Vector2(size, size);
    }

    private static void Line(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
    {
        var image = CodeUI.Panel(parent, "Line", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var r = image.rectTransform;
        var d = b - a;
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = a;
        r.sizeDelta = new Vector2(d.magnitude, width);
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private static void Chip(RectTransform card, string text, Color color, TooltipTheme theme, float x, float y)
    {
        var chip = CodeUI.Panel(card, "Purpose", new Vector2(0f, 0f), new Vector2(0f, 0f));
        chip.pivot = new Vector2(0f, 0f);
        chip.anchoredPosition = new Vector2(x, y);
        chip.sizeDelta = new Vector2(26f + text.Length * 11.5f, 26f);
        var edge = chip.gameObject.AddComponent<Image>();
        edge.color = new Color(color.r, color.g, color.b, 0.8f);
        edge.raycastTarget = false;
        var fill = CodeUI.Solid(chip, "Fill", new Color(0.08f, 0.06f, 0.05f, 0.92f));
        fill.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
        fill.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
        var label = CodeUI.Label(chip, "Text", text, 11.5f, color, FontStyles.Bold, theme);
        label.characterSpacing = 6f;
        label.alignment = TextAlignmentOptions.Center;
        CodeUI.Stretch(label.rectTransform);
    }
}
