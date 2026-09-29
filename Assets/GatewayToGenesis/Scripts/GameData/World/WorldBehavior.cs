using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>How one species has come to feel about one authority (saved): its temper, -1 (harmed) to +1 (befriended).</summary>
[Serializable]
public class BehaviorBond
{
    public string authority;
    public float temper;
    /// <summary>The share of the species' habitat this authority held at the last Echo (land taken since is habitat lost).</summary>
    public float heldShare;
    /// <summary>Hunted by this authority since the last Echo (no easing that Echo).</summary>
    public bool hunted;
    /// <summary>Every hunt this authority has made of the species (the Bestiary's knowledge reads it).</summary>
    public int hunts;
    /// <summary>The Phase of its last hunt (<see cref="WorldMap.phaseCount"/>; -1 never): a Ritual Seventh calms only toward those who let it be that Phase.</summary>
    public int lastHuntPhase = -1;
}

/// <summary>
/// A species' memory (saved): its overall temper, what the lineage has learned from everyone together and what a newcomer
/// meets, and its bond with each authority it has met.
/// </summary>
[Serializable]
public class SpeciesBehavior
{
    public string species;
    public float overall;
    public List<BehaviorBond> bonds = new List<BehaviorBond>();

    public BehaviorBond Bond(string authority) => bonds.FirstOrDefault(b => b.authority == authority);
}

/// <summary>A species' behavior toward the player moving one step (<see cref="WorldBehavior.Tick"/>), for the notices.</summary>
public class BehaviorChange
{
    public SpeciesSpec species;
    public ThreatResponse from, to;
    /// <summary>Toward its nature (gentler) or away from it (harsher).</summary>
    public bool gentler;
}

/// <summary>
/// How the species learn from the way they are treated (vault: Arcanorian Ecology.md, "Behavior: The Memory of a Lineage"). Every number is a proposal (Canon Gaps).
/// </summary>
[Serializable]
public class BehaviorSettings
{
    [UnityEngine.Tooltip("Temper per step along the response ladder: two steps at most either way (temper runs -1 to +1).")]
    [UnityEngine.Range(0.1f, 1f)] public float stepAt = 0.34f;

    [UnityEngine.Header("Harm")]
    [UnityEngine.Tooltip("Temper one hunt costs with the hunter (times the party's reward multiplier). A den hunted every Phase costs three times this an Echo.")]
    [UnityEngine.Range(0f, 1f)] public float huntHarm = 0.06f;
    [UnityEngine.Tooltip("Temper lost per whole habitat taken: an authority that newly holds 10% of a species' habitat in an Echo loses a tenth of this.")]
    [UnityEngine.Range(0f, 2f)] public float habitatHarm = 0.6f;

    [UnityEngine.Header("Easing")]
    [UnityEngine.Tooltip("Temper gained each Echo an authority lives beside a species (holds some of its habitat) without hunting it.")]
    [UnityEngine.Range(0f, 1f)] public float coexist = 0.04f;
    [UnityEngine.Tooltip("Living beside it alone makes it no gentler than this (one step); befriending (rescues, sanctuaries, domestication) goes further.")]
    [UnityEngine.Range(0f, 1f)] public float coexistCap = 0.4f;

    [UnityEngine.Header("Memory")]
    [UnityEngine.Tooltip("Share of every change with one authority that the species' overall temper takes: it spreads the word.")]
    [UnityEngine.Range(0f, 1f)] public float spread = 0.35f;
    [UnityEngine.Tooltip("Scar spectra: share of every temper kept when an Age turns; the rest eases back to the species' nature.")]
    [UnityEngine.Range(0f, 1f)] public float memoryKept = 0.3f;

    [UnityEngine.Header("Consequences")]
    [UnityEngine.Tooltip("Share of a hunt a species brings back when it has learned to flee or hide from the hunter.")]
    [UnityEngine.Range(0f, 1f)] public float warySpoils = 0.6f;
    [UnityEngine.Tooltip("Danger a den casts on its cells (fading over hostileReach) once its species has grown hostile toward you.")]
    [UnityEngine.Range(0f, 1f)] public float hostileDanger = 0.35f;
    public int hostileReach = 1;
}

