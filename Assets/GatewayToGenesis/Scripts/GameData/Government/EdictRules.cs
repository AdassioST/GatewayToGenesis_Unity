using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The edicts' rules with no scene (<see cref="EdictSystem"/> owns the state): when the government may issue edicts,
/// how many it can hold, when a stance may change or an edict be sealed, what the laws in force add up to (effects and
/// levers), and what the council thinks of them (its accord). Pure, so it also runs outside Unity.
/// </summary>
public static class EdictRules
{
    public enum Mood { Discord, Steady, Harmony }

    /// <summary>A seated council member, as the accord sees it.</summary>
    public struct Judge
    {
        public string legend, seat;
        public IReadOnlyList<string> areas;
    }

    // ===== ESTABLISHMENT AND SLOTS =====

    /// <summary>Council seats held, counting the Head of State.</summary>
    public static int Seats(int openRegularPositions) => 1 + Math.Max(0, openRegularPositions);

    public static bool CanEstablish(int seats, EdictTuning t) => seats >= Math.Max(1, t.minCouncilSeats);

    /// <summary>Edicts that can be in force at once: seats beyond <see cref="EdictTuning.seatsBeforeFirstSlot"/> (3 seats: 1, 7 seats: 5).</summary>
    public static int Capacity(int seats, EdictTuning t) => CanEstablish(seats, t) ? Math.Max(1, seats - Math.Max(0, t.seatsBeforeFirstSlot)) : 0;

