using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GlobalCharacterManager : MonoBehaviour
{
    public static GlobalCharacterManager Instance { get; private set; }

    [SerializeField] private Transform characterParent; // Parent for organizing characters in the hierarchy

    [SerializeField] private List<GameUnit> characterUnits; // GameUnits available for characters
    [SerializeField] private GameObject characterPrefab; // A generic prefab for any character

    public List<GameCharacterLogic> activeCharacters = new List<GameCharacterLogic>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GlobalCharacterManager found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SpawnCharacterFromName(string name)
    {
        // Look for the character prefab in the characterUnits list
        GameUnit characterUnit = characterUnits.FirstOrDefault(unit => unit.name == name);

        if (characterUnit != null)
        {
            // Get the collider component of the "Characters" GameObject
            PolygonCollider2D characterCollider = characterParent.GetComponent<PolygonCollider2D>();

            if (characterCollider != null)
            {
                Vector3 randomPosition = GetRandomSpawnPositionInCollider(characterCollider);

                // Instantiate the character at the random position
                GameObject character = Instantiate(characterPrefab, randomPosition, Quaternion.identity);
                character.transform.SetParent(characterParent.Find("Population"));

                GameCharacterLogic characterLogic = character.GetComponent<GameCharacterLogic>();

                if (characterLogic != null)
                {
                    characterLogic.gameUnit = characterUnit;
                    characterLogic.AssignRandomSprite();

                    activeCharacters.Add(characterLogic);
                }
            }
            else
            {
                Debug.LogWarning("No collider found on the 'Characters' GameObject.");
            }
        }
        else
        {
            Debug.LogWarning($"Character prefab {name} not found in the GameUnit list.");
        }
    }



    public GameCharacterLogic SpawnCharacterFromUnit(GameUnit unit, Vector3 position)
    {
        if (unit == null || characterPrefab == null)
        {
            Debug.LogError("Cannot spawn character: GameUnit or characterPrefab is null.");
            return null;
        }

        GameObject characterObject = Instantiate(characterPrefab, position, Quaternion.identity, characterParent);
        GameCharacterLogic characterLogic = characterObject.GetComponent<GameCharacterLogic>();

        if (characterLogic == null)
        {
            Debug.LogError("Character prefab is missing GameCharacterLogic component.");
            Destroy(characterObject);
            return null;
        }

        characterLogic.Initialize(unit); // Sets the GameUnit properties
        activeCharacters.Add(characterLogic);

        return characterLogic;
    }

    public void RemoveCharacter(GameCharacterLogic character, bool destroy = true)
    {
        if (character == null) return;

        if (activeCharacters.Contains(character))
        {
            activeCharacters.Remove(character);

            if (destroy)
            {
                Destroy(character.gameObject);
            }
        }
    }
    public Vector3 GetRandomSpawnPositionInCollider(PolygonCollider2D spawnAreaCollider)
    {
        // Get a random point within the bounds of the PolygonCollider2D
        Bounds bounds = spawnAreaCollider.bounds;
        Vector3 randomPoint = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            0); // Assuming you're working in a 2D plane, use 0 for z-axis

        // Ensure the point is inside the collider
        while (!spawnAreaCollider.OverlapPoint(randomPoint))
        {
            randomPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                0); // Keep trying until we get a point inside the polygon collider
        }

        return randomPoint;
    }
    public List<GameCharacterLogic> GetActiveCharacters()
    {
        return activeCharacters;
    }
}
