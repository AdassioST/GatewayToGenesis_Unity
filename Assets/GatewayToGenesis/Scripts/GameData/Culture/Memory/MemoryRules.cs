using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The memory's rules, with no scene state (tested in <c>CultureMemoryTests</c>): learning a cause once, dedicating a
/// practice to it, keeping it, the recovery kept memories support (capped: a loss is never a currency), dedications
/// sleeping and waking with what they were made through, the lineage carried over an Age passage, and the ecology of a
/// memorial place read as it is. <see cref="CultureSystem"/> (CultureSystem.Memory.cs) feeds it the world.
///
/// Remembrance is not suffering: nothing here strains a settlement or feeds a bloom. A memorial garden is only as alive
/// as the blooms that actually grow there by their own needs.
/// </summary>
public static class MemoryRules
{
    // ===== IDS =====

    public static string CrisisId(string ageId, int pass) => $"crisis:{ageId}:{pass}";
    public static string FallId(int ruin) => $"fall:{ruin}";
    public static string RecoveredId(int ruin) => $"recovered:{ruin}";
    public static string LegendId(string legend) => $"legend-lost:{legend}";
    public static string LineageId(string ageId, int pass) => $"lineage:{ageId}:{pass}";

    // ===== LEARNING A CAUSE =====

    /// <summary>
    /// Keep <paramref name="evidence"/> unless a cause with its id is already remembered (a reload, a second report, a
    /// reconstruction of something already witnessed). Returns the new record, or null when it was already known. The
    /// record already kept is never changed.
    /// </summary>
    public static MemoryEvidence Learn(MemoryState state, MemoryEvidence evidence)
    {
        if (state == null || evidence == null || string.IsNullOrEmpty(evidence.id)) return null;
        if (state.Evidence(evidence.id) != null) return null;
        state.evidence.Add(evidence);
        return evidence;
    }

    public static MemoryEvidence Crisis(string ageId, string ageTitle, string crisis, int pass, int deaths, int survivors, CultureStamp now, bool witnessed)
    {
        string lost = deaths > 0 ? $"{deaths} of the people did not live through it; {survivors} did." : "The people lived through it.";
        return new MemoryEvidence
        {
            id = CrisisId(ageId, pass), cause = MemoryCause.Crisis, title = crisis, subject = crisis,
            text = $"The crisis of {ageTitle ?? ageId}. {lost}",
            source = CultureEntityRef.Of(CultureEntityKind.Age, ageId, ageTitle), deaths = Math.Max(0, deaths), occurredAge = ageId,
            occurred = witnessed ? now?.Copy() : null, recorded = now?.Copy(), reconstructed = !witnessed,
        };
    }

    public static MemoryEvidence Fall(int ruin, string name, string causeWords, int cell, string fallenAge, CultureStamp now, bool witnessed)
    {
        return new MemoryEvidence
        {
            id = FallId(ruin), cause = MemoryCause.SettlementFall, title = $"The fall of {name}", subject = name,
            text = string.IsNullOrEmpty(causeWords) ? $"{name} Surrendered and fell into ruins." : $"{name} Surrendered and fell into ruins, {causeWords}.",
            source = CultureEntityRef.Of(CultureEntityKind.Ruin, ruin.ToString(System.Globalization.CultureInfo.InvariantCulture), name),
            cell = cell, occurredAge = fallenAge, occurred = witnessed ? now?.Copy() : null, recorded = now?.Copy(), reconstructed = !witnessed,
        };
    }

    public static MemoryEvidence Recovered(int ruin, string name, string reclaimedBy, int cell, CultureStamp now, bool witnessed)
    {
        return new MemoryEvidence
        {
            id = RecoveredId(ruin), cause = MemoryCause.RuinRecovered, title = $"{name} reclaimed", subject = name,
            text = string.IsNullOrEmpty(reclaimedBy) ? $"The ruins of {name} were settled again." : $"{reclaimedBy} rose on the ruins of {name}: what was lost was reclaimed.",
            source = CultureEntityRef.Of(CultureEntityKind.Ruin, ruin.ToString(System.Globalization.CultureInfo.InvariantCulture), name),
            related = FallId(ruin), cell = cell, occurredAge = now?.ageId, occurred = witnessed ? now?.Copy() : null, recorded = now?.Copy(), reconstructed = !witnessed,
        };
    }

