using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A detached graph of recorded culture. Keys have namespaces; labels never establish identity.</summary>
public sealed class CultureAtlasNode
{
    public string key, title, category, detail, command, argument;
    public readonly List<string> links = new List<string>();
}

public sealed class CultureAtlasReadModel
{
    public readonly Dictionary<string, CultureAtlasNode> nodes = new Dictionary<string, CultureAtlasNode>(StringComparer.OrdinalIgnoreCase);
    public readonly List<SettlementProfileView> places = new List<SettlementProfileView>();
    public string opportunity, opportunityKey;

    public static string Key(string kind, string id) => kind + ":" + id;
    public static string Key(CultureEntityRef r) => r == null || !r.IsKnown ? null
        : r.kind == CultureEntityKind.Tradition && r.id.StartsWith("local:", StringComparison.Ordinal) ? Key("LocalDefinition", r.id.Substring(6))
        : r.kind == CultureEntityKind.Tradition && r.id.StartsWith("tradition:", StringComparison.Ordinal) ? Key("TraditionDefinition", r.id.Substring(10))
        : Key(r.kind.ToString(), r.id);

    public CultureAtlasNode Add(string kind, string id, string title, string detail, string command = null, string argument = null)
    {
        string key = Key(kind, id);
        var n = new CultureAtlasNode { key = key, title = title ?? id, category = kind, detail = detail ?? "No history recorded.", command = command, argument = argument };
        if (nodes.TryGetValue(key, out var previous)) n.links.AddRange(previous.links);
        nodes[key] = n;
        return n;
    }

    public CultureAtlasNode Resolve(string key) => key != null && nodes.TryGetValue(key, out var n) ? n :
        new CultureAtlasNode { key = key, title = "Record unavailable", category = "History", detail = "This record is no longer available. Its origin and connections cannot be inferred." };

