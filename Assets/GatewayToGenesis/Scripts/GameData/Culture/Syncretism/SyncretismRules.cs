using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Syncretism and ruin inheritance, with no scene state (numbers and authored hybrids in <see cref="SyncretismTuning"/>):
/// - A hybrid is offered only by an authored rule, where both parents are kept long enough and have actually met: kept
///   in the same community (a nation-wide tradition is kept in every settlement), or kept in two settlements joined by
///   an open road across which the records show an exchange (a custom that came from one to the other). A road alone,
///   or two traditions kept apart, is no meeting.
/// - The people keep them side by side (always possible, nothing lost), make a new form beside them (Unity), or let it
///   take their place where it is made (a community that keeps a parent as its own refuses; the nation's own cannot be
///   replaced by one settlement). A hybrid's depth is capped.
/// - A ruin's inheritance is read from its records: its civic, district, the customs and traditions that fell with it
///   support ways of living; its binding and its fall are remembered and support none. Only a supported way can be
///   digested from it, once. A ruin whose people nobody knows offers only a guess from its ground, at a smaller shift.
/// - Renewing an inherited or blended tradition needs a completed Age passage since it was made or last renewed.
/// </summary>
public static class SyncretismRules
{
    public const string TraditionPrefix = "tradition:", LocalPrefix = "local:";
    /// <summary>A tradition merged into a new form where it was kept (its own practice there lives on in the new one).</summary>
    public const string MergedMilestone = "merged";
    public const string InheritedMilestone = "inherited";

    public static void Ensure(SyncretismState s)
    {
        if (s == null) return;
        if (s.decisions == null) s.decisions = new List<SyncretismDecision>();
        if (s.variants == null) s.variants = new List<VariantRecord>();
        if (s.announced == null) s.announced = new List<string>();
        s.decisions.RemoveAll(d => d == null);
        s.variants.RemoveAll(v => v == null || string.IsNullOrEmpty(v.tradition));
        foreach (var d in s.decisions)
        {
            if (d.evidence == null) d.evidence = new List<string>();
            if (d.retained == null) d.retained = new List<string>();
            if (d.lost == null) d.lost = new List<string>();
            if (d.paid == null) d.paid = new List<ResourceAmount>();
            if (d.stamp == null) d.stamp = new CultureStamp();
        }
        foreach (var v in s.variants)
        {
            if (v.evidence == null) v.evidence = new List<string>();
            if (v.renewals == null) v.renewals = new List<string>();
        }
        if (s.nextId < 1) s.nextId = 1;
        foreach (var d in s.decisions)
            if (d.id != null && d.id.StartsWith("syn-") && int.TryParse(d.id.Substring(4), out int n) && n >= s.nextId) s.nextId = n + 1;
        if (s.version < SyncretismState.CurrentVersion) s.version = SyncretismState.CurrentVersion;
    }

    public static string OfferKey(string rule, int place) => $"hybrid:{rule}:{place}";

    public static string RuinKey(int ruin, InheritanceChoice choice, string evidence = null) =>
        string.IsNullOrEmpty(evidence) ? $"ruin:{ruin}:{choice}" : $"ruin:{ruin}:{choice}:{evidence}";

    /// <summary>"tradition:evening-song" → ("tradition", "evening-song").</summary>
    public static (string kind, string id) Parent(string key)
    {
        if (string.IsNullOrEmpty(key)) return (null, null);
        if (key.StartsWith(TraditionPrefix)) return ("tradition", key.Substring(TraditionPrefix.Length));
        if (key.StartsWith(LocalPrefix)) return ("local", key.Substring(LocalPrefix.Length));
        return (null, key);
    }

    /// <summary>
    /// How many times a tradition definition is reworked: 0 for an original, 1 + the deeper parent's for a hybrid's
    /// result. A cycle among rules counts as too deep.
    /// </summary>
    public static int Depth(string definition, SyncretismTuning t, int guard = 0)
    {
        if (guard > 8) return int.MaxValue / 2;
        var rule = t?.HybridMaking(definition);
        if (rule == null) return 0;
        int Of(string key)
        {
            var p = Parent(key);
            return p.kind == "tradition" ? Depth(p.id, t, guard + 1) : 0;
        }
        return 1 + Math.Max(Of(rule.parentA), Of(rule.parentB));
    }