    public static MemoryEvidence LegendLost(string legend, CultureStamp now, bool witnessed)
    {
        return new MemoryEvidence
        {
            id = LegendId(legend), cause = MemoryCause.LegendLost, title = $"{legend}, lost to Dissonance", subject = legend,
            text = $"{legend}'s Soul Leitmotif reached Surrender. They never came home.",
            source = CultureEntityRef.Of(CultureEntityKind.Legend, legend, legend), occurredAge = witnessed ? now?.ageId : null,
            occurred = witnessed ? now?.Copy() : null, recorded = now?.Copy(), reconstructed = !witnessed, worldTruthCandidate = true,
        };
    }

    // ===== DEDICATIONS =====

    /// <summary>The dedication that holds <paramref name="target"/> now (active or dormant), or null.</summary>
    public static MemorialDedication Holding(MemoryState state, CultureEntityRef target) =>
        state?.dedications.FirstOrDefault(d => d != null && d.status != DedicationStatus.Released && d.target != null && d.target.Same(target));

    /// <summary>
    /// Why <paramref name="target"/> cannot be dedicated to <paramref name="evidenceId"/>, or null. <paramref name="exists"/>
    /// says whether the dish, landmark or observance exists now (only existing practices are dedicated).
    /// </summary>
    public static string WhyNotDedicate(MemoryState state, string evidenceId, CultureEntityRef target, bool exists, MemoryTuning tuning)
    {
        tuning = tuning ?? new MemoryTuning();
        var evidence = state?.Evidence(evidenceId);
        if (evidence == null) return "Nothing of that is remembered.";
        if (target == null || !target.IsKnown) return "Choose a dish, a landmark or an observance.";
        if (!Dedicable(target.kind)) return "Only a dish, a landmark, a rite or a holiday can carry a memory.";
        if (!exists) return $"{target.Display} does not exist now: only a practice the people already have can be dedicated.";
        var held = Holding(state, target);
        if (held != null)
        {
            var other = state.Evidence(held.evidence);
            return held.evidence == evidenceId ? $"{target.Display} already remembers {evidence.title}." : $"{target.Display} already remembers {other?.title ?? "another loss"}: release it first.";
        }
        int count = state.DedicationsOf(evidenceId).Count(d => d.status != DedicationStatus.Released);
        if (count >= tuning.maxDedicationsPerWound) return $"{evidence.title} is already kept through {count} practices.";
        return null;
    }

    public static bool Dedicable(CultureEntityKind kind) =>
        kind == CultureEntityKind.Recipe || kind == CultureEntityKind.Landmark || kind == CultureEntityKind.Activity || kind == CultureEntityKind.Holiday;

    /// <summary>Dedicate <paramref name="target"/> to a remembered cause: a lasting link by id. Null (nothing changed) when it cannot be.</summary>
    public static MemorialDedication Dedicate(MemoryState state, string evidenceId, CultureEntityRef target, bool exists, CultureStamp now, MemoryTuning tuning)
    {
        if (WhyNotDedicate(state, evidenceId, target, exists, tuning) != null) return null;
        var d = new MemorialDedication
        {
            id = $"ded-{state.nextDedication++}", evidence = evidenceId, target = target.Copy(), dedicated = now?.Copy(), status = DedicationStatus.Active,
        };
        if (!string.IsNullOrEmpty(now?.ageId)) d.ages.Add(now.ageId);
        state.dedications.Add(d);
        return d;
    }

    /// <summary>The people release a dedication: its record stays, it no longer carries the memory. False when there was none to release.</summary>
    public static bool Release(MemoryState state, string dedicationId)
    {
        var d = state?.Dedication(dedicationId);
        if (d == null || d.status == DedicationStatus.Released) return false;
        d.status = DedicationStatus.Released;
        d.dormantReason = null;
        return true;
    }

    /// <summary>
    /// Put to sleep the dedications whose practice is gone, and wake those whose practice is back (<paramref name="exists"/>
    /// answers for a target; <paramref name="why"/> says why it is gone). Nothing is deleted. Returns those that changed.
    /// </summary>
    public static List<MemorialDedication> Settle(MemoryState state, Func<CultureEntityRef, bool> exists, Func<CultureEntityRef, string> why = null)
    {
        var changed = new List<MemorialDedication>();
        if (state == null || exists == null) return changed;
        foreach (var d in state.dedications.Where(d => d != null && d.status != DedicationStatus.Released))
        {
            bool here = exists(d.target);
            if (here && d.status == DedicationStatus.Dormant) { d.status = DedicationStatus.Active; d.dormantReason = null; changed.Add(d); }
            else if (!here && d.status == DedicationStatus.Active) { d.status = DedicationStatus.Dormant; d.dormantReason = why?.Invoke(d.target) ?? $"{d.target.Display} is gone."; changed.Add(d); }
        }
        return changed;
    }

