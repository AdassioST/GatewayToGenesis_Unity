using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The characters walking the settlement: one villager per citizen, spawned and removed by
/// <see cref="PopGrowthLogic"/>. Characters appear at a random point inside the spawn area's PolygonCollider2D
/// and unregister themselves when destroyed, so the roster never holds dead entries.
/// </summary>
public class GlobalCharacterManager : SingletonBehaviour<GlobalCharacterManager>
{
    private const LogChannel Log = LogChannel.Population;
    private const string DefaultPopulationChild = "Population";
    private const int MaxSpawnAttempts = 30;

    [Tooltip("Spawn area. Its PolygonCollider2D is the region characters appear in.")]
    [SerializeField] private Transform characterParent;
    [Tooltip("Parent of spawned characters. Defaults to the spawn area's 'Population' child, then the spawn area itself.")]
    [SerializeField] private Transform populationParent;

    [SerializeField] private List<GameUnit> characterUnits; // GameUnits available for characters
    [SerializeField] private GameObject characterPrefab; // A generic prefab for any character

    private readonly List<GameCharacterLogic> activeCharacters = new List<GameCharacterLogic>();
    private PolygonCollider2D spawnArea;

    public IReadOnlyList<GameCharacterLogic> ActiveCharacters => activeCharacters;

    public int Count => activeCharacters.Count;

    protected override void OnSingletonAwake()
    {
        if (characterParent == null)
        {
            GameLog.Warning("No spawn area (characterParent) assigned; no characters will appear.", Log);
            return;
        }
        spawnArea = characterParent.GetComponent<PolygonCollider2D>();
        if (spawnArea == null) GameLog.Warning($"'{characterParent.name}' has no PolygonCollider2D; no characters will appear.", Log);
        if (populationParent == null) populationParent = characterParent.Find(DefaultPopulationChild);
        if (populationParent == null) populationParent = characterParent;
    }

    /// <summary>Spawn <paramref name="count"/> characters of the unit named <paramref name="unitName"/> at random points in the spawn area.</summary>
    public void Spawn(string unitName, int count = 1)
    {
        if (count <= 0 || spawnArea == null) return;
        var unit = FindUnit(unitName);
        if (unit == null)
        {
            GameLog.Warning($"No character unit named '{unitName}' in characterUnits.", Log);
            return;
        }
        for (int i = 0; i < count; i++) Spawn(unit, RandomSpawnPosition());
    }

    /// <summary>Spawn one character of <paramref name="unit"/> at a world position.</summary>
    public GameCharacterLogic Spawn(GameUnit unit, Vector3 position)
    {
        if (unit == null || characterPrefab == null)
        {
            GameLog.Error("Cannot spawn a character: the GameUnit or the character prefab is missing.", Log);
            return null;
        }

        var characterObject = Instantiate(characterPrefab, position, Quaternion.identity, populationParent);
        if (!characterObject.TryGetComponent(out GameCharacterLogic character))
        {
            GameLog.Error($"Character prefab '{characterPrefab.name}' has no GameCharacterLogic component.", Log);
            Destroy(characterObject);
            return null;
        }

        character.Initialize(unit);
        activeCharacters.Add(character);
        return character;
    }

    /// <summary>Remove up to <paramref name="count"/> characters chosen at random.</summary>
    public void RemoveRandom(int count)
    {
        for (int i = 0; i < count && activeCharacters.Count > 0; i++)
        {
            RemoveCharacter(activeCharacters[Random.Range(0, activeCharacters.Count)]);
        }
    }

    public void RemoveCharacter(GameCharacterLogic character, bool destroy = true)
    {
        if (!activeCharacters.Remove(character)) return;
        if (destroy && character != null) Destroy(character.gameObject);
    }

    // Called by a character as it is destroyed, whoever destroyed it.
    internal void Unregister(GameCharacterLogic character) => activeCharacters.Remove(character);

    private GameUnit FindUnit(string unitName)
    {
        if (characterUnits == null) return null;
        foreach (var unit in characterUnits)
        {
            if (unit != null && string.Equals(unit.name, unitName, StringComparison.OrdinalIgnoreCase)) return unit;
        }
        return null;
    }

    // Rejection sampling inside the polygon's bounds. A disabled or degenerate collider never reports a hit,
    // so the attempts are capped and the last guess is pulled onto the polygon instead.
    private Vector3 RandomSpawnPosition()
    {
        Bounds bounds = spawnArea.bounds;
        Vector2 point = bounds.center;
        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            point = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            if (spawnArea.OverlapPoint(point)) return point;
        }
        return spawnArea.ClosestPoint(point);
    }
}