    // ===== MEETINGS =====

    // The presence of a parent at a place: kept there, else kept by the nation (every settlement shares the nation's own).
    private static ParentPresence At(IEnumerable<ParentPresence> presence, string key, int place) =>
        presence.Where(p => p != null && p.kept && p.key == key && p.settlement == place).OrderByDescending(p => p.sustained).FirstOrDefault()
        ?? presence.Where(p => p != null && p.kept && p.key == key && p.settlement == -1).OrderByDescending(p => p.sustained).FirstOrDefault();

    /// <summary>
    /// Whether the rule's two parents have met at <paramref name="place"/> and are kept long enough: null (with the
    /// evidence and the two presences) or why not.
    /// </summary>
    public static string WhyNotMet(HybridRule r, int place, IList<ParentPresence> presence, IList<ContactLink> links, Func<string, string> nameOf,
        out List<string> evidence, out ParentPresence a, out ParentPresence b, out string contact)
    {
        evidence = new List<string>();
        contact = null;
        presence = presence ?? new List<ParentPresence>();
        a = At(presence, r?.parentA, place);
        b = At(presence, r?.parentB, place);
        if (r == null) return "No such blend.";
        string nameA = nameOf?.Invoke(r.parentA) ?? r.parentA, nameB = nameOf?.Invoke(r.parentB) ?? r.parentB;
        if (a != null && b != null)
        {
            if (!a.sustained) return $"{nameA} is not kept long enough there yet to blend.";
            if (!b.sustained) return $"{nameB} is not kept long enough there yet to blend.";
            evidence.Add($"together:{place}");
            if (!string.IsNullOrEmpty(a.instance)) evidence.Add("tradition:" + a.instance);
            if (!string.IsNullOrEmpty(b.instance)) evidence.Add("tradition:" + b.instance);
            if (a.exchanged && !string.IsNullOrEmpty(a.provenance)) evidence.Add(a.provenance);
            if (b.exchanged && !string.IsNullOrEmpty(b.provenance)) evidence.Add(b.provenance);
            contact = place < 0 ? "both are kept by the nation" : "both are kept in the same community";
            return null;
        }
        // Kept apart: they meet only where an open road joins them and the records show something crossed it.
        if (place >= 0 && (a != null || b != null))
        {
            var here = a ?? b;
            string otherKey = a != null ? r.parentB : r.parentA;
            if (!here.sustained) return $"{(a != null ? nameA : nameB)} is not kept long enough there yet to blend.";
            foreach (var there in presence.Where(p => p != null && p.kept && p.key == otherKey && p.settlement >= 0 && p.settlement != place && p.sustained))
            {
                var link = links?.FirstOrDefault(l => l != null && ((l.a == place && l.b == there.settlement) || (l.b == place && l.a == there.settlement)));
                if (link == null || !link.open) continue;
                var crossed = presence.FirstOrDefault(p => p != null && p.exchanged
                    && ((p.settlement == place && p.fromSettlement == there.settlement) || (p.settlement == there.settlement && p.fromSettlement == place)));
                if (crossed == null) continue;
                evidence.Add($"exchange:{crossed.fromSettlement}->{crossed.settlement}:{crossed.key}");
                if (!string.IsNullOrEmpty(crossed.provenance)) evidence.Add(crossed.provenance);
                if (!string.IsNullOrEmpty(here.instance)) evidence.Add("tradition:" + here.instance);
                if (!string.IsNullOrEmpty(there.instance)) evidence.Add("tradition:" + there.instance);
                if (a == null) a = there; else b = there;
                contact = "they met along the road between them";
                return null;
            }
        }
        if (a == null && b == null) return $"Neither {nameA} nor {nameB} is kept there.";
        return $"{(a == null ? nameA : nameB)} and {(a == null ? nameB : nameA)} have never met: no community keeps both, and no open road with an exchange joins where they are kept.";
    }