    public void Join(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b) return;
        if (nodes.TryGetValue(a, out var left) && !left.links.Contains(b)) left.links.Add(b);
        if (nodes.TryGetValue(b, out var right) && !right.links.Contains(a)) right.links.Add(a);
    }

    private void Reference(CultureAtlasNode from, CultureEntityRef r)
    {
        string key = Key(r);
        if (key == null) return;
        if (!nodes.ContainsKey(key))
        {
            int colon = key.IndexOf(':');
            Add(key.Substring(0, colon), key.Substring(colon + 1), r.Display, "Referenced in a cultural record. Further history is not recorded.");
        }
        Join(from.key, key);
    }

    public IEnumerable<CultureAtlasNode> Browse(string search, string category) => nodes.Values
        .Where(n => (string.IsNullOrEmpty(category) || n.category == category) &&
            (string.IsNullOrWhiteSpace(search) || (n.title + " " + n.detail).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
        .OrderBy(n => n.category).ThenBy(n => n.title, StringComparer.OrdinalIgnoreCase);

    public static string Compare(SettlementProfileView a, SettlementProfileView b)
    {
        if (a == null || b == null) return "Choose two recorded settlements.";
        string Customs(SettlementProfileView p) => string.Join("\n", p.practices.Select(x =>
            $"{x.name}: {x.stage}; {x.provenanceText ?? "origin unknown"}; {x.gatherings} gatherings"));
        var common = a.Kept.Select(p => p.practice).Intersect(b.Kept.Select(p => p.practice)).ToList();
        return $"{a.name}\n{Customs(a)}\n\n{b.name}\n{Customs(b)}\n\nShared recorded customs: {common.Count}. Exposure is not adoption; a quiet custom remains part of its history.";
    }

    public static CultureAtlasReadModel Capture(CultureSystem c)
    {
        var m = new CultureAtlasReadModel();
        if (c == null || !c.State.founded) { m.opportunity = "Research Horology and tell the founding story."; return m; }
        var traditions = c.Traditions();
        foreach (var r in c.Life.recipes.Where(r => r != null))
        {
            var info = c.RecipeInfo(r.id);
            if (info == null || !info.available) continue;
            m.Add("Recipe", r.id, info.dish, $"{(info.custom ? "Invented by your people" : "Known recipe")}. {info.description}\nMethod: {info.method}. Open the kitchen to prepare this dish.", "Kitchen");
        }
        foreach (var t in traditions)
        {
            var n = m.Add("Tradition", t.id, t.name, $"{t.stage} in {t.place ?? "a place not recorded"}\n{t.origin ?? "Origin not recorded"}\n{t.explanation}\n{t.progress}\n{t.canon}: {t.canonSource}\nBenefits: {string.Join("; ", t.benefits ?? Array.Empty<string>())}", "Traditions", t.id);
            m.Reference(n, t.subject);
            var definition = Key("TraditionDefinition", t.definition);
            if (!m.nodes.ContainsKey(definition)) m.Add("TraditionDefinition", t.definition, t.name + " — practice", t.description + "\n" + t.canonSource);
            m.Join(n.key, definition);
            foreach (var h in t.history ?? Array.Empty<TraditionParticipation>()) m.Reference(n, h.subject);
            foreach (var r in (t.venues ?? Array.Empty<CultureEntityRef>()).Concat(t.links ?? Array.Empty<CultureEntityRef>())) m.Reference(n, r);
            foreach (var name in t.bearers ?? Array.Empty<string>()) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Legend, name));
            if (t.settlement >= 0) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Settlement, t.settlement.ToString(), t.place));
        }
        // Populate concrete nodes before joins so a late placeholder cannot erase a real description.
        foreach (var e in c.MemoryCauses()) m.Add("Evidence", e.id, e.title, $"RECORDED EVIDENCE\n{e.text}\nSubject: {e.subject ?? "unknown"}\nAge: {e.occurredAge ?? "unknown"}\n{(e.reconstructed ? "Reconstructed from an older record; exact time unknown." : "Recorded when learned.")}", "Memorials");
        foreach (var p in c.LocalProfiles())
        {
            m.places.Add(p);
            m.Add("Settlement", p.settlement.ToString(), p.name, $"{(p.gone ? "Former settlement" : "Local culture")}\n" + string.Join("\n", p.practices.Select(x => $"{x.name}: {x.stage}; {x.provenanceText}")), "Tables", p.settlement.ToString());
            foreach (var practice in p.practices)
            {
                var local = m.Add("LocalPractice", p.settlement + ":" + practice.practice, practice.name + " in " + p.name, $"{practice.stage}\n{practice.provenanceText}\n{practice.canon}: {practice.vault}");
                string key = Key("LocalDefinition", practice.practice);
                if (!m.nodes.ContainsKey(key)) m.Add("LocalDefinition", practice.practice, practice.name, practice.canon + "\n" + practice.vault);
                m.Join(local.key, key); m.Join(local.key, Key("Settlement", p.settlement.ToString()));
            }
        }
        foreach (var i in c.TeachingInstitutions())
            m.Add("Institution", i.id, i.name, $"Teaching institution ({i.spec}) in {i.settlementName}. Founded at Seventh {i.foundedSeventh}.\n{(string.IsNullOrEmpty(i.evolvedInto) ? "" : "Evolved into " + i.evolvedInto)}", "Teaching");
        foreach (var e in c.MemoryCauses())
        {
            var n = m.Resolve(Key("Evidence", e.id));
            m.Reference(n, e.source);
        }
        foreach (var d in c.MemorialDedications())
        {
            var n = m.Add("Dedication", d.id, d.target.Display + " — memorial", $"{d.status}\n{d.dormantReason}\nA dedication links a practice to recorded evidence; it does not change the dish.", "Memorials");
            m.Reference(n, d.target);
            m.Join(n.key, Key("Evidence", d.evidence));
            // Recipe -> cause is a direct, intentional selection.
            m.Join(Key(d.target), Key("Evidence", d.evidence));
        }
        var occurrences = c.RecentOccurrences(500);
        foreach (var o in occurrences)
        {
            var n = m.Add("Occurrence", o.key, o.text ?? o.kind.ToString(), $"RECORDED ACTION\n{o.text}\nSeventh {o.stamp?.cultureSeventh.ToString() ?? "unknown"}\n{o.quantity:0.#} {o.unit}");
            m.Reference(n, o.subject); m.Reference(n, o.recipe); m.Reference(n, o.source); m.Reference(n, o.evidence);
            if (o.SettlementKnown) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Settlement, o.settlement.ToString()));
            foreach (var actor in o.actors ?? new List<string>()) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Legend, actor));
            foreach (var t in traditions.Where(t => (t.history ?? Array.Empty<TraditionParticipation>()).Any(h => h.key == o.key))) m.Join(n.key, Key("Tradition", t.id));
            // A dish's recorded use also links directly to the participating practice and venue.
            if (o.recipe != null && o.recipe.IsKnown)
            {
                foreach (var t in traditions.Where(t => (t.history ?? Array.Empty<TraditionParticipation>()).Any(h => h.key == o.key))) m.Join(Key(o.recipe), Key("Tradition", t.id));
                if (o.SettlementKnown) m.Join(Key(o.recipe), Key("Settlement", o.settlement.ToString()));
            }
        }
        foreach (var t in traditions)
        {
            var carriers = c.CarriersOf(t.id);
            var n = m.Resolve(Key("Tradition", t.id));
            n.detail += "\nLiving carriers: " + string.Join(", ", carriers.livingLegends.Concat(carriers.communities.Select(p => p.name)));
            n.detail += "\nAway: " + string.Join(", ", carriers.awayLegends.Select(p => p.legend + " (" + p.why + ")"));
            n.detail += "\nLost: " + string.Join(", ", carriers.lostLegends);
        }
        foreach (var v in c.PracticeVariants())
        {
            var n = m.Add("TeachingVariant", v.id, v.name, $"Adapted from {v.parentName}, taught by {v.taughtBy} in {v.settlementName} at Seventh {v.seventh}.", "Teaching");
            m.Join(n.key, Key("Tradition", v.parent)); m.Reference(n, v.recipe);
        }
        foreach (var r in c.WrittenRecords())
        {
            var n = m.Add("WrittenRecord", r.id, r.traditionName + " — written", $"Written from {r.writtenFrom} at {r.institutionName}, {r.settlementName}, Seventh {r.seventh}. A record is not a living practitioner.", "Teaching");
            m.Join(n.key, Key("Tradition", r.tradition)); m.Join(n.key, Key("Institution", r.institution)); m.Reference(n, r.recipe);
        }
        foreach (var o in c.TeachingOrders())
        {
            var n = m.Add("TeachingOrder", o.id, o.traditionName + " — " + o.mode, $"{o.status}: {o.progress}/{o.required} Sevenths\n{o.teacherLabel} teaches {o.learnerLabel} in {o.settlementName}.\n{o.pausedReason}", "Teaching");
            m.Join(n.key, Key("Tradition", o.tradition)); m.Join(n.key, Key("Institution", o.institution)); m.Reference(n, o.recipe);
        }
        foreach (var o in c.Observances())
        {
            var n = m.Add("Observance", o.id, o.name, $"{o.objective}; {o.scale}; {o.place}\n{o.day}; next {o.next}\n{o.plan}\nKept {o.kept}; missed {o.missed}.\n{o.canon}: {o.canonNote}", "Holidays", o.id);
            m.Reference(n, o.foodRef); m.Reference(n, o.causeRef); m.Reference(n, o.venueRef);
            if (o.settlement >= 0) m.Join(n.key, Key("Settlement", o.settlement.ToString()));
        }
        foreach (var table in c.RecentTables(100))
        {
            var n = m.Add("Table", table.key, table.text, $"RECORDED DISTRIBUTION\n{table.policy} in {table.place}, Seventh {table.seventh}.\n" +
                string.Join("\n", table.served.Select(s => $"{s.amount:0.#} {s.name}")) + "\nGroups: " + string.Join(", ", table.groups), "Tables", table.settlement.ToString());
            m.Join(n.key, Key("Settlement", table.settlement.ToString()));
            foreach (var actor in table.legends) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Legend, actor));
            // Serving occurrence IDs are the shared boundary's stable table/line IDs, never dish labels.
            foreach (var o in occurrences.Where(o => o.key != null && o.key.StartsWith("hospitality:" + table.key + ":", StringComparison.Ordinal)))
            { m.Join(n.key, Key("Occurrence", o.key)); m.Reference(n, o.recipe); }
        }
        foreach (var l in c.PublicLinks())
        {
            var n = m.Add("PublicLink", l.id, l.traditionName + " / " + l.promiseName, $"PUBLIC CLAIM — {l.status}\n{l.endedReason}\nAccounts describe what the council says; they never replace evidence.", "Accounts");
            m.Join(n.key, Key("Tradition", l.tradition));
            foreach (var a in c.AccountsOf(l.id))
            {
                var account = m.Add("Account", a.id, $"{l.promiseName}: account v{a.version}", $"PUBLIC ACCOUNT — {a.kind}\n{a.author}, Seventh {a.seventh}\n{a.text}", "Accounts", a.dispute);
                m.Join(account.key, n.key); m.Join(account.key, Key("Dispute", a.dispute));
            }
        }
        foreach (var d in c.PublicDisputes())
        {
            var n = m.Add("Dispute", d.id, d.traditionName + " — " + d.promiseName, $"{d.status}\n{d.outcome}\nCITED RECORDS\n" + string.Join("\n", d.records.Select(r => $"{r.bearing}: {r.text}")), "Accounts", d.id);
            m.Join(n.key, Key("PublicLink", d.link));
            foreach (var a in c.AccountsOf(d.link).Where(a => a.dispute == d.id)) m.Join(n.key, Key("Account", a.id));
        }
        foreach (var p in c.PerformanceHistory(100))
        {
            var n = m.Add("Performance", p.id, p.name + " in " + p.place, $"{p.status}: {p.band}\n{p.text}\n" + string.Join("\n", p.factors ?? Array.Empty<string>()), "Performances");
            foreach (var name in p.cast ?? Array.Empty<string>()) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Legend, name));
            m.Join(n.key, Key("Settlement", p.settlement.ToString()));
        }
        foreach (var h in c.RuinHeritages()) m.Add("Ruin", h.record.id.ToString(), "Ruins of " + h.record.name, h.summary, "Heritage", h.record.id.ToString());
        foreach (var v in c.Variants())
        {
            var n = m.Resolve(Key("Tradition", v.tradition));
            n.detail += $"\nInheritance: {v.source}; parents {v.parentA}, {v.parentB}; depth {v.depth}.\n" + string.Join("\n", v.evidence);
            if (!string.IsNullOrEmpty(v.parentA)) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Tradition, v.parentA));
            if (!string.IsNullOrEmpty(v.parentB)) m.Reference(n, CultureEntityRef.Of(CultureEntityKind.Tradition, v.parentB));
        }
        foreach (var l in c.MemoryLineages())
        {
            var n = m.Add("Lineage", l.id, l.ageTitle + " → " + l.nextAgeTitle, "Recorded at an Age passage. These are the memories and practices carried forward.");
            foreach (var e in l.evidence) m.Join(n.key, Key("Evidence", e));
            foreach (var p in l.practices) m.Reference(n, p);
        }
        // Repair reciprocal links after late concrete nodes replaced placeholders.
        foreach (var n in m.nodes.Values.ToList()) foreach (var link in n.links.ToList()) m.Join(n.key, link);
        var dispute = c.OpenDisputes().FirstOrDefault();
        var pending = c.PendingTraditionChoices().FirstOrDefault();
        m.opportunityKey = dispute != null ? Key("Dispute", dispute.id) : pending != null ? Key("Tradition", pending.id) : null;
        m.opportunity = dispute != null ? "Answer the account of " + dispute.traditionName : pending != null ? "Decide how to keep " + pending.name : "Explore the history your people have made.";
        return m;
    }
}
