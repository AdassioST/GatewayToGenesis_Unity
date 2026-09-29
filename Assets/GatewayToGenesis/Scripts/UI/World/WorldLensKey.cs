using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// The lens key: every colour a lens paints, with what it means. The renderer names each cell's colour as it paints it
/// (<see cref="LensAt"/>) and gathers the colours that depend on the world (Macro Biomes, weather fronts, seats,
/// resource sites, Quadrants) as it goes, so the key and the hover always agree with the map.
/// </summary>
public partial class WorldRenderer
{
    /// <summary>One line of the key: a swatch (one colour, or a run from dim to bright) and what it means.</summary>
    public struct LensKeyEntry
    {
        public Color[] colors;
        public string label;
    }

    // The lens colours, shared by the painting (LensColor) and the key.
    private static readonly Color Unlivable = new Color(0.08f, 0.08f, 0.1f);
    private static readonly Color SettlePoor = new Color(0.55f, 0.12f, 0.1f), SettleFair = new Color(0.9f, 0.7f, 0.2f), SettleRich = new Color(0.3f, 0.85f, 0.35f);
    private static readonly Color AuthorityFar = new Color(0.2f, 0.45f, 0.3f), AuthorityNear = new Color(0.6f, 0.95f, 0.65f);
    private static readonly Color OtherClaim = new Color(0.85f, 0.45f, 0.2f), WildernessGrey = new Color(0.4f, 0.4f, 0.36f);
    private static readonly Color AuthorityWater = new Color(0.15f, 0.3f, 0.5f), ImpassableGrey = new Color(0.22f, 0.2f, 0.2f);
    private static readonly Color SacredWhite = new Color(1f, 0.98f, 0.9f), DissonanceDim = new Color(0.2f, 0.04f, 0.22f), DissonanceBright = new Color(0.78f, 0.2f, 0.85f);
    private static readonly Color NoRoad = new Color(0.12f, 0.12f, 0.12f), NoRoadSea = new Color(0.08f, 0.14f, 0.24f);
    private static readonly Color Blight = new Color(0.55f, 0.1f, 0.12f), Unidentified = new Color(0.78f, 0.78f, 0.74f), Barren = new Color(0.1f, 0.1f, 0.1f);
    private static readonly Color FairerDim = new Color(0.12f, 0.14f, 0.12f), Fairer = new Color(0.35f, 0.7f, 0.4f);
    private static readonly Color SpoiledDim = new Color(0.14f, 0.12f, 0.12f), Spoiled = new Color(0.75f, 0.3f, 0.25f);
    private static readonly Color Quiet = new Color(0.12f, 0.12f, 0.14f), FeelingBase = new Color(0.18f, 0.18f, 0.2f);
    private static readonly Color Plain = new Color(0.52f, 0.52f, 0.5f), Hideous = new Color(0.28f, 0.2f, 0.08f), Beautiful = new Color(1f, 0.74f, 0.78f), LakeBlue = new Color(0.3f, 0.5f, 0.7f);
    private static readonly Color Unpulled = new Color(0.15f, 0.15f, 0.15f), PullDim = new Color(0.35f, 0.25f, 0.1f), PullBright = new Color(0.95f, 0.65f, 0.2f);
    private static readonly Color AdoptNext = new Color(1f, 0.95f, 0.75f), RivalDim = new Color(0.3f, 0.12f, 0.1f), RivalBright = new Color(0.9f, 0.25f, 0.2f), HeldDefault = new Color(0.3f, 0.6f, 0.4f);
    private static readonly Color CultureNew = new Color(0.3f, 0.2f, 0.42f), CultureRooting = new Color(0.72f, 0.42f, 0.62f), CultureLong = new Color(1f, 0.78f, 0.28f);
    private static readonly Color CultureFading = new Color(0.45f, 0.45f, 0.45f), NoCulture = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color Calm = new Color(0.2f, 0.24f, 0.22f), Bruise = new Color(0.42f, 0.16f, 0.4f);
    private static readonly Color Lowland = new Color(0.15f, 0.4f, 0.2f), DeepSea = new Color(0.04f, 0.1f, 0.25f), ShallowSea = new Color(0.3f, 0.6f, 0.75f);
    private static readonly Color CompositionSea = new Color(0.36f, 0.43f, 0.62f);

    private readonly List<LensKeyEntry> _key = new List<LensKeyEntry>();
    private readonly List<LensKeyEntry> _found = new List<LensKeyEntry>();
    private readonly HashSet<string> _foundLabels = new HashSet<string>();
    // What each painted cell's colour means (by meso index), and the meaning of the cell being painted now.
    private string[] _bands;
    private string _band;

