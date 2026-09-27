using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WeatherExtent { Radius, Sectors, World }

/// <summary>A saved weather footprint. Higher priority wins; newer id breaks ties. Zero lifetime is permanent.</summary>
[Serializable]
public class WorldWeatherFront
{
    public int id;
    public WeatherProfileSO profile;
    public WeatherExtent extent;
    public HexCoord center;
    public int radius = 6;
    public List<string> sectors = new List<string>();
    public int remainingSevenths;
    public int priority;
    public bool procedural;

    public bool Covers(WorldTile tile) => tile != null && (extent == WeatherExtent.World ||
        (extent == WeatherExtent.Radius && HexCoord.Distance(center, tile.coord) <= radius) ||
        (extent == WeatherExtent.Sectors && sectors != null && sectors.Any(s => string.Equals(s, tile.sector, StringComparison.OrdinalIgnoreCase))));
}

public static class WorldWeather
{
    public static WorldWeatherFront At(IEnumerable<WorldWeatherFront> fronts, WorldTile tile)
    {
        WorldWeatherFront best = null;
        if (fronts == null || tile == null) return null;
        foreach (var front in fronts)
            if (front != null && front.profile != null && front.Covers(tile) &&
                (best == null || front.priority > best.priority || front.priority == best.priority && front.id > best.id)) best = front;
        return best;
    }

    public static void Tick(List<WorldWeatherFront> fronts)
    {
        for (int i = fronts.Count - 1; i >= 0; i--)
        {
            var front = fronts[i];
            if (front == null || front.profile == null || front.remainingSevenths > 0 && --front.remainingSevenths == 0)
                fronts.RemoveAt(i);
        }
    }
}

public partial class CelestialWeatherSystemLogic
{
    private List<WorldWeatherFront> regionalWeather = new List<WorldWeatherFront>();
    private int nextFrontId, regionalWeatherStep;
    private WeatherProfileSO resolvedCapitalWeather;
    public int RegionalWeatherVersion { get; private set; }
    public IReadOnlyList<WorldWeatherFront> WeatherFronts => regionalWeather.AsReadOnly();

    public WeatherProfileSO WeatherAt(HexCoord coord)
    {
        var map = WorldSystem.Instance?.Map;
        var tile = map?.Get(coord);
        if (tile == null) return null;
        var front = WorldWeather.At(regionalWeather, tile);
        // Legacy story weather still represents a world event and outranks ordinary fronts.
        return front == null || isHardSetWeather && front.procedural ? activeWeatherProfile : front.profile;
    }

    /// <summary>Event API: radius, named sectors, or a world-wide crisis; returns an id for cancellation.</summary>
    public int AddWeatherFront(WeatherProfileSO profile, WeatherExtent extent, HexCoord center,
        int radius = 6, int durationSevenths = 4, int priority = 100, IEnumerable<string> sectors = null)
    {
        var map = WorldSystem.Instance?.Map;
        if (profile == null || map == null || radius < 0 || durationSevenths < 0 || !Enum.IsDefined(typeof(WeatherExtent), extent)) return -1;
        var regionNames = sectors?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        if (extent == WeatherExtent.Radius && !map.InBounds(center) ||
            extent == WeatherExtent.Sectors && !map.Tiles.Any(t => regionNames.Any(s => string.Equals(s, t.sector, StringComparison.OrdinalIgnoreCase)))) return -1;
        var front = new WorldWeatherFront { id = ++nextFrontId, profile = profile, extent = extent,
            center = center, radius = radius, remainingSevenths = durationSevenths, priority = priority, sectors = regionNames };
        regionalWeather.Add(front);
        RefreshWeatherTiles();
        SyncCapitalWeather();
        return front.id;
    }

    public bool RemoveWeatherFront(int id)
    {
        bool removed = regionalWeather.RemoveAll(f => f.id == id) > 0;
        if (removed) { RefreshWeatherTiles(); SyncCapitalWeather(); }
        return removed;
    }

    private void TickRegionalWeather()
    {
        WorldWeather.Tick(regionalWeather);
        regionalWeatherStep++;
        var map = WorldSystem.Instance?.Map;
        if (enableProceduralWeather && map != null)
        {
            var rng = new System.Random(WorldNoise.Stream(map.seed, "weather:" + regionalWeatherStep));
            var candidates = proceduralWeatherPool.Where(p => p != null && p.triggerWeight > 0f && p.AreConditionsMet()).ToList();
            var weights = candidates.Select(p => p.triggerWeight).ToList();
            // Bounded fronts, spread over the complete world rather than anchored to the capital.
            int target = Math.Min(24, Math.Max(8, map.Count / 700));
            for (int i = regionalWeather.Count(f => f.procedural); i < target && candidates.Count > 0; i++)
            {
                var tile = map[rng.Next(map.Count)];
                var front = new WorldWeatherFront { id = ++nextFrontId,
                    profile = candidates[WeatherRules.PickWeighted(weights, (float)rng.NextDouble())],
                    center = tile.coord, radius = rng.Next(5, 15), remainingSevenths = rng.Next(3, 9), procedural = true };
                regionalWeather.Add(front);
            }
        }
        RefreshWeatherTiles();
        SyncCapitalWeather();
    }

    // Handles initialization order, save restoration, and a newly generated map too.
    private void LateUpdate()
    {
        if (isInitialized && regionalWeatherStep == 0 && WorldSystem.Instance?.Map != null) TickRegionalWeather();
        SyncCapitalWeather();
    }

    private void RefreshWeatherTiles()
    {
        RegionalWeatherVersion++;
        var map = WorldSystem.Instance?.Map;
        if (map == null) return;
        foreach (var tile in map.Tiles)
            tile.weatherTravelMultiplier = Mathf.Clamp(WeatherAt(tile.coord)?.mapTravelMultiplier ?? 1f, 0.25f, 4f);
    }

    public void RestoreRegionalWeather()
    {
        // Modifiers have already been restored from the snapshot; discard stale scene caches.
        resolvedCapitalWeather = null;
        RefreshWeatherTiles();
        SyncCapitalWeather();
    }

    public void SyncCapitalWeather()
    {
        var map = WorldSystem.Instance?.Map;
        var profile = map == null ? activeWeatherProfile : WeatherAt(map.Capital);
        if (profile == resolvedCapitalWeather) return;
        if (resolvedCapitalWeather != null) RemoveWeatherEffects(resolvedCapitalWeather);
        resolvedCapitalWeather = profile;
        if (profile != null)
        {
            ApplyWeatherEffects(profile);
            visualLogic?.OnWeatherChanged(profile);
        }
        OnWeatherChanged?.Invoke(profile);
    }
}