    /// <summary>Why a rule is closed now (its Age, technology, result or depth), or null.</summary>
    public static string WhyNotRule(HybridRule r, int age, Func<string, bool> researched, Func<string, bool> resultExists, SyncretismTuning t)
    {
        if (r == null) return "No such blend.";
        if (!r.OpenIn(age)) return r.maxAge >= 0 && age > r.maxAge ? $"It belonged to Ages up to {AgeRules.Roman(r.maxAge)}." : $"It begins in Age {AgeRules.Roman(r.minAge)}.";
        if (!string.IsNullOrEmpty(r.technology) && !(researched?.Invoke(r.technology) ?? false)) return $"Needs {r.technology}.";
        if (resultExists != null && !resultExists(r.result)) return $"Its tradition '{r.result}' is not defined.";
        int depth = Depth(r.result, t);
        if (depth > Math.Max(1, t?.maxDepth ?? 2)) return $"It would be reworked {depth} times, more than {t?.maxDepth ?? 2}.";
        return null;
    }

    /// <summary>
    /// Every blend the people could decide on now: authored rules only, where the parents met and are kept, not decided
    /// yet (a side-by-side choice is offered again after a while), and whose new form is not kept there already.
    /// </summary>
    public static List<HybridOffer> Offers(SyncretismTuning t, IList<ParentPresence> presence, IList<ContactLink> links, int age,
        Func<string, bool> researched, Func<string, bool> resultExists, Func<string, int, bool> resultAt, SyncretismState s, int now,
        Func<string, string> nameOf, Func<int, string> placeName)
    {
        var offers = new List<HybridOffer>();
        if (t?.hybrids == null) return offers;
        presence = presence ?? new List<ParentPresence>();
        var places = new List<int> { -1 };
        places.AddRange(presence.Where(p => p != null && p.kept && p.settlement >= 0).Select(p => p.settlement).Distinct().OrderBy(x => x));
        foreach (var r in t.hybrids.Where(r => r != null))
        {
            if (WhyNotRule(r, age, researched, resultExists, t) != null) continue;
            foreach (int place in places)
            {
                if (WhyNotMet(r, place, presence, links, nameOf, out var evidence, out var a, out var b, out string contact) != null) continue;
                // Both kept only by the nation: offered once, to the nation.
                if (place >= 0 && a.settlement < 0 && b.settlement < 0) continue;
                if (resultAt != null && resultAt(r.result, place)) continue;
                if (!Open(s, OfferKey(r.id, place), now, t)) continue;
                offers.Add(new HybridOffer { rule = r, settlement = place, place = placeName?.Invoke(place), a = a, b = b, evidence = evidence, contact = contact });
            }
        }
        return offers;
    }

    // A meeting not decided yet, or kept side by side long enough ago to be offered again.
    private static bool Open(SyncretismState s, string key, int now, SyncretismTuning t)
    {
        var d = s?.Decision(key);
        if (d == null) return true;
        return d.mode == SyncretismMode.SideBySide && now - (d.stamp?.cultureSeventh ?? 0) >= Math.Max(1, t?.reofferSevenths ?? 63);
    }

    /// <summary>Why the people cannot decide <paramref name="mode"/> for <paramref name="o"/> now, or null; <paramref name="cost"/>: its Unity.</summary>
    public static string WhyNotMode(HybridOffer o, SyncretismMode mode, float unityHeld, SyncretismTuning t, Func<string, string> nameOf, out float cost)
    {
        cost = 0f;
        if (o?.rule == null) return "No such blend.";
        string nameA = nameOf?.Invoke(o.rule.parentA) ?? o.rule.parentA, nameB = nameOf?.Invoke(o.rule.parentB) ?? o.rule.parentB;
        switch (mode)
        {
            case SyncretismMode.SideBySide:
                return null;
            case SyncretismMode.Adapt:
                cost = Math.Max(0f, o.rule.adaptUnity);
                break;
            case SyncretismMode.Replace:
                foreach (var (p, name) in new[] { (o.a, nameA), (o.b, nameB) })
                {
                    if (p == null) continue;
                    if (p.settlement != o.settlement) return $"{name} is not kept here: only a form kept here can be replaced (blend a new form beside it instead).";
                    if (p.preserved) return $"{(o.settlement >= 0 ? $"The people of {o.place ?? "this place"}" : "Your people")} keep {name} as their own custom: they would keep it side by side, or blend a new form beside it, but not let it be replaced.";
                }
                cost = Math.Max(0f, o.rule.replaceUnity) + (o.a != null && o.a.recognized ? t?.recognizedPremium ?? 0f : 0f) + (o.b != null && o.b.recognized ? t?.recognizedPremium ?? 0f : 0f);
                break;
        }
        return cost > 0f && unityHeld + 1e-4f < cost ? $"Needs {cost:0} Unity ({unityHeld:0} held)." : null;
    }

