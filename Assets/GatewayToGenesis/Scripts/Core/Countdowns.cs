using System.Collections.Generic;

/// <summary>
/// Per-key timers counted in sevenths: weather cooldowns, council seat cooldowns. A key with no timer (or one
/// that ran out) has 0 remaining. Pure data, so the rules that use it can be tested without a scene.
/// </summary>
public class Countdowns<TKey>
{
    private readonly Dictionary<TKey, int> _remaining;

    public Countdowns(IEqualityComparer<TKey> comparer = null)
    {
        _remaining = new Dictionary<TKey, int>(comparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>Start (or restart) a timer. Zero or less clears it.</summary>
    public void Start(TKey key, int sevenths)
    {
        if (sevenths > 0) _remaining[key] = sevenths;
        else _remaining.Remove(key);
    }

    public int Remaining(TKey key) => key != null && _remaining.TryGetValue(key, out int left) ? left : 0;

    public bool IsRunning(TKey key) => Remaining(key) > 0;

    /// <summary>Count every timer down by one seventh. Returns the keys whose timer just ran out.</summary>
    public List<TKey> Tick()
    {
        var expired = new List<TKey>();
        foreach (var key in new List<TKey>(_remaining.Keys))
        {
            if (--_remaining[key] > 0) continue;
            _remaining.Remove(key);
            expired.Add(key);
        }
        return expired;
    }

    public void Clear() => _remaining.Clear();

    public int Count => _remaining.Count;
}
