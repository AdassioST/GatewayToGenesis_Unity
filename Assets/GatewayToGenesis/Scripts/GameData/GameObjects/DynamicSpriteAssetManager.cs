using TMPro;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Builds a TMP sprite asset from a sprite atlas at start-up, so text can show those icons inline by name
/// (<c>&lt;sprite name="Food"&gt;</c>). Optional: with no atlas assigned it does nothing. The assigned sprite asset
/// is only a template; entries are added to a runtime copy, so Play mode never rewrites the project asset.
/// Nothing uses it yet: technology cost icons come from TextMesh Pro's default sprite asset.
/// </summary>
public class DynamicSpriteAssetManager : SingletonBehaviour<DynamicSpriteAssetManager>
{
    private const LogChannel Log = LogChannel.Content;

    [Tooltip("Blank TMP sprite asset used as the template (its sprite sheet and material). Left untouched.")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("spriteAsset")] private TMP_SpriteAsset templateSpriteAsset;
    [Tooltip("Atlas whose sprites become inline icons, each named after its sprite.")]
    [SerializeField] private SpriteAtlas iconAtlas;

    /// <summary>The runtime sprite asset holding the atlas icons, or null when the feature is not set up.</summary>
    public TMP_SpriteAsset SpriteAsset { get; private set; }

    private void Start()
    {
        if (iconAtlas == null && templateSpriteAsset == null)
        {
            GameLog.Event("Inline icons are off: no icon atlas or sprite asset assigned.", Log);
            return;
        }
        if (iconAtlas == null || templateSpriteAsset == null)
        {
            GameLog.Warning("Inline icons need both an icon atlas and a template sprite asset assigned; they are off.", Log);
            return;
        }

        SpriteAsset = Instantiate(templateSpriteAsset);
        SpriteAsset.name = $"{templateSpriteAsset.name} (runtime)";
        SpriteAsset.spriteCharacterTable.Clear();
        SpriteAsset.spriteGlyphTable.Clear();

        var sprites = new Sprite[iconAtlas.spriteCount];
        iconAtlas.GetSprites(sprites);
        int added = 0;
        foreach (var sprite in sprites)
        {
            if (AddSprite(sprite)) added++;
        }
        SpriteAsset.UpdateLookupTables(); // once, after every sprite is in

        GameLog.Event($"Inline icon sprite asset: {added} sprites from atlas '{iconAtlas.name}'", Log);
    }

    protected override void OnSingletonDestroy()
    {
        if (SpriteAsset != null) Destroy(SpriteAsset);
    }

    // True when the sprite was added (false when missing or already present). Call UpdateLookupTables afterwards.
    private bool AddSprite(Sprite sprite)
    {
        if (sprite == null) return false;

        // Atlas copies are named "Name(Clone)"; icons are addressed by the original name.
        string spriteName = sprite.name.Replace("(Clone)", string.Empty);
        uint unicode = (uint)TMP_TextUtilities.GetSimpleHashCode(spriteName);
        if (SpriteAsset.spriteCharacterTable.Exists(c => c.unicode == unicode)) return false;

        var glyph = new TMP_SpriteGlyph { sprite = sprite };
        SpriteAsset.spriteCharacterTable.Add(new TMP_SpriteCharacter(unicode, glyph) { name = spriteName });
        SpriteAsset.spriteGlyphTable.Add(glyph);
        return true;
    }

    public void AssignSpriteAssetToTMP(TextMeshProUGUI tmpText)
    {
        if (tmpText != null && SpriteAsset != null) tmpText.spriteAsset = SpriteAsset;
    }
}