    /// <summary>What a choice keeps and what it lets go, in words.</summary>
    public static (List<string> retained, List<string> lost) Outcome(HybridOffer o, SyncretismMode mode, Func<string, string> nameOf, string resultName)
    {
        var retained = new List<string>();
        var lost = new List<string>();
        if (o?.rule == null) return (retained, lost);
        string nameA = nameOf?.Invoke(o.rule.parentA) ?? o.rule.parentA, nameB = nameOf?.Invoke(o.rule.parentB) ?? o.rule.parentB;
        switch (mode)
        {
            case SyncretismMode.SideBySide:
                retained.Add($"{nameA}, as it was");
                retained.Add($"{nameB}, as it was");
                lost.Add($"nothing: {resultName} is not made (it may be offered again)");
                break;
            case SyncretismMode.Adapt:
                retained.Add($"{nameA} and {nameB} go on beside it");
                retained.Add($"{resultName}: {o.rule.fromA ?? nameA}, with {o.rule.fromB ?? nameB}");
                lost.Add($"nothing yet: {resultName} fades if it is not kept");
                break;
            case SyncretismMode.Replace:
                retained.Add($"{o.rule.fromA ?? nameA}, in {resultName}");
                retained.Add($"{o.rule.fromB ?? nameB}, in {resultName}");
                lost.Add($"{nameA} as a practice of its own here (its record stays)");
                lost.Add($"{nameB} as a practice of its own here (its record stays)");
                lost.Add("their own benefits here, unless kept elsewhere");
                break;
        }
        return (retained, lost);
    }

    // ===== RUINS =====

    /// <summary>What a ruin's records tell (nothing is invented: a record not kept is not guessed).</summary>
    public static RuinHeritage Read(RuinRecord r)
    {
        var h = new RuinHeritage { record = r };
        if (r == null)
        {
            h.unknown = true;
            h.summary = "Nothing is recorded of these ruins.";
            return h;
        }
        string E(string what) => $"ruin:{r.id}:{what}";
        string ages = r.ancient || r.foundedAge < 0 ? "of the Old World, fallen before the Ages"
            : $"founded in Age {AgeRules.Roman(r.foundedAge)}, fallen in Age {AgeRules.Roman(Math.Max(0, r.fallenAge))}{(r.fullAges > 0 ? $" after standing {r.fullAges} whole Age{(r.fullAges == 1 ? "" : "s")}" : string.Empty)}";
        h.clues.Add(new RuinClue { kind = RuinClueKind.Settlement, text = $"{(r.ancient ? "A city" : KindWord(r.kind))} {ages}.", evidence = E("settlement") });
        if (!string.IsNullOrEmpty(r.cause)) h.clues.Add(new RuinClue { kind = RuinClueKind.Fall, text = $"How it fell: {WorldRuins.CauseWords(r.cause)}.", evidence = E("fall") });
        if (!r.investigated)
        {
            h.lost.Add("Nothing of its people is read until an expedition investigates it.");
            h.summary = $"The ruins of {r.name} have not been investigated: what its people lived by is not known yet.";
            return h;
        }
        if (!string.IsNullOrEmpty(r.civic))
            h.clues.Add(new RuinClue
            {
                kind = RuinClueKind.Civic, family = r.civicFamily, evidence = E("civic"),
                text = $"Its people lived by {r.civic}{(r.civicFamily != null ? $" (the {r.civicFamily} ways)" : string.Empty)}{(r.civicAdopted ? "; it lives on in your government" : string.Empty)}.",
            });
        if (!string.IsNullOrEmpty(r.district))
            h.clues.Add(new RuinClue { kind = RuinClueKind.District, family = r.districtFamily, evidence = E("district"), text = $"Its people worked a {r.district} district{(r.districtFamily != null ? $" (the {r.districtFamily} ways)" : string.Empty)}." });
        if (!string.IsNullOrEmpty(r.binding))
        {
            h.clues.Add(new RuinClue { kind = RuinClueKind.Binding, evidence = E("binding"), text = $"It was attuned to {r.binding}." });
            h.lost.Add($"Its attunement to {r.binding}: a binding of magic is not a way of living, so it is remembered, not taken in.");
        }
        foreach (var p in r.practices ?? new List<(string, string, string, bool)>())
        {
            if (p.kept)
                h.clues.Add(new RuinClue { kind = RuinClueKind.Practice, family = p.family, evidence = E("practice:" + p.id), revivable = true, practice = p.id, text = $"Its people kept {p.name}." });
            else h.lost.Add($"They had only met {p.name}: nothing of it can be taken up from them.");
        }
        foreach (var t in r.traditions ?? new List<(string, string, string, string, bool)>())
            h.clues.Add(new RuinClue
            {
                kind = RuinClueKind.Tradition, family = t.family, evidence = E("tradition:" + t.instance), revivable = true, definition = t.definition,
                text = t.established ? $"{t.name} was a custom there." : $"{t.name} was only beginning there.",
            });
        foreach (var c in h.clues.Where(c => !string.IsNullOrEmpty(c.family)))
            if (!h.families.Contains(c.family)) h.families.Add(c.family);
        h.unknown = h.families.Count == 0 && !h.clues.Any(c => c.revivable);
        if (h.unknown)
        {
            if (!string.IsNullOrEmpty(r.groundFamily))
                h.clues.Add(new RuinClue { kind = RuinClueKind.Ground, family = r.groundFamily, evidence = E("ground"), text = $"Nothing tells who lived here; its ground only suggests the {r.groundFamily} ways (a guess)." });
            h.lost.Add("Who lived here, and how: no civic, district, custom or tradition of theirs was recorded.");
        }
        h.lost.Add("Whatever they kept that no record held.");
        if (!r.onMap) h.lost.Add("The ruins themselves are gone from the map: only what was recorded remains.");
        h.summary = h.unknown
            ? $"Nobody knows who lived in {r.name}: only an honest guess from its ground is offered, and a smaller one."
            : $"The records of {r.name} speak of {(h.families.Count == 0 ? "no way of living" : "the " + string.Join(", ", h.families) + " ways")}{(h.clues.Any(c => c.revivable) ? ", and of what its people kept" : string.Empty)}.";
        return h;
    }