    /// <summary>The key of the lens in force: each colour it paints and what that colour means.</summary>
    public IReadOnlyList<LensKeyEntry> LensKey => _key;

    /// <summary>What the lens in force paints on <paramref name="t"/>: its colour and what the colour means (null under no lens).</summary>
    public string LensAt(WorldTile t, out Color color)
    {
        color = Color.clear;
        if (t == null || _lens == WorldLens.Normal || _bands == null) return null;
        int i = MesoIndex(t.coord);
        if (i < 0 || i >= _bands.Length) return null;
        color = _overlay[i];
        return _bands[i];
    }

    // The colour a cell gets, named: what it means in the key.
    private Color32 Named(string band, Color32 color)
    {
        _band = band;
        return color;
    }

    // A colour that depends on the world (a biome, a seat, a site): listed once, in the order first painted.
    private void Found(string label, params Color[] colors)
    {
        if (string.IsNullOrEmpty(label) || !_foundLabels.Add(label)) return;
        _found.Add(new LensKeyEntry { label = label, colors = colors });
    }

    private void Key(string label, params Color[] colors) => _key.Add(new LensKeyEntry { label = label, colors = colors });

    private static Color Scale(float v)
    {
        var (r, g, b) = WorldLenses.ScaleColor(v);
        return new Color(r, g, b);
    }

    // The shared percentage scale, band by band (each band a run of its own colours).
    private void ScaleKey()
    {
        foreach (var (from, to, label) in WorldLenses.ScaleBands)
        {
            if (from >= WorldLenses.ScalePeak) Key(label, Scale(1f));
            else Key(label, Scale(from + 0.02f), Scale((from + to) / 2f), Scale(to - 0.02f));
        }
    }

    /// <summary>Start a new key: nothing found yet.</summary>
    private void BeginKey(int cells)
    {
        _key.Clear();
        _found.Clear();
        _foundLabels.Clear();
        if (_bands == null || _bands.Length != cells) _bands = new string[cells];
        else Array.Clear(_bands, 0, _bands.Length);
    }

