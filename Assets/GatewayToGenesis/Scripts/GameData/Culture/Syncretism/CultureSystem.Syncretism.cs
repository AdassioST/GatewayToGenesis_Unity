using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Syncretism and ruin inheritance (T07; rules in <see cref="SyncretismRules"/>, numbers and authored blends in
/// <see cref="SyncretismTuning"/>, saved in <see cref="CultureExtensionState.syncretism"/>).
/// - Blends: two traditions that met (kept together, or across an open road the records show something crossed) and
///   are kept long enough may be kept side by side, blended into a new form beside them, or blended in their place.
///   The new form is a T01 tradition (linked only), so it lives, lapses, is recognised and is carried into the next
///   Age like any other.
/// - Ruins: what a fallen people's ruins record (civic, district, binding, customs, traditions) decides what can be
///   taken from them. Their civic is adopted through the world's own path (<see cref="WorldSystem.AdoptRuinCivic"/>:
///   one ledger, <see cref="Ruin.civicAdopted"/>), beside yours or in place of one of yours; their ways are digested
///   through the reform (<see cref="Reform"/>: one ledger, <see cref="CultureState.digestedRuins"/>), only toward what
///   their records support; what they kept can be taken up again, once each. Policy: adopting a ruin's civic and
///   digesting its ways are separate one-time uses of the same ruin; either leaves the other open.
/// - Renewal: an inherited or blended tradition is renewed for a new Age only after an Age has actually passed
///   (AgeProgression's history), and only so many times.
/// </summary>
public partial class CultureSystem
{
    private readonly SyncretismTuning _syncretismDefaults = new SyncretismTuning();

    public SyncretismTuning SyncretismTuning => Settings != null && Settings.syncretism != null ? Settings.syncretism : _syncretismDefaults;

    private SyncretismState SyncretismData
    {
        get
        {
            var x = Extensions;
            if (x.syncretism == null) x.syncretism = new SyncretismState();
            return x.syncretism;
        }
    }

    /// <summary>A blend or an inheritance was decided.</summary>
    public event Action<SyncretismDecision> SyncretismDecided;

    /// <summary>Completed Age passages (the actual history, not the Age's number).</summary>
    private static int AgePasses => AgeProgression.Instance != null ? AgeProgression.Instance.History.Count : 0;

    private static readonly PracticeChannel[] ExchangeChannels = { PracticeChannel.Road, PracticeChannel.Visit, PracticeChannel.Founders, PracticeChannel.Arrival, PracticeChannel.Table };

    // ===== WHAT IS KEPT WHERE =====

    /// <summary>The parents as the rules read them: every living tradition and every settlement's custom (met ones only as evidence of exchange).</summary>
    public List<ParentPresence> ParentPresences()
    {
        var t = SyncretismTuning;
        var list = new List<ParentPresence>();
        foreach (var i in Extensions.traditions.instances.Where(i => i != null && !i.fallen && i.settlement != TraditionRules.FallenPlace && string.IsNullOrEmpty(i.mergedInto)))
            list.Add(new ParentPresence
            {
                key = SyncretismRules.TraditionPrefix + i.definition, settlement = i.settlement, sustained = i.Lived, instance = i.id, momentum = i.momentum,
                preserved = i.recognition == TraditionRecognition.Preserved, recognized = i.recognition == TraditionRecognition.Recognized,
            });
        foreach (var profile in LocalProfiles().Where(p => p != null && !p.gone))
            foreach (var p in profile.practices.Where(p => p != null))
            {
                var o = p.origin;
                bool exchanged = o != null && ExchangeChannels.Contains(o.channel) && o.fromSettlement >= 0 && o.fromSettlement != profile.settlement;
                list.Add(new ParentPresence
                {
                    key = SyncretismRules.LocalPrefix + p.practice, settlement = profile.settlement, kept = p.Kept,
                    sustained = p.stage == LocalPracticeStage.Practiced && p.gatherings >= Math.Max(1, t.sustainedGatherings),
                    fromSettlement = o?.fromSettlement ?? -1, exchanged = exchanged, recognized = p.recognized, momentum = p.participation,
                    provenance = exchanged ? $"provenance:{profile.settlement}:{p.practice}:{o.channel}:{o.fromSettlement}" : null,
                });
            }
        return list;
    }

    private List<ContactLink> ContactLinks() => ContactEdges().Where(e => e != null).Select(e => new ContactLink { a = e.a, b = e.b, open = e.open }).ToList();

    /// <summary>A parent's name ("The Evening Song", "Crossing Songs").</summary>
    public string ParentName(string key)
    {
        var p = SyncretismRules.Parent(key);
        if (p.kind == "tradition") return TraditionTuning.Definition(p.id)?.name ?? p.id;
        if (p.kind == "local") return LocalPracticeCatalog.NameOf(p.id);
        return key;
    }

    private string PlaceName(int settlement) => settlement < 0 ? "the nation" : SettlementName(settlement) ?? $"a settlement now gone ({settlement})";

    private bool TraditionKeptAt(string definition, int place) =>
        Extensions.traditions.instances.Any(i => i != null && !i.fallen && i.settlement == place && string.Equals(i.definition, definition, StringComparison.OrdinalIgnoreCase));

    // ===== BLENDS =====

    /// <summary>Every blend the people could decide on now (authored only; they met; kept long enough; not decided yet).</summary>
    public IReadOnlyList<HybridOffer> HybridOffers()
    {
        if (!_state.founded) return new List<HybridOffer>();
        SyncretismRules.Ensure(SyncretismData);
        // A new form whose own Age or technology is not reached is not offered (no blend unlocks what research has not).
        return SyncretismRules.Offers(SyncretismTuning, ParentPresences(), ContactLinks(), GameAge.Number, Researched, id => TraditionAvailable(TraditionTuning.Definition(id)),
            TraditionKeptAt, SyncretismData, _state.sevenths, ParentName, PlaceName);
    }

    // The offer for one rule and place, or why there is none.
    private HybridOffer OfferFor(string ruleId, int place, out string why)
    {
        var t = SyncretismTuning;
        var r = t.Hybrid(ruleId);
        why = !_state.founded ? "The culture has not been founded yet." : SyncretismRules.WhyNotRule(r, GameAge.Number, Researched, id => TraditionTuning.Definition(id) != null, t);
        if (why != null) return null;
        var result = TraditionTuning.Definition(r.result);
        if (!TraditionAvailable(result)) { why = GameAge.Number < result.minAge ? $"{result.name} begins in Age {AgeRules.Roman(result.minAge)}." : $"{result.name} needs {result.technology} first."; return null; }
        why = SyncretismRules.WhyNotMet(r, place, ParentPresences(), ContactLinks(), ParentName, out var evidence, out var a, out var b, out string contact);
        if (why != null) return null;
        var d = TraditionTuning.Definition(r.result);
        if (TraditionKeptAt(r.result, place)) { why = $"{d.name} is kept in {PlaceName(place)} already."; return null; }
        var offer = HybridOffers().FirstOrDefault(o => o.rule.id == r.id && o.settlement == place);
        if (offer == null) { why = $"The people of {PlaceName(place)} decided about {d.name} not long ago."; return null; }
        return offer;
    }

    /// <summary>What deciding <paramref name="mode"/> for a blend would do (kept, let go, paid), or why it cannot. Nothing is changed.</summary>
    public CultureCommandResult PreviewHybrid(string ruleId, int settlement, SyncretismMode mode)
    {
        var offer = OfferFor(ruleId, settlement, out string why);
        if (offer == null) return CultureCommandResult.Fail(why);
        why = SyncretismRules.WhyNotMode(offer, mode, UnityHeld, SyncretismTuning, ParentName, out float cost);
        if (why != null) return CultureCommandResult.Fail(why);
        var d = TraditionTuning.Definition(offer.rule.result);
        var (retained, lost) = SyncretismRules.Outcome(offer, mode, ParentName, d.name);
        var r = CultureCommandResult.Ok($"{ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)} in {offer.place}: {offer.contact}. {Capital(SyncretismRules.ModeWord(mode))}. "
            + $"Keeps {string.Join("; ", retained)}. Lets go of {string.Join("; ", lost)}.{(cost > 0f ? $" Costs {cost:0} {UnityResource}." : " Costs nothing.")}");
        if (cost > 0f) r.paid.Add(new ResourceAmount { resource = UnityResource, amount = cost });
        return r;
    }

    /// <summary>
    /// Decide a blend: keep the two side by side (nothing is made, it may be offered again later), blend a new form beside
    /// them, or let it take their place where it is made (they fall quiet there and live on in it). Paid now, once.
    /// </summary>
    public CultureCommandResult Hybridize(string ruleId, int settlement, SyncretismMode mode)
    {
        var preview = PreviewHybrid(ruleId, settlement, mode);
        if (!preview.succeeded) { GameLog.Event("Blend refused: " + preview.reason, Log); return preview; }
        var offer = OfferFor(ruleId, settlement, out _);
        var d = TraditionTuning.Definition(offer.rule.result);
        var (retained, lost) = SyncretismRules.Outcome(offer, mode, ParentName, d.name);
        Pay(preview.paid);
        var decision = NewDecision(SyncretismRules.OfferKey(offer.rule.id, settlement), preview.paid, retained, lost);
        decision.hybrid = true;
        decision.rule = offer.rule.id;
        decision.mode = mode;
        decision.settlement = settlement;
        decision.parentA = offer.rule.parentA;
        decision.parentB = offer.rule.parentB;
        decision.evidence.AddRange(offer.evidence);
        if (mode == SyncretismMode.SideBySide)
        {
            decision.text = $"In {offer.place}, {ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)} are kept side by side, each as it was.";
            FinishDecision(decision, null);
            return CultureCommandResult.Ok(decision.text);
        }
        int now = _state.sevenths;
        var state = Extensions.traditions;
        var origin = new TraditionOrigin
        {
            key = decision.key, kind = CulturalOccurrenceKind.Contact, subject = CultureEntityRef.Of(CultureEntityKind.Tradition, offer.rule.result, d.name), settlement = settlement,
            stamp = Stamp(), text = $"{ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)} met in {offer.place} ({offer.contact}) and were blended",
        };
        var made = TraditionRules.Create(state, d, settlement, null, origin, now);
        foreach (var key in new[] { offer.rule.parentA, offer.rule.parentB })
            made.links.Add(CultureEntityRef.Of(CultureEntityKind.Tradition, key, ParentName(key)));
        TraditionRules.Mark(made, TraditionRules.EmergedMilestone);
        if (mode == SyncretismMode.Replace)
        {
            // It takes their place here: it is a custom at once, carrying their momentum; they fall quiet here and live on in it.
            made.stage = TraditionStage.Practiced;
            made.stageSince = now;
            made.distinctSevenths = Math.Max(made.distinctSevenths, 1);
            made.momentum = (offer.a?.momentum ?? 0f) + (offer.b?.momentum ?? 0f);
            TraditionRules.Mark(made, TraditionRules.PracticedMilestone);
            if (made.recognition == TraditionRecognition.None) made.recognition = TraditionRecognition.Offered;
            foreach (var parent in new[] { offer.a, offer.b }.Where(p => p != null && p.settlement == settlement))
            {
                var instance = TraditionRules.Get(state, parent.instance);
                if (instance != null)
                {
                    instance.stage = TraditionStage.Dormant;
                    instance.stageSince = now;
                    instance.mergedInto = made.id;
                    TraditionRules.Mark(instance, SyncretismRules.MergedMilestone);
                }
                else if (SyncretismRules.Parent(parent.key).kind == "local")
                {
                    var p = LocalCultureRules.Practice(LocalCultureRules.Profile(Local, settlement), SyncretismRules.Parent(parent.key).id);
                    if (p != null && p.stage == LocalPracticeStage.Practiced) p.stage = LocalPracticeStage.Quiet;
                }
            }
        }
        decision.tradition = made.id;
        SyncretismData.variants.Add(new VariantRecord
        {
            tradition = made.id, source = "hybrid:" + offer.rule.id, parentA = offer.rule.parentA, parentB = offer.rule.parentB,
            depth = SyncretismRules.Depth(offer.rule.result, SyncretismTuning), agePasses = AgePasses, ageId = Stamp().ageId, evidence = new List<string>(offer.evidence),
        });
        decision.text = mode == SyncretismMode.Replace
            ? $"In {offer.place}, {ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)} became {d.name}: {offer.rule.fromA}, with {offer.rule.fromB}."
            : $"In {offer.place}, {d.name} was made beside {ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)}: {offer.rule.fromA}, with {offer.rule.fromB}.";
        FinishDecision(decision, d.name);
        ApplyTraditionEffects(false);
        var result = CultureCommandResult.Ok(decision.text);
        result.paid.AddRange(preview.paid);
        return result;
    }

    // ===== RUINS =====

    /// <summary>A ruin as its records keep it (null only when nothing at all is known of that id).</summary>
    private RuinRecord RuinRecordOf(int id)
    {
        var world = WorldSystem.Instance;
        var map = world != null ? world.Map : null;
        var ruin = map?.Ruins.FirstOrDefault(r => r != null && r.id == id);
        var fallen = Extensions.traditions.instances.Where(i => i != null && i.fallen && i.ruin == id).ToList();
        if (ruin == null && fallen.Count == 0) return null;
        var rec = new RuinRecord
        {
            id = id, name = ruin?.name ?? fallen.Select(i => i.formerPlace).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? $"a fallen settlement ({id})",
            onMap = ruin != null, investigated = ruin == null || ruin.investigated, digested = _state.digestedRuins.Contains(id),
        };
        if (ruin != null)
        {
            rec.ancient = ruin.ancient;
            rec.reclaimed = ruin.reclaimed;
            rec.kind = ruin.kind;
            rec.district = ruin.district;
            if (!string.IsNullOrEmpty(ruin.district))
                rec.districtFamily = CultureRules.TryFamily(ruin.district, out var df) || CultureRules.TryFamily(world.Rules?.tributaries?.District(ruin.district)?.enclave, out df) ? df.ToString() : null;
            rec.binding = ruin.binding;
            rec.civic = ruin.civic;
            rec.civicFamily = Tuning.FamilyOfCivic(ruin.civic)?.ToString();
            rec.civicAdopted = ruin.civicAdopted;
            rec.foundedAge = ruin.foundedAge;
            rec.fallenAge = ruin.fallenAge;
            rec.fullAges = ruin.fullAges;
            rec.cause = ruin.cause;
            var tile = map.Get(ruin.coord);
            if (tile != null) rec.groundFamily = Tuning.FamilyOfLand(tile.terrain, tile.macroBiome, tile.landform)?.ToString();
            // Its settlement's own customs, kept after it fell (T02's former profiles; ids are reused, so the name must match too).
            foreach (var profile in FormerProfiles().Where(p => p != null && p.settlement == ruin.settlement && (string.IsNullOrEmpty(p.name) || p.name == ruin.name)))
                foreach (var p in profile.practices.Where(p => p != null))
                    if (!rec.practices.Any(x => x.id == p.practice)) rec.practices.Add((p.practice, p.name ?? LocalPracticeCatalog.NameOf(p.practice), p.family, p.Kept));
        }
        foreach (var i in fallen)
        {
            var d = TraditionTuning.Definition(i.definition);
            rec.traditions.Add((i.id, i.definition, d?.name ?? i.definition, d?.family, i.Established));
        }
        return rec;
    }

    /// <summary>What a ruin offers, read from its records. Never throws: a ruin gone from the map or never investigated says so.</summary>
    public RuinHeritage InspectRuin(int id)
    {
        try { return SyncretismRules.Read(RuinRecordOf(id)); }
        catch (Exception e)
        {
            GameLog.Warning($"The ruins {id} could not be read: {e.Message}", Log);
            return SyncretismRules.Read(null);
        }
    }

    /// <summary>Every ruin whose inheritance can be read: those on the map (investigated first), and those known only through what fell with them.</summary>
    public IReadOnlyList<RuinHeritage> RuinHeritages()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        var ids = new List<int>();
        if (map != null) ids.AddRange(map.Ruins.Where(r => r != null).OrderByDescending(r => r.investigated).ThenBy(r => r.ancient).ThenBy(r => r.id).Select(r => r.id));
        ids.AddRange(Extensions.traditions.instances.Where(i => i != null && i.fallen && i.ruin >= 0).Select(i => i.ruin).Distinct().Where(id => !ids.Contains(id)));
        return ids.Select(InspectRuin).Where(h => h?.record != null).ToList();
    }

    private string WhyNotInheritNow(InheritanceRequest q, RuinHeritage h)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        string why = SyncretismRules.WhyNotInherit(h, q, SyncretismData);
        if (why != null) return why;
        var r = h.record;
        var world = WorldSystem.Instance;
        var ruin = world?.Map?.Ruins.FirstOrDefault(x => x != null && x.id == r.id);
        switch (q.choice)
        {
            case InheritanceChoice.PreserveCivic:
                return world == null ? "There is no world." : world.WhyNotAdoptRuinCivic(ruin);
            case InheritanceChoice.ReplaceCivic:
                return world == null ? "There is no world." : world.WhyNotAdoptRuinCivic(ruin, q.replacing);
            case InheritanceChoice.AdaptWays:
                if (!CultureRules.TryFamily(q.family, out _)) return $"'{q.family}' is not a way of living.";
                return WhyNotReform();
            case InheritanceChoice.ReadTheStones:
                return WhyNotReform();
            case InheritanceChoice.Revive:
                var clue = h.clues.First(c => c.evidence == q.evidence);
                if (q.settlement >= 0 && SettlementName(q.settlement) == null) return "That settlement no longer stands.";
                if (clue.kind == RuinClueKind.Tradition)
                {
                    var d = TraditionTuning.Definition(clue.definition);
                    if (d == null) return "The records name a tradition this world does not know.";
                    if (!TraditionAvailable(d)) return GameAge.Number < d.minAge ? $"{d.name} begins in Age {AgeRules.Roman(d.minAge)}." : $"{d.name} needs {d.technology} first.";
                    if (q.settlement >= 0 && !d.local) return $"{d.name} is kept by the nation, not by one settlement.";
                    if (TraditionKeptAt(d.id, q.settlement)) return $"{d.name} is kept in {PlaceName(q.settlement)} already.";
                    return null;
                }
                var spec = LocalPracticeCatalog.Find(clue.practice);
                if (spec == null) return "The records name a custom this world does not know.";
                if (!spec.OpenIn(GameAge.Number)) return $"{spec.name} belongs to {spec.AgeText}.";
                var kept = LocalCultureRules.Practice(LocalCultureRules.Profile(Local, q.settlement), spec.id);
                if (kept != null && kept.Kept) return $"{spec.name} is kept in {PlaceName(q.settlement)} already.";
                return null;
        }
        return null;
    }

    /// <summary>What an inheritance choice would do (kept, let go, paid), or why it cannot. Nothing is changed.</summary>
    public CultureCommandResult PreviewInherit(InheritanceRequest q)
    {
        var h = InspectRuin(q?.ruin ?? -1);
        if (h?.record == null) return CultureCommandResult.Fail("Nothing is recorded of those ruins.");
        string why = WhyNotInheritNow(q, h);
        if (why != null) return CultureCommandResult.Fail(why);
        var (retained, lost) = SyncretismRules.Outcome(h, q, q.choice == InheritanceChoice.ReplaceCivic ? Tuning.FamilyOfCivic(q.replacing)?.ToString() : null);
        string what;
        switch (q.choice)
        {
            case InheritanceChoice.AdaptWays:
                what = $"The culture reforms toward the {q.family} ways ({CultureRules.Percent(Tuning.reformShift)} of it), digesting {h.record.name} (Digestive Rebirth; a ruin feeds one reform).";
                break;
            case InheritanceChoice.ReadTheStones:
                what = $"A guess: the culture reforms a little toward the {h.record.groundFamily} ways ({CultureRules.Percent(Tuning.reformShift * SyncretismTuning.uncertainShare)} of it), digesting {h.record.name}. Nobody knows who lived there.";
                break;
            case InheritanceChoice.PreserveCivic:
                what = $"{h.record.civic} is adopted from the ruins, beside your civics (its requirements waived: its people lived by it).";
                break;
            case InheritanceChoice.ReplaceCivic:
                what = $"{q.replacing} is removed, with all it gives and its removal penalties, and {h.record.civic} takes its place.";
                break;
            default:
                var clue = h.clues.First(c => c.evidence == q.evidence);
                what = clue.kind == RuinClueKind.Tradition
                    ? $"{TraditionTuning.Definition(clue.definition)?.name} is taken up in {PlaceName(q.settlement)}: it begins again, and becomes a custom only if it is kept."
                    : $"{LocalPracticeCatalog.NameOf(clue.practice)} is known in {PlaceName(q.settlement)} again: its people can gather for it, and keep it if they do.";
                break;
        }
        return CultureCommandResult.Ok($"{what} Keeps {string.Join("; ", retained)}. Lets go of {string.Join("; ", lost)}.");
    }

    /// <summary>Make an inheritance choice about a ruin (each one-time use of a ruin is spent once, whatever path asks for it).</summary>
    public CultureCommandResult Inherit(InheritanceRequest q)
    {
        var preview = PreviewInherit(q);
        if (!preview.succeeded) { GameLog.Event("Inheritance refused: " + preview.reason, Log); return preview; }
        var h = InspectRuin(q.ruin);
        var r = h.record;
        var world = WorldSystem.Instance;
        var ruin = world?.Map?.Ruins.FirstOrDefault(x => x != null && x.id == r.id);
        var (retained, lost) = SyncretismRules.Outcome(h, q, q.choice == InheritanceChoice.ReplaceCivic ? Tuning.FamilyOfCivic(q.replacing)?.ToString() : null);
        var decision = NewDecision(SyncretismRules.RuinKey(r.id, q.choice, q.choice == InheritanceChoice.Revive ? q.evidence : null), null, retained, lost);
        decision.choice = q.choice;
        decision.ruin = r.id;
        decision.ruinName = r.name;
        decision.settlement = q.settlement;
        decision.evidence.AddRange(h.clues.Where(c => q.choice == InheritanceChoice.Revive ? c.evidence == q.evidence
            : q.choice == InheritanceChoice.AdaptWays ? string.Equals(c.family, q.family, StringComparison.OrdinalIgnoreCase)
            : q.choice == InheritanceChoice.ReadTheStones ? c.kind == RuinClueKind.Ground
            : c.kind == RuinClueKind.Civic).Select(c => c.evidence));
        string made = null;
        switch (q.choice)
        {
            case InheritanceChoice.PreserveCivic:
            case InheritanceChoice.ReplaceCivic:
                if (!world.AdoptRuinCivic(ruin, q.choice == InheritanceChoice.ReplaceCivic ? q.replacing : null)) return CultureCommandResult.Fail($"{r.civic} could not be adopted.");
                decision.civic = r.civic;
                decision.replaced = q.choice == InheritanceChoice.ReplaceCivic ? q.replacing : null;
                decision.text = q.choice == InheritanceChoice.ReplaceCivic
                    ? $"{Capital(PeopleWord)} took up {r.civic} from the ruins of {r.name} in place of {q.replacing}."
                    : $"{Capital(PeopleWord)} took up {r.civic} from the ruins of {r.name}, beside their own ways.";
                break;
            case InheritanceChoice.AdaptWays:
            case InheritanceChoice.ReadTheStones:
                string family = q.choice == InheritanceChoice.AdaptWays ? q.family : r.groundFamily;
                CultureRules.TryFamily(family, out var toward);
                ReformCore(toward, ruin, q.choice == InheritanceChoice.AdaptWays ? Tuning.reformShift : Tuning.reformShift * Mathf.Clamp01(SyncretismTuning.uncertainShare));
                decision.family = toward.ToString();
                decision.text = q.choice == InheritanceChoice.AdaptWays
                    ? $"{Capital(PeopleWord)} took in the {toward} ways the records of {r.name} speak of."
                    : $"{Capital(PeopleWord)} read the stones of {r.name} and guessed at the {toward} ways; nobody knows who lived there.";
                break;
            case InheritanceChoice.Revive:
                var clue = h.clues.First(c => c.evidence == q.evidence);
                if (clue.kind == RuinClueKind.Tradition)
                {
                    var d = TraditionTuning.Definition(clue.definition);
                    var instance = TraditionRules.Create(Extensions.traditions, d, q.settlement, null, new TraditionOrigin
                    {
                        key = decision.key, kind = CulturalOccurrenceKind.Contact, subject = CultureEntityRef.Of(CultureEntityKind.Ruin, r.id.ToString(), r.name), settlement = q.settlement,
                        stamp = Stamp(), text = $"taken up from the ruins of {r.name}, where it was kept before the fall",
                    }, _state.sevenths);
                    instance.links.Add(CultureEntityRef.Of(CultureEntityKind.Ruin, r.id.ToString(), r.name));
                    TraditionRules.Mark(instance, TraditionRules.EmergedMilestone);
                    TraditionRules.Mark(instance, SyncretismRules.InheritedMilestone);
                    SyncretismData.variants.Add(new VariantRecord
                    {
                        tradition = instance.id, source = "ruin:" + r.id, ruin = r.id, depth = 0, agePasses = AgePasses, ageId = Stamp().ageId, evidence = { clue.evidence },
                    });
                    decision.tradition = instance.id;
                    made = d.name;
                    decision.text = $"{Capital(PeopleWord)} took up {d.name} in {PlaceName(q.settlement)}, as the people of {r.name} kept it.";
                }
                else
                {
                    var spec = LocalPracticeCatalog.Find(clue.practice);
                    LocalCultureRules.Expose(Local, q.settlement, SettlementName(q.settlement), spec.id, Mathf.Clamp01(SyncretismTuning.reviveExposure), new PracticeProvenance
                    {
                        channel = PracticeChannel.Inherited, fromSettlement = -1, fromName = r.name, carrier = $"the ruins of {r.name}", seventh = _state.sevenths, ageId = Stamp().ageId,
                    }, GameAge.Number);
                    decision.text = $"{PlaceName(q.settlement)} learned {spec.name} anew from what the ruins of {r.name} record.";
                }
                break;
        }
        FinishDecision(decision, made);
        if (made != null) ApplyTraditionEffects(false);
        return CultureCommandResult.Ok(decision.text);
    }

    /// <summary>Why the culture cannot reform toward <paramref name="toward"/> now, digesting <paramref name="ruinId"/> (-1: by its own will), or null.</summary>
    public string WhyNotReformToward(EnclaveFamily toward, int ruinId)
    {
        if (ruinId < 0) return WhyNotReform();
        var h = InspectRuin(ruinId);
        if (h?.record == null) return "Nothing is recorded of those ruins.";
        var q = ReformRequest(toward, ruinId, h);
        string why = WhyNotInheritNow(q, h);
        if (why != null) return why;
        return h.unknown && !string.Equals(h.record.groundFamily, toward.ToString(), StringComparison.OrdinalIgnoreCase)
            ? $"Nobody knows who lived in {h.record.name}: its ground suggests only the {h.record.groundFamily} ways." : null;
    }

    private static InheritanceRequest ReformRequest(EnclaveFamily toward, int ruinId, RuinHeritage h) => new InheritanceRequest
    {
        ruin = ruinId, choice = h.unknown ? InheritanceChoice.ReadTheStones : InheritanceChoice.AdaptWays, family = toward.ToString(),
    };

    // A reform digesting a ruin goes through the inheritance (its records justify it, the decision is kept).
    private bool ReformFromRuin(EnclaveFamily toward, int ruinId)
    {
        string why = WhyNotReformToward(toward, ruinId);
        if (why != null) { GameLog.Event("Reform refused: " + why, Log); return false; }
        return Inherit(ReformRequest(toward, ruinId, InspectRuin(ruinId))).succeeded;
    }

    // ===== RENEWAL FOR A NEW AGE =====

    public string WhyNotRenew(string tradition)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        var i = TraditionRules.Get(Extensions.traditions, tradition);
        if (i == null || i.fallen) return "No such tradition.";
        string why = SyncretismRules.WhyNotRenew(SyncretismData.Variant(tradition), AgePasses, SyncretismTuning);
        if (why != null) return why;
        float cost = SyncretismTuning.renewUnity;
        return cost > 0f && !Has(UnityResource, cost) ? $"Needs {cost:0} {UnityResource} ({UnityHeld:0} held)." : null;
    }

    public CultureCommandResult PreviewRenew(string tradition)
    {
        string why = WhyNotRenew(tradition);
        if (why != null) return CultureCommandResult.Fail(why);
        var i = TraditionRules.Get(Extensions.traditions, tradition);
        var v = SyncretismData.Variant(tradition);
        var d = TraditionTuning.Definition(i.definition);
        var r = CultureCommandResult.Ok($"{d?.name ?? i.definition} is reworked for this Age (reworked {v.depth + 1} of at most {SyncretismTuning.maxDepth} times): it is kept as a custom of the Age from now on{(i.Lived ? string.Empty : ", taken up again")}.");
        if (SyncretismTuning.renewUnity > 0f) r.paid.Add(new ResourceAmount { resource = UnityResource, amount = SyncretismTuning.renewUnity });
        return r;
    }

    /// <summary>Renew an inherited or blended tradition for the Age the people now live in (only after an Age has passed since it was made or renewed).</summary>
    public CultureCommandResult Renew(string tradition)
    {
        var preview = PreviewRenew(tradition);
        if (!preview.succeeded) return preview;
        Pay(preview.paid);
        var i = TraditionRules.Get(Extensions.traditions, tradition);
        var v = SyncretismData.Variant(tradition);
        var d = TraditionTuning.Definition(i.definition);
        int now = _state.sevenths;
        string age = Stamp().ageId ?? $"pass-{AgePasses}";
        v.depth++;
        v.agePasses = AgePasses;
        v.renewals.Add(age);
        i.lastPracticed = now;
        if (!i.Lived)
        {
            i.stage = i.Established ? TraditionStage.Revived : TraditionStage.Practiced;
            i.stageSince = now;
            TraditionRules.Mark(i, TraditionRules.PracticedMilestone);
            if (i.recognition == TraditionRecognition.None) i.recognition = TraditionRecognition.Offered;
        }
        TraditionRules.Mark(i, "renewed:" + age);
        var decision = NewDecision($"renew:{tradition}:{v.agePasses}", preview.paid, new List<string> { $"{d?.name}, reworked for {age}" }, new List<string> { "a little more of what it was" });
        decision.tradition = tradition;
        decision.settlement = i.settlement;
        decision.text = $"{Capital(PeopleWord)} renewed {d?.name ?? i.definition} for a new Age.";
        FinishDecision(decision, null);
        ApplyTraditionEffects(false);
        return CultureCommandResult.Ok(decision.text);
    }

    // ===== THE RECORD =====

    private SyncretismDecision NewDecision(string key, IEnumerable<ResourceAmount> paid, List<string> retained, List<string> lost)
    {
        var s = SyncretismData;
        SyncretismRules.Ensure(s);
        return new SyncretismDecision
        {
            id = "syn-" + s.nextId++, key = key, stamp = Stamp(), retained = retained ?? new List<string>(), lost = lost ?? new List<string>(),
            paid = (paid ?? Enumerable.Empty<ResourceAmount>()).Select(p => new ResourceAmount { resource = p.resource, amount = p.amount }).ToList(),
        };
    }

    private void FinishDecision(SyncretismDecision decision, string made)
    {
        var s = SyncretismData;
        s.decisions.Add(decision);
        int keep = Math.Max(1, SyncretismTuning.decisionsKept);
        // Older decisions are trimmed, but never one that still guards a one-time use (a ruin's revival, a renewal).
        while (s.decisions.Count > keep)
        {
            var old = s.decisions.FirstOrDefault(d => d.hybrid);
            if (old == null) break;
            s.decisions.Remove(old);
        }
        string title = decision.hybrid ? (made ?? "Kept side by side") : decision.ruin >= 0 ? $"The ruins of {decision.ruinName}" : "Renewed";
        Remember(decision.hybrid ? "syncretism" : decision.ruin >= 0 ? "inheritance" : "renewal", title, decision.text
            + (decision.lost.Count > 0 && decision.mode != SyncretismMode.SideBySide ? $" Lost: {string.Join("; ", decision.lost)}." : string.Empty));
        GameLog.Event($"Syncretism: {decision.text}", Log);
        NotificationFeed.Push(title, decision.text, NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:syncretism:" + decision.id);
        SyncretismDecided?.Invoke(decision);
        RaiseChanged();
    }

    // ===== QUESTIONS =====

    /// <summary>The decisions made, newest last (copies).</summary>
    public IReadOnlyList<SyncretismDecision> SyncretismDecisions() =>
        SyncretismData.decisions.Where(d => d != null).Select(d => JsonUtility.FromJson<SyncretismDecision>(JsonUtility.ToJson(d))).ToList();

    /// <summary>The variant record of a tradition (a blend, or taken up from ruins), or null (a copy).</summary>
    public VariantRecord VariantOf(string tradition)
    {
        var v = SyncretismData.Variant(tradition);
        return v == null ? null : JsonUtility.FromJson<VariantRecord>(JsonUtility.ToJson(v));
    }

    /// <summary>Every inherited or blended tradition still recorded (copies).</summary>
    public IReadOnlyList<VariantRecord> Variants() => SyncretismData.variants.Where(v => v != null).Select(v => JsonUtility.FromJson<VariantRecord>(JsonUtility.ToJson(v))).ToList();

    // A condition's value (CultureSystem.Value): blends (made), inherited (taken up from ruins), syncretism (decisions),
    // blend:<rule> (1 while one lives).
    private float SyncretismValue(string t)
    {
        var s = SyncretismData;
        switch (t)
        {
            case "blends": return s.variants.Count(v => v.source != null && v.source.StartsWith("hybrid:"));
            case "inherited": return s.variants.Count(v => v.source != null && v.source.StartsWith("ruin:"));
            case "syncretism": return s.decisions.Count;
        }
        if (!t.StartsWith("blend:")) return float.NaN;
        string rule = t.Substring(6).Trim();
        return s.variants.Any(v => v.source == "hybrid:" + rule && TraditionRules.Get(Extensions.traditions, v.tradition)?.Lived == true) ? 1f : 0f;
    }

    // ===== THE SEVENTH =====

    /// <summary>After the traditions' lifecycle: each blend newly possible is told once (its meeting, where).</summary>
    public void AnnounceBlends(CultureSeventh context)
    {
        var s = SyncretismData;
        SyncretismRules.Ensure(s);
        foreach (var offer in HybridOffers())
        {
            string key = offer.Key;
            if (s.announced.Contains(key)) continue;
            s.announced.Add(key);
            var d = TraditionTuning.Definition(offer.rule.result);
            context?.Notices.Add(($"Two ways meet in {offer.place}", $"{ParentName(offer.rule.parentA)} and {ParentName(offer.rule.parentB)} meet in {offer.place} ({offer.contact}): keep them side by side, or blend them into {d?.name} (Culture window, Heritage).", "culture:" + key));
        }
    }

    public void ReconcileSyncretism() => SyncretismRules.Ensure(SyncretismData);
}

/// <summary>Syncretism in the culture's phases: after the traditions' lifecycle, meetings newly possible are told once.</summary>
public sealed class SyncretismFeature : ICultureFeature
{
    public string Id => "syncretism";
    public CulturePhase Phase => CulturePhase.TraditionLifecycle;
    public int Order => 80;

    public void Seventh(CultureSeventh context) => context?.Culture?.AnnounceBlends(context);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileSyncretism();
}
