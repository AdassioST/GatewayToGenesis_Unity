using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps the bestiary's memory (vault: Arcanorian Ecology.md, "Knowledge of Creatures"): what the people have identified, watched and hunted
/// (<see cref="SpeciesLore"/>), saved whole (<see cref="_state"/> in GameSnapshot.Schema). It remembers every identified
/// species whenever the world changes, counts an Echo of watching at each Echo, and says so when a species' ways become
/// known. Numbers in <see cref="SpeciesLoreSettings"/> (Resources/World/SpeciesLore). Created by <see cref="GenesisLoop"/>.
/// </summary>
public class SpeciesLoreKeeper : SingletonBehaviour<SpeciesLoreKeeper>
{
    private const LogChannel Log = LogChannel.World;

    // Saved (GameSnapshot.Schema).
    private SpeciesLoreState _state = new SpeciesLoreState();

    private readonly SpeciesLoreTuning _defaults = new SpeciesLoreTuning();
    private SpeciesLoreSettings _settings;
    private TimeSystemLogic _time;
    private WorldSystem _world;

    public SpeciesLoreState State => _state;
    public SpeciesLoreTuning Tuning => _settings != null && _settings.tuning != null ? _settings.tuning : _defaults;

    /// <summary>What the Bestiary, GameValues and the HUD may read; null with no keeper in the scene (knowledge then reads the map alone).</summary>
    public static LoreView View => Instance != null ? new LoreView { state = Instance._state, tuning = Instance.Tuning, understands = Understands } : null;

    /// <summary>A researched technology carries <see cref="SpeciesLore.StudiesUnlock"/>, or you hold an Auric Enclave's Suzerainty. Read from the technologies and the map themselves, so a load needs nothing.</summary>
    public static bool Understands
    {
        get
        {
            // An Auric Enclave you hold as suzerain lends its scholars (WorldEnclaveEcology, E7).
            var world = WorldSystem.Instance;
            if (world != null && WorldEnclaveEcology.AuricUnderstanding(world.Map)) return true;
            var units = GameUnitsLogic.Instance;
            if (units == null) return false;
            return GameCatalog.Technologies.All.Any(t => t != null && t.gameUnit != null && OpensStudies(t) && units.IsTechnologyUnlocked(t.gameUnit.name));
        }
    }

    /// <summary>The technology carries the <see cref="SpeciesLore.StudiesUnlock"/> Special unlockable.</summary>
    public static bool OpensStudies(TechnologyData tech) =>
        tech != null && tech.techUnlockables != null && tech.techUnlockables.Any(u => u != null && u.unlockableType == TechUnlockableType.Special && u.name == SpeciesLore.StudiesUnlock);

    // ===== LIFETIME =====

    protected override void OnSingletonAwake()
    {
        _settings = Resources.Load<SpeciesLoreSettings>("World/SpeciesLore");
        if (_settings == null) GameLog.Warning("No Resources/World/SpeciesLore: the bestiary's memory uses its default tuning.", Log);
    }

    protected override void OnSingletonDestroy()
    {
        if (_time != null) _time.OnEchoChange -= OnEcho;
        if (_world != null) _world.Changed -= OnWorldChanged;
        GameTechnologySlot.Researched -= OnResearched;
    }

    private void Start()
    {
        _world = WorldSystem.Instance;
        if (_world != null) _world.Changed += OnWorldChanged;
        GameTechnologySlot.Researched += OnResearched;
        TimeSystemLogic.WhenReady(this, time =>
        {
            _time = time;
            _time.OnEchoChange += OnEcho;
        });
    }

    // Surveys and hunts change the world: remember what was identified before a bloom can fade with it.
    private void OnWorldChanged()
    {
        if (SaveSession.Restoring || _world == null || _world.Map == null) return;
        Announce(SpeciesLore.Refresh(_state, _world.Map, _world.Settings.generation, Tuning));
        WeighHunts();
    }

    private void OnEcho(int echo)
    {
        if (SaveSession.Restoring || _world == null || _world.Map == null) return;
        var gen = _world.Settings.generation;
        var watched = SpeciesLore.WatchedNow(_world.Map, gen, Tuning);
        Announce(SpeciesLore.Watch(_state, _world.Map, gen, Tuning));
        // Watching tests the guesses it can bear on (SpeciesHypotheses): the Echo's breeding, the niche.
        foreach (string id in watched)
        {
            var record = _state.Of(id);
            if (record == null || !record.identified) continue;
            if (echo >= 1 && echo <= TimeSystemLogic.EchoesPerCycle) record.echoMask |= 1 << (echo - 1);
            Weigh(record, Evidence.Watch, echo);
        }
    }

    private void Announce(System.Collections.Generic.List<string> observed)
    {
        foreach (string id in observed)
        {
            var species = _world.Settings.generation.Species(id);
            if (species == null) continue;
            string name = species.name ?? species.id;
            NotificationFeed.Push($"The ways of the {name}", $"Your people have watched the {name} long enough to know how it treats them and whether its numbers grow or fall. The Bestiary keeps what they learned.",
                NotificationFeed.Topic.Discovery, BestiaryWindow.Toggle, "species-observed:" + id);
            GameLog.Event($"Species observed: {id}", Log);
        }
        Pay();
    }

    // ===== HYPOTHESES (SpeciesHypotheses) =====

    /// <summary>A guess was made, tested or answered (the Bestiary redraws).</summary>
    public static event System.Action HypothesesChanged;

