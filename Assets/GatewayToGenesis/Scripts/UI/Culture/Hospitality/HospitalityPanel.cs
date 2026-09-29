using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Tables panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body (T05): set
/// a communal table in one settlement, under a serving policy, from the foods the stores hold, with a preview of the
/// portions, who is represented and what the stores lose; and see who has had a place at the table, settlement by
/// settlement, and where luxuries stop. It only reads <see cref="ICultureQuery"/> snapshots and calls
/// <see cref="CultureSystem.SetTable"/>; nothing is paid without the last click.
/// </summary>
public static class HospitalityPanel
{
    // What is being set up, by ids and names only (a stale selection after a load shows the list again).
    private static int _settlement = -1;
    private static TablePolicy _policy = TablePolicy.PublicWelcome;
    private static readonly List<string> _menu = new List<string>();
    private static string _patron, _last;

    /// <summary>Forget the table being set up (the window was opened afresh).</summary>
    public static void Reset()
    {
        _settlement = -1;
        _policy = TablePolicy.PublicWelcome;
        _menu.Clear();
        _patron = null;
        _last = null;
    }

    /// <summary>Open the panel on one settlement (its card's "Set a communal table").</summary>
    public static void Select(int settlement)
    {
        Reset();
        _settlement = settlement;
    }

    private static TableRequest Request() => new TableRequest { settlement = _settlement, policy = _policy, menu = new List<string>(_menu), patron = _policy == TablePolicy.PatronHosted ? _patron : null };

    /// <summary>Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row (label, why not or null, tooltip, action).</summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        var settlement = map != null && _settlement >= 0 ? WorldCivilization.Get(map, _settlement) : null;
        var access = query.TableAccess();
        if (settlement == null)
        {
            _settlement = -1;
            header($"A shared table changes who belongs: set one in a settlement from what the stores hold, open to all, for a community under strain, or hosted by a Legend for their guests. {TooltipText.Muted($"Reached by shared tables lately: {access.coverage:P0} of your settlements.")}"
                + (access.problems.Count > 0 ? " " + TooltipText.Warn(access.problems[0]) : string.Empty));
            foreach (var row in access.rows)
            {
                var r = row;
                choice($"{r.name} {TooltipText.Muted($"({RowText(r)})")}", null, RowTip(r), () => Select(r.settlement));
            }
            foreach (string problem in access.problems.Skip(1)) choice(TooltipText.Muted(problem), null, null, () => { });
            foreach (var t in query.RecentTables(4))
                choice(TooltipText.Muted($"Seventh {t.seventh}: {t.text}"), null, TableTip(t), () => { });
            return;
        }

