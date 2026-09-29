using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Memorials panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body: the losses
/// the people remember (<see cref="CultureSystem"/>, CultureSystem.Memory.cs), what keeps each one, a quiet remembrance,
/// dedicating an existing dish, landmark, rite or holiday, recognizing what an Age passage handed on, and the ground of a
/// memorial place with the blooms that actually live there. It only calls the culture's commands and reads its snapshots.
/// </summary>
public static class MemorialPanel
{
    // The cause open in the panel (null: the list of causes). A stale id after a load simply shows the list.
    private static string _selected;

    /// <summary>Forget the open cause (the window was opened afresh).</summary>
    public static void Reset() => _selected = null;

    /// <summary>
    /// Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row
    /// (label, why not or null, tooltip, action).
    /// </summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        var causes = culture.MemoryCauses();
        var tuning = culture.MemoryTuning;
        int morale = culture.RemembranceMorale;
        string recovery = morale > 0 ? $" Kept memories support {TooltipText.Good($"+{morale} morale")} now (at most +{tuning.moraleCap}, however many were lost)." : $" A memory kept within {tuning.practiceWindowSevenths} Sevenths supports +{tuning.moralePerWound} morale (at most +{tuning.moraleCap} for all).";
        var evidence = _selected != null ? causes.FirstOrDefault(e => e.id == _selected) : null;
        if (evidence == null)
        {
            _selected = null;
            header($"What {CultureSystem.PeopleWord} remember of what they lost: a crisis lived through, a settlement fallen or reclaimed, a Legend who never came home. Choose one to keep it.{recovery}");
            if (causes.Count == 0) { choice(TooltipText.Muted("Nothing lost is remembered yet."), null, "Crises, fallen settlements, reclaimed ruins and Legends lost to Dissonance are remembered as they happen.", () => { }); return; }
            foreach (var e in causes.OrderByDescending(e => e.recorded?.cultureSeventh ?? 0))
            {
                var cause = e;
                choice($"{cause.title} {TooltipText.Muted($"({Kind(cause.cause)}; {Kept(culture, cause)})")}", null, Tip(culture, cause), () => _selected = cause.id);
            }
            return;
        }

        header($"{evidence.title}: {evidence.text}{(evidence.reconstructed ? " " + TooltipText.Muted("(learned from older records: when it happened is not known)") : string.Empty)}{recovery}");
        choice("< All memories", null, "Back to the list of what the people remember.", () => _selected = null);
        string quiet = culture.WhyNotRemembrance(evidence.id);
        choice($"Keep a quiet remembrance {TooltipText.Muted("(free: no feast, no Unity, no morale needed)")}", quiet,
            "The names are spoken, nothing is spent. It keeps the memory for this Seventh; mourning never waits for a good harvest.", () => culture.KeepRemembrance(evidence.id));

        foreach (var d in culture.DedicationsOf(evidence.id))
        {
            var dedication = d;
            string label = culture.MemoryTargetLabel(d.target);
            string state = d.status == DedicationStatus.Active ? $"kept {d.practiced} time{(d.practiced == 1 ? "" : "s")}" : d.status == DedicationStatus.Dormant ? $"dormant: {d.dormantReason}" : "released";
            string renamed = !string.IsNullOrEmpty(d.target.label) && !string.Equals(d.target.label, label, StringComparison.Ordinal) ? $", dedicated as {d.target.label}" : string.Empty;
            if (d.status != DedicationStatus.Released)
                choice($"Release {label} {TooltipText.Muted($"({KindWord(d.target.kind)}; {state}{renamed})")}", null,
                    $"{label} carries this memory. Releasing it keeps its history; it no longer keeps the memory.", () => culture.ReleaseDedication(dedication.id));
            else choice(TooltipText.Muted($"{label} ({KindWord(d.target.kind)}; released)"), null, "Its history is kept.", () => { });
            if (d.inherited && d.status != DedicationStatus.Released)
            {
                string why = culture.WhyNotRecognize(d.id);
                choice($"Recognize {label} as this Age's inheritance {TooltipText.Muted($"(through {string.Join(", ", d.ages.Select(AgeTitle))})")}", why,
                    "An Age passed and this memorial came with it. Recognizing it records it as this Age's own; nothing is canonized (the Stellar Legacy's World Truths are not built yet).", () => culture.RecognizeInheritance(dedication.id));
            }
        }

