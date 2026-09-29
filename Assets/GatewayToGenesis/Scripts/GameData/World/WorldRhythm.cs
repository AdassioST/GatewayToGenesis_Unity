using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What one Echo does to the creatures (vault: Arcanorian Ecology.md, "The Creatures' Calendar"), after its theme in Cycle.md. Multipliers are 1 for
/// "as E2 has it"; across the four Echoes each averages about 1, so a whole Cycle keeps E2's balance.
/// </summary>
[Serializable]
public class EchoRhythm
{
    [UnityEngine.Tooltip("The Echo's name, for the card and the notices (Echo 1 = Resonance ... 4 = Silence).")]
    public string name;
    [UnityEngine.Tooltip("What the creatures do this Echo, for the card and the notice when it begins.")]
    public string summary;
    [UnityEngine.Header("Lived through (applied at the Echo's end)")]
    [UnityEngine.Tooltip("Births: logistic growth times this.")]
    public float growth = 1f;
    [UnityEngine.Tooltip("Decomposers follow decay, not births: their growth times this instead.")]
    public float detritivoreGrowth = 1f;
    [UnityEngine.Tooltip("How far populations spread into neighbouring Macro Biomes.")]
    public float dispersal = 1f;
    [UnityEngine.Tooltip("How much predators take from their prey.")]
    public float predation = 1f;
    [UnityEngine.Header("While it lasts (strongest in its middle Phase)")]
    [UnityEngine.Tooltip("Den yields and hunts.")]
    public float yields = 1f;
    [UnityEngine.Tooltip("Temper lost to hunts and habitat taken (WorldBehavior).")]
    public float harm = 1f;
    [UnityEngine.Tooltip("Temper gained by living beside a species unhunted (at the Echo's end) and at its Ritual Sevenths.")]
    public float ease = 1f;
    [UnityEngine.Tooltip("Danger a predator's den casts on your land within hungerReach: hungry predators stray toward settlements.")]
    [UnityEngine.Range(0f, 1f)] public float hungerDanger;
    [UnityEngine.Tooltip("How readily resonance plagues break out (WorldPlagues): their chance times this.")]
    public float plagues = 1f;
}

/// <summary>
/// The creatures' calendar (vault: Arcanorian Ecology.md, "The Creatures' Calendar"): the four Echoes, the shape of each Echo's three Phases, and the
/// Ritual Seventh. Every number is a proposal (Canon Gaps).
/// </summary>
[Serializable]
public class RhythmSettings
{
    [UnityEngine.Tooltip("Resonance, Crescendo, Dissonance, Silence (in the calendar's order).")]
    public List<EchoRhythm> echoes = new List<EchoRhythm>();
    [UnityEngine.Tooltip("How strongly each of an Echo's three Phases carries its live effects: it opens, peaks in the middle Phase (Harmonics, Zenith, Ashfall, Repose) and wanes.")]
    public float[] phaseShape = { 0.5f, 1f, 0.5f };
    [UnityEngine.Tooltip("How far (cells) a hungry predator's den reaches toward your land.")]
    public int hungerReach = 3;

    [UnityEngine.Tooltip("A species with a breeding Echo (SpeciesSpec.breedingEcho) has this many times its births in it, and the rest spread over the other three Echoes (so a Cycle keeps E2's balance: 2.2 leaves 0.6 each).")]
    public float breedingPeak = 2.2f;

    [UnityEngine.Header("Ritual Seventh: the world attunes (the Full Moon, the fullest Lunehymn flow)")]
    [UnityEngine.Tooltip("Pure Light beings attune (CreatureTaxonomy.IsPureLightBeing: under 65% Structure, or a Coherence-Binding organ). An attuned population grows by this share times its Pure Light, logistic, where its range holds the Coherence it needs (times the Echo's growth).")]
    [UnityEngine.Range(0f, 1f)] public float surge = 0.08f;
    [UnityEngine.Tooltip("Where its range falls short, the attunement overloads it: it loses this share times its Pure Light.")]
    [UnityEngine.Range(0f, 1f)] public float overload = 0.05f;
    [UnityEngine.Tooltip("Silver water (Lunehymn in the rivers) counts as this much more Coherence at a Ritual Seventh.")]
    [UnityEngine.Range(0f, 1f)] public float lunehymnBoost = 0.15f;
    [UnityEngine.Tooltip("Temper every species gains with each authority that has not hunted it this Phase (times the Echo's ease; up to one step).")]
    [UnityEngine.Range(0f, 1f)] public float attuneEase = 0.03f;
    [UnityEngine.Tooltip("A hunt made on a Ritual Seventh costs this many times the temper.")]
    public float ritualHarm = 2f;
}

