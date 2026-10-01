using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Auric Aria speaking (<see cref="Tutorials"/>): words that float with nothing behind them, as a voice would, and
/// are themselves made of light. The letters carry the celestial look: each glows with its own soft gold (the font's
/// distance-field glow, breathing), keeps a faint shadow of its own for legibility, and twinkles on its own rhythm like
/// a star, now and then flaring toward white. A voice has layered lines (an announcement's whisper, title and words, or
/// a subtitle's one line) plus ornaments (a gold rule under the title or the subtitle). Each letter condenses out of starlight in turn, line after line: it begins
/// large, faint and a little above its place, and slowly settles. Afterwards a slow wave runs through every line and a
/// shimmer from pale to deep gold travels with it. Built on the old WobblyText (per-vertex waves on a TMP mesh),
/// rebuilt for a divine voice. With Reduce Motion the words keep still and only glow. Nothing here catches clicks.
/// The look is tuned in Resources/UI/AuricVoice.mat (the Distance Field shader with Glow and Underlay on); the asset
/// also keeps those shader variants in a build.
/// </summary>
public class AuricVoice : MonoBehaviour
{
    public static readonly Color PaleGold = new Color(1f, 0.91f, 0.66f, 1f);
    public static readonly Color DeepGold = new Color(1f, 0.73f, 0.27f, 1f);
    private const string Preset = "UI/AuricVoice";

    // The wave through a line: its speed (radians a second), how far apart two letters sit on it (radians a canvas
    // unit), how high a letter rises (share of the font size), and the shimmer that runs the other way.
    private const float WaveSpeed = 1.1f, WaveSpread = 0.012f, WaveHeight = 0.06f, ShimmerSpeed = 0.8f, ShimmerSpread = 0.006f;
    // The voice's breath (seconds a breath), how low a letter's light sinks on it, and how far its glow reaches (share
    // of the material's own reach) at the bottom of a breath.
    private const float BreathSeconds = 4.6f, LowestLight = 0.78f, LowestGlow = 0.55f;
    // A letter's own twinkle: its slowest and fastest (radians a second), how much it dims between flares, and how
    // sharp and bright a flare is.
    private const float TwinkleSlow = 0.7f, TwinkleFast = 2.1f, TwinkleDepth = 0.18f, FlareSharpness = 14f, FlareWhite = 0.55f;
    // The words condense out of the air: the longest the whole voice takes, seconds for one letter to settle, how large it
    // begins, and how far above its place (share of the font size).
    private const float RevealLongest = 3f, SettleSeconds = 1.6f, BeginScale = 1.45f, DescendFrom = 0.3f;

    /// <summary>Seconds between two letters condensing (a subtitle speaks quicker than an announcement).</summary>
    public float RevealPerLetter { get; set; } = 0.035f;

    private TMP_Text[] _lines = new TMP_Text[0];
    private Color[] _tints = new Color[0];
    private Graphic[] _ornaments = new Graphic[0];
    private int[] _ornamentAfter = new int[0];
    private Material _material;
    private Color _glowColor;
    private float _glowOuter;
    private float _saidAt;
    private bool _speaking;

    /// <summary>
    /// Give the voice its lines, in speaking order, each with a tint over its gold (alpha dims a line), and ornaments
    /// that appear once the line before them (<paramref name="ornamentAfter"/>: index into the lines, -1 as she begins) is spoken. The
    /// lines share one glowing material.
    /// </summary>
    public void Bind(TMP_Text[] lines, Color[] tints, Graphic[] ornaments = null, int[] ornamentAfter = null)
    {
        _lines = lines;
        _tints = tints;
        _ornaments = ornaments ?? new Graphic[0];
        _ornamentAfter = ornamentAfter ?? new int[0];
        if (_lines.Length == 0) return;
        _material = Luminous(_lines[0]);
        foreach (var line in _lines)
        {
            line.color = Color.white;
            if (_material == null) continue;
            line.fontSharedMaterial = _material;
            line.extraPadding = true;
            line.UpdateMeshPadding();
        }
    }