/// <summary>
/// Behavior, a species' memory (vault: Arcanorian Ecology.md, "Behavior: The Memory of a Lineage"), with no scene state (tested in <c>BehaviorTests</c>). A
/// species' subgroup is its fixed nature (<see cref="CreatureTaxonomy"/>); its behavior is how it acts toward an
/// authority, and it applies to every authority (your civilization, the enclaves), never per den:
///
/// - Temper: -1 to +1 per species and authority (<see cref="BehaviorBond"/>). Hunting and taking its habitat lower it;
///   living beside it without hunting it raises it, up to one step; befriending (<see cref="Befriend"/>: rescues,
///   sanctuaries, domestication, still to come) goes further.
/// - Overall: every change with one authority also moves the species' overall temper by a share (it spreads the word).
///   An authority meets that overall temper on first encounter: identifying a den (you), or holding land in its range.
/// - Response: temper moves the response along AECOR's ladders one step per <see cref="BehaviorSettings.stepAt"/>.
///   Species that flee go from letting you near, to fleeing when threatened, to fleeing on sight; the others from letting
///   you near, to defending themselves, to defending their territory, to attacking on sight.
/// - Scar spectra: at each Age both tempers ease back toward the nature, keeping <see cref="BehaviorSettings.memoryKept"/>.
/// - Consequences, read toward the authority involved: its land presses on a fleeing species harder (it moves away), a
///   species that learned to flee from its hunters yields less to them, and a den grown hostile toward you is dangerous.
/// None of this concerns the Atonalis.
/// </summary>
public static class WorldBehavior
{
    private static readonly ThreatResponse[] FearLadder = { ThreatResponse.Tolerates, ThreatResponse.FleesWhenThreatened, ThreatResponse.FleesOnSight };
    private static readonly ThreatResponse[] HostilityLadder =
        { ThreatResponse.Tolerates, ThreatResponse.DefendsWhenThreatened, ThreatResponse.DefendsTerritory, ThreatResponse.AttacksOnSight };

    /// <summary>The ledger an authority keeps with the creatures: your outposts are you.</summary>
    public static string Key(string authority) => WorldAuthority.IsPlayers(authority) ? WorldAuthority.Player : authority;

    /// <summary>The species' nature: its subgroup's response.</summary>
    public static ThreatResponse Nature(SpeciesSpec species) =>
        CreatureTaxonomy.Profile(species.diet, species.subgroup)?.response ?? ThreatResponse.DefendsWhenThreatened;

    // Species that meet trouble by fleeing or hiding climb the fear ladder; the rest the hostility ladder.
    private static bool Fearful(ThreatResponse r) =>
        r == ThreatResponse.FleesOnSight || r == ThreatResponse.FleesWhenThreatened || r == ThreatResponse.Hides || r == ThreatResponse.Unmoved || r == ThreatResponse.Tolerates;

    private static ThreatResponse[] LadderOf(ThreatResponse nature) => Fearful(nature) ? FearLadder : HostilityLadder;

    // Where a nature stands on its ladder (0 the gentlest).
    private static int Rung(ThreatResponse r)
    {
        switch (r)
        {
            case ThreatResponse.Tolerates: case ThreatResponse.Unmoved: return 0;
            case ThreatResponse.FleesWhenThreatened: case ThreatResponse.Hides: case ThreatResponse.AvoidsConflict: case ThreatResponse.DefendsWhenThreatened: return 1;
            case ThreatResponse.FleesOnSight: case ThreatResponse.DefendsTerritory: case ThreatResponse.Expands: case ThreatResponse.Unpredictable: return 2;
            default: return 3; // attacks on sight, hunts, lures
        }
    }

    /// <summary>Steps a temper moves the response: positive gentler, negative harsher.</summary>
    public static int Steps(BehaviorSettings b, float temper) => (int)Math.Truncate(Clamp(temper, -1f, 1f) / Math.Max(0.01f, b.stepAt));

    /// <summary>The response a species with <paramref name="nature"/> shows at <paramref name="temper"/>: its own nature until the temper moves it a step.</summary>
    public static ThreatResponse Response(BehaviorSettings b, ThreatResponse nature, float temper)
    {
        int steps = Steps(b, temper);
        if (steps == 0) return nature;
        var ladder = LadderOf(nature);
        int from = Math.Min(Rung(nature), ladder.Length - 1), to = Math.Max(0, Math.Min(ladder.Length - 1, from - steps));
        return to == from ? nature : ladder[to];
    }

