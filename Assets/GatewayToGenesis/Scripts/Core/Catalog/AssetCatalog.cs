using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lazily loaded, case-insensitive lookup of every asset of one type under a Resources folder.
///
/// Content scales by adding assets: drop a new ScriptableObject into the folder and it is found by
/// name everywhere, with no code or scene changes. Duplicate keys are reported once instead of
/// silently shadowing each other.
/// </summary>
public sealed class AssetCatalog<T> where T : UnityEngine.Object
{
    private readonly string _resourcesPath;
    private readonly Func<T, string> _keyOf;
    private readonly string _label;
    private Dictionary<string, T> _byKey;
    private List<T> _ordered;

    /// <param name="resourcesPath">Folder relative to any Resources root ("" searches every Resources folder).</param>
    /// <param name="keyOf">Lookup key for an asset; assets whose key is empty are skipped.</param>
    /// <param name="label">Name used in warnings, e.g. "technology".</param>
    public AssetCatalog(string resourcesPath, Func<T, string> keyOf, string label)
    {
        _resourcesPath = resourcesPath ?? string.Empty;
        _keyOf = keyOf;
        _label = label;
    }

    public string ResourcesPath => _resourcesPath;

    /// <summary>Every asset in load order.</summary>
    public IReadOnlyList<T> All
    {
        get
        {
            EnsureLoaded();
            return _ordered;
        }
    }

    public IEnumerable<string> Keys
    {
        get
        {
            EnsureLoaded();
            return _byKey.Keys;
        }
    }

    public int Count
    {
        get
        {
            EnsureLoaded();
            return _byKey.Count;
        }
    }

    public bool Contains(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        EnsureLoaded();
        return _byKey.ContainsKey(key);
    }

    public bool TryGet(string key, out T asset)
    {
        asset = null;
        if (string.IsNullOrEmpty(key)) return false;
        EnsureLoaded();
        return _byKey.TryGetValue(key, out asset);
    }

    /// <summary>Lookup that warns when the key is unknown, naming the system that asked.</summary>
    public T Get(string key, string requestingSystem)
    {
        if (TryGet(key, out var asset)) return asset;
        if (!string.IsNullOrEmpty(key))
        {
            GameLog.Warning($"{_label} '{key}' requested by {requestingSystem} was not found in Resources/{_resourcesPath}.", LogChannel.Content);
        }
        return null;
    }

    /// <summary>Forget the cached assets so the next access reloads them (editor hot reload, tests).</summary>
    public void Invalidate()
    {
        _byKey = null;
        _ordered = null;
    }

    private void EnsureLoaded()
    {
        if (_byKey != null) return;
        _byKey = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        _ordered = new List<T>();

        T[] assets;
        try
        {
            assets = Resources.LoadAll<T>(_resourcesPath);
        }
        catch (Exception e)
        {
            GameLog.Error($"Could not load {_label} assets from Resources/{_resourcesPath}: {e.Message}", LogChannel.Content);
            return;
        }

        foreach (var asset in assets)
        {
            if (asset == null) continue;
            string key = _keyOf(asset);
            if (string.IsNullOrEmpty(key))
            {
                GameLog.Warning($"{_label} asset '{asset.name}' has no name and was skipped.", LogChannel.Content);
                continue;
            }
            if (_byKey.TryGetValue(key, out var existing))
            {
                if (existing != asset)
                {
                    GameLog.Warning($"Two {_label} assets share the name '{key}' ('{existing.name}' and '{asset.name}'); the first one wins.", LogChannel.Content);
                }
                continue;
            }
            _byKey[key] = asset;
            _ordered.Add(asset);
        }
        GameLog.Event($"Loaded {_byKey.Count} {_label} assets from Resources/{_resourcesPath}", LogChannel.Content);
    }
}