    // The font's own material with Glow and Underlay on, tuned from the preset (its values, not its atlas, so any font
    // may speak). Null when the font's shader has no glow: the letters then only shimmer.
    private static Material Luminous(TMP_Text line)
    {
        var source = line.fontSharedMaterial;
        if (source == null || !source.HasProperty(ShaderUtilities.ID_GlowColor)) return null;
        var material = new Material(source) { name = source.name + " (Auric Aria)" };
        var preset = Resources.Load<Material>(Preset);
        material.EnableKeyword(ShaderUtilities.Keyword_Glow);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        Copy(preset, material, ShaderUtilities.ID_GlowColor, new Color(1f, 0.78f, 0.36f, 0.75f));
        Copy(preset, material, ShaderUtilities.ID_GlowOffset, 0f);
        Copy(preset, material, ShaderUtilities.ID_GlowInner, 0.08f);
        Copy(preset, material, ShaderUtilities.ID_GlowOuter, 0.7f);
        Copy(preset, material, ShaderUtilities.ID_GlowPower, 0.6f);
        Copy(preset, material, ShaderUtilities.ID_UnderlayColor, new Color(0.03f, 0.02f, 0.01f, 0.45f));
        Copy(preset, material, ShaderUtilities.ID_UnderlayOffsetX, 0f);
        Copy(preset, material, ShaderUtilities.ID_UnderlayOffsetY, 0f);
        Copy(preset, material, ShaderUtilities.ID_UnderlayDilate, 0.15f);
        Copy(preset, material, ShaderUtilities.ID_UnderlaySoftness, 0.6f);
        return material;
    }

    private static void Copy(Material preset, Material to, int id, float fallback) =>
        to.SetFloat(id, preset != null && preset.HasProperty(id) ? preset.GetFloat(id) : fallback);

    private static void Copy(Material preset, Material to, int id, Color fallback) =>
        to.SetColor(id, preset != null && preset.HasProperty(id) ? preset.GetColor(id) : fallback);

    private void Start()
    {
        if (_material == null) return;
        _glowColor = _material.GetColor(ShaderUtilities.ID_GlowColor);
        _glowOuter = _material.GetFloat(ShaderUtilities.ID_GlowOuter);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    /// <summary>Speak the lines' words (already set on them): the letters condense out of the air again from the first.</summary>
    public void Say()
    {
        _saidAt = Time.unscaledTime;
        _speaking = true;
    }

    private void LateUpdate()
    {
        if (!_speaking) return;
        float now = Time.unscaledTime;
        bool still = GameSettings.ReduceMotion;
        float breath = still ? 0.8f : 0.5f - 0.5f * Mathf.Cos(now * 2f * Mathf.PI / BreathSeconds);
        if (_material != null && _glowOuter > 0f)
        {
            _material.SetFloat(ShaderUtilities.ID_GlowOuter, _glowOuter * Mathf.Lerp(LowestGlow, 1f, breath));
            _material.SetColor(ShaderUtilities.ID_GlowColor, new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowColor.a * Mathf.Lerp(0.7f, 1f, breath)));
        }

        int total = 0;
        foreach (var line in _lines)
        {
            if (!line.gameObject.activeSelf) continue;
            line.ForceMeshUpdate();
            total += line.textInfo.characterCount;
        }
        float per = total > 0 ? Mathf.Min(RevealPerLetter, RevealLongest / total) : 0f;
        float light = Mathf.Lerp(LowestLight, 1f, breath);
        int spoken = 0;
        Unfold(-1, 0, per, now, breath, still);
        for (int l = 0; l < _lines.Length; l++)
        {
            var line = _lines[l];
            if (line.gameObject.activeSelf)
            {
                Animate(line, l, l < _tints.Length ? _tints[l] : Color.white, now, spoken, per, light, still);
                spoken += line.textInfo.characterCount;
            }
            Unfold(l, spoken, per, now, breath, still);
        }
    }

    // The ornaments after line <paramref name="after"/> unfold as it finishes (-1: as she begins), dimmed by their own tint.
    private void Unfold(int after, int spoken, float per, float now, float breath, bool still)
    {
        for (int o = 0; o < _ornaments.Length; o++)
        {
            if (o >= _ornamentAfter.Length || _ornamentAfter[o] != after || _ornaments[o] == null) continue;
            float settle = still ? 1f : Smooth(Mathf.Clamp01((now - _saidAt - spoken * per) / SettleSeconds));
            var c = _ornaments[o].color;
            _ornaments[o].color = new Color(c.r, c.g, c.b, settle * Mathf.Lerp(0.45f, 0.9f, breath) * OrnamentLight);
            _ornaments[o].rectTransform.localScale = new Vector3(Mathf.Lerp(0.1f, 1f, settle), 1f, 1f);
        }
    }