    /// <summary>Harsher than its nature (further up its ladder).</summary>
    public static bool Harsher(ThreatResponse nature, ThreatResponse now) => now != nature && Rung(now) > Math.Min(Rung(nature), LadderOf(nature).Length - 1);

    // ===== THE LEDGERS =====

    /// <summary>A species' memory, or null when nothing has touched it yet.</summary>
    public static SpeciesBehavior Of(WorldMap map, string species) =>
        map?.Behaviors?.FirstOrDefault(s => string.Equals(s.species, species, StringComparison.OrdinalIgnoreCase));

    private static SpeciesBehavior Ensure(WorldMap map, string species)
    {
        var memory = Of(map, species);
        if (memory == null) map.Behaviors.Add(memory = new SpeciesBehavior { species = species });
        return memory;
    }

    /// <summary>The temper a species shows <paramref name="authority"/>: its bond once met, its overall temper before.</summary>
    public static float Temper(WorldMap map, string species, string authority)
    {
        var memory = Of(map, species);
        if (memory == null) return 0f;
        return memory.Bond(Key(authority))?.temper ?? memory.overall;
    }

    /// <summary>Whether <paramref name="authority"/> has met the species.</summary>
    public static bool Met(WorldMap map, string species, string authority) => Of(map, species)?.Bond(Key(authority)) != null;

    /// <summary>First encounter: the authority meets the species as everyone does (its overall temper).</summary>
    public static BehaviorBond Meet(WorldMap map, string species, string authority, float heldShare = 0f)
    {
        var memory = Ensure(map, species);
        string key = Key(authority);
        var bond = memory.Bond(key);
        if (bond == null) memory.bonds.Add(bond = new BehaviorBond { authority = key, temper = memory.overall, heldShare = heldShare });
        return bond;
    }

    /// <summary>What the species shows <paramref name="authority"/> now.</summary>
    public static ThreatResponse Toward(WorldMap map, WorldGenSettings settings, SpeciesSpec species, string authority) =>
        Response(settings.behavior, Nature(species), Temper(map, species.id, authority));

    /// <summary>What a newcomer meets (the species' overall behavior).</summary>
    public static ThreatResponse Overall(WorldMap map, WorldGenSettings settings, SpeciesSpec species) =>
        Response(settings.behavior, Nature(species), Of(map, species.id)?.overall ?? 0f);

    // A change in one bond, and the word it spreads. Gains stop at `cap` (living beside it), harms at -1.
    private static void Change(SpeciesBehavior memory, BehaviorBond bond, float delta, float cap, BehaviorSettings b)
    {
        bond.temper = Move(bond.temper, delta, cap);
        memory.overall = Move(memory.overall, delta * b.spread, cap);
    }

    private static float Move(float temper, float delta, float cap) =>
        delta >= 0f ? (temper >= cap ? temper : Math.Min(cap, temper + delta)) : Math.Max(-1f, temper + delta);

    /// <summary>A hunt by <paramref name="authority"/> (<see cref="WorldEcology.Hunt"/>): the species remembers.</summary>
    public static void Hunted(WorldMap map, WorldGenSettings settings, string species, string authority, float multiplier)
    {
        var b = settings?.behavior;
        if (b == null || map == null || string.IsNullOrEmpty(species)) return;
        var bond = Meet(map, species, authority);
        bond.hunted = true;
        bond.hunts++;
        bond.lastHuntPhase = map.phaseCount;
        // The season weighs on it (WorldRhythm): Dissonance fractures trust, and a hunt on a Ritual Seventh wounds it twice.
        // A species your suzerain keepers keep is culled with care (WorldEnclaveEcology).
        float harm = b.huntHarm * Math.Max(0f, multiplier) * WorldRhythm.Harm(map, settings) * WorldEnclaveEcology.HuntHarmScale(map, settings, species, authority);
        Change(Of(map, species), bond, -harm, 1f, b);
    }

    /// <summary>
    /// Kindness beyond living beside it (event rescues, sanctuaries; the Enclaves' keeping and taming, <see cref="WorldEnclaveEcology"/>):
    /// raises the bond by <paramref name="amount"/>, up to +1.
    /// </summary>
    public static void Befriend(WorldMap map, WorldGenSettings settings, string species, string authority, float amount)
    {
        var b = settings?.behavior;
        if (b == null || map == null || string.IsNullOrEmpty(species) || amount <= 0f) return;
        var bond = Meet(map, species, authority);
        Change(Of(map, species), bond, amount, 1f, b);
    }