        var preview = query.PreviewTable(Request());
        string portions = preview.portions.Count > 0 ? HospitalityRules.MenuText(preview.portions.Select(p => (p.resource, p.amount))) : null;
        header($"{HospitalityRules.PolicyName(_policy)} in {settlement.name}: {HospitalityRules.PolicyText(_policy)}. "
            + (preview.ok ? preview.reason : TooltipText.Bad(preview.reason) + (portions != null ? TooltipText.Muted($" (planned: {portions}, {preview.foodValue:0.#} food value)") : string.Empty)));
        choice("< All settlements", null, "Back to who has had a place at the table.", () => _settlement = -1);
        foreach (TablePolicy policy in Enum.GetValues(typeof(TablePolicy)))
        {
            var p = policy;
            choice($"{(p == _policy ? "(o)" : "( )")} {HospitalityRules.PolicyName(p)} {TooltipText.Muted($"({HospitalityRules.PolicyText(p)})")}", null, PolicyTip(culture, p), () => _policy = p);
        }
        if (_policy == TablePolicy.PatronHosted)
        {
            var patrons = culture.PatronChoices();
            if (patrons.Count == 0) choice(TooltipText.Muted("No Legend is at home to host (those away with an expedition cannot)."), null, null, () => { });
            foreach (string legend in patrons)
            {
                string l = legend;
                choice($"   {(string.Equals(l, _patron, StringComparison.OrdinalIgnoreCase) ? "(o)" : "( )")} hosted by {l}", null, "The patron's standing grows with the tables they give (at most once in a while); few of the town have a place at them.", () => _patron = l);
            }
        }
        var foods = query.ServableFoods();
        int max = culture.HospitalityTuning.maxMenu;
        choice(TooltipText.Muted($"Foods to serve (up to {max}; each brings an equal share of the table):"), null, null, () => { });
        foreach (var f in foods)
        {
            var food = f;
            bool on = _menu.Contains(food.resource, StringComparer.OrdinalIgnoreCase);
            string why = !on && _menu.Count >= max ? $"A table serves {max} foods at most: set one aside first." : null;
            choice($"   {(on ? "[x]" : "[ ]")} {food.resource} {TooltipText.Muted($"({FoodText(food)})")}", why, FoodTip(food), () => Toggle(food.resource));
        }
        foreach (string gone in _menu.Where(m => !foods.Any(f => string.Equals(f.resource, m, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            string g = gone;
            choice($"   [x] {g} {TooltipText.Muted("(no longer held)")}", null, "Set it aside.", () => Toggle(g));
        }
        if (foods.Count == 0) choice(TooltipText.Muted("The stores hold no finished food to serve: cook a dish in the Kitchen first."), null, null, () => { });
        if (preview.groups.Count > 0) choice(TooltipText.Muted("At the table: " + string.Join("; ", preview.groups)), null, "Only who is known is named: the simulation keeps no head count of each settlement.", () => { });
        choice($"Set the table in {settlement.name}", preview.ok ? null : preview.reason, preview.ok ? preview.reason : null, () =>
        {
            var result = culture.SetTable(Request());
            _last = result.reason;
            if (result.succeeded) _menu.Clear();
        });
        if (_last != null) choice(TooltipText.Muted(_last), null, _last, () => { });
        foreach (var t in query.TablesAt(settlement.id).Take(3))
            choice(TooltipText.Muted($"Seventh {t.seventh}: {t.text}"), null, TableTip(t), () => { });
    }

    private static void Toggle(string resource)
    {
        int i = _menu.FindIndex(m => string.Equals(m, resource, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) _menu.RemoveAt(i);
        else _menu.Add(resource);
    }

    private static string RowText(AccessRow r)
    {
        string last = r.lastShared >= 0 ? $"last open table at Seventh {r.lastShared}" : r.lastPatron >= 0 ? $"only a patron's table, Seventh {r.lastPatron}" : "no shared table yet";
        string lux = r.shared.Length > 0 ? $"; shared {string.Join(", ", r.shared)}" : r.privateOnly.Length > 0 ? $"; {string.Join(", ", r.privateOnly)} only for a patron's guests" : string.Empty;
        return $"{last}; reached {r.reach:P0}{lux}";
    }

    private static string RowTip(AccessRow r) => string.Join("\n", new[]
    {
        TooltipText.Row("Tables", r.tables == 0 ? "none yet" : r.tables.ToString()),
        TooltipText.Row("Reached now", $"{r.reach:P0}"),
        r.shared.Length > 0 ? TooltipText.Row("Luxuries shared openly", string.Join(", ", r.shared)) : null,
        r.privateOnly.Length > 0 ? TooltipText.Row("Only at a patron's table", string.Join(", ", r.privateOnly)) : null,
        TooltipText.Muted("Choose it to set a table there."),
    }.Where(l => l != null));

    private static string FoodText(ServableFood f)
    {
        var parts = new List<string> { $"{f.held:0.#} held, {f.foodValue:0.##} food value each" };
        if (f.Luxury) parts.Add(string.Join(", ", f.luxuries));
        if (f.invented) parts.Add("your own");
        if (f.national) parts.Add("national");
        return string.Join("; ", parts);
    }

    private static string FoodTip(ServableFood f) => string.Join("\n", new[]
    {
        TooltipText.Row("Kitchen", CultureRules.ClassName(f.cuisine)),
        TooltipText.Row("Recipe", f.recipe ?? "none (a food as it is kept)"),
        f.Luxury ? TooltipText.Row("Luxury", string.Join(", ", f.luxuries) + ": shared openly at a public or recovery table; kept among the guests at a patron's") : null,
        TooltipText.Muted("Served portions leave the stores at once: they are not also a luxury draw or a meal against hunger."),
    }.Where(l => l != null));

    private static string PolicyTip(CultureSystem culture, TablePolicy p)
    {
        var t = culture.HospitalityTuning;
        var e = HospitalityRules.Effects(p, false, false, t);
        return string.Join("\n", new[]
        {
            HospitalityRules.PolicyText(p),
            TooltipText.Row("Table", $"{HospitalityRules.TableSize(p, 0f, t):0.#} food value, more for a developed settlement"),
            TooltipText.Row("Gives", $"+{e.unity:0.#} Unity; strain eased {e.relief:0.#}; reaches {e.coverage:P0} of the town"),
            p == TablePolicy.PublicWelcome ? TooltipText.Row("Neighbours", "guests by open road: each side meets the other's customs") : null,
            p == TablePolicy.RecoverySupport ? TooltipText.Row("Needs", $"Composure strain {t.recoveryStrain:0} or more") : null,
            p == TablePolicy.PatronHosted ? TooltipText.Row("Patron", $"{t.patronFragments} Meaning fragment at most once in {t.patronRestSevenths} Sevenths; luxuries stay private") : null,
        }.Where(l => l != null));
    }

    private static string TableTip(TableView t) => string.Join("\n", new[]
    {
        TooltipText.Row("Served", string.Join(", ", t.served.Select(s => $"{s.amount:0.#} {s.name}"))),
        t.groups.Length > 0 ? TooltipText.Row("At the table", string.Join("; ", t.groups)) : null,
        t.legends.Length > 0 ? TooltipText.Row("Legends", string.Join(", ", t.legends)) : null,
        TooltipText.Row("Food value", $"{t.foodValue:0.#}"),
    }.Where(l => l != null));

    /// <summary>The Shared tables section of the window's body (nothing before the first table).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var recent = query.RecentTables(3);
        var access = query.TableAccess();
        if (recent.Count == 0 && access.hoarded.Count == 0) return;
        text.AppendLine(TooltipText.Heading("Shared tables", $"{access.coverage:P0} of your settlements reached lately"));
        foreach (var t in recent) text.AppendLine(TooltipText.Bullet($"Seventh {t.seventh}: {t.text}"));
        foreach (string problem in access.problems) text.AppendLine(TooltipText.Warn(problem));
        var tuning = culture.HospitalityTuning;
        text.AppendLine(TooltipText.Muted($"A settlement counts as reached for {tuning.coverageWindowSevenths} Sevenths after an open table (a patron's reaches {tuning.patronCoverage:P0}); full coverage adds up to {tuning.coverageLiving * 100f:0} points of living, apart from luxuries. Set tables from Tables below or a settlement's card."));
        text.AppendLine();
    }
}
