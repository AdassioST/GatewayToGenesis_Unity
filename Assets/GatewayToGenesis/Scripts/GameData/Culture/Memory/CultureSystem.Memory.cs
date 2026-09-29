using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Shared wounds, memorial places and inheritance (T03; rules in <see cref="MemoryRules"/>, saved in
/// <see cref="CultureExtensionState.memory"/>). "The bakery remembers the famine, and the garden remembers the people who
/// never came home."
///
/// - Causes: an Age Crisis lived through, a settlement fallen, its ruins reclaimed, a Legend lost to Dissonance. Each is
///   learned once, from the event as it happens (AgeProgression.AgePassed, LegendProgress.Lost, WorldSystem.SettlementFell
///   and RuinReclaimed) or, for a save made before, read from the world's own records and marked as learned later.
/// - Dedications: the people dedicate an existing dish, landmark, rite or holiday to a cause (a link by id: a renamed dish
///   is the same dish). Keeping that practice (the dish cooked, the rite held, the holiday kept: T01's occurrences) keeps
///   the memory; so does a quiet remembrance, which asks for no feast and no high morale.
/// - Recovery: a memory kept within the last Phase supports morale ("Culture: Remembrance"), capped over all wounds. The
///   loss itself gives nothing; a lapsed practice gives nothing.
/// - Inheritance: at each Age passage the causes, dedications and practices are kept as a lineage and the dedications go
///   on, awaiting the new Age's recognition. A Legend's loss is linked as a future World Truth, never canonized here.
/// - Memorial ground: a memorial with a place (a ruin, a dedicated landmark's settlement) shows the blooms that actually
///   live there by their own needs. Nothing is fed and no bonus is given (first release).
/// </summary>
public partial class CultureSystem
{
    public const string RemembranceSource = "Culture: Remembrance";

    private readonly MemoryTuning _memoryTuning = new MemoryTuning();
    private string _memoryKey;
    private AgeProgression _memoryAges;
    private LegendProgress _memoryLegends;
    private WorldSystem _memoryWorld;

    public MemoryTuning MemoryTuning => _memoryTuning;

    /// <summary>A cause was remembered for the first time (witnessed or read from older records).</summary>
    public event Action<MemoryEvidence> CauseRemembered;

    private MemoryState Memory
    {
        get
        {
            if (_state.extensions == null) _state.extensions = new CultureExtensionState();
            return _state.extensions.memory ?? (_state.extensions.memory = new MemoryState());
        }
    }

    // ===== READS (snapshots: callers never hold the live lists) =====

    public IReadOnlyList<MemoryEvidence> MemoryCauses() => Memory.evidence.Where(e => e != null).Select(e => e.Copy()).ToList();
    public IReadOnlyList<MemorialDedication> MemorialDedications() => Memory.dedications.Where(d => d != null).Select(d => d.Copy()).ToList();
    public IReadOnlyList<MemoryLineage> MemoryLineages() => Memory.lineage.Where(l => l != null).ToList();
    public MemoryEvidence RememberedCause(string id) => Memory.Evidence(id)?.Copy();
    public MemoryPractice MemoryPracticeOf(string evidenceId) => Memory.PracticeOf(evidenceId)?.Copy();
    public IReadOnlyList<MemorialDedication> DedicationsOf(string evidenceId) => Memory.DedicationsOf(evidenceId).Select(d => d.Copy()).ToList();
    /// <summary>The active dedication carried by <paramref name="target"/> (a dish, landmark, rite or holiday), or null.</summary>
    public MemorialDedication DedicationThrough(CultureEntityRef target) => MemoryRules.ActiveFor(Memory, target)?.Copy();
    /// <summary>Causes kept within the window now, and the morale they support.</summary>
    public IReadOnlyList<string> KeptMemories => MemoryRules.Kept(Memory, _state.sevenths, _memoryTuning);
    public int RemembranceMorale => _state.founded ? MemoryRules.Recovery(Memory, _state.sevenths, _memoryTuning) : 0;
    /// <summary>The memorial suggestions the vault names for <paramref name="evidenceId"/>.</summary>
    public IEnumerable<MemorialSuggestion> SuggestionsFor(string evidenceId) => MemorialSuggestion.All.Where(s => s.Fits(Memory.Evidence(evidenceId)));

    // ===== WHAT CAN CARRY A MEMORY =====

    /// <summary>Whether the dish, landmark, rite or holiday <paramref name="target"/> exists now.</summary>
    public bool MemoryTargetExists(CultureEntityRef target)
    {
        if (target == null || !target.IsKnown) return false;
        switch (target.kind)
        {
            case CultureEntityKind.Recipe:
                var recipe = Life.Recipe(target.id);
                return recipe != null && (string.IsNullOrEmpty(recipe.technology) || Researched(recipe.technology));
            case CultureEntityKind.Activity:
                var activity = Life.Activity(target.id);
                return activity != null && (string.IsNullOrEmpty(activity.technology) || Researched(activity.technology));
            case CultureEntityKind.Holiday:
                return _state.holidays.Any(h => h != null && string.Equals(h.name, target.id, StringComparison.OrdinalIgnoreCase));
            case CultureEntityKind.Landmark:
                var landmark = LandmarkOf(target.id);
                if (landmark == null) return false;
                var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
                return map == null || map.Settlements.Any(s => s != null && s.id == landmark.settlement);
            default:
                return false;
        }
    }

    // Why a dedicated practice sleeps.
    private string WhyGone(CultureEntityRef target)
    {
        if (target != null && target.kind == CultureEntityKind.Landmark && LandmarkOf(target.id) != null) return $"{MemoryTargetLabel(target)} stood in a settlement that fell.";
        return $"{MemoryTargetLabel(target)} is no longer kept.";
    }

    /// <summary>What <paramref name="target"/> is called now (a renamed dish by its new name), else the name it was dedicated under.</summary>
    public string MemoryTargetLabel(CultureEntityRef target)
    {
        if (target == null) return "unknown";
        switch (target.kind)
        {
            case CultureEntityKind.Recipe: return Life.Recipe(target.id)?.dish ?? target.Display;
            case CultureEntityKind.Activity: return Life.Activity(target.id)?.name ?? target.Display;
            case CultureEntityKind.Landmark: return LandmarkOf(target.id)?.name ?? target.Display;
            default: return target.Display;
        }
    }

    private Landmark LandmarkOf(string id) => _state.landmarks.FirstOrDefault(l => l != null && string.Equals(CultureIds.Landmark(l.settlement, l.spec), id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The existing dishes, landmarks, rites and holidays a memory could be dedicated through.</summary>
    public List<CultureEntityRef> DedicationCandidates()
    {
        var list = new List<CultureEntityRef>();
        foreach (var r in Life.recipes.Where(r => r != null && !r.InCellar)) list.Add(CultureEntityRef.Of(CultureEntityKind.Recipe, r.id, r.dish));
        foreach (var a in Life.activities.Where(a => a != null)) list.Add(CultureEntityRef.Of(CultureEntityKind.Activity, a.id, a.name));
        foreach (var h in _state.holidays.Where(h => h != null)) list.Add(CultureEntityRef.Of(CultureEntityKind.Holiday, h.name, h.name));
        foreach (var l in _state.landmarks.Where(l => l != null)) list.Add(CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(l.settlement, l.spec), l.name));
        return list.Where(MemoryTargetExists).ToList();
    }

    // ===== COMMANDS =====

    /// <summary>Why <paramref name="target"/> cannot be dedicated to <paramref name="evidenceId"/> now, or null.</summary>
    public string WhyNotDedicate(string evidenceId, CultureEntityRef target)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        return MemoryRules.WhyNotDedicate(Memory, evidenceId, target, MemoryTargetExists(target), _memoryTuning);
    }

    /// <summary>Dedicate an existing dish, landmark, rite or holiday to a remembered cause (a lasting link; it makes nothing new).</summary>
    public CultureCommandResult Dedicate(string evidenceId, CultureEntityRef target)
    {
        string why = WhyNotDedicate(evidenceId, target);
        if (why != null) return CultureCommandResult.Fail(why);
        var label = CultureEntityRef.Of(target.kind, target.id, MemoryTargetLabel(target));
        var d = MemoryRules.Dedicate(Memory, evidenceId, label, true, Stamp(), _memoryTuning);
        if (d == null) return CultureCommandResult.Fail("It could not be dedicated.");
        var evidence = Memory.Evidence(evidenceId);
        var result = CultureCommandResult.Ok($"{label.Display} now remembers {evidence.title}.");
        Remember("dedication", $"{label.Display} remembers {evidence.title}", $"{Capital(PeopleWord)} dedicated {label.Display} to {evidence.title}.");
        GameLog.Event($"Memorial: {label.Display} dedicated to {evidence.title} ({d.id}).", Log);
        EmitMemorial($"dedicated:{d.id}", label, evidence, $"{label.Display} was dedicated to {evidence.title}", result);
        NotificationFeed.Push($"{label.Display} remembers", $"{Capital(PeopleWord)} dedicated {label.Display} to {evidence.title}. Keeping it keeps the memory.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:dedication:" + d.id);
        RaiseChanged();
        return result;
    }

    /// <summary>Release a dedication: its record stays in the history; it no longer carries the memory.</summary>
    public CultureCommandResult ReleaseDedication(string dedicationId)
    {
        var d = Memory.Dedication(dedicationId);
        if (d == null) return CultureCommandResult.Fail("No such dedication.");
        if (!MemoryRules.Release(Memory, dedicationId)) return CultureCommandResult.Fail("It was released already.");
        GameLog.Event($"Memorial: {d.target.Display} released from {Memory.Evidence(d.evidence)?.title}.", Log);
        ApplyRemembrance();
        RaiseChanged();
        return CultureCommandResult.Ok($"{MemoryTargetLabel(d.target)} no longer carries the memory.");
    }

    /// <summary>Why a quiet remembrance of <paramref name="evidenceId"/> cannot be kept now, or null (it needs no feast, Unity or morale).</summary>
    public string WhyNotRemembrance(string evidenceId) => MemoryRules.WhyNotQuiet(Memory, evidenceId, _state.founded, _state.sevenths, _memoryTuning);

    /// <summary>Keep a quiet remembrance: the names spoken, nothing spent. It keeps the memory for this Seventh.</summary>
    public CultureCommandResult KeepRemembrance(string evidenceId)
    {
        string why = WhyNotRemembrance(evidenceId);
        if (why != null) return CultureCommandResult.Fail(why);
        var evidence = Memory.Evidence(evidenceId);
        MemoryRules.Keep(Memory, evidenceId, _state.sevenths, "a quiet remembrance", quiet: true);
        var result = CultureCommandResult.Ok($"{Capital(PeopleWord)} remember {evidence.title}.");
        EmitMemorial($"quiet:{evidenceId}:{_state.sevenths}", CultureEntityRef.Of(CultureEntityKind.Observance, "quiet-remembrance", "A quiet remembrance"), evidence,
            $"A quiet remembrance of {evidence.title}", result);
        GameLog.Event($"Memorial: a quiet remembrance of {evidence.title}.", Log);
        ApplyRemembrance();
        RaiseChanged();
        return result;
    }

    /// <summary>Why an inherited dedication cannot be recognized as this Age's own now, or null.</summary>
    public string WhyNotRecognize(string dedicationId) => MemoryRules.WhyNotRecognize(Memory.Dedication(dedicationId), CurrentAgeId);

    /// <summary>Recognize an inherited dedication as this Age's own inheritance (a record: nothing is canonized).</summary>
    public CultureCommandResult RecognizeInheritance(string dedicationId)
    {
        string why = WhyNotRecognize(dedicationId);
        if (why != null) return CultureCommandResult.Fail(why);
        var d = Memory.Dedication(dedicationId);
        MemoryRules.Recognize(d, CurrentAgeId);
        var evidence = Memory.Evidence(d.evidence);
        var result = CultureCommandResult.Ok($"{MemoryTargetLabel(d.target)} is recognized as this Age's inheritance.");
        if (_state.founded)
        {
            var o = new CulturalOccurrence
            {
                key = $"recognized:{d.id}:{CurrentAgeId}", kind = CulturalOccurrenceKind.Recognition, subject = CultureEntityRef.Of(CultureEntityKind.Dedication, d.id, MemoryTargetLabel(d.target)),
                evidence = evidence?.Ref ?? new CultureEntityRef(), quantity = 1f, unit = CultureQuantityUnit.Occasions, text = $"{MemoryTargetLabel(d.target)}, remembering {evidence?.title}, carried into a new Age",
            };
            if (Record(o)) result.occurrences.Add(o.key);
        }
        Remember("inheritance", $"{MemoryTargetLabel(d.target)} carried on", $"{Capital(PeopleWord)} still keep {MemoryTargetLabel(d.target)} for {evidence?.title}, as those before them did.");
        RaiseChanged();
        return result;
    }

    // ===== THE GROUND =====

    /// <summary>
    /// Where the memory of <paramref name="evidenceId"/> has a place (a dedicated landmark's settlement first, else where it
    /// happened), and the blooms that actually live there. Null when it has no place.
    /// </summary>
    public MemorialGround MemorialGroundOf(string evidenceId)
    {
        var world = WorldSystem.Instance;
        var map = world != null ? world.Map : null;
        var evidence = Memory.Evidence(evidenceId);
        if (map == null || evidence == null) return null;
        int cell = -1;
        string place = null;
        foreach (var d in Memory.DedicationsOf(evidenceId).Where(d => d.Counts && d.target.kind == CultureEntityKind.Landmark))
        {
            var landmark = LandmarkOf(d.target.id);
            var s = landmark != null ? map.Settlements.FirstOrDefault(x => x != null && x.id == landmark.settlement) : null;
            var t = s != null ? map.Get(s.coord) : null;
            if (t == null) continue;
            cell = t.index;
            place = $"{landmark.name}, {s.name}";
            break;
        }
        if (cell < 0 && evidence.cell >= 0 && evidence.cell < map.Count) { cell = evidence.cell; place = evidence.cause == MemoryCause.RuinRecovered ? evidence.subject : $"the ruins of {evidence.subject}"; }
        return cell < 0 ? null : MemoryRules.Ground(map, world.Settings?.generation, cell, place, _memoryTuning);
    }

    // ===== LEARNING CAUSES =====

    private string CurrentAgeId => AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null;

    // Subscribe to the owners' events once they exist (they may start after the culture).
    internal void EnsureMemoryHooks()
    {
        var ages = AgeProgression.Instance;
        if (ages != null && ages != _memoryAges)
        {
            if (_memoryAges != null) _memoryAges.AgePassed -= OnMemoryAgePassed;
            _memoryAges = ages;
            _memoryAges.AgePassed += OnMemoryAgePassed;
        }
        var legends = LegendProgress.Instance;
        if (legends != null && legends != _memoryLegends)
        {
            if (_memoryLegends != null) _memoryLegends.Lost -= OnMemoryLegendLost;
            _memoryLegends = legends;
            _memoryLegends.Lost += OnMemoryLegendLost;
        }
        var world = WorldSystem.Instance;
        if (world != null && world != _memoryWorld)
        {
            if (_memoryWorld != null) { _memoryWorld.SettlementFell -= OnMemorySettlementFell; _memoryWorld.RuinReclaimed -= OnMemoryRuinReclaimed; }
            _memoryWorld = world;
            _memoryWorld.SettlementFell += OnMemorySettlementFell;
            _memoryWorld.RuinReclaimed += OnMemoryRuinReclaimed;
        }
    }

    private void UnhookMemory()
    {
        if (_memoryAges != null) _memoryAges.AgePassed -= OnMemoryAgePassed;
        if (_memoryLegends != null) _memoryLegends.Lost -= OnMemoryLegendLost;
        if (_memoryWorld != null) { _memoryWorld.SettlementFell -= OnMemorySettlementFell; _memoryWorld.RuinReclaimed -= OnMemoryRuinReclaimed; }
        _memoryAges = null;
        _memoryLegends = null;
        _memoryWorld = null;
    }

    private void OnMemoryAgePassed(AgeRecord record)
    {
        if (this == null || record == null) return;
        int pass = _memoryAges != null ? _memoryAges.History.Count : 1;
        var now = Stamp();
        if (!string.IsNullOrEmpty(record.crisisTitle))
            LearnCause(MemoryRules.Crisis(record.ageId, record.ageTitle, record.crisisTitle, pass, record.deaths, record.Survivors, now, witnessed: true));
        // The lineage the new Age inherits.
        var lineage = MemoryRules.Pass(Memory, record.ageId, record.ageTitle, record.nextAgeId, record.nextAgeTitle, pass, now, LivedPractices());
        if (lineage != null && _state.founded)
        {
            int carried = lineage.dedications.Count(d => d.status != DedicationStatus.Released);
            Remember("lineage", $"What {record.ageTitle} left behind", carried > 0
                ? $"{carried} memorial{(carried == 1 ? "" : "s")} went on into {record.nextAgeTitle ?? "the next Age"}, and {lineage.practices.Count} practices with them."
                : $"{lineage.evidence.Count} remembered losses and {lineage.practices.Count} practices went on into {record.nextAgeTitle ?? "the next Age"}.");
            if (carried > 0)
                NotificationFeed.Push("An inheritance", $"{carried} memorial{(carried == 1 ? "" : "s")} of {record.ageTitle} came into {record.nextAgeTitle ?? "the new Age"}: recognize {(carried == 1 ? "it" : "them")} as your own in the Culture window (Memorials).",
                    NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:lineage:" + lineage.id);
        }
        RaiseChanged();
    }

    private void OnMemoryLegendLost(string legend)
    {
        if (this == null || string.IsNullOrEmpty(legend)) return;
        LearnCause(MemoryRules.LegendLost(legend, Stamp(), witnessed: true));
        RaiseChanged();
    }

    private void OnMemorySettlementFell(Ruin ruin)
    {
        if (this == null || ruin == null || ruin.ancient) return;
        LearnCause(FallOf(ruin, true));
        SettleDedications();
        RaiseChanged();
    }

    private void OnMemoryRuinReclaimed(Ruin ruin)
    {
        if (this == null || ruin == null || ruin.ancient) return;
        // The fall is remembered first (a save may hold a ruin fallen before memories were kept).
        LearnCause(FallOf(ruin, false));
        LearnCause(RecoveryOf(ruin, true));
        RaiseChanged();
    }

    private MemoryEvidence FallOf(Ruin ruin, bool witnessed)
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        int cell = map?.Get(ruin.coord)?.index ?? -1;
        string age = GameCatalog.Ages.All.FirstOrDefault(a => a != null && a.number == ruin.fallenAge)?.id;
        return MemoryRules.Fall(ruin.id, ruin.name, WorldRuins.CauseWords(ruin.cause), cell, age, Stamp(), witnessed);
    }

    private MemoryEvidence RecoveryOf(Ruin ruin, bool witnessed)
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        return MemoryRules.Recovered(ruin.id, ruin.name, ruin.reclaimedBy, map?.Get(ruin.coord)?.index ?? -1, Stamp(), witnessed);
    }

    // Keep a cause once; a witnessed one is announced (after the founding).
    private void LearnCause(MemoryEvidence evidence)
    {
        var learned = MemoryRules.Learn(Memory, evidence);
        if (learned == null) return;
        GameLog.Event($"Memory: {learned.title} is remembered{(learned.reconstructed ? " (from older records)" : string.Empty)}.", Log);
        CauseRemembered?.Invoke(learned);
        if (learned.reconstructed || !_state.founded) return;
        var suggestion = MemorialSuggestion.All.FirstOrDefault(s => s.Fits(learned));
        string hint = suggestion != null && MemoryTargetExists(CultureEntityRef.Of(suggestion.kind, suggestion.target))
            ? $" {MemoryTargetLabel(CultureEntityRef.Of(suggestion.kind, suggestion.target))} could carry it."
            : " A dish, a landmark, a rite or a holiday can be dedicated to it; a quiet remembrance asks for nothing.";
        NotificationFeed.Push(learned.title, $"{Capital(PeopleWord)} will remember this.{hint}", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:memory:" + learned.id);
    }

    /// <summary>
    /// Read the causes the world already records (Age history, fallen and reclaimed ruins of the people's own, lost
    /// Legends) that are not remembered yet: a save made before memories were kept. They are marked as learned later and
    /// never announced; nothing is given for them.
    /// </summary>
    internal void ReadRecordedCauses()
    {
        var ages = AgeProgression.Instance;
        if (ages != null)
            for (int i = 0; i < ages.History.Count; i++)
            {
                var r = ages.History[i];
                if (r == null || string.IsNullOrEmpty(r.crisisTitle) || Memory.Evidence(MemoryRules.CrisisId(r.ageId, i + 1)) != null) continue;
                LearnCause(MemoryRules.Crisis(r.ageId, r.ageTitle, r.crisisTitle, i + 1, r.deaths, r.Survivors, Stamp(), witnessed: false));
            }
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null)
            foreach (var ruin in map.Ruins.Where(r => r != null && !r.ancient).OrderBy(r => r.id))
            {
                if (Memory.Evidence(MemoryRules.FallId(ruin.id)) == null) LearnCause(FallOf(ruin, false));
                if (ruin.reclaimed && Memory.Evidence(MemoryRules.RecoveredId(ruin.id)) == null) LearnCause(RecoveryOf(ruin, false));
            }
        var legends = LegendProgress.Instance;
        if (legends != null)
            foreach (string name in legends.LostNames.OrderBy(n => n, StringComparer.Ordinal).ToList())
                if (Memory.Evidence(MemoryRules.LegendId(name)) == null) LearnCause(MemoryRules.LegendLost(name, Stamp(), witnessed: false));
    }

    // The practices the culture lives now (the lineage an Age passage keeps).
    private List<CultureEntityRef> LivedPractices()
    {
        var list = new List<CultureEntityRef>();
        foreach (var h in _state.holidays.Where(h => h != null)) list.Add(CultureEntityRef.Of(CultureEntityKind.Holiday, h.name, h.name));
        foreach (var f in _state.foodways.Where(f => f != null && f.national)) list.Add(CultureEntityRef.Of(CultureEntityKind.Resource, f.resource, f.resource));
        foreach (var a in _state.activities.Where(a => a != null && a.count > 0)) list.Add(CultureEntityRef.Of(CultureEntityKind.Activity, a.id, Life.Activity(a.id)?.name ?? a.id));
        foreach (var l in _state.landmarks.Where(l => l != null)) list.Add(CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(l.settlement, l.spec), l.name));
        var traditions = _state.extensions?.traditions?.instances;
        if (traditions != null) foreach (var t in traditions.Where(t => t != null && !string.IsNullOrEmpty(t.id))) list.Add(CultureEntityRef.Of(CultureEntityKind.Tradition, t.id, t.definition));
        return list;
    }

    // ===== THE SEVENTH =====

    // Dedications whose practice is gone sleep; those whose practice came back wake.
    internal void SettleDedications()
    {
        foreach (var d in MemoryRules.Settle(Memory, MemoryTargetExists, WhyGone))
            GameLog.Event($"Memorial: {d.target.Display} is {(d.status == DedicationStatus.Dormant ? "dormant (" + d.dormantReason + ")" : "kept again")}.", Log);
    }

    /// <summary>
    /// The memory's Seventh (<see cref="MemoryFeature"/>): causes read, dedications settled, and each dedication whose
    /// practice was committed this Seventh (a dish cooked, a rite held, a holiday kept) keeps its memory, once.
    /// </summary>
    internal void MemorySeventh(CultureSeventh context)
    {
        EnsureMemoryHooks();
        ReadRecordedCauses();
        SettleDedications();
        int seventh = context?.Now != null ? context.Now.cultureSeventh : _state.sevenths;
        if (context != null)
            foreach (var o in context.Occurrences.ToList())
                foreach (var target in PracticeTargets(o))
                {
                    var d = MemoryRules.ActiveFor(Memory, target);
                    if (d == null || !MemoryRules.Practise(Memory, d, seventh)) continue;
                    var evidence = Memory.Evidence(d.evidence);
                    EmitMemorial($"memorial:{d.id}:{seventh}", d.target, evidence, $"{MemoryTargetLabel(d.target)}, kept for {evidence?.title}", null);
                }
        ApplyRemembrance();
    }

    // What a committed occurrence practised that could carry a memory (T01's adapters): a batch of a recipe cooked, a
    // rite held, a holiday kept, and a festival in a settlement where a dedicated landmark stands (the people gathered there).
    private IEnumerable<CultureEntityRef> PracticeTargets(CulturalOccurrence o)
    {
        if (o == null) yield break;
        switch (o.kind)
        {
            case CulturalOccurrenceKind.Production:
            case CulturalOccurrenceKind.Hospitality:
                if (o.recipe != null && o.recipe.IsKnown) yield return o.recipe;
                else if (o.subject != null && o.subject.kind == CultureEntityKind.Recipe) yield return o.subject;
                break;
            case CulturalOccurrenceKind.Gathering:
            case CulturalOccurrenceKind.Observance:
                if (o.subject != null && o.subject.IsKnown) yield return o.subject;
                break;
            case CulturalOccurrenceKind.Festival:
                if (!o.SettlementKnown) break;
                foreach (var l in _state.landmarks.Where(l => l != null && l.settlement == o.settlement))
                    yield return CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(l.settlement, l.spec), l.name);
                break;
        }
    }

    private void EmitMemorial(string key, CultureEntityRef subject, MemoryEvidence evidence, string text, CultureCommandResult result)
    {
        if (!_state.founded || evidence == null) return;
        var o = new CulturalOccurrence
        {
            key = key, kind = CulturalOccurrenceKind.Memorial, subject = subject?.Copy() ?? new CultureEntityRef(), evidence = evidence.Ref,
            quantity = 1f, unit = CultureQuantityUnit.Occasions, text = text,
        };
        if (subject != null && subject.kind == CultureEntityKind.Recipe) o.recipe = subject.Copy();
        // A memorial landmark is a place: its settlement keeps the memory (unknown otherwise, never guessed).
        if (subject != null && subject.kind == CultureEntityKind.Landmark) o.settlement = LandmarkOf(subject.id)?.settlement ?? -1;
        if (Record(o)) result?.occurrences.Add(o.key);
    }

    // The morale the kept memories support, re-applied only when it changed.
    private void ApplyRemembrance()
    {
        int morale = RemembranceMorale;
        string key = morale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (key == _memoryKey) return;
        _memoryKey = key;
        EffectRouter.ApplySet(RemembranceSource, morale > 0 ? new List<GameEffect> { new GameEffect(GameEffectType.MoraleModifier, morale, ModifierType.Add) } : new List<GameEffect>());
    }

    /// <summary>After a load, the founding or a start: hooks, causes the world already records, dormancy, and the remembrance effect (never a reward).</summary>
    internal void ReconcileMemory(CultureReconcileReason reason)
    {
        _memoryKey = null;
        EnsureMemoryHooks();
        ReadRecordedCauses();
        SettleDedications();
        ApplyRemembrance();
    }
}

/// <summary>What the memory offers the culture's queries (T01's <see cref="ICultureQuery"/>): snapshots only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<MemoryEvidence> MemoryCauses();
    IReadOnlyList<MemorialDedication> MemorialDedications();
    IReadOnlyList<MemoryLineage> MemoryLineages();
}

/// <summary>The memory's part of the culture's Seventh (found by <see cref="CultureFeatures"/>).</summary>
public sealed class MemoryFeature : ICultureFeature
{
    public string Id => "memory";
    public CulturePhase Phase => CulturePhase.SourceActions;
    public int Order => 50;

    public void Seventh(CultureSeventh context) => context?.Culture?.MemorySeventh(context);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason)
    {
        if (culture != null) culture.ReconcileMemory(reason);
    }
}