    /// <summary>Stances in force on the day the edicts are established: the defaults, except the borders, which keep the policy the realm already had.</summary>
    public static Dictionary<string, string> Founding(BorderPolicy borders)
    {
        var stances = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stance in EdictCatalog.Stances) stances[stance.id] = stance.Default?.id;
        var border = EdictCatalog.BorderOption(borders);
        if (border != null) stances[EdictCatalog.Borders.id] = border.id;
        return stances;
    }

    // ===== STANCES =====

    public static StanceOption InForce(EdictState state, StanceDefinition stance)
    {
        if (stance == null) return null;
        string id = state != null && state.stances.TryGetValue(stance.id, out var chosen) ? chosen : null;
        return stance.Option(id) ?? stance.Default;
    }

    public static IEnumerable<StanceOption> OptionsInForce(EdictState state) => EdictCatalog.Stances.Select(s => InForce(state, s)).Where(o => o != null);

    public static int Cooldown(EdictState state, string key, bool stance)
    {
        var map = stance ? state?.stanceCooldowns : state?.edictCooldowns;
        return map != null && map.TryGetValue(key, out int left) ? Math.Max(0, left) : 0;
    }

    /// <summary>Sevenths a changed stance must stand: longer while the council is in discord.</summary>
    public static int StanceCooldown(EdictTuning t, Mood mood) =>
        (int)Math.Ceiling(Math.Max(0, t.stanceCooldownSevenths) * (mood == Mood.Discord ? Math.Max(1f, t.discordCooldownMultiplier) : 1f));

    /// <summary>Why a stance cannot change to <paramref name="optionId"/> now, or null.</summary>
    public static string WhyNotChange(EdictState state, string stanceId, string optionId, Func<string, bool> researched)
    {
        if (state == null || !state.established) return "The government cannot issue edicts yet.";
        var stance = EdictCatalog.Stance(stanceId);
        if (stance == null) return $"There is no stance '{stanceId}'.";
        var option = stance.Option(optionId);
        if (option == null) return $"{stance.title} has no option '{optionId}'.";
        if (InForce(state, stance) == option) return $"{option.title} is already the law.";
        int wait = Cooldown(state, stance.id, true);
        if (wait > 0) return $"{stance.title} changed recently: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        if (!string.IsNullOrEmpty(option.requiresTechnology) && (researched == null || !researched(option.requiresTechnology))) return $"Needs {option.requiresTechnology}.";
        return null;
    }

    // ===== EDICTS =====

    public static bool IsActive(EdictState state, string edictId) => state != null && state.active.Any(a => string.Equals(a.id, edictId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Why an edict cannot be sealed now, or null.</summary>
    public static string WhyNotSeal(EdictState state, EdictDefinition edict, int capacity, string headOfState, Func<string, bool> researched, Func<string, float, bool> affordable)
    {
        if (state == null || !state.established) return "The government cannot issue edicts yet.";
        if (edict == null) return "There is no such edict.";
        if (IsActive(state, edict.id)) return $"{edict.title} is already in force.";
        int wait = Cooldown(state, edict.id, false);
        if (wait > 0) return $"{edict.title} rests: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        if (string.IsNullOrEmpty(headOfState)) return "Only a Head of State can seal an edict: seat a legend there first.";
        if (state.active.Count >= capacity) return $"Every edict slot is in use ({state.active.Count}/{capacity}): more council seats open more.";
        if (!string.IsNullOrEmpty(edict.requiresTechnology) && (researched == null || !researched(edict.requiresTechnology))) return $"Needs {edict.requiresTechnology}.";
        if (edict.cost > 0f && !string.IsNullOrEmpty(edict.costResource) && (affordable == null || !affordable(edict.costResource, edict.cost))) return $"Needs {edict.cost:0} {edict.costResource}.";
        return null;
    }

    /// <summary>How strongly an edict is sealed: stronger when a legend answers for its area (0 = a seat covering it, 1+ = a related seat).</summary>
    public static float Strength(bool answered, int distance, EdictTuning t) => !answered ? 1f : distance <= 0 ? Math.Max(1f, t.answeredStrength) : Math.Max(1f, t.relatedStrength);

    /// <summary>
    /// A Seventh passes: cooldowns count down, edicts in force run down, and those that ran out rest (their cooldown
    /// starts). Returns the edicts that ended.
    /// </summary>
    public static List<string> Tick(EdictState state)
    {
        var ended = new List<string>();
        if (state == null || !state.established) return ended;
        state.sevenths++;
        CountDown(state.stanceCooldowns);
        CountDown(state.edictCooldowns);
        for (int i = state.active.Count - 1; i >= 0; i--)
        {
            var edict = state.active[i];
            if (--edict.remaining > 0) continue;
            state.active.RemoveAt(i);
            ended.Add(edict.id);
            var def = EdictCatalog.Edict(edict.id);
            if (def != null && def.cooldown > 0) state.edictCooldowns[edict.id] = def.cooldown;
        }
        return ended;
    }

    private static void CountDown(Dictionary<string, int> cooldowns)
    {
        foreach (var key in cooldowns.Keys.ToList())
        {
            int left = cooldowns[key] - 1;
            if (left <= 0) cooldowns.Remove(key);
            else cooldowns[key] = left;
        }
    }

    // ===== WHAT THE LAWS ADD UP TO =====

    public static EdictLevers Levers(EdictState state)
    {
        var levers = EdictLevers.Neutral;
        if (state == null || !state.established) return levers;
        foreach (var option in OptionsInForce(state)) levers = levers.Combine(option.levers);
        return levers;
    }

    /// <summary>Every effect the stances in force give.</summary>
    public static List<GameEffect> StanceEffects(EdictState state) =>
        state != null && state.established ? OptionsInForce(state).SelectMany(o => o.effects).ToList() : new List<GameEffect>();

    /// <summary>An edict's effects at the strength it was sealed with.</summary>
    public static List<GameEffect> EdictEffects(ActiveEdict active)
    {
        var def = active != null ? EdictCatalog.Edict(active.id) : null;
        if (def == null) return new List<GameEffect>();
        float strength = active.strength > 0f ? active.strength : 1f;
        return def.effects.Select(e => { var scaled = e; scaled.value = e.value * strength; return scaled; }).ToList();
    }

    // ===== THE COUNCIL'S ACCORD =====

    /// <summary>What each seated legend thinks of the laws in force: a seat covering a favoring area approves, one covering an opposing area objects.</summary>
    public static List<LegendOpinion> Opinions(EdictState state, IEnumerable<Judge> judges)
    {
        var options = OptionsInForce(state).ToList();
        var opinions = new List<LegendOpinion>();
        foreach (var judge in judges ?? Enumerable.Empty<Judge>())
        {
            if (string.IsNullOrEmpty(judge.legend)) continue;
            var areas = new HashSet<string>(judge.areas ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var opinion = new LegendOpinion { legend = judge.legend, seat = judge.seat };
            foreach (var option in options)
            {
                bool likes = option.favoredBy.Any(areas.Contains), dislikes = option.opposedBy.Any(areas.Contains);
                if (likes && !dislikes) opinion.approves++;
                else if (dislikes && !likes) opinion.opposes++;
            }
            opinions.Add(opinion);
        }
        return opinions;
    }

    /// <summary>
    /// How an option sits with the government: +1 when it leans the way the compass already leans, -1 when it leans
    /// against a pillar the government holds firmly (2 or more), 0 otherwise. Compass as <see cref="GovernmentLogic"/>
    /// reports it: waltzRegalia &gt; 0 leans Regalia, chorusAureus &gt; 0 leans Chorus.
    /// </summary>
    public static int Resonance(StanceOption option, int waltzRegalia, int chorusAureus)
    {
        if (option == null || string.IsNullOrEmpty(option.lean)) return 0;
        int coordinate, side;
        switch (option.lean.ToLowerInvariant())
        {
            case "regalia": coordinate = waltzRegalia; side = 1; break;
            case "waltz": coordinate = waltzRegalia; side = -1; break;
            case "chorus": coordinate = chorusAureus; side = 1; break;
            case "aureus": coordinate = chorusAureus; side = -1; break;
            default: return 0;
        }
        int along = coordinate * side;
        return along > 0 ? 1 : along <= -2 ? -1 : 0;
    }

    /// <summary>
    /// The council's accord, -100 to 100: the seated legends' opinions plus the government's own (its compass's
    /// resonance with each leaning law, less one for each Dissonant law), over the laws that take a side. 0 when no
    /// law takes a side.
    /// </summary>
    public static float Accord(EdictState state, IList<LegendOpinion> opinions, int waltzRegalia, int chorusAureus)
    {
        var options = OptionsInForce(state).Where(TakesASide).ToList();
        if (state == null || !state.established || options.Count == 0) return 0f;
        int points = (opinions ?? Array.Empty<LegendOpinion>()).Sum(o => o.Net);
        points += options.Sum(o => Resonance(o, waltzRegalia, chorusAureus) - (o.dissonant ? 1 : 0));
        int judges = (opinions?.Count ?? 0) + 1;
        float accord = 100f * points / (judges * options.Count);
        return Math.Max(-100f, Math.Min(100f, accord));
    }

    public static bool TakesASide(StanceOption option) => option != null && (!string.IsNullOrEmpty(option.lean) || option.favoredBy.Count > 0 || option.opposedBy.Count > 0 || option.dissonant);

    public static Mood MoodOf(float accord, EdictTuning t) => accord >= t.harmonyAt ? Mood.Harmony : accord <= t.discordAt ? Mood.Discord : Mood.Steady;

    public static string MoodName(Mood mood) => mood == Mood.Harmony ? "In harmony" : mood == Mood.Discord ? "In discord" : "Steady";

    /// <summary>What the council's mood gives (harmony: Legend Effectiveness; discord: morale).</summary>
    public static List<GameEffect> MoodEffects(Mood mood, EdictTuning t)
    {
        var effects = new List<GameEffect>();
        if (mood == Mood.Harmony && t.harmonyLegendEffectiveness != 0f) effects.Add(new GameEffect(GameEffectType.DerivedStatBonus, t.harmonyLegendEffectiveness, ModifierType.Percentage, "legendEffectiveness"));
        if (mood == Mood.Discord && t.discordMorale != 0f) effects.Add(new GameEffect(GameEffectType.MoraleModifier, t.discordMorale, ModifierType.Add));
        return effects;
    }

    // ===== STORIES =====

    /// <summary>
    /// A condition's value (GameValues): "edicts" → established, capacity, active, accord, decrees; "stance" →
    /// "&lt;stance&gt;:&lt;option&gt;" 1 while that option is the law; "edict" → "&lt;id&gt;" 1 while in force.
    /// </summary>
    public static float Value(EdictState state, string domain, string target, int capacity, float accord)
    {
        string t = (target ?? string.Empty).Trim().ToLowerInvariant();
        switch (domain)
        {
            case "stance":
                int colon = t.IndexOf(':');
                if (colon <= 0) return 0f;
                var option = InForce(state, EdictCatalog.Stance(t.Substring(0, colon)));
                return state != null && state.established && option != null && string.Equals(option.id, t.Substring(colon + 1), StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
            case "edict":
                return IsActive(state, t) ? 1f : 0f;
            default:
                if (t.Length == 0 || t == "established") return state != null && state.established ? 1f : 0f;
                if (t == "capacity") return capacity;
                if (t == "active") return state?.active.Count ?? 0;
                if (t == "accord") return (float)Math.Round(accord);
                if (t == "decrees") return state?.decrees ?? 0;
                return 0f;
        }
    }
}