    /// <summary>The key once every cell is painted: what the world gave (sorted), then the lens's fixed colours.</summary>
    private void EndKey(WorldLens lens)
    {
        _key.AddRange(_found.OrderBy(e => e.label, StringComparer.OrdinalIgnoreCase));
        switch (lens)
        {
            case WorldLens.Settle:
                Key("Poor: City Development under 15", SettlePoor * 0.8f);
                Key("Fair: 15 to 45", SettleFair * 0.8f);
                Key("Rich: 45 and up", SettleRich * 0.8f);
                Key("Brighter: a town may be founded here now", Color.Lerp(SettleFair, Color.white, 0.3f), Color.Lerp(SettleRich, Color.white, 0.3f));
                Key("No town can stand here", Unlivable);
                break;
            case WorldLens.LandFertility:
            case WorldLens.MagicalFertility:
                ScaleKey();
                break;
            case WorldLens.Coherence:
                ScaleKey();
                Key("A Sacred Site", SacredWhite);
                Key("Dissonance rules here (brighter the stronger)", DissonanceDim, DissonanceBright);
                break;
            case WorldLens.Desirability:
                ScaleKey();
                Key("Lifted toward white: a tributary could be raised now", Color.Lerp(Scale(0.6f), Color.white, 0.35f), Color.Lerp(Scale(0.85f), Color.white, 0.35f));
                Key("No one lives here", Unlivable);
                break;
            case WorldLens.Authority:
                Key("Your Administrative Authority (brighter nearer an anchor)", AuthorityFar, Color.Lerp(AuthorityFar, AuthorityNear, 0.5f), AuthorityNear);
                Key("Your detached Outposts", OutpostColor);
                Key("Enclaves", EnclaveColor);
                Key("Independent claims", OtherClaim);
                Key("Wilderness: Outposts only", WildernessGrey);
                Key("Water", AuthorityWater);
                Key("Impassable", ImpassableGrey);
                break;
            case WorldLens.Trade:
                Key("Trade Nexus site", NexusColor);
                Key("Trade Node", NodeColor);
                Key("Road", RoadColor);
                Key("No road", NoRoad);
                break;
            case WorldLens.Resources:
                Key("A blight: lowers land value", Blight);
                Key("Sighted, not yet identified: survey it", Unidentified);
                Key("Made fairer by sites nearby", FairerDim, Fairer);
                Key("Spoiled by sites nearby", SpoiledDim, Spoiled);
                Key("Nothing found", Barren);
                break;
            case WorldLens.Danger:
                Key("Hazard or fresh signs of a hunter (redder the stronger)", Color.Lerp(Calm, ThreatColor, 0.5f), ThreatColor);
                Key("Suffering the land holds", Color.Lerp(Calm, Bruise, 0.5f), Bruise);
                Key("Calm", Calm);
                break;
            case WorldLens.Terrain:
                {
                    float sea = _settings != null ? _settings.seaLevel : 0.4f;
                    Key("Lowlands", Color.Lerp(Lowland, Color.white, sea + (1f - sea) * 0.15f));
                    Key("Uplands", Color.Lerp(Lowland, Color.white, sea + (1f - sea) * 0.5f));
                    Key("Highlands", Color.Lerp(Lowland, Color.white, sea + (1f - sea) * 0.85f));
                    Key("Deep sea", Color.Lerp(DeepSea, ShallowSea, sea * 0.2f));
                    Key("Shallow sea", Color.Lerp(DeepSea, ShallowSea, sea * 0.9f));
                    break;
                }
            case WorldLens.Composition:
                Key("Lighter: a handmade interior", Color.Lerp(Plain, Color.white, 0.35f));
                Key("Darker: a seam at a biome's edge", Color.Lerp(Plain, Color.black, 0.2f));
                Key("An intersection", Color.black);
                Key("Ocean", CompositionSea);
                break;
            case WorldLens.Weather:
                if (_key.Count == 0) Key("No weather fronts on the map", Quiet);
                break;
            case WorldLens.Feelings:
                foreach (var f in EmotionalRegister.All) Key(f.ToString(), Color.Lerp(FeelingBase, FeelingColor(f), 0.5f), FeelingColor(f));
                Key("Quiet, or not yet known", Quiet);
                break;
            case WorldLens.Beauty:
                Key("Hideous", Hideous);
                Key("Plain", Plain);
                Key("Beautiful", Color.Lerp(Plain, Beautiful, 0.5f), Beautiful);
                Key("Lake", LakeBlue);
                break;
            case WorldLens.Territory:
                Key("Your Outposts (tinted cyan)", Color.Lerp(HeldDefault, OutpostColor, 0.5f));
                Key("Wilderness you pull (brighter the harder)", PullDim, Color.Lerp(PullDim, PullBright, 0.5f), PullBright);
                Key("Society adopts it next", AdoptNext);
                Key("A rival pulls harder", RivalDim, RivalBright);
                Key("Held by another authority", EnclaveColor * 0.7f);
                Key("Beyond every pull", Unpulled);
                Key("No one can live here", Unlivable);
                break;
            case WorldLens.Culture:
                Key("Newly arrived", CultureNew);
                Key("Taking root", CultureRooting);
                Key("Long lived in", CultureLong);
                Key("Fading: the land is no longer held", Color.Lerp(CultureRooting, CultureFading, 0.6f));
                Key("No culture of yours", NoCulture);
                break;
        }
    }

    // ===== AS TEXT =====

    /// <summary>
    /// A colour chip for TextMesh Pro: a highlight behind invisible characters (the fonts have no block glyph), one
    /// chip per colour so a run reads as a small gradient.
    /// </summary>
    public static string Swatch(params Color[] colors)
    {
        if (colors == null || colors.Length == 0) return string.Empty;
        var sb = new StringBuilder();
        string blank = colors.Length == 1 ? "00" : "0";
        foreach (var c in colors)
            sb.Append("<mark=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append("FF><color=#00000000>").Append(blank).Append("</color></mark>");
        return sb.ToString();
    }

    /// <summary>
    /// The key as rich text: each colour chip beside what it means, flowing across the line; colours the world added
    /// (biomes, seats, sites) are capped at <paramref name="maxFound"/> with a count of the rest.
    /// </summary>
    public string LensKeyText(int maxFound = 12)
    {
        var parts = new List<string>();
        int found = _found.Count;
        for (int i = 0; i < _key.Count; i++)
        {
            if (i < found && i >= maxFound)
            {
                if (i == maxFound) parts.Add(TooltipText.Muted($"+{found - maxFound} more"));
                continue;
            }
            parts.Add($"<nobr>{Swatch(_key[i].colors)} {_key[i].label}</nobr>");
        }
        return string.Join("    ", parts);
    }
}
