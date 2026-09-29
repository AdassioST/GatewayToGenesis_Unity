using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Accounts panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body: public
/// memory and civic legitimacy (CultureSystem.PublicMemory.cs). The council links a practised tradition to a promise in
/// force; each link shows its account's versions, what the records say for and against, and how each place stands;
/// a dispute is answered here (or in its story): acknowledge, revise the promise, or a Legend's different account. It
/// only reads the culture's snapshots and calls its commands.
/// </summary>
public static class AccountsPanel
{
    // What the panel shows: a dispute, a link, the linking of one tradition, else the overview. Stale ids fall back.
    private static string _dispute, _link, _linking;
    private static bool _sponsors;

    /// <summary>Back to the overview (the window was opened afresh).</summary>
    public static void Reset()
    {
        _dispute = _link = _linking = null;
        _sponsors = false;
    }

    /// <summary>Open on one dispute (a notice's link).</summary>
    public static void Select(string dispute)
    {
        Reset();
        _dispute = dispute;
    }

    /// <summary>Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row (label, why not or null, tooltip, action).</summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        if (_dispute != null && culture.PublicDispute(_dispute) == null) _dispute = null;
        if (_link != null && culture.PublicLinks().All(l => l.id != _link)) _link = null;
        if (_linking != null && culture.Tradition(_linking) == null) _linking = null;
        if (_dispute != null) { Dispute(culture, header, choice); return; }
        if (_link != null) { Link(culture, header, choice); return; }
        if (_linking != null) { Linking(culture, header, choice); return; }
        Overview(culture, header, choice);
    }

    // ===== THE OVERVIEW =====

    private static void Overview(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var (points, terms) = culture.PublicAccord();
        header($"What your people commemorate can support your laws, or show the gap between what was promised and what was done. The council can say a practised tradition shows a promise in force (a stance of the Edicts, or a civic); the records of the last {culture.PublicMemoryTuning.window} Sevenths are then compared with it. Public memory's part of the council's accord: {points:+0;-0;0} (at most {culture.PublicMemoryTuning.accordCap:0} either way). An account is what is said; the records stay as they were.");
        foreach (var d in culture.OpenDisputes())
        {
            var dispute = d;
            choice($"{TooltipText.Warn(culture.PublicMemoryTuning.Dispute(dispute.spec)?.title ?? "A dispute")}: {dispute.traditionName} and {dispute.promiseName} {TooltipText.Muted($"(since Seventh {dispute.openedSeventh})")}", null, "The records and the promise disagree. Open it to answer.", () => _dispute = dispute.id);
        }
        foreach (var l in culture.PublicLinks().Where(l => l.Active))
        {
            var link = l;
            choice($"{link.traditionName} shows {link.promiseName} {TooltipText.Muted($"({Verdict(culture, link)}; account v{culture.CurrentAccount(link.id)?.version ?? 1})")}", null, LinkTip(culture, link), () => _link = link.id);
        }
        foreach (var t in culture.Traditions().Where(t => t.Lived && culture.PublicLinks().All(l => !(l.Active && l.tradition == t.id))))
        {
            var view = t;
            bool any = culture.PromiseOptions(view.id).Any(p => p.why == null);
            choice($"Say {view.name} shows a promise…", null, any ? "Choose a promise in force for the council to link it to." : "No promise can be linked to it now (open it to see why).", () => _linking = view.id);
        }
        foreach (var l in culture.PublicLinks().Where(l => !l.Active).OrderByDescending(l => l.endedSeventh).Take(4))
        {
            var link = l;
            choice(TooltipText.Muted($"{link.traditionName} and {link.promiseName}: {(link.status == LinkStatus.Withdrawn ? "revised" : "lapsed")} at Seventh {link.endedSeventh}"), null, LinkTip(culture, link), () => _link = link.id);
        }
        foreach (var d in culture.PublicDisputes().Where(d => !d.Open).OrderByDescending(d => d.resolvedSeventh).Take(4))
        {
            var dispute = d;
            choice(TooltipText.Muted($"{culture.PublicMemoryTuning.Dispute(dispute.spec)?.title ?? "A dispute"}: {PublicMemoryRules.ResolutionWord(dispute.resolution)} (Seventh {dispute.resolvedSeventh})"), null, dispute.outcome, () => _dispute = dispute.id);
        }
        if (terms.Count == 0 && !culture.PublicLinks().Any())
            choice(TooltipText.Muted("No tradition is said to show a promise yet."), null, "A promise needs the Edicts (a council of three) or a civic in force, and a tradition practised now.", () => { });
    }

    private static string Verdict(CultureSystem culture, PublicLink l)
    {
        if (l.last == null || l.last.seventh < 0) return "not compared yet";
        if (l.last.practiceQuiet) return "the practice lies dormant";
        return l.last.Gap(culture.PublicMemoryTuning) ? $"the records disagree: {l.last.contradicts} against, {l.last.supports} for"
            : l.last.Kept ? $"borne out: {l.last.supports} for, {l.last.contradicts} against" : $"unclear: {l.last.supports} for, {l.last.contradicts} against";
    }

    // ===== LINKING =====

    private static void Linking(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var view = culture.Tradition(_linking);
        header($"Which promise does {view.name} show? The council says it, for {culture.PublicMemoryTuning.linkUnity:0} Unity: from then on the records are compared with the promise. Unity held: {culture.UnityHeld:0}.");
        choice("< All accounts", null, "Back to the accounts.", () => _linking = null);
        foreach (var (promise, why) in culture.PromiseOptions(view.id))
        {
            var p = promise;
            string tip = $"\"{p.pledge}\"\n{TooltipText.Row("In force through", p.SourceText)}\n{TooltipText.Row("Compared with", p.kind == PromiseKind.Hospitality ? "the tables set, the people admitted, the luxuries held back" : "the dedications to the losses remembered, and whether they are kept")}\n{TooltipText.Muted($"{CanonWord(p.canon)}: {p.canonNote}")}";
            if (why == null) tip = culture.PreviewLinkPromise(view.id, p.id).reason + "\n" + tip;
            choice($"{p.name} {TooltipText.Muted($"({p.SourceText})")}", why, tip, () =>
            {
                var result = culture.LinkPromise(view.id, p.id);
                if (result.succeeded) { _linking = null; _link = culture.PublicLinks().LastOrDefault(l => l.Active && l.tradition == view.id)?.id; }
            });
        }
    }

    // ===== A LINK =====

    private static void Link(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var l = culture.PublicLinks().First(x => x.id == _link);
        var (comparison, places) = culture.CompareNow(l.id);
        var text = new StringBuilder($"{l.traditionName} and {l.promiseName}: {(l.Active ? Verdict(culture, l) : l.endedReason)}.");
        text.Append('\n').Append(TooltipText.Heading("What is said", "versions of the account"));
        foreach (var a in culture.AccountsOf(l.id)) text.Append('\n').Append(TooltipText.Bullet($"v{a.version} {TooltipText.Muted($"({KindWord(a.kind)}, {a.author}, Seventh {a.seventh})")}: {a.text}"));
        if (l.Active) Records(text, comparison.actions, places);
        header(text.ToString());
        choice("< All accounts", null, "Back to the accounts.", () => _link = null);
        var open = culture.OpenDisputes().FirstOrDefault(d => d.link == l.id);
        if (open != null) choice(TooltipText.Warn("Answer the dispute"), null, "The records and the promise disagree.", () => { _link = null; _dispute = open.id; });
    }

    // ===== A DISPUTE =====

    private static void Dispute(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var d = culture.PublicDispute(_dispute);
        var spec = culture.PublicMemoryTuning.Dispute(d.spec);
        var text = new StringBuilder($"{TooltipText.Value(spec?.title ?? "A dispute")}: {PublicMemoryRules.Words(spec?.summary, d)}");
        if (!d.Open) text.Append('\n').Append(TooltipText.Row(Capital(PublicMemoryRules.ResolutionWord(d.resolution)), $"{d.outcome} (Seventh {d.resolvedSeventh}{(d.paid.Count > 0 ? $", {string.Join(", ", d.paid.Select(p => $"{p.amount:0} {p.resource}"))} paid" : string.Empty)})"));
        Records(text, d.records, d.places);
        if (spec != null) text.Append('\n').Append(TooltipText.Muted($"{CanonWord(spec.canon)}: {spec.canonNote}"));
        header(text.ToString());
        choice("< All accounts", null, "Back to the accounts.", () => { _dispute = null; _sponsors = false; });
        if (!d.Open) return;
        if (_sponsors)
        {
            choice("< The answers", null, "Choose another answer.", () => _sponsors = false);
            foreach (var (legend, why) in culture.SponsorOptions())
            {
                string name = legend;
                string blocked = why ?? culture.WhyNotResolve(d.id, DisputeResolution.Sponsor, name);
                choice($"{name} tells it another way", blocked, blocked == null ? culture.PreviewResolve(d.id, DisputeResolution.Sponsor, name).reason : null, () => { if (culture.ResolveDispute(d.id, DisputeResolution.Sponsor, name).succeeded) _sponsors = false; });
            }
            return;
        }
        Answer(culture, choice, d, DisputeResolution.Acknowledge, "Acknowledge the gap");
        Answer(culture, choice, d, DisputeResolution.Revise, "Revise the promise");
        string sponsorWhy = culture.SponsorOptions().Any(s => s.why == null) ? null : "No Legend is here to sponsor another account.";
        choice("Let a Legend tell it another way…", sponsorWhy, "A Legend sponsors a different account. The records stay as they are, and places whose records disagree doubt it.", () => _sponsors = true);
    }

    private static void Answer(CultureSystem culture, Action<string, string, string, Action> choice, PublicDispute d, DisputeResolution r, string label)
    {
        string why = culture.WhyNotResolve(d.id, r);
        choice(label, why, why == null ? culture.PreviewResolve(d.id, r).reason : null, () => culture.ResolveDispute(d.id, r));
    }

    // The records for and against, and each place, told apart from what is said.
    private static void Records(StringBuilder text, IEnumerable<RecordedAction> actions, IEnumerable<LocalSupport> places)
    {
        var list = actions.ToList();
        text.Append('\n').Append(TooltipText.Heading("What was recorded", "never changed by an account"));
        if (list.Count == 0) text.Append('\n').Append(TooltipText.Muted("Nothing relevant is recorded in the window."));
        foreach (var a in list.Where(a => a.bearing == RecordBearing.Contradicts)) text.Append('\n').Append(TooltipText.Bullet($"{TooltipText.Bad("against")} {a.text}"));
        foreach (var a in list.Where(a => a.bearing == RecordBearing.Supports)) text.Append('\n').Append(TooltipText.Bullet($"{TooltipText.Good("for")} {a.text}"));
        foreach (var a in list.Where(a => a.bearing == RecordBearing.Silent)) text.Append('\n').Append(TooltipText.Bullet(TooltipText.Muted(a.text)));
        var shown = places.Where(p => p.stance != LocalStance.NoRecord).ToList();
        if (shown.Count > 0)
        {
            text.Append('\n').Append(TooltipText.Heading("Place by place", "by what was recorded there"));
            foreach (var p in shown) text.Append('\n').Append(TooltipText.Bullet($"{TooltipText.Value(p.place)} {PublicMemoryRules.StanceWord(p.stance)}: {p.reason}"));
        }
        text.Append('\n').Append(TooltipText.Muted("Places differ in their customs; that alone is never read as discontent. A place with nothing recorded has no opinion assumed."));
    }

    // ===== THE WINDOW'S BODY =====

    /// <summary>The Accounts section of the window's body (nothing before any link).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        var links = culture.PublicLinks();
        if (links.Count == 0) return;
        var (points, terms) = culture.PublicAccord();
        text.AppendLine(TooltipText.Heading("Public memory", $"{points:+0;-0;0} to the council's accord"));
        foreach (var d in culture.OpenDisputes()) text.AppendLine(TooltipText.Bullet(TooltipText.Warn($"{culture.PublicMemoryTuning.Dispute(d.spec)?.title}: {d.traditionName} and {d.promiseName}")));
        foreach (var l in links.Where(l => l.Active)) text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(l.traditionName)} shows {l.promiseName} ({Verdict(culture, l)})"));
        foreach (var (reason, value) in terms) text.AppendLine(TooltipText.Row($"{value:+0;-0}", reason));
        text.AppendLine(TooltipText.Muted("An account is what is said about a promise; what was recorded stays as it was, whatever is said."));
        text.AppendLine();
    }

    // ===== WORDS =====

    private static string LinkTip(CultureSystem culture, PublicLink l)
    {
        var a = culture.CurrentAccount(l.id);
        return $"{a?.text}\n{TooltipText.Row("Since", $"Seventh {l.linkedSeventh}")}\n{TooltipText.Row("Records", l.last != null ? $"{l.last.supports} for, {l.last.contradicts} against" : "not compared yet")}";
    }

    private static string KindWord(AccountKind k) =>
        k == AccountKind.Official ? "the council's word" : k == AccountKind.Acknowledged ? "acknowledged" : k == AccountKind.Revised ? "revised" : "sponsored";

    private static string CanonWord(CanonStatus c) =>
        c == CanonStatus.ExplicitCanon ? "Explicit canon" : c == CanonStatus.CanonSupported ? "Canon-supported adaptation" : "New game rule (proposal)";

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
