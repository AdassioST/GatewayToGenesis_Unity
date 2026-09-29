using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Heritage panel of the Culture window (<see cref="CultureWindow"/>, its Heritage button) and its body section
/// (T07): the ruins whose inheritance can be read, the blends the people could make where two traditions met, and the
/// inherited or blended traditions that can be renewed for a new Age. It reads snapshots and previews and calls the
/// culture's commands; every choice says what it keeps, what it lets go, and what it costs before it is made.
/// </summary>
public static class HeritagePanel
{
    private static int _ruin = -1;
    private static string _rule;
    private static int _place = int.MinValue;
    private static int _replace;
    private static readonly Dictionary<string, int> _destination = new Dictionary<string, int>();
    public static void Select(int ruin) { _ruin = ruin; _rule = null; }

    public static void Reset()
    {
        _ruin = -1;
        _rule = null;
        _place = int.MinValue;
        _replace = 0;
        _destination.Clear();
    }

    /// <summary>Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row (label, why not or null, tooltip, action).</summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        if (_rule != null) { FillBlend(culture, header, choice); return; }
        if (_ruin >= 0) { FillRuin(culture, header, choice); return; }

        header("What your people take from others: a fallen people's ways, read from what their ruins record, and new forms where two traditions truly met. "
            + "Nothing is taken that the records do not show; keeping two ways side by side is always possible.");
        foreach (var o in culture.HybridOffers())
        {
            var offer = o;
            string result = culture.TraditionTuning.Definition(offer.rule.result)?.name ?? offer.rule.result;
            choice($"Meeting in {offer.place}: {culture.ParentName(offer.rule.parentA)} and {culture.ParentName(offer.rule.parentB)} {TooltipText.Muted($"(could become {result})")}", null,
                $"{offer.contact}. {offer.rule.canonNote}", () => { _rule = offer.rule.id; _place = offer.settlement; });
        }
        foreach (var h in culture.RuinHeritages())
        {
            var heritage = h;
            string state = !heritage.record.investigated ? "not investigated" : heritage.record.digested ? "digested" : heritage.unknown ? "nobody knows who lived there" : string.Join(", ", heritage.families);
            choice($"The ruins of {heritage.record.name} {TooltipText.Muted($"({state})")}", null, heritage.summary, () => _ruin = heritage.record.id);
        }
        foreach (var v in culture.Variants())
        {
            var view = culture.Tradition(v.tradition);
            if (view == null) continue;
            var variant = v;
            string why = culture.WhyNotRenew(variant.tradition);
            var preview = culture.PreviewRenew(variant.tradition);
            choice($"Renew {view.name} for this Age {TooltipText.Muted($"({(variant.source.StartsWith("hybrid:") ? "a blend" : "inherited")}, reworked {variant.depth} time{(variant.depth == 1 ? "" : "s")})")}",
                why, preview.succeeded ? preview.reason : "Renewing needs an Age to have passed since it was made or last renewed.", () => culture.Renew(variant.tradition));
        }
        foreach (var d in culture.SyncretismDecisions().Reverse().Take(8))
            choice(TooltipText.Muted(d.text), null, (d.retained.Count > 0 ? "Kept: " + string.Join("; ", d.retained) : string.Empty) + (d.lost.Count > 0 ? "\nLost: " + string.Join("; ", d.lost) : string.Empty), () => { });
    }

    // ===== A MEETING =====

    private static void FillBlend(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var offer = culture.HybridOffers().FirstOrDefault(o => o.rule.id == _rule && o.settlement == _place);
        var rule = culture.SyncretismTuning.Hybrid(_rule);
        var d = rule != null ? culture.TraditionTuning.Definition(rule.result) : null;
        if (rule == null || d == null) { _rule = null; Fill(culture, header, choice); return; }
        header($"{d.name}: {d.description} {TooltipText.Muted($"{CanonWord(rule.canon)}: {rule.canonNote}")}"
            + (offer != null ? $"\nThey met: {offer.contact}. Evidence: {string.Join(", ", offer.evidence)}." : "\nThis meeting is no longer open."));
        choice("< Heritage", null, "Back.", () => _rule = null);
        foreach (var mode in new[] { SyncretismMode.SideBySide, SyncretismMode.Adapt, SyncretismMode.Replace })
        {
            var m = mode;
            var preview = culture.PreviewHybrid(_rule, _place, m);
            string label = m == SyncretismMode.SideBySide ? "Keep them side by side" : m == SyncretismMode.Adapt ? $"Blend {d.name} beside them" : $"Let {d.name} take their place";
            choice($"{label} {TooltipText.Muted(preview.succeeded ? $"({(preview.paid.Count > 0 ? preview.PaidText : "free")})" : string.Empty)}", preview.succeeded ? null : preview.reason, preview.reason,
                () => { if (culture.Hybridize(_rule, _place, m).succeeded) _rule = null; });
        }
    }

    // ===== A RUIN =====

    private static void FillRuin(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var h = culture.InspectRuin(_ruin);
        if (h?.record == null) { _ruin = -1; Fill(culture, header, choice); return; }
        var text = new StringBuilder();
        text.AppendLine(h.summary);
        foreach (var c in h.clues) text.AppendLine(TooltipText.Bullet(c.text + (c.family != null && c.kind != RuinClueKind.Ground ? TooltipText.Muted($" (supports the {c.family} ways)") : string.Empty)));
        if (h.lost.Count > 0) text.AppendLine(TooltipText.Muted("Lost: " + string.Join(" ", h.lost)));
        header(text.ToString().TrimEnd());
        choice("< Heritage", null, "Back.", () => _ruin = -1);
        var r = h.record;
        if (!string.IsNullOrEmpty(r.civic))
        {
            var keep = new InheritanceRequest { ruin = r.id, choice = InheritanceChoice.PreserveCivic };
            var preview = culture.PreviewInherit(keep);
            choice($"Adopt {r.civic} beside your civics", preview.succeeded ? null : preview.reason, preview.reason, () => culture.Inherit(keep));
            var yours = CivicManager.Instance != null ? CivicManager.Instance.GetAllActiveCivics().Select(c => c.civicName).OrderBy(n => n, StringComparer.Ordinal).ToList() : new List<string>();
            if (yours.Count > 0)
            {
                string replacing = yours[_replace % yours.Count];
                var swap = new InheritanceRequest { ruin = r.id, choice = InheritanceChoice.ReplaceCivic, replacing = replacing };
                var swapPreview = culture.PreviewInherit(swap);
                choice($"Adopt {r.civic} in place of {replacing}{(yours.Count > 1 ? TooltipText.Muted(" (change)") : string.Empty)}", null, swapPreview.reason, () => _replace++);
                choice($"  Confirm: {replacing} gives way to {r.civic}", swapPreview.succeeded ? null : swapPreview.reason, swapPreview.reason, () => culture.Inherit(swap));
            }
        }
        foreach (string family in h.families)
        {
            var adapt = new InheritanceRequest { ruin = r.id, choice = InheritanceChoice.AdaptWays, family = family };
            var preview = culture.PreviewInherit(adapt);
            choice($"Take in the {family} ways (Digestive Rebirth)", preview.succeeded ? null : preview.reason, preview.reason, () => culture.Inherit(adapt));
        }
        if (h.unknown && r.investigated)
        {
            var guess = new InheritanceRequest { ruin = r.id, choice = InheritanceChoice.ReadTheStones };
            var preview = culture.PreviewInherit(guess);
            choice($"Read the stones{(r.groundFamily != null ? $" (a guess: the {r.groundFamily} ways)" : string.Empty)}", preview.succeeded ? null : preview.reason, preview.reason, () => culture.Inherit(guess));
        }
        var places = new List<int> { -1 };
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null) places.AddRange(map.Settlements.Where(s => s != null).Select(s => s.id));
        foreach (var clue in h.clues.Where(c => c.revivable))
        {
            var c = clue;
            if (!_destination.TryGetValue(c.evidence, out int at)) at = c.kind == RuinClueKind.Practice ? (places.Count > 1 ? places[1] : -1) : -1;
            string where = at < 0 ? "the nation" : map?.Settlements.FirstOrDefault(s => s != null && s.id == at)?.name ?? "a settlement";
            var revive = new InheritanceRequest { ruin = r.id, choice = InheritanceChoice.Revive, evidence = c.evidence, settlement = at };
            var preview = culture.PreviewInherit(revive);
            string name = c.kind == RuinClueKind.Tradition ? culture.TraditionTuning.Definition(c.definition)?.name ?? c.definition : LocalPracticeCatalog.NameOf(c.practice);
            choice($"Take up {name} in {where} {TooltipText.Muted("(change place)")}", null, preview.reason, () =>
            {
                int i = places.IndexOf(at);
                _destination[c.evidence] = places[(i + 1) % places.Count];
            });
            choice($"  Confirm: take up {name} in {where}", preview.succeeded ? null : preview.reason, preview.reason, () => culture.Inherit(revive));
        }
    }

    private static string CanonWord(CanonStatus c) => c == CanonStatus.ExplicitCanon ? "Explicit canon" : c == CanonStatus.CanonSupported ? "Canon-supported adaptation" : "New game rule (proposal)";

    /// <summary>The body's heritage section: meetings open, ruins to read, blends and inherited traditions kept.</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        var offers = culture.HybridOffers();
        var variants = culture.Variants();
        var ruins = culture.RuinHeritages().Where(h => h.record.investigated && !h.record.digested).ToList();
        if (offers.Count == 0 && variants.Count == 0 && ruins.Count == 0) return;
        text.AppendLine(TooltipText.Heading("Inheritance and blends"));
        foreach (var o in offers)
            text.AppendLine(TooltipText.Bullet($"{culture.ParentName(o.rule.parentA)} and {culture.ParentName(o.rule.parentB)} meet in {o.place}: they could be blended (Heritage)."));
        foreach (var h in ruins.Take(4))
            text.AppendLine(TooltipText.Bullet($"The ruins of {h.record.name}: {h.summary}"));
        foreach (var v in variants)
        {
            var t = culture.Tradition(v.tradition);
            if (t != null) text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(t.name)} {TooltipText.Muted($"({(v.source.StartsWith("hybrid:") ? $"blended from {culture.ParentName(v.parentA)} and {culture.ParentName(v.parentB)}" : "taken up from ruins")}; {TraditionRules.StageWord(t.stage)})")}"));
        }
        text.AppendLine();
    }
}
