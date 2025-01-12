using UnityEngine;

public class GameCharacterLogic : MonoBehaviour
{
    public GameUnit gameUnit; // The GameUnit assigned to this character

    [SerializeField] private Sprite[] characterSprites; // Random sprites for in-game looks

    private SpriteRenderer spriteRenderer;

    public bool DestroyOnDeath = true;  // Flag to allow for controlled destruction

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        AssignRandomSprite(); // Assign a random in-game sprite
    }

    public void Initialize(GameUnit unit)
    {
        gameUnit = unit;

        // Debug log to confirm initialization
        Debug.Log($"Initialized character with GameUnit: {gameUnit.name}");
    }
    public void AssignRandomSprite()
    {
        if (characterSprites != null && characterSprites.Length > 0)
        {
            spriteRenderer.sprite = characterSprites[Random.Range(0, characterSprites.Length)];
        }
        else
        {
            Debug.LogWarning("No character sprites available for random assignment.");
        }
    }
    public void OnDeath()
    {
        if (DestroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}