/// <summary>What a Ritual Seventh did to one population, for the notices.</summary>
public class AttunementChange
{
    public Population population;
    public bool surged;
}

/// <summary>
/// The creatures' calendar (vault: Arcanorian Ecology.md, "The Creatures' Calendar"), with no scene state (tested in <c>RhythmTests</c>). The world
/// reads the calendar from <see cref="WorldMap.echo"/>, <see cref="WorldMap.echoPhase"/>, <see cref="WorldMap.phaseCount"/>
/// and <see cref="WorldMap.ritualSeventh"/> (set by <c>WorldSystem</c> each Seventh; 0 means no calendar, and then
/// nothing here changes anything). After Cycle.md:
///
/// - Echoes (the seasons): each has an <see cref="EchoRhythm"/>. What a population lived through is applied at the Echo's
///   end (<see cref="WorldEcology.Tick"/>: births, decay feeding the decomposers, spreading, predation); what lasts
///   while it runs (den yields, hunger, behavior) is strongest in its middle Phase.
/// - Silence: hungry predators stray toward settlements, so their dens cast danger on your land nearby.
/// - The Ritual Seventh (the 21st of each Phase), where attunement peaks: Pure Light species surge where their range
///   holds the Coherence they need (silver water helping) and overload where it does not; every species calms a little
///   toward each authority that has let it be this Phase, and a hunt made then wounds its trust twice over.
/// </summary>
public static class WorldRhythm
{
    /// <summary>The rhythm of Echo <paramref name="echo"/> (1-4), or null (no calendar, or none set).</summary>
    public static EchoRhythm Of(WorldGenSettings settings, int echo)
    {
        var echoes = settings?.rhythm?.echoes;
        return echoes != null && echo >= 1 && echo <= echoes.Count ? echoes[echo - 1] : null;
    }

    /// <summary>The Echo before <paramref name="echo"/> (the one just lived when <paramref name="echo"/> begins).</summary>
    public static int Previous(int echo) => echo <= 0 ? 0 : (echo + TimeSystemLogic.EchoesPerCycle - 2) % TimeSystemLogic.EchoesPerCycle + 1;

    /// <summary>How strongly the current Phase carries its Echo's live effects (1 without a calendar).</summary>
    public static float Intensity(WorldMap map, WorldGenSettings settings)
    {
        var shape = settings?.rhythm?.phaseShape;
        if (map == null || map.echo <= 0 || shape == null || shape.Length == 0) return 1f;
        return shape[Math.Max(0, Math.Min(shape.Length - 1, map.echoPhase - 1))];
    }

    // A live multiplier: 1 plus the Echo's difference, as strong as the Phase carries it.
    private static float Live(WorldMap map, WorldGenSettings settings, Func<EchoRhythm, float> pick)
    {
        var r = map != null ? Of(settings, map.echo) : null;
        return r == null ? 1f : 1f + (pick(r) - 1f) * Intensity(map, settings);
    }

    /// <summary>What dens yield and hunts bring now, by the season.</summary>
    public static float Yields(WorldMap map, WorldGenSettings settings) => Math.Max(0f, Live(map, settings, r => r.yields));

    /// <summary>What hunting and taking habitat cost in temper now: the season's harm, and more on a Ritual Seventh.</summary>
    public static float Harm(WorldMap map, WorldGenSettings settings) =>
        Math.Max(0f, Live(map, settings, r => r.harm)) * (map != null && map.ritualSeventh ? Math.Max(1f, settings?.rhythm?.ritualHarm ?? 1f) : 1f);

