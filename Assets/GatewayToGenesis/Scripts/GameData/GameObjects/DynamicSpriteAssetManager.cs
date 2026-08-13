using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

public class DynamicSpriteAssetManager : MonoBehaviour
{
    public static DynamicSpriteAssetManager Instance { get; private set; }

    public TMP_SpriteAsset spriteAsset; // Assign a blank TMP_SpriteAsset in the editor

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate Dynamic Sprite Asset Manager found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }

    void Start()
    {
        if (spriteAsset == null)
        {
            Debug.LogError("Sprite Asset is not assigned to the Dynamic Sprite Asset Manager.");
            return;
        }

        // Clear existing data before adding new sprites
        spriteAsset.spriteCharacterTable.Clear();
        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.UpdateLookupTables();

        // Load the SpriteAtlas from Resources
        SpriteAtlas atlas = Resources.Load<SpriteAtlas>("Icons/IconAtlas"); // Make sure to replace with your actual atlas path

        if (atlas == null)
        {
            Debug.LogError("Sprite Atlas not found. Make sure it's in the correct Resources folder.");
            return;
        }

        // Extract sprites from the atlas and add them to TMP_SpriteAsset
        // Get all sprites from the atlas
        Sprite[] sprites = new Sprite[atlas.spriteCount];
        atlas.GetSprites(sprites);

        foreach (var sprite in sprites)
        {
            AddSpriteToSpriteAsset(sprite);
        }

        Debug.Log($"Loaded sprites from Atlas: {atlas.name}");
    }

    void AddSpriteToSpriteAsset(Sprite sprite)
    {
        if (spriteAsset == null)
        {
            Debug.LogError("Sprite Asset is null. Cannot add sprites.");
            return;
        }

        // Check if the sprite already exists in the table
        uint unicode = (uint)TMP_TextUtilities.GetSimpleHashCode(sprite.name);
        TMP_SpriteCharacter existingCharacter = spriteAsset.spriteCharacterTable.Find(c => c.unicode == unicode);

        if (existingCharacter != null)
        {
            Debug.Log($"Sprite {sprite.name} already exists in the sprite asset.");
            return; // Skip adding if already present
        }

        // Create and add the new sprite if not present
        TMP_SpriteGlyph spriteGlyph = new TMP_SpriteGlyph
        {
            sprite = sprite
        };

        TMP_SpriteCharacter spriteCharacter = new TMP_SpriteCharacter(unicode, spriteGlyph)
        {
            name = sprite.name
        };

        spriteAsset.spriteCharacterTable.Add(spriteCharacter);
        spriteAsset.spriteGlyphTable.Add(spriteGlyph);
        spriteAsset.UpdateLookupTables();

        Debug.Log($"Added sprite: {sprite.name} with hash {TMP_TextUtilities.GetSimpleHashCode(sprite.name)}");
    }




    public void AssignSpriteAssetToTMP(TextMeshProUGUI tmpText)
    {
        if (spriteAsset == null)
        {
            Debug.LogError("Sprite Asset is null. Cannot assign it to TextMeshProUGUI.");
            return;
        }

        if (tmpText == null)
        {
            Debug.LogError("TextMeshProUGUI component is null. Cannot assign Sprite Asset.");
            return;
        }

        tmpText.spriteAsset = spriteAsset;
    }


}


