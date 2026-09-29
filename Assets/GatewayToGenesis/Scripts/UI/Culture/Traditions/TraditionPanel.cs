using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Traditions panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body: the
/// practices the people keep (<see cref="CultureSystem"/>, CultureSystem.Traditions.cs), where each came from, who
/// carried it, whether it is emerging, practised, dormant or revived, and the choices waiting (recognise for the nation,
/// preserve as a local custom, decide later). It only reads <see cref="ICultureQuery"/> snapshots and calls the
/// culture's commands; nothing is decided or paid without a click.
/// </summary>
public static class TraditionPanel
{
    // The tradition open in the panel (null: the list). A stale id after a load simply shows the list.
    private static string _selected;

    /// <summary>Forget the open tradition (the window was opened afresh).</summary>
    public static void Reset() => _selected = null;

    /// <summary>
    /// Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row
    /// (label, why not or null, tooltip, action).
    /// </summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var all = query.Traditions();
        var open = _selected != null ? query.Tradition(_selected) : null;
        if (open == null)
        {
            _selected = null;
            var waiting = query.PendingTraditionChoices();
            header($"What {CultureSystem.PeopleWord} do again and again, somewhere, becomes theirs: a practice emerges, becomes a custom when kept over several Sevenths, lies dormant when no one keeps it, and can be revived."
                + (waiting.Count > 0 ? " " + TooltipText.Warn($"{waiting.Count} custom{(waiting.Count == 1 ? "" : "s")} await{(waiting.Count == 1 ? "s" : "")} your decision.") : string.Empty));
            if (all.Count == 0)
            {
                choice(TooltipText.Muted("No practice has emerged yet."), null,
                    "Hold Tales at the Hearth or An Evening of Song (Rites), send cultural parties to celebrate, bake a memorial bread: what is repeated becomes a tradition where it happens.", () => { });
                return;
            }
            foreach (var t in waiting.Concat(all.Where(t => !waiting.Any(w => w.id == t.id)).OrderBy(Order).ThenBy(t => t.name, StringComparer.Ordinal)))
            {
                var view = t;
                choice($"{view.name} {TooltipText.Muted($"({view.place}; {State(view)})")}", null, Tip(view), () => _selected = view.id);
            }
            return;
        }

        header($"{open.name}, {open.place}: {open.description} {TooltipText.Muted(open.explanation)}");
        choice("< All traditions", null, "Back to the list of traditions.", () => _selected = null);
        choice(TooltipText.Muted($"{Capital(State(open))}{(open.lastPracticed > 0 || open.participations > 0 ? $"; last kept at Seventh {open.lastPracticed}" : string.Empty)}"), null, Tip(open), () => { });
        if (!open.Lived || open.recognition != TraditionRecognition.Recognized)
        {
            var recognize = query.PreviewRecognizeTradition(open.id);
            string cost = open.recognitionCost > 0f ? $"{open.recognitionCost:0} Unity" : "free";
            choice($"Recognise it for the nation {TooltipText.Muted($"({cost})")}", recognize.succeeded ? null : recognize.reason,
                recognize.succeeded ? recognize.reason : "Its recognised benefit applies while it is kept; it is paid only when you choose it.", () => culture.RecognizeTradition(open.id));
            var preserve = query.PreviewPreserveTradition(open.id);
            choice($"Preserve it as a local custom {TooltipText.Muted("(free)")}", preserve.succeeded ? null : preserve.reason, preserve.succeeded ? preserve.reason : null, () => culture.PreserveTradition(open.id));
            var defer = query.PreviewDeferTradition(open.id);
            choice("Decide later", defer.succeeded ? null : defer.reason, defer.succeeded ? defer.reason : null, () => culture.DeferTradition(open.id));
        }
        foreach (var h in open.history.Reverse().Take(8))
            choice(TooltipText.Muted($"Seventh {h.seventh}: {h.text ?? TraditionRules.KindWord(h.kind)}"), null,
                h.actors != null && h.actors.Count > 0 ? "With " + string.Join(", ", h.actors) : "Who took part was not recorded.", () => { });
    }

    private static int Order(TraditionView t) => t.Lived ? 0 : t.stage == TraditionStage.Emerging ? 1 : 2;

    private static string State(TraditionView t)
    {
        string stage = TraditionRules.StageWord(t.stage);
        if (t.stage == TraditionStage.Emerging) return $"{stage}: {t.progress}";
        string decided = t.recognition == TraditionRecognition.None ? string.Empty : ", " + TraditionRules.RecognitionWord(t.recognition);
        return stage + decided + (t.benefitCapped ? ", benefits waiting (too many customs at once)" : string.Empty);
    }

    private static string Tip(TraditionView t)
    {
        var lines = new List<string>
        {
            t.description,
            TooltipText.Row("Began", (t.origin ?? "an occasion no one recorded") + (t.legacy ? TooltipText.Muted(" (before traditions were recorded)") : string.Empty)),
            TooltipText.Row("Kept", t.participations == 0 ? "no occasion recorded" : $"{t.participations} occasion{(t.participations == 1 ? "" : "s")} over {t.distinctSevenths} Seventh{(t.distinctSevenths == 1 ? "" : "s")}"),
            TooltipText.Row("Carried by", t.bearers.Length > 0 ? string.Join(", ", t.bearers) : TooltipText.Muted("not recorded")),
        };
        if (t.venues.Length > 0) lines.Add(TooltipText.Row("Held at", string.Join(", ", t.venues.Select(v => v.Display))));
        if (t.benefits.Length > 0) lines.Add(TooltipText.Row("Gives", string.Join("; ", t.benefits) + (t.benefitActive ? TooltipText.Good(" (now)") : TooltipText.Muted(" (not now)"))));
        if (t.revivals > 0) lines.Add(TooltipText.Row("Revived", $"{t.revivals} time{(t.revivals == 1 ? "" : "s")}"));
        lines.Add(TooltipText.Muted($"{CanonWord(t.canon)}{(string.IsNullOrEmpty(t.canonSource) ? string.Empty : ": " + t.canonSource)}"));
        return string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));
    }

    private static string CanonWord(CanonStatus c) => c == CanonStatus.ExplicitCanon ? "Explicit canon" : c == CanonStatus.CanonSupported ? "Canon-supported adaptation" : "New game rule (proposal)";

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>The Traditions section of the window's body (nothing before the first practice emerges).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var all = query.Traditions();
        if (all.Count == 0) return;
        int lived = all.Count(t => t.Lived);
        text.AppendLine(TooltipText.Heading("Traditions", $"{lived} kept, {all.Count - lived} emerging or dormant"));
        foreach (var t in all.OrderBy(Order).ThenByDescending(t => t.momentum).Take(10))
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(t.name)}, {t.place}: {State(t)} {TooltipText.Muted($"(began with {t.origin ?? "an occasion no one recorded"})")}"));
        var waiting = query.PendingTraditionChoices();
        if (waiting.Count > 0) text.AppendLine(TooltipText.Warn($"Waiting for your decision: {string.Join(", ", waiting.Select(w => w.name))} (Traditions below)."));
        var tuning = culture.TraditionTuning;
        text.AppendLine(TooltipText.Muted($"A practice kept in {tuning.establishSevenths} different Sevenths becomes a custom where it happened; one not kept for {tuning.lapseSevenths} Sevenths lies dormant (its benefits stop, its history stays). At most {tuning.maxActiveBenefits} customs give their benefits at once, however many places keep them."));
        text.AppendLine();
    }
}