        var ground = culture.MemorialGroundOf(evidence.id);
        choice(TooltipText.Muted(GroundLine(ground)), null, GroundTip(ground), () => { });

        var suggested = new HashSet<string>(culture.SuggestionsFor(evidence.id).Select(s => $"{s.kind}:{s.target}"), StringComparer.OrdinalIgnoreCase);
        foreach (var target in culture.DedicationCandidates().OrderByDescending(t => suggested.Contains(t.Key)).ThenBy(t => t.kind).ThenBy(t => t.Display, StringComparer.Ordinal))
        {
            var t = target;
            var suggestion = culture.SuggestionsFor(evidence.id).FirstOrDefault(s => string.Equals($"{s.kind}:{s.target}", t.Key, StringComparison.OrdinalIgnoreCase));
            string why = culture.WhyNotDedicate(evidence.id, t);
            string tip = $"{t.Display} ({KindWord(t.kind)}) will carry the memory of {evidence.title}: each Seventh it is kept (cooked, held, kept on its day, or a festival where it stands) keeps the memory. It makes nothing new and changes nothing of the {KindWord(t.kind)} itself."
                + (suggestion != null ? $"\n{TooltipText.Row("The vault", $"{suggestion.text} ({suggestion.StatusText}: {suggestion.source})")}" : string.Empty);
            choice($"Dedicate {t.Display} {TooltipText.Muted($"({KindWord(t.kind)}{(suggestion != null ? ", the vault's own memorial" : string.Empty)})")}", why, tip, () => culture.Dedicate(evidence.id, t));
        }
    }

    /// <summary>The Memorials section of the window's body (nothing when nothing is remembered).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        var causes = culture.MemoryCauses();
        if (causes.Count == 0) return;
        int morale = culture.RemembranceMorale;
        text.AppendLine(TooltipText.Heading("Memorials", morale > 0 ? $"+{morale} morale from remembrance" : null));
        foreach (var e in causes.OrderByDescending(e => e.recorded?.cultureSeventh ?? 0).Take(12))
        {
            var kept = culture.DedicationsOf(e.id).Where(d => d.status == DedicationStatus.Active).Select(d => culture.MemoryTargetLabel(d.target)).ToList();
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(e.title)} {TooltipText.Muted($"({Kind(e.cause)}{AgeText(e.occurredAge)})")}: {Kept(culture, e)}{(kept.Count > 0 ? $"; kept through {string.Join(", ", kept)}" : string.Empty)}"
                + (e.worldTruthCandidate ? TooltipText.Muted(" A future World Truth, should the Stellar Legacy canonize their story.") : string.Empty)));
        }
        var lineage = culture.MemoryLineages();
        if (lineage.Count > 0)
            text.AppendLine(TooltipText.Row("Inherited", string.Join("; ", lineage.Select(l => $"{l.ageTitle ?? l.ageId}: {l.dedications.Count(d => d.status != DedicationStatus.Released)} memorials, {l.practices.Count} practices"))));
        text.AppendLine(TooltipText.Muted("Remembrance is not suffering: keeping a memory (a quiet remembrance, or a dedicated dish cooked, rite held, holiday kept) supports a little morale for a Phase; a loss alone gives nothing, and many losses no more than a few. Memorials are carried into each new Age."));
        text.AppendLine();
    }

    // ===== WORDS =====

    private static string Kind(MemoryCause cause)
    {
        switch (cause)
        {
            case MemoryCause.Crisis: return "a crisis lived through";
            case MemoryCause.SettlementFall: return "a settlement fallen";
            case MemoryCause.RuinRecovered: return "ruins reclaimed";
            case MemoryCause.LegendLost: return "a Legend lost";
            default: return cause.ToString();
        }
    }

    private static string KindWord(CultureEntityKind kind)
    {
        switch (kind)
        {
            case CultureEntityKind.Recipe: return "dish";
            case CultureEntityKind.Activity: return "rite";
            case CultureEntityKind.Holiday: return "holiday";
            case CultureEntityKind.Landmark: return "landmark";
            default: return kind.ToString().ToLowerInvariant();
        }
    }

    private static string Kept(CultureSystem culture, MemoryEvidence e)
    {
        var p = culture.MemoryPracticeOf(e.id);
        if (p == null || p.last < 0) return "not yet kept";
        int ago = culture.Age - p.last;
        return $"kept {p.kept} time{(p.kept == 1 ? "" : "s")}, last {(ago <= 0 ? "this Seventh" : $"{ago} Seventh{(ago == 1 ? "" : "s")} ago")} ({p.lastHow})";
    }

    private static string Tip(CultureSystem culture, MemoryEvidence e)
    {
        var s = new StringBuilder(e.text);
        s.Append('\n').Append(TooltipText.Row("Remembered", e.reconstructed ? "from older records (its moment is not known)" : e.occurred != null ? e.occurred.ToString() : "unknown"));
        if (!string.IsNullOrEmpty(e.related)) s.Append('\n').Append(TooltipText.Row("Follows", culture.RememberedCause(e.related)?.title ?? e.related));
        if (e.deaths > 0) s.Append('\n').Append(TooltipText.Row("Lost", $"{e.deaths} people"));
        if (e.worldTruthCandidate) s.Append('\n').Append(TooltipText.Muted("The Stellar Legacy Score may one day canonize their story as a World Truth; nothing does so yet."));
        return s.ToString();
    }

    private static string GroundLine(MemorialGround ground)
    {
        if (ground == null) return "No memorial place: dedicate a landmark to give this memory a place (a fallen settlement's ruins are one already).";
        int thriving = ground.blooms.Count(b => b.thriving);
        if (ground.blooms.Count == 0) return $"At {ground.place}: no Eleos Bloom grows here{(ground.seedSource == null ? " and no Lumen Seeds reach it" : string.Empty)}. No garden, and no bonus.";
        return thriving > 0 ? $"At {ground.place}: a memorial garden of {string.Join(", ", ground.blooms.Where(b => b.thriving).Select(b => b.name).Distinct())}." : $"At {ground.place}: blooms grow near, but none lives well here.";
    }

    private static string GroundTip(MemorialGround ground)
    {
        var s = new StringBuilder("A memorial garden is only the blooms that actually live there by their own needs: grief does not conjure them, and a comforting memorial does not feed a bloom that needs sorrow. It gives no bonus yet (the garden is shown, not scored).");
        if (ground == null) return s.ToString();
        foreach (var b in ground.blooms)
            s.Append('\n').Append(TooltipText.Bullet($"{b.name} ({b.distance} cell{(b.distance == 1 ? "" : "s")} away, vigor {b.vigor:P0}): {b.why}{(b.griefLinked ? "; it is shaped by sorrow" : string.Empty)}"));
        s.Append('\n').Append(TooltipText.Row("Lumen Seeds", ground.seedSource ?? "none reach here yet"));
        return s.ToString();
    }

    private static string AgeTitle(string ageId) => GameCatalog.Ages.All.FirstOrDefault(a => a != null && a.id == ageId)?.title ?? ageId;

    private static string AgeText(string ageId) => string.IsNullOrEmpty(ageId) ? string.Empty : ", " + AgeTitle(ageId);
}
