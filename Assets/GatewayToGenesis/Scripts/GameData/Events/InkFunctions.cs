using System;
using System.Linq;
using Ink.Runtime;

/// <summary>
/// The C# functions Ink stories can call (declare them with EXTERNAL in the .ink file). Every story the
/// game creates is bound here, so every Ink file sees the same API.
///
/// Generic access to any game value:
///   GetValue(domain, target)              e.g. GetValue("resource", "Food"), GetValue("population", "")
///   CheckValue(domain, target, required)  true when the value is at least required
/// Domains are those registered in <see cref="GameValues"/>; registering a new one there makes it available
/// to Ink immediately. The named functions below are kept for existing and readable Ink.
///
/// Preview stories (<see cref="CreatePreviewStory"/>) read text and simulate choices for the UI: their
/// queries return live values but every action is a no-op, so previewing never changes the game.
/// </summary>
public static class InkFunctions
{
    private const LogChannel Log = LogChannel.Events;

    /// <summary>A story for reading content and simulating choices. Reuse it with ResetState() rather than re-parsing.</summary>
    public static Story CreatePreviewStory(string compiledJson)
    {
        var story = new Story(compiledJson);
        Bind(story, null, preview: true);
        return story;
    }

    /// <param name="log">Where Log() and action messages go (defaults to the InkStoryManager log origin).</param>
    /// <param name="preview">When true, functions that change the game do nothing.</param>
    public static void Bind(Story story, Action<string> log = null, bool preview = false)
    {
        if (story == null) return;
        log ??= message => GameLog.Event(message, Log);

        // Runs a game-changing action unless this is a preview story.
        void Act(Action action)
        {
            if (!preview) action();
        }

        // Generic values
        story.BindExternalFunction("GetResonanceAnchors", () => SaveSession.Anchors);
        story.BindExternalFunction("SacrificeWorld", () => Act(() =>
        {
            if (GameAge.Id != "age-of-the-end") throw new InvalidOperationException("World sacrifice is reserved for the final Choice of the End.");
            var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
            if (menu == null) throw new InvalidOperationException("The save menu is unavailable; sacrifice was not performed.");
            menu.CompleteSacrifice();
        }));
        story.BindExternalFunction("GetValue", (string domain, string target) => GameValues.Get(domain, target));
        story.BindExternalFunction("CheckValue", (string domain, string target, int required) => GameValues.Get(domain, target) >= required);

        // Stats
        story.BindExternalFunction("GetStat", (string statName) => Stats()?.GetStatValue(statName) ?? 0);
        story.BindExternalFunction("GetStatValue", (string statName) => Stats()?.GetStatValue(statName) ?? 0);
        story.BindExternalFunction("CheckStat", (string statName, int requiredValue) => Stats() != null && Stats().CheckStat(statName, requiredValue));
        story.BindExternalFunction("ModifyStat", (string statName, int change) => Act(() =>
        {
            Stats()?.ModifyStat(statName, change);
            log($"Modified stat {statName} by {change}");
        }));
        story.BindExternalFunction("ModifyMorale", (int change) => Act(() =>
        {
            Stats()?.ApplyMoraleShift(change);
            log($"Modified morale by {change}");
        }));

        // Resources and production units
        story.BindExternalFunction("GetResource", (string resourceName) => Units()?.GetResourceAmount(resourceName) ?? 0);
        story.BindExternalFunction("GetResourceAmount", (string resourceName) => Units()?.GetResourceAmount(resourceName) ?? 0);
        story.BindExternalFunction("CheckResource", (string resourceName, int requiredAmount) => (Units()?.GetResourceAmount(resourceName) ?? 0) >= requiredAmount);
        story.BindExternalFunction("ModifyResource", (string resourceName, int change) => Act(() =>
        {
            Units()?.ChangeResourceFromName(resourceName, change, false);
            log($"Modified resource {resourceName} by {change}");
        }));
        story.BindExternalFunction("ModifyProductionUnit", (string unitName, int change) => Act(() =>
        {
            Units()?.ChangeProductionUnitFromName(unitName, change);
            log($"Modified production unit {unitName} by {change}");
        }));
        story.BindExternalFunction("BuildProductionUnit", (string unitName) =>
        {
            if (preview) return Units() != null && Units().CanBuildProductionUnit(unitName);
            bool built = Units() != null && Units().BuildProductionUnit(unitName);
            log($"Build {unitName}: {(built ? "done" : "failed")}");
            return built;
        });

        // Technologies
        story.BindExternalFunction("CheckTechnology", (string technologyName) => GameValues.Get("technology", technologyName) >= 1f);
        story.BindExternalFunction("TriggerTechnologyEnlightened", (string technologyName) =>
        {
            var slot = Units()?.GetTechnologySlot(technologyName);
            if (slot == null) return false;
            if (preview) return true;
            // The full Enlightenment (uncovered, part of its research paid, announced), as a met goal gives it.
            Units().EnlightenTechnology(slot, "A story");
            log($"Enlightened technology {technologyName}");
            return true;
        });

        // Event scores
        story.BindExternalFunction("GetEventScore", (string scoreName) => Events()?.GetEventScore(scoreName) ?? 0);
        story.BindExternalFunction("CheckEventScore", (string scoreName, int requiredValue) => (Events()?.GetEventScore(scoreName) ?? 0) >= requiredValue);
        story.BindExternalFunction("ModifyEventScore", (string scoreName, int change) => Act(() => Events()?.ModifyEventScore(scoreName, change)));

        // Time
        story.BindExternalFunction("GetCurrentSeventh", () => TimeSystemLogic.Instance?.CurrentSeventh ?? 1);
        story.BindExternalFunction("GetCurrentPhase", () => TimeSystemLogic.Instance?.CurrentPhase ?? 1);
        story.BindExternalFunction("GetCurrentEcho", () => TimeSystemLogic.Instance?.CurrentEcho ?? 1);
        story.BindExternalFunction("GetCurrentCycle", () => TimeSystemLogic.Instance?.CurrentCycle ?? 1);
        story.BindExternalFunction("IsRitualSeventh", () => TimeSystemLogic.Instance != null && TimeSystemLogic.Instance.IsRitualSeventh);

        // Population
        story.BindExternalFunction("GetPopulation", () => PopGrowthLogic.Instance?.population ?? 0);
        story.BindExternalFunction("GetHousing", () => PopGrowthLogic.Instance?.housing ?? 0);
        story.BindExternalFunction("GetVagrants", () => PopGrowthLogic.Instance?.vagrants ?? 0);
        story.BindExternalFunction("GetDeaths", () => PopGrowthLogic.Instance?.deaths ?? 0);
        story.BindExternalFunction("GetVagrantDeaths", () => PopGrowthLogic.Instance?.vagrantDeaths ?? 0);
        story.BindExternalFunction("GetTrueDeaths", () => PopGrowthLogic.Instance?.trueDeaths ?? 0);
        story.BindExternalFunction("ModifyPopulation", (int change) => Act(() => PopGrowthLogic.Instance?.ModifyPopulation(change)));
        story.BindExternalFunction("ModifyHousing", (int change) => Act(() => PopGrowthLogic.Instance?.ModifyHousing(change)));
        story.BindExternalFunction("ModifyVagrants", (int change) => Act(() => PopGrowthLogic.Instance?.ModifyVagrants(change)));
        story.BindExternalFunction("ProcessEventDeaths", (int deathCount) => Act(() => PopGrowthLogic.Instance?.ProcessEventDeaths(deathCount)));

        // Weather
        story.BindExternalFunction("GetTileWeather", (int q, int r) =>
            CelestialWeatherSystemLogic.Instance?.WeatherAt(new HexCoord(q, r))?.name ?? "");
        story.BindExternalFunction("SetRegionalWeather", (string profileName, string quadrants, int duration) =>
        {
            if (preview) return 0;
            var weather = CelestialWeatherSystemLogic.Instance;
            var profile = CelestialWeatherSystemLogic.FindWeatherProfile(profileName);
            return weather == null ? -1 : weather.AddWeatherFront(profile, WeatherExtent.Quadrants, HexCoord.Zero,
                durationSevenths: duration, quadrants: (quadrants ?? "").Split(',').Select(s => s.Trim()));
        });
        story.BindExternalFunction("SetWorldWeather", (string profileName, int duration) =>
        {
            if (preview) return 0;
            var weather = CelestialWeatherSystemLogic.Instance;
            return weather == null ? -1 : weather.AddWeatherFront(CelestialWeatherSystemLogic.FindWeatherProfile(profileName),
                WeatherExtent.World, HexCoord.Zero, durationSevenths: duration, priority: 1000);
        });
        story.BindExternalFunction("ClearWeatherFront", (int id) => preview || (CelestialWeatherSystemLogic.Instance?.RemoveWeatherFront(id) ?? false));
        story.BindExternalFunction("GetCurrentWeather", () => CelestialWeatherSystemLogic.Instance?.GetCurrentWeatherName() ?? "");
        story.BindExternalFunction("IsWeather", (string profileName) => CelestialWeatherSystemLogic.Instance != null && CelestialWeatherSystemLogic.Instance.IsWeatherActive(profileName));
        story.BindExternalFunction("ChangeWeather", (string profileName) =>
        {
            var profile = CelestialWeatherSystemLogic.FindWeatherProfile(profileName);
            if (profile == null || CelestialWeatherSystemLogic.Instance == null) return false;
            if (preview) return true;
            CelestialWeatherSystemLogic.Instance.SetWeatherProfile(profile, ignoreEchoValidation: true);
            log($"Weather changed to {profile.weatherDisplayName}");
            return true;
        });
        story.BindExternalFunction("SetTimedWeather", (string profileName, int durationSevenths) =>
        {
            var profile = CelestialWeatherSystemLogic.FindWeatherProfile(profileName);
            if (profile == null || durationSevenths <= 0 || CelestialWeatherSystemLogic.Instance == null) return false;
            if (preview) return true;
            CelestialWeatherSystemLogic.Instance.SetTimedWeatherProfile(profile, durationSevenths, ignoreEchoValidation: true);
            log($"Timed weather {profile.weatherDisplayName} for {durationSevenths} sevenths");
            return true;
        });

        // Utilities
        story.BindExternalFunction("Random", (int min, int max) => UnityEngine.Random.Range(min, max + 1));
        story.BindExternalFunction("Log", (string message) => Act(() => log($"Ink: {message}")));
    }

    // Singletons are cleared on destroy, so these are plain C# nulls and ?. is safe.
    private static StatManager Stats() => StatManager.Instance;

    private static GameUnitsLogic Units() => GameUnitsLogic.Instance;

    private static EventSystemLogic Events() => EventSystemLogic.Instance;
}