    /// <summary>Whether a species hunts others (a predator: it has prey, or is a carnivore).</summary>
    public static bool Predator(SpeciesSpec species) =>
        species != null && (species.diet == CreatureDiet.Carnivore || (species.prey != null && species.prey.Count > 0));

    /// <summary>The danger a hungry predator's den casts toward your land now (0 out of season).</summary>
    public static float HungerDanger(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        var r = map != null ? Of(settings, map.echo) : null;
        if (r == null || r.hungerDanger <= 0f || !Predator(WorldEcology.SpeciesAt(settings, site))) return 0f;
        return r.hungerDanger * Intensity(map, settings);
    }

    /// <summary>
    /// How Echo <paramref name="season"/> weighs a species' births (1 with no calendar): its breeding Echo (E10) crowds
    /// them in (<see cref="RhythmSettings.breedingPeak"/> there, the rest shared by the other Echoes); otherwise decomposers
    /// follow decay and the rest follow the Echo's births.
    /// </summary>
    public static float Births(WorldGenSettings settings, SpeciesSpec species, int season)
    {
        var lived = Of(settings, season);
        if (lived == null || species == null) return 1f;
        int breeding = CreatureTaxonomy.BreedingEchoOf(species);
        if (breeding > 0)
        {
            int echoes = TimeSystemLogic.EchoesPerCycle;
            float peak = Math.Max(1f, Math.Min(echoes, settings.rhythm.breedingPeak));
            return season == breeding ? peak : (echoes - peak) / (echoes - 1);
        }
        return Math.Max(0f, species.diet == CreatureDiet.Detritivore ? lived.detritivoreGrowth : lived.growth);
    }

    /// <summary>A species' Pure Light share (the rest of its makeup is Auric Structure).</summary>
    public static float PureLight(SpeciesSpec species) => species == null ? 0f : Math.Max(0f, Math.Min(1f, 1f - species.structure));

    /// <summary>
    /// A Ritual Seventh: attuned populations surge or overload by the Coherence of their range (silver water adding
    /// Lunehymn), and every species calms toward whoever let it be this Phase (<see cref="WorldBehavior.Attune"/>).
    /// </summary>
    public static List<AttunementChange> Attune(WorldMap map, WorldGenSettings settings, int age)
    {
        var changes = new List<AttunementChange>();
        var rhythm = settings?.rhythm;
        var e = settings?.ecology;
        if (map == null || rhythm == null || e == null) return changes;
        foreach (var p in map.Populations.Where(p => p.groups > 0f && p.capacity > 0.0001f))
        {
            var species = settings.Species(p.species);
            float light = PureLight(species);
            if (!CreatureTaxonomy.IsPureLightBeing(species)) continue;
            if (!WorldEcology.Habitat(map, settings, species.id, age).TryGetValue(p.slot, out var cells) || cells.Count == 0) continue;
            float growth = Births(settings, species, map.echo);
            // What its niche finds there (E10), and the Lunehymn of silver water (a Leyline lineage drinks it on any leyline).
            float attunement = cells.Average(i => WorldEcology.Attunement(e, species, map[i]) +
                (map[i].silver || (species.niche == HarmonicNiche.Leyline && WorldEcology.OnLeyline(e, map[i])) ? rhythm.lunehymnBoost : 0f));
            float need = light * e.pureLightCoherence;
            if (attunement >= need)
            {
                float grown = rhythm.surge * light * Math.Max(0f, growth) * p.groups * Math.Max(0f, 1f - p.groups / p.capacity);
                if (grown <= 0.0001f) continue;
                p.groups += grown;
                changes.Add(new AttunementChange { population = p, surged = true });
            }
            else
            {
                p.groups = Math.Max(0f, p.groups - rhythm.overload * light * p.groups);
                changes.Add(new AttunementChange { population = p, surged = false });
            }
        }
        WorldBehavior.Attune(map, settings);
        return changes;
    }

    /// <summary>"Echo of Silence", or null.</summary>
    public static string EchoName(WorldGenSettings settings, int echo)
    {
        var r = Of(settings, echo);
        return r == null || string.IsNullOrEmpty(r.name) ? null : r.name;
    }
}
