using UnityEngine;

/// <summary>
/// A character in the settlement (the Villager prefab). Picks a random look when it appears and leaves
/// <see cref="GlobalCharacterManager"/>'s roster when it is destroyed.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GameCharacterLogic : MonoBehaviour
{
    private const LogChannel Log = LogChannel.Population;

    public GameUnit gameUnit; // The GameUnit assigned to this character

    [SerializeField] private Sprite[] characterSprites; // Random sprites for in-game looks

    public bool DestroyOnDeath = true; // Flag to allow for controlled destruction

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        AssignRandomSprite();
    }

    private void OnDestroy()
    {
        var manager = GlobalCharacterManager.Instance;
        if (manager != null) manager.Unregister(this);
    }

    public void Initialize(GameUnit unit) => gameUnit = unit;

    public void AssignRandomSprite()
    {
        if (characterSprites == null || characterSprites.Length == 0)
        {
            GameLog.Warning($"'{name}' has no character sprites to pick from.", Log);
            return;
        }
        spriteRenderer.sprite = characterSprites[Random.Range(0, characterSprites.Length)];
    }

    public void OnDeath()
    {
        if (DestroyOnDeath) Destroy(gameObject);
    }
}