    /// <summary>How bright the ornaments are at the height of a breath (a subtitle's rule is fainter than a title's).</summary>
    public float OrnamentLight { get; set; } = 1f;

    // One line: its letters condensing after the letters before it, then carried on the wave, each twinkling.
    private void Animate(TMP_Text text, int lineIndex, Color tint, float now, int before, float per, float light, bool still)
    {
        var info = text.textInfo;
        float height = text.fontSize * WaveHeight, descend = text.fontSize * DescendFrom;
        for (int i = 0; i < info.characterCount; i++)
        {
            var letter = info.characterInfo[i];
            if (!letter.isVisible) continue;
            var mesh = info.meshInfo[letter.materialReferenceIndex];
            var vertices = mesh.vertices;
            var colors = mesh.colors32;
            float settle = still ? 1f : Smooth(Mathf.Clamp01((now - _saidAt - (before + i) * per) / SettleSeconds));
            // The letter's middle: the wave and the shimmer are read there, so a letter moves whole and keeps its shape.
            var middle = (vertices[letter.vertexIndex] + vertices[letter.vertexIndex + 2]) * 0.5f;
            float lift = still ? 0f : Mathf.Sin(now * WaveSpeed + middle.x * WaveSpread) * height;
            float shimmer = still ? 0.5f : 0.5f + 0.5f * Mathf.Sin(now * ShimmerSpeed * 2f - middle.x * ShimmerSpread);
            // Its own star: a rhythm and a phase of its own, dimming a little and flaring briefly toward white.
            float seed = Hash(lineIndex * 131 + i);
            float twinkle = still ? 0.5f : 0.5f + 0.5f * Mathf.Sin(now * Mathf.Lerp(TwinkleSlow, TwinkleFast, seed) + seed * 40f);
            float flare = still ? 0f : Mathf.Pow(twinkle, FlareSharpness);
            var gold = Color.Lerp(Color.Lerp(PaleGold, DeepGold, shimmer) * tint, Color.white, flare * FlareWhite);
            float alpha = settle * light * tint.a * Mathf.Lerp(1f - TwinkleDepth, 1f, twinkle) * Mathf.Lerp(0.86f, 1f, shimmer);
            float scale = Mathf.Lerp(BeginScale, 1f, settle);
            var offset = new Vector3(0f, lift + (1f - settle) * descend, 0f);
            for (int j = 0; j < 4; j++)
            {
                int v = letter.vertexIndex + j;
                vertices[v] = middle + (vertices[v] - middle) * scale + offset;
                var c = colors[v];
                colors[v] = new Color32((byte)(c.r * gold.r), (byte)(c.g * gold.g), (byte)(c.b * gold.b), (byte)(c.a * Mathf.Clamp01(alpha)));
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    // Eased in and out, so nothing starts or stops abruptly.
    private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    // A steady pseudo-random 0..1 for a letter (the same letter twinkles the same way every time she speaks).
    private static float Hash(int n)
    {
        unchecked
        {
            uint h = (uint)n * 747796405u + 2891336453u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            return ((h >> 22) ^ h) / (float)uint.MaxValue;
        }
    }

    private static Sprite _ruleSprite;

    /// <summary>A thin rule of light fading at both ends, with a small diamond at its heart (drawn once, white: tint it).</summary>
    public static Sprite RuleSprite()
    {
        if (_ruleSprite != null) return _ruleSprite;
        const int width = 256, height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs(x + 0.5f - width / 2f) / (width / 2f), up = Mathf.Abs(y + 0.5f - height / 2f);
                float line = up <= 1f ? Mathf.Clamp01(1f - across * across) : 0f;
                float diamond = Mathf.Abs(x + 0.5f - width / 2f) + up <= 6f ? 1f : 0f;
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Max(line, diamond)));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return _ruleSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