    // ===== THE ECHO =====

    /// <summary>
    /// The species' Echo (after <see cref="WorldEcology.Tick"/>): each authority holding land in a species' habitat meets
    /// it; habitat newly taken costs temper; living beside it unhunted eases it. You meet a species when you identify one
    /// of its dens. <paramref name="season"/> is the Echo just lived (1-4; 0 none), which weighs the harm and the easing
    /// (<see cref="WorldRhythm"/>). Returns the steps its behavior toward you moved.
    /// </summary>
    public static List<BehaviorChange> Tick(WorldMap map, WorldGenSettings settings, int age, int season = 0)
    {
        var changes = new List<BehaviorChange>();
        var b = settings?.behavior;
        if (map == null || b == null || settings.species == null) return changes;
        // The Echo just lived (WorldRhythm): how hard habitat taken weighed on them and how much living beside them eased them.
        var lived = WorldRhythm.Of(settings, season);
        float harm = Math.Max(0f, lived?.harm ?? 1f), ease = Math.Max(0f, lived?.ease ?? 1f);
        // Resolve identified dens once, rather than scanning the full site catalog for every ecotype.
        var identified = new HashSet<SpeciesSpec>(map.ResourceSites.Where(s => WorldResources.Identified(map, s))
            .Select(s => WorldEcology.SpeciesAt(settings, s)).Where(s => s != null));
        foreach (var species in settings.species)
        {
            if (species == null || string.IsNullOrEmpty(species.id)) continue;
            var before = Met(map, species.id, WorldAuthority.Player) ? Toward(map, settings, species, WorldAuthority.Player) : (ThreatResponse?)null;
            var slots = map.Populations.Where(p => p.groups > 0f && string.Equals(p.species, species.id, StringComparison.OrdinalIgnoreCase)).Select(p => p.slot).ToList();
            var held = new Dictionary<string, int>();
            int cells = 0;
            if (slots.Count > 0)
            {
                var habitat = WorldEcology.Habitat(map, settings, species.id, age);
                foreach (int slot in slots)
                {
                    if (!habitat.TryGetValue(slot, out var list)) continue;
                    cells += list.Count;
                    foreach (int i in list)
                        if (WorldEcology.Held(map[i])) { string key = Key(map[i].authorityId); held[key] = (held.TryGetValue(key, out int n) ? n : 0) + 1; }
                }
            }
            // First encounters: whoever lives in its range, and you once you have identified one of its dens.
            foreach (var pair in held) Meet(map, species.id, pair.Key, (float)pair.Value / Math.Max(1, cells));
            if (identified.Contains(species))
                Meet(map, species.id, WorldAuthority.Player, held.TryGetValue(WorldAuthority.Player, out int mine) ? (float)mine / Math.Max(1, cells) : 0f);
            var memory = Of(map, species.id);
            if (memory != null)
                foreach (var bond in memory.bonds)
                {
                    float share = held.TryGetValue(bond.authority, out int n) ? (float)n / Math.Max(1, cells) : 0f;
                    if (share > bond.heldShare) Change(memory, bond, -(share - bond.heldShare) * b.habitatHarm * harm, 1f, b);
                    if (share > 0f && !bond.hunted) Change(memory, bond, b.coexist * ease, b.coexistCap, b);
                    bond.heldShare = share;
                    bond.hunted = false;
                }
            if (!Met(map, species.id, WorldAuthority.Player)) continue;
            var after = Toward(map, settings, species, WorldAuthority.Player);
            if (before.HasValue && before.Value != after)
                changes.Add(new BehaviorChange { species = species, from = before.Value, to = after, gentler = Rung(after) < Rung(before.Value) });
        }
        return changes;
    }

    /// <summary>
    /// A Ritual Seventh (<see cref="WorldRhythm.Attune"/>): as the world attunes, every species calms a little toward each
    /// authority it has met that has not hunted it this Phase (<see cref="RhythmSettings.attuneEase"/> times the season's
    /// ease, up to what living beside it can reach).
    /// </summary>
    public static void Attune(WorldMap map, WorldGenSettings settings)
    {
        var b = settings?.behavior;
        var rhythm = settings?.rhythm;
        if (map == null || b == null || rhythm == null || rhythm.attuneEase <= 0f) return;
        float ease = rhythm.attuneEase * Math.Max(0f, WorldRhythm.Of(settings, map.echo)?.ease ?? 1f);
        foreach (var memory in map.Behaviors)
            foreach (var bond in memory.bonds)
                if (bond.lastHuntPhase != map.phaseCount) Change(memory, bond, ease, b.coexistCap, b);
    }

