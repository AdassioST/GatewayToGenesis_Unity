using TMPro;
using UnityEngine;

/// <summary>Space between the tooltip's frame and its text. A plain struct: Unity forbids creating a RectOffset in a
/// ScriptableObject's field initializer.</summary>
[System.Serializable]
public struct TooltipPadding
{
    public int left, right, top, bottom;

    public TooltipPadding(int left, int right, int top, int bottom)
    {
        this.left = left;
        this.right = right;
        this.top = top;
        this.bottom = bottom;
    }
}

/// <summary>
/// How tooltips look and behave: font, pixel-art pieces, colours, sizes and timings. One asset
/// (Resources/UI/TooltipTheme) styles every tooltip, so the look is tuned here, in the inspector, not in code.
/// Colours inside tooltip text (keywords, good, bad, minor details) are <see cref="TooltipText"/>'s palette.
/// </summary>
[CreateAssetMenu(fileName = "TooltipTheme", menuName = "Game Object/UI/Tooltip Theme")]
public class TooltipTheme : ScriptableObject
{
    [Header("Art")]
    public TMP_FontAsset font;
    [Tooltip("Seamless tile behind the text: the Storage Tab texture, repeated at backgroundScale.")]
    public Sprite background;
    [Tooltip("Opacity of the background (1 = solid).")]
    [Range(0f, 1f)] public float backgroundAlpha = 0.9f;
    [Tooltip("Canvas units per background pixel. 4 matches how the Storage Tab shows the same texture.")]
    public float backgroundScale = 4f;
    [Tooltip("9-sliced frame around the tooltip; its edges are tiled and its centre is left to the background.")]
    public Sprite frame;
    [Tooltip("9-sliced soft shadow under the frame.")]
    public Sprite shadow;
    [Tooltip("9-sliced rule between sections (sliced left and right).")]
    public Sprite divider;
    [Tooltip("White ring, filled radially while the pointer holds still (tinted below).")]
    public Sprite ring;
    [Tooltip("Dark disc the ring sits in, on the tooltip's bottom edge.")]
    public Sprite ringSocket;
    [Tooltip("Canvas units per art pixel, so the frame matches the HUD's pixel size.")]
    public float pixelScale = 2f;

    [Header("Colours")]
    public Color titleColor = new Color32(0xFF, 0xE6, 0xC7, 0xFF);
    [Tooltip("The small-caps line under the title.")]
    public Color subtitleColor = new Color32(0xB9, 0xA5, 0xE0, 0xFF);
    public Color bodyColor = new Color32(0xEC, 0xE3, 0xD4, 0xFF);
    [Tooltip("Flavour text (the in-world description).")]
    public Color flavorColor = new Color32(0xEC, 0xA6, 0xBB, 0xFF);
    [Tooltip("Frame tint while the tooltip follows the pointer.")]
    public Color frameFluid = new Color32(0xB0, 0xA4, 0x98, 0xFF);
    [Tooltip("Frame tint once it is solid.")]
    public Color frameSolid = Color.white;
    public Color ringTrack = new Color32(0x3B, 0x2F, 0x29, 0xFF);
    public Color ringFill = new Color32(0xE6, 0xBC, 0x93, 0xFF);
    public Color ringSolid = new Color32(0xFE, 0xD4, 0xAA, 0xFF);
    [Tooltip("A hovered keyword in a solid tooltip.")]
    public Color linkHover = new Color32(0xFF, 0xF6, 0xD2, 0xFF);

    [Header("Sizes (canvas units)")]
    public float titleSize = 23f;
    public float subtitleSize = 14f;
    public float bodySize = 17f;
    public float flavorSize = 16f;
    public float minWidth = 260f;
    public float maxWidth = 480f;
    public float bannerWidth = 650f;
    public TooltipPadding padding = new TooltipPadding(28, 28, 24, 34);
    [Tooltip("Space between sections. Rows inside a section are spaced by TooltipText.RowSpacing.")]
    public float spacing = 14f;
    [Tooltip("Gap between the pointer and the tooltip's corner.")]
    public Vector2 pointerOffset = new Vector2(20f, -8f);

    [Header("Behaviour (seconds and pixels, unscaled)")]
    [Tooltip("How long the pointer must stay still for a tooltip with keywords to turn solid.")]
    public float lockDelay = 0.6f;
    [Tooltip("How far (screen pixels) the pointer may wander from where it came to rest and still count as holding still.")]
    public float stillTolerance = 4f;
    [Tooltip("How long a keyword is hovered in a solid tooltip before its own tooltip opens.")]
    public float linkHoverDelay = 0.12f;
    [Tooltip("Grace period for crossing the gap into a solid tooltip.")]
    public float closeGrace = 0.35f;
    [Tooltip("Lists longer than this show their first lines and a \"+N more\" to click.")]
    public int collapseAfter = 5;
    [Tooltip("Visible characters per page of a keyword's details (the long read behind \"More...\"); longer texts get Previous and Next.")]
    public int detailsPageChars = 900;
    public float fadeIn = 0.12f;
    public float fadeOut = 0.08f;
}