    /// <summary>Why <paramref name="q"/> cannot be chosen for the ruin read as <paramref name="h"/>, or null (world and culture checks come after).</summary>
    public static string WhyNotInherit(RuinHeritage h, InheritanceRequest q, SyncretismState s)
    {
        var r = h?.record;
        if (r == null || q == null) return "No such ruins.";
        if (!r.investigated) return $"Investigate the ruins of {r.name} first: nothing of its people is known yet.";
        switch (q.choice)
        {
            case InheritanceChoice.PreserveCivic:
            case InheritanceChoice.ReplaceCivic:
                if (string.IsNullOrEmpty(r.civic)) return "The ruins left no civic.";
                if (r.civicAdopted) return $"{r.civic} was adopted from these ruins already.";
                if (!r.onMap) return "The ruins no longer stand: their civic cannot be adopted from them.";
                if (q.choice == InheritanceChoice.ReplaceCivic && string.IsNullOrEmpty(q.replacing)) return "Choose one of your civics for it to replace.";
                return null;
            case InheritanceChoice.AdaptWays:
                if (r.digested) return $"The ways of {r.name} were digested already: a ruin feeds one reform.";
                if (!r.onMap) return "The ruins no longer stand: their ways can no longer be digested (what they kept can still be taken up).";
                if (h.unknown) return $"Nothing of who lived in {r.name} is known: its ground only suggests a guess (read the stones).";
                if (string.IsNullOrEmpty(q.family)) return "Choose a way of living its records support.";
                if (!h.families.Any(f => string.Equals(f, q.family, StringComparison.OrdinalIgnoreCase)))
                    return h.families.Count == 0
                        ? $"The records of {r.name} speak of no way of living: take up what its people kept instead."
                        : $"Nothing in the records of {r.name} speaks of the {q.family} ways: they support {string.Join(", ", h.families)}.";
                return null;
            case InheritanceChoice.ReadTheStones:
                if (r.digested) return $"The ways of {r.name} were digested already: a ruin feeds one reform.";
                if (!r.onMap) return "The ruins no longer stand: there are no stones left to read.";
                if (!h.unknown) return $"The records of {r.name} are known: take in what they say instead of guessing.";
                if (string.IsNullOrEmpty(r.groundFamily)) return "Even its ground suggests nothing.";
                return null;
            case InheritanceChoice.Revive:
                var clue = h.clues.FirstOrDefault(c => c.revivable && c.evidence == q.evidence);
                if (clue == null) return "The records keep no such practice.";
                if (s?.Decision(RuinKey(r.id, InheritanceChoice.Revive, q.evidence)) != null) return "It was taken up from these ruins already.";
                if (clue.kind == RuinClueKind.Practice && q.settlement < 0) return "A custom is taken up by a settlement: choose one.";
                return null;
        }
        return "No such choice.";
    }