    // ===== KEEPING A MEMORY =====

    /// <summary>
    /// Why a quiet remembrance of <paramref name="evidenceId"/> cannot be kept now, or null. Ordinary mourning asks for
    /// no feast, no Unity and no high morale: only that the people know the loss and have not just kept it.
    /// </summary>
    public static string WhyNotQuiet(MemoryState state, string evidenceId, bool founded, int seventh, MemoryTuning tuning)
    {
        tuning = tuning ?? new MemoryTuning();
        if (!founded) return "The culture has not been founded yet.";
        var evidence = state?.Evidence(evidenceId);
        if (evidence == null) return "Nothing of that is remembered.";
        var p = state.PracticeOf(evidenceId);
        if (p != null && p.lastQuiet >= 0)
        {
            int wait = p.lastQuiet + Math.Max(1, tuning.quietCooldownSevenths) - seventh;
            if (wait > 0) return $"{evidence.title} was remembered not long ago: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        }
        return null;
    }

    /// <summary>
    /// The people kept the memory of <paramref name="evidenceId"/> this Seventh (<paramref name="how"/>). It counts once
    /// per Seventh however many practices kept it. True when this was the first time this Seventh.
    /// </summary>
    public static bool Keep(MemoryState state, string evidenceId, int seventh, string how, bool quiet = false)
    {
        if (state?.Evidence(evidenceId) == null) return false;
        var p = state.PracticeOf(evidenceId);
        if (p == null) state.practice.Add(p = new MemoryPractice { evidence = evidenceId });
        if (quiet) p.lastQuiet = seventh;
        if (p.last == seventh) return false;
        p.kept++;
        p.last = seventh;
        p.lastHow = how;
        return true;
    }

    /// <summary>A dedication was practised this Seventh (its dish cooked, its rite held, its holiday kept). Once per Seventh; only while it is active.</summary>
    public static bool Practise(MemoryState state, MemorialDedication d, int seventh)
    {
        if (state == null || d == null || !d.Counts) return false;
        bool first = d.lastPracticed != seventh;
        if (first) { d.practiced++; d.lastPracticed = seventh; }
        Keep(state, d.evidence, seventh, d.target?.Display);
        return first;
    }

    /// <summary>The active dedication through <paramref name="target"/>, or null (dormant and released ones carry nothing).</summary>
    public static MemorialDedication ActiveFor(MemoryState state, CultureEntityRef target) =>
        state?.dedications.FirstOrDefault(d => d != null && d.Counts && d.target != null && d.target.Same(target));

    /// <summary>The causes remembered within the window before <paramref name="seventh"/> (the loss alone counts for nothing).</summary>
    public static List<string> Kept(MemoryState state, int seventh, MemoryTuning tuning)
    {
        tuning = tuning ?? new MemoryTuning();
        if (state == null) return new List<string>();
        return state.practice.Where(p => p != null && p.last >= 0 && seventh - p.last < Math.Max(1, tuning.practiceWindowSevenths) && state.Evidence(p.evidence) != null)
            .Select(p => p.evidence).Distinct().ToList();
    }

    /// <summary>Morale the kept memories support now: per kept wound, never above the cap however many were lost.</summary>
    public static int Recovery(MemoryState state, int seventh, MemoryTuning tuning)
    {
        tuning = tuning ?? new MemoryTuning();
        int kept = Kept(state, seventh, tuning).Count;
        return Math.Max(0, Math.Min(tuning.moraleCap, kept * Math.Max(0, tuning.moralePerWound)));
    }

    // ===== THE PASSAGE OF AN AGE =====

    /// <summary>
    /// The Age passed (<paramref name="pass"/>: its place in the Age history): the causes, the dedications as they stood and
    /// the practices are kept as its lineage, and every living dedication goes on into <paramref name="nextAgeId"/>
    /// (inherited, awaiting recognition). Once per passage: null when this passage was already kept.
    /// </summary>
    public static MemoryLineage Pass(MemoryState state, string ageId, string ageTitle, string nextAgeId, string nextAgeTitle, int pass, CultureStamp now, IEnumerable<CultureEntityRef> practices)
    {
        if (state == null) return null;
        string id = LineageId(ageId, pass);
        if (state.lineage.Any(l => l != null && l.id == id)) return null;
        var lineage = new MemoryLineage
        {
            id = id, ageId = ageId, ageTitle = ageTitle, nextAgeId = nextAgeId, nextAgeTitle = nextAgeTitle, pass = pass, stamp = now?.Copy(),
            evidence = state.evidence.Where(e => e != null).Select(e => e.id).ToList(),
            dedications = state.dedications.Where(d => d != null).Select(d => d.Copy()).ToList(),
            practices = (practices ?? Enumerable.Empty<CultureEntityRef>()).Where(p => p != null && p.IsKnown).GroupBy(p => p.Key).Select(g => g.First().Copy()).ToList(),
        };
        state.lineage.Add(lineage);
        if (!string.IsNullOrEmpty(nextAgeId))
            foreach (var d in state.dedications.Where(d => d != null && d.status != DedicationStatus.Released))
            {
                if (!d.ages.Contains(nextAgeId)) d.ages.Add(nextAgeId);
                d.inherited = true;
                d.recognizedAge = null;
            }
        return lineage;
    }

    /// <summary>Why <paramref name="d"/> cannot be recognized as this Age's inheritance now, or null.</summary>
    public static string WhyNotRecognize(MemorialDedication d, string ageId)
    {
        if (d == null) return "No such dedication.";
        if (!d.inherited) return "It was made in this Age: there is nothing to inherit yet.";
        if (d.status == DedicationStatus.Released) return "It was released.";
        if (!string.IsNullOrEmpty(d.recognizedAge) && d.recognizedAge == ageId) return "This Age has recognized it already.";
        return null;
    }

    /// <summary>The player recognizes an inherited dedication as this Age's own (a record only: nothing is canonized). False when it cannot be.</summary>
    public static bool Recognize(MemorialDedication d, string ageId)
    {
        if (WhyNotRecognize(d, ageId) != null) return false;
        d.recognizedAge = ageId;
        return true;
    }

    // ===== THE MEMORIAL'S GROUND =====

    /// <summary>
    /// The blooms living around <paramref name="cell"/> and whether each thrives there by its own needs (residue, Coherence,
    /// vigor). Read only: the world is not changed and no bloom is fed. A place without a suitable bloom is no garden.
    /// </summary>
    public static MemorialGround Ground(WorldMap map, WorldGenSettings settings, int cell, string place, MemoryTuning tuning)
    {
        tuning = tuning ?? new MemoryTuning();
        var ground = new MemorialGround { cell = cell, place = place };
        if (map == null || settings == null || cell < 0 || cell >= map.Count) return ground;
        var centre = map[cell];
        var near = map.Settlements?.Where(s => s != null).OrderBy(s => HexCoord.Distance(s.coord, centre.coord)).ThenBy(s => s.id).FirstOrDefault();
        if (near != null && HexCoord.Distance(near.coord, centre.coord) <= Math.Max(1, tuning.gardenReach)) ground.seedSource = WorldResources.SeedSource(map, settings, near);
        foreach (var site in map.ResourceSites ?? new List<ResourceSite>())
        {
            if (site == null || site.kind != ResourceKind.Bloom || site.center < 0 || site.center >= map.Count) continue;
            int distance = site.cells.Where(c => c >= 0 && c < map.Count).Select(c => HexCoord.Distance(map[c].coord, centre.coord)).DefaultIfEmpty(int.MaxValue).Min();
            if (distance > Math.Max(0, tuning.gardenReach)) continue;
            var spec = settings.ResourceSite(site.spec);
            var t = map[site.center];
            var bloom = new MemorialBloom { site = site.index.ToString(System.Globalization.CultureInfo.InvariantCulture), spec = site.spec, name = site.name ?? spec?.name ?? site.spec, distance = distance, vigor = site.vigor, residue = t.residue, need = spec?.residueNeed ?? 0f };
            if (spec == null) { bloom.why = "its kind is not known"; ground.blooms.Add(bloom); continue; }
            bloom.griefLinked = spec.drawnBy == BloomDraw.Suffering || spec.minimumHurt > 0f || spec.maximumHurt < 1f || spec.turnHurt > 0f;
            float coherence = t.coherence - t.siteCoherence;
            if (site.vigor < tuning.gardenVigor) bloom.why = $"it is withering (vigor {site.vigor:P0})";
            else if (t.residue + 1e-4f < spec.residueNeed) bloom.why = $"the feeling here is too faint for it ({t.residue:0.00} of {spec.residueNeed:0.00})";
            else if (coherence < spec.minimumCoherence || coherence > spec.maximumCoherence) bloom.why = "the ground's Coherence does not suit it";
            else { bloom.thriving = true; bloom.why = "its own needs are met here"; }
            ground.blooms.Add(bloom);
        }
        ground.blooms.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance) : string.CompareOrdinal(a.spec, b.spec));
        return ground;
    }
}