    /// <summary>
    /// The player guesses an answer to one of the Bestiary's questions about an identified species (the options come from
    /// <see cref="SpeciesHypotheses.Options"/>). False when refused (answered, ruled out, not an option, not identified).
    /// </summary>
    public bool Guess(string speciesId, HypothesisQuestion question, string key)
    {
        var record = _state.Of(speciesId);
        var species = _world != null ? _world.Settings.generation.Species(speciesId) : null;
        if (record == null || !record.identified || species == null || !SpeciesHypotheses.Questions(species).Contains(question)) return false;
        if (!SpeciesHypotheses.Guess(record, question, key, OptionsFor(species, question))) return false;
        GameLog.Event($"Hypothesis: {speciesId} {question} = {key}", Log);
        HypothesesChanged?.Invoke();
        return true;
    }

    /// <summary>The answers the player may guess for a question about a species (prey: the species identified so far).</summary>
    public System.Collections.Generic.List<HypothesisOption> OptionsFor(SpeciesSpec species, HypothesisQuestion question)
    {
        var gen = _world != null ? _world.Settings.generation : null;
        var identified = gen == null ? new System.Collections.Generic.List<SpeciesSpec>()
            : _state.species.Where(r => r != null && r.identified).Select(r => gen.Species(r.species)).Where(s => s != null).ToList();
        return SpeciesHypotheses.Options(question, species, identified, e => gen != null ? WorldRhythm.EchoName(gen, e) : null);
    }

    // Hunts of each species since last weighed test its prey guess, one hunt at a time.
    private void WeighHunts()
    {
        foreach (var record in _state.species.Where(r => r != null && r.identified))
        {
            int hunts = SpeciesLore.HuntsByYou(_world.Map, record.species);
            while (record.huntsWeighed < hunts)
            {
                record.huntsWeighed++;
                Weigh(record, Evidence.Hunt, 0);
            }
        }
    }

    // Test every open guess on the species against one piece of evidence; say what came of it; pay insight; a species
    // whose every question is answered is understood.
    private void Weigh(SpeciesRecord record, Evidence evidence, int echo)
    {
        var species = _world.Settings.generation.Species(record.species);
        if (species == null || record.hypotheses == null) return;
        string name = species.name ?? species.id;
        bool any = false;
        foreach (var h in record.hypotheses.Where(h => h != null && !h.confirmed && !string.IsNullOrEmpty(h.guess)).ToList())
        {
            var verdict = SpeciesHypotheses.Test(h, species, evidence, echo, record.echoMask);
            if (verdict == Verdict.Untested) continue;
            string key = h.guess;
            string label = OptionsFor(species, h.question).FirstOrDefault(o => o.key == key).label ?? key;
            SpeciesHypotheses.Apply(h, verdict);
            any = true;
            string report = SpeciesHypotheses.Report(h.question, key, label, verdict, name);
            GameLog.Event($"Hypothesis {verdict}: {record.species} {h.question} = {key}", Log);
            if (verdict == Verdict.Confirmed && h.insight && Tuning.insightEra > 0) AgeProgression.Award(Tuning.insightEra, $"Insight into the {name}");
            NotificationFeed.Push(verdict == Verdict.Confirmed ? $"The {name}: a guess held" : $"The {name}: a guess failed",
                report + (verdict == Verdict.Confirmed && h.insight ? " Right the first time: insight." : string.Empty),
                NotificationFeed.Topic.Discovery, BestiaryWindow.Toggle, $"hypothesis:{record.species}:{h.question}:{key}");
        }
        if (!any) return;
        if (!record.deduced && SpeciesHypotheses.Deduced(record, species))
        {
            record.deduced = true;
            NotificationFeed.Push($"The {name}, understood", $"Your people worked out everything the Bestiary asked of the {name} by watching and hunting it: they understand it without the research.",
                NotificationFeed.Topic.Discovery, BestiaryWindow.Toggle, "species-deduced:" + record.species);
            Pay();
        }
        HypothesesChanged?.Invoke();
    }

    // A technology can bring understanding (Creature Studies): its rewards are due at once.
    private void OnResearched(GameTechnologySlot _)
    {
        if (!SaveSession.Restoring) Pay();
    }

    // Discovery rewards (E10): each level of knowledge of each species pays once, in Era Score and resources.
    private void Pay()
    {
        if (_world == null || _world.Map == null || AgeProgression.Instance == null || AgeProgression.Instance.Current == null) return;
        var map = _world.Map;
        var gen = _world.Settings.generation;
        var view = View;
        foreach (var pay in SpeciesLore.Rewards(_state, gen, Tuning, id => SpeciesKnowledge.LevelOf(map, gen, id, view)))
        {
            string name = pay.species.name ?? pay.species.id;
            string reason = pay.level == SpeciesLevel.Identified ? $"Identified the {name}" : pay.level == SpeciesLevel.Observed ? $"Learned the ways of the {name}"
                : pay.level == SpeciesLevel.Understood ? $"Understood the {name}" : $"Mastered the {name}";
            if (pay.eraScore > 0) AgeProgression.Award(pay.eraScore, reason);
            if (pay.species.unmerged && pay.level == SpeciesLevel.Identified)
                NotificationFeed.Push("Light that never merged",
                    "Your explorers have found an Elemental Sprite. Unlike the water, forest and other sprites, it has never borrowed a body from its surroundings. Only the extraordinary Coherence of sacred ground or converging leylines lets such beings endure. The Bestiary records this discovery.",
                    NotificationFeed.Topic.Discovery, BestiaryWindow.Toggle, "unmerged-discovery:" + pay.species.id);
            foreach (var r in pay.resources) GameUnitsLogic.Instance?.ChangeResourceFromName(r.resource, r.amount, false);
            if (pay.resources.Count > 0) GameLog.Event($"{reason}: {string.Join(", ", pay.resources.Select(r => $"+{r.amount:0.#} {r.resource}"))}", Log);
        }
    }
}