    /// <summary>What an inheritance choice keeps and lets go, in words.</summary>
    public static (List<string> retained, List<string> lost) Outcome(RuinHeritage h, InheritanceRequest q, string replacedFamily = null)
    {
        var retained = new List<string>();
        var lost = new List<string>();
        var r = h?.record;
        if (r == null || q == null) return (retained, lost);
        switch (q.choice)
        {
            case InheritanceChoice.PreserveCivic:
                retained.Add($"{r.civic}, as its people lived by it, beside your own civics");
                lost.Add("nothing of your own");
                break;
            case InheritanceChoice.ReplaceCivic:
                retained.Add($"{r.civic}, as its people lived by it");
                lost.Add($"{q.replacing} and everything it gave (its effects are removed){(replacedFamily != null ? $"; the {replacedFamily} ways it leant toward" : string.Empty)}");
                break;
            case InheritanceChoice.AdaptWays:
                retained.Add($"the {q.family} ways, as {r.name}'s records show them");
                lost.Add($"the rest of what {r.name} was{(h.families.Count > 1 ? $" (its {string.Join(", ", h.families.Where(f => !string.Equals(f, q.family, StringComparison.OrdinalIgnoreCase)))} ways stay in the ruins)" : string.Empty)}");
                break;
            case InheritanceChoice.ReadTheStones:
                retained.Add($"a guess: the {r.groundFamily} ways its ground suggests");
                lost.Add("who they really were: no record says");
                break;
            case InheritanceChoice.Revive:
                var clue = h.clues.FirstOrDefault(c => c.evidence == q.evidence);
                retained.Add(clue?.text ?? "what its people kept");
                lost.Add("the people who kept it, and how they kept it: it must be learned anew");
                break;
        }
        return (retained, lost);
    }

    /// <summary>Why an inherited or blended tradition cannot be renewed for this Age now, or null.</summary>
    public static string WhyNotRenew(VariantRecord v, int agePassesNow, SyncretismTuning t)
    {
        if (v == null) return "Only a tradition blended or taken up from ruins is renewed for a new Age.";
        if (agePassesNow <= v.agePasses) return v.renewals.Count > 0 ? "No Age has passed since it was last renewed." : "No Age has passed since it was made: it is already of this Age.";
        if (v.depth >= Math.Max(1, t?.maxDepth ?? 2)) return $"It has been reworked {v.depth} times: reworked again, nothing of what it was would remain (at most {t?.maxDepth ?? 2}).";
        return null;
    }

    public static string KindWord(SettlementKind kind) =>
        kind == SettlementKind.Tributary ? "A tributary" : kind == SettlementKind.Outpost ? "An outpost" : kind == SettlementKind.Town ? "A town"
        : kind == SettlementKind.Major ? "A major settlement" : kind == SettlementKind.Haven ? "A haven" : "A capital";

    public static string ModeWord(SyncretismMode m) => m == SyncretismMode.Adapt ? "blended beside them" : m == SyncretismMode.Replace ? "blended in their place" : "kept side by side";

    public static string ChoiceWord(InheritanceChoice c)
    {
        switch (c)
        {
            case InheritanceChoice.PreserveCivic: return "adopted its civic";
            case InheritanceChoice.ReplaceCivic: return "adopted its civic in place of one of theirs";
            case InheritanceChoice.AdaptWays: return "digested its ways";
            case InheritanceChoice.Revive: return "took up what its people kept";
            default: return "read its stones";
        }
    }
}
