using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

/// <summary>
/// The achievement coverage register (Docs/Planning/ACHIEVEMENT_COVERAGE.csv) against the imported note and the
/// award rules (U00): one row per achievement, in the note's order, each routed to a wave and owner; "predicate-wired"
/// exactly for the ids <see cref="AchievementTriggers.Rules"/> can award; aliases and the completion set consistent.
/// Reads both files from disk, so it runs outside Unity too.
/// </summary>
public class AchievementRegisterTests
{
    private const string RegisterPath = "Docs/Planning/ACHIEVEMENT_COVERAGE.csv";
    private const string NotePath = "Assets/Resources/Achievements/Achievements.md";

    private static List<Dictionary<string, string>> Register()
    {
        var lines = File.ReadAllLines(RegisterPath).Where(l => l.Length > 0).ToList();
        var header = Fields(lines[0]);
        return lines.Skip(1).Select(line =>
        {
            var fields = Fields(line);
            Assert.AreEqual(header.Count, fields.Count, $"register row has {fields.Count} fields: {line}");
            return header.Zip(fields, (h, f) => (h, f)).ToDictionary(p => p.h, p => p.f);
        }).ToList();
    }

    // RFC 4180 fields: quoted, with "" for a quote.
    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }

    private static List<AchievementDefinition> Note() => AchievementNote.Parse(File.ReadAllText(NotePath)).achievements;

    [Test]
    public void EveryAchievementHasExactlyOneRowInTheNotesOrder()
    {
        var rows = Register();
        var note = Note();
        Assert.AreEqual(98, note.Count, "the imported note's achievements (Critically Thinking Hater has no requirement and is excluded)");
        CollectionAssert.AllItemsAreUnique(rows.Select(r => r["id"]));
        CollectionAssert.AreEqual(note.Select(a => a.id), rows.Select(r => r["id"]));
        CollectionAssert.AreEqual(Enumerable.Range(1, rows.Count).Select(n => n.ToString()), rows.Select(r => r["number"]));
        foreach (var (row, achievement) in rows.Zip(note, (r, a) => (r, a)))
            Assert.AreEqual(achievement.requirement, row["canon_requirement"], $"{row["id"]}: the requirement is quoted exactly");
    }

    [Test]
    public void EveryRowIsRoutedAndWiringMatchesTheRules()
    {
        foreach (var row in Register())
        {
            string id = row["id"];
            StringAssert.IsMatch("^U0[0-7]$", row["wave"], $"{id} has a wave");
            Assert.IsNotEmpty(row["owner_role"], $"{id} has an owner role");
            string expected = AchievementTriggers.CanBeEarned(id) ? "predicate-wired" : id == "the-purest-of-all-love" ? "direct-profile-only" : "planned";
            Assert.AreEqual(expected, row["wiring"], $"{id}'s wiring");
            if (expected == "predicate-wired") Assert.IsNotEmpty(row["producer_evidence"], $"{id} names the producer that reports it");
        }
    }

    [Test]
    public void AliasesAndTheCompletionSetFitTheNote()
    {
        var note = Note();
        CollectionAssert.IsEmpty(AchievementAliases.Problems(note.Select(a => a.id)));
        Assert.IsTrue(note.Any(a => a.id == AchievementCompletion.FinalId), "the final achievement exists under its id");
        var set = AchievementCompletion.Set(note);
        Assert.AreEqual(note.Count - 1, set.Count);
        CollectionAssert.DoesNotContain(set, AchievementCompletion.FinalId);
    }
}
