using System;
using System.Collections.Generic;

/// <summary>
/// One contribution from one source: a flat amount and a percentage (25 = +25%).
/// </summary>
public struct ModifierValue
{
    public float Flat;
    public float Percent;

    public ModifierValue(float flat, float percent)
    {
        Flat = flat;
        Percent = percent;
    }

    public bool IsZero => Math.Abs(Flat) < 0.0001f && Math.Abs(Percent) < 0.0001f;

    public static ModifierValue operator +(ModifierValue a, ModifierValue b) => new ModifierValue(a.Flat + b.Flat, a.Percent + b.Percent);

    /// <summary>(value + Flat) * (1 + Percent / 100).</summary>
    public float ApplyTo(float value) => (value + Flat) * (1f + Percent / 100f);
}

/// <summary>
/// Source-tracked modifier storage shared by every system that accepts bonuses
/// (stats, production, click power, construction cost, housing...).
///
/// Every contribution is keyed by (target, source). Removing a legend, civic, weather or event is
/// therefore always a single <see cref="RemoveSource"/> call, and nothing can be applied twice.
/// Targets are case-insensitive strings; see <see cref="ModifierTargets"/> for the scoped key format.
/// </summary>
public sealed class ModifierLedger
{
    private readonly Dictionary<string, Dictionary<string, ModifierValue>> _byTarget =
        new Dictionary<string, Dictionary<string, ModifierValue>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Increments on every change; consumers can cache derived values against it.</summary>
    public int Version { get; private set; }

    /// <summary>Raised with the target key whenever that target's contributions change.</summary>
    public event Action<string> Changed;

    /// <summary>Replace the contribution of <paramref name="source"/> on <paramref name="target"/>.</summary>
    public void Set(string target, string source, ModifierValue value)
    {
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(source)) return;
        if (value.IsZero)
        {
            Remove(target, source);
            return;
        }
        if (!_byTarget.TryGetValue(target, out var sources))
        {
            sources = new Dictionary<string, ModifierValue>(StringComparer.OrdinalIgnoreCase);
            _byTarget[target] = sources;
        }
        if (sources.TryGetValue(source, out var existing) && Math.Abs(existing.Flat - value.Flat) < 0.0001f && Math.Abs(existing.Percent - value.Percent) < 0.0001f)
        {
            return;
        }
        sources[source] = value;
        MarkChanged(target);
    }

    public void SetFlat(string target, string source, float flat) => Set(target, source, new ModifierValue(flat, Get(target, source).Percent));

    public void SetPercent(string target, string source, float percent) => Set(target, source, new ModifierValue(Get(target, source).Flat, percent));

    /// <summary>Accumulate onto the existing contribution of <paramref name="source"/>.</summary>
    public void Add(string target, string source, ModifierValue delta) => Set(target, source, Get(target, source) + delta);

    public ModifierValue Get(string target, string source)
    {
        if (target != null && source != null && _byTarget.TryGetValue(target, out var sources) && sources.TryGetValue(source, out var value))
        {
            return value;
        }
        return default;
    }

    public bool Remove(string target, string source)
    {
        if (target == null || source == null || !_byTarget.TryGetValue(target, out var sources)) return false;
        if (!sources.Remove(source)) return false;
        if (sources.Count == 0) _byTarget.Remove(target);
        MarkChanged(target);
        return true;
    }

    /// <summary>Remove every contribution made by <paramref name="source"/>. Returns how many targets were affected.</summary>
    public int RemoveSource(string source)
    {
        if (string.IsNullOrEmpty(source)) return 0;
        List<string> affected = null;
        foreach (var pair in _byTarget)
        {
            if (pair.Value.ContainsKey(source)) (affected ??= new List<string>()).Add(pair.Key);
        }
        if (affected == null) return 0;
        foreach (var target in affected) Remove(target, source);
        return affected.Count;
    }

    /// <summary>Sum of every source's contribution to one target.</summary>
    public ModifierValue Total(string target)
    {
        var total = default(ModifierValue);
        if (target != null && _byTarget.TryGetValue(target, out var sources))
        {
            foreach (var value in sources.Values) total += value;
        }
        return total;
    }

    /// <summary>Sum of contributions across several scoped keys (e.g. unit + its section + "*").</summary>
    public ModifierValue Total(string a, string b, string c = null, string d = null)
    {
        return Total(a) + Total(b) + (c != null ? Total(c) : default) + (d != null ? Total(d) : default);
    }

    public bool HasTarget(string target) => target != null && _byTarget.ContainsKey(target);

    public IEnumerable<string> Targets => _byTarget.Keys;

    /// <summary>Per-source contributions for tooltips and debugging (empty when the target has none).</summary>
    public IReadOnlyDictionary<string, ModifierValue> Sources(string target)
    {
        if (target != null && _byTarget.TryGetValue(target, out var sources)) return sources;
        return Empty;
    }

    public bool HasSource(string source)
    {
        foreach (var sources in _byTarget.Values)
        {
            if (sources.ContainsKey(source)) return true;
        }
        return false;
    }

    public void Clear()
    {
        if (_byTarget.Count == 0) return;
        var targets = new List<string>(_byTarget.Keys);
        _byTarget.Clear();
        foreach (var target in targets) MarkChanged(target);
    }

    private void MarkChanged(string target)
    {
        Version++;
        Changed?.Invoke(target);
    }

    private static readonly IReadOnlyDictionary<string, ModifierValue> Empty = new Dictionary<string, ModifierValue>();
}