    /// <summary>Scar spectra: at an Age's turn every temper eases back toward the nature, keeping <see cref="BehaviorSettings.memoryKept"/>.</summary>
    public static void AgePassed(WorldMap map, WorldGenSettings settings) => AgePassed(map, settings, null);

    /// <summary>
    /// The same, with a share kept for each species (<paramref name="keptOf"/>; null: the usual): a lineage scarred by the
    /// aftermath keeps more of its memory (<see cref="WorldAftermath"/>).
    /// </summary>
    public static void AgePassed(WorldMap map, WorldGenSettings settings, Func<string, float?> keptOf)
    {
        float usual = settings?.behavior?.memoryKept ?? 1f;
        foreach (var memory in map?.Behaviors ?? new List<SpeciesBehavior>())
        {
            float kept = keptOf?.Invoke(memory.species) ?? usual;
            memory.overall *= kept;
            foreach (var bond in memory.bonds) bond.temper *= kept;
        }
    }

    // ===== CONSEQUENCES =====

    /// <summary>The share of a hunt a den gives <paramref name="authority"/>: less once its species has learned to flee or hide from it.</summary>
    public static float HuntShare(WorldMap map, WorldGenSettings settings, ResourceSite site, string authority)
    {
        var species = WorldEcology.SpeciesAt(settings, site);
        if (species == null || settings.behavior == null) return 1f;
        var nature = Nature(species);
        var now = Toward(map, settings, species, authority);
        return Fearful(nature) && Harsher(nature, now) ? settings.behavior.warySpoils : 1f;
    }

    /// <summary>The danger a den casts once its species has grown hostile toward you (0 otherwise).</summary>
    public static float DenDanger(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        var species = WorldEcology.SpeciesAt(settings, site);
        if (species == null || settings.behavior == null) return 0f;
        var nature = Nature(species);
        if (Fearful(nature)) return 0f;
        var now = Toward(map, settings, species, WorldAuthority.Player);
        return Harsher(nature, now) && Rung(now) >= 2 ? settings.behavior.hostileDanger : 0f;
    }

    /// <summary>The responses a species shows, by authority, for <see cref="WorldEcology.Capacity"/> (newcomers get the overall one).</summary>
    public static Func<string, ThreatResponse> Responses(WorldMap map, WorldGenSettings settings, SpeciesSpec species)
    {
        var memory = Of(map, species.id);
        var nature = Nature(species);
        if (memory == null || settings.behavior == null) return _ => nature;
        var overall = Response(settings.behavior, nature, memory.overall);
        var bonds = memory.bonds.ToDictionary(bond => bond.authority, bond => Response(settings.behavior, nature, bond.temper));
        return authority => authority != null && bonds.TryGetValue(authority, out var r) ? r : overall;
    }

    /// <summary>"flees on sight", "lets you come near", for the card and the notices.</summary>
    public static string Words(ThreatResponse r)
    {
        switch (r)
        {
            case ThreatResponse.FleesOnSight: return "flees on sight";
            case ThreatResponse.FleesWhenThreatened: return "flees when threatened";
            case ThreatResponse.Hides: return "hides when threatened";
            case ThreatResponse.Unmoved: return "is unmoved by almost anything";
            case ThreatResponse.AvoidsConflict: return "avoids a fight";
            case ThreatResponse.DefendsWhenThreatened: return "defends itself when threatened";
            case ThreatResponse.DefendsTerritory: return "defends its territory";
            case ThreatResponse.Expands: return "pushes its colony outward";
            case ThreatResponse.Unpredictable: return "strikes without warning";
            case ThreatResponse.AttacksOnSight: return "attacks on sight";
            case ThreatResponse.Hunts: return "hunts";
            case ThreatResponse.HuntsLoudly: return "hunts, screeching";
            case ThreatResponse.Lures: return "lures its prey";
            default: return "lets you come near";
        }
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
