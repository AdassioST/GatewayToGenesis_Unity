using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Conscripted companies (Grave Wardens, Wasteland Archers) raised from the population, stacks with a legend in
/// command and legends leading companies, battles written back into the roster, legends' attachment to their companies,
/// and the promotion ledger. No scene: a fake bank stands in for the people, the stores and the Age.
/// </summary>
public class ConscriptionTests
{
    private sealed class Bank : IConscriptionBank
    {
        public int people = 20, age = 1;
        public readonly HashSet<string> techs = new HashSet<string> { "The Rekindling" };
        public readonly Dictionary<string, float> stores = new Dictionary<string, float> { ["Elderwood"] = 1000f, ["Duskstone"] = 1000f };
        public int enlisted, discharged, fallen, eraScore, eraAwards;
        public readonly List<(string legend, FragmentAward award)> fragments = new List<(string, FragmentAward)>();
        public readonly List<(ConscriptUnit unit, string legend)> nameRequests = new List<(ConscriptUnit, string)>();
        public readonly List<List<string>> shared = new List<List<string>>();

        public int People => people;
        public int Age => age;
        public bool Has(string technology) => techs.Contains(technology);
        public float Amount(string resource) => stores.TryGetValue(resource, out float v) ? v : 0f;
        public void Spend(string resource, float amount) => stores[resource] = Amount(resource) - amount;
        public void Enlist(int n, string company) { people -= n; enlisted += n; }
        public void Discharge(int n, string company) { people += n; discharged += n; }
        public void Fallen(int n, string cause) => fallen += n;
        public void AwardFragments(string legend, List<FragmentAward> reward, string deed) { foreach (var a in reward) fragments.Add((legend, a)); }
        public void AwardEraScore(int points, string reason) { eraScore += points; eraAwards++; }
        public void ShareBattle(IEnumerable<string> legends, string key, string memory, bool wound) => shared.Add(legends.ToList());
        public void RequestName(ConscriptUnit unit, string legend) => nameRequests.Add((unit, legend));
    }

    private static readonly CombatSettings Settings = new CombatSettings();

    private static BattleLegend Legend(string name, params (LegendClass great, int stars)[] greats) =>
        new BattleLegend { name = name, leitmotif = SpellBinding.Unattuned, greats = greats.ToDictionary(g => g.great, g => g.stars) };

    [Test]
    public void BasicUnits_AreConscriptedFromThePopulation()
    {
        var conscriptable = ArmyRoster.Conscriptable(Settings).Select(s => s.id).ToList();
        CollectionAssert.AreEquivalent(new[] { "grave-warden", "wasteland-archer" }, conscriptable);
        var warden = Settings.Section("grave-warden");
        Assert.AreEqual(FormationRow.Front, warden.row);
        Assert.AreEqual(2, warden.people);
        Assert.AreEqual("Golden Hymn Citadel", warden.category);
        Assert.AreEqual(125f, warden.cost.Single(c => c.resource == "Elderwood").amount);
        var archer = Settings.Section("wasteland-archer");
        Assert.AreEqual(FormationRow.Back, archer.row);
        Assert.AreEqual(50f, archer.cost.Single(c => c.resource == "Duskstone").amount);
        Assert.Greater(archer.attack, warden.attack, "2 Attack against 1");
        Assert.Greater(warden.defense, archer.defense, "2 Defense against 1");
    }

    [Test]
    public void Conscript_SpendsItsCostAndEnlistsItsPeople()
    {
        var bank = new Bank();
        var roster = new ArmyRoster();
        var first = roster.Conscript("grave-warden", bank, Settings);
        Assert.AreEqual("1st Grave Wardens", first.name);
        Assert.AreEqual(2, first.people);
        Assert.AreEqual(18, bank.people);
        Assert.AreEqual(875f, bank.Amount("Elderwood"));
        Assert.AreEqual("2nd Grave Wardens", roster.Conscript("grave-warden", bank, Settings).name);
        Assert.AreEqual("1st Wasteland Archers", roster.Conscript("wasteland-archer", bank, Settings).name);
        Assert.AreEqual(950f, bank.Amount("Duskstone"));
        Assert.IsTrue(roster.Disband(first.id, bank));
        Assert.AreEqual(2, bank.discharged, "the soldiers come home");
        Assert.IsNull(roster.Unit(first.id));
    }

    [Test]
    public void Conscript_SaysWhyNot()
    {
        var bank = new Bank();
        bank.techs.Clear();
        StringAssert.Contains("The Rekindling", ArmyRoster.WhyNotConscript("grave-warden", bank, Settings));
        bank.techs.Add("The Rekindling");
        bank.people = 1;
        StringAssert.Contains("people", ArmyRoster.WhyNotConscript("grave-warden", bank, Settings));
        bank.people = 20;
        bank.stores["Elderwood"] = 10f;
        StringAssert.Contains("Elderwood", ArmyRoster.WhyNotConscript("grave-warden", bank, Settings));
        StringAssert.Contains("not raised from the population", ArmyRoster.WhyNotConscript("shieldwall", bank, Settings));
        Assert.IsNull(new ArmyRoster().Conscript("grave-warden", bank, Settings));
        Assert.IsNull(ArmyRoster.WhyNotConscript("wasteland-archer", bank, Settings));
    }

    [Test]
    public void ALegend_HoldsOnePostAtATime()
    {
        var bank = new Bank();
        var roster = new ArmyRoster();
        var a = roster.Conscript("grave-warden", bank, Settings);
        var b = roster.Conscript("wasteland-archer", bank, Settings);
        Assert.IsTrue(roster.Lead(a.id, "Mira"));
        Assert.IsFalse(roster.Lead(b.id, "Mira"), "already leads a company");
        Assert.IsNull(roster.FormStack("Host", "Mira", new[] { a.id }), "cannot command while leading a company");
        var stack = roster.FormStack("Host", "Aldric", new[] { a.id, b.id });
        Assert.IsNotNull(stack);
        Assert.AreEqual("commands Host", roster.PostOf("Aldric"));
        Assert.AreEqual("leads 1st Grave Wardens", roster.PostOf("Mira"));
        CollectionAssert.AreEquivalent(new[] { "Aldric", "Mira" }, roster.LegendsInService.ToArray());
    }

    [Test]
    public void Muster_BringsItsWoundsAndItsLegends()
    {
        var bank = new Bank();
        var roster = new ArmyRoster();
        var a = roster.Conscript("grave-warden", bank, Settings);
        a.integrity = 50f;
        roster.Lead(a.id, "Mira");
        var stack = roster.FormStack("Host", "Aldric", new[] { a.id, roster.Conscript("wasteland-archer", bank, Settings).id });
        var side = roster.Muster(stack.id, Settings, n => Legend(n));
        Assert.AreEqual("Host", side.name);
        Assert.AreEqual("Aldric", side.conductor.name);
        Assert.AreEqual(2, side.sections.Count);
        Assert.AreEqual(50f, side.sections[0].integrity);
        Assert.AreEqual("Mira", side.sections[0].leader.name);
        Assert.AreEqual(a.id, side.sections[0].unitId);
        Assert.AreEqual(2, side.sections[0].count);
    }

    private static (ArmyRoster roster, ArmyStack stack, Bank bank) Host(params string[] specs)
    {
        var bank = new Bank { people = 100 };
        var roster = new ArmyRoster();
        var ids = specs.Select(s => roster.Conscript(s, bank, Settings).id).ToList();
        var stack = roster.FormStack("Ember Watch", "Aldric", ids);
        return (roster, stack, bank);
    }

    private static readonly SpeciesSpec Wolf = new SpeciesSpec { id = "wolf", name = "Grey Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Medium, structure = 0.9f };

    private static BattleReport Fight(ArmyRoster roster, ArmyStack stack, Bank bank, int seed, BattleSide enemy = null)
    {
        var side = roster.Muster(stack.id, Settings, n => Legend(n, (LegendClass.Vanguard, 1)));
        var report = BattleResolver.Resolve(new BattleSetup { attacker = side, defender = enemy ?? CreatureCombat.Side(Wolf, 4), field = new Battlefield { age = 1 }, seed = seed }, Settings);
        roster.Record(stack.id, side, report, attacker: true, bank, Settings);
        return report;
    }

    [Test]
    public void Record_WritesTheBattleBack()
    {
        var (roster, stack, bank) = Host("grave-warden", "grave-warden", "wasteland-archer");
        int before = roster.units.Sum(u => u.people);
        var report = Fight(roster, stack, bank, 3);
        Assert.IsTrue(report.AttackerWon, "a small pack does not hold against three companies");
        Assert.IsTrue(roster.units.All(u => u.battles == 1 && u.victories == 1 && u.merit > 0f));
        Assert.AreEqual(before - roster.units.Sum(u => u.people), bank.fallen, "the fallen are reported, the rest carry on");
        Assert.IsTrue(roster.units.All(u => u.integrity <= Settings.Section(u.specId).integrity));
        Assert.IsEmpty(bank.shared, "one legend alone shares the battle with no one");
    }

    [Test]
    public void Attachment_GrowsAfterThreeBattles()
    {
        var (roster, stack, bank) = Host("grave-warden", "wasteland-archer");
        var warden = roster.units[0];
        for (int i = 0; i < 2; i++) Fight(roster, stack, bank, 10 + i);
        Assert.AreEqual(0, warden.Stars("Aldric"), "two battles are not enough");
        Assert.IsEmpty(bank.nameRequests);
        Fight(roster, stack, bank, 12);
        Assert.AreEqual(1, warden.Stars("Aldric"), "after three battles the commander grows attached");
        Assert.IsTrue(bank.nameRequests.Any(r => r.unit == warden && r.legend == "Aldric"), "the player is asked to name the company");
        Assert.IsTrue(bank.fragments.Any(f => f.legend == "Aldric" && f.award.kind == FragmentKind.Defiance));
        Assert.IsTrue(bank.fragments.Any(f => f.legend == "Aldric" && f.award.kind == FragmentKind.Meaning));
        Assert.AreEqual(1, bank.eraAwards, "Era Score only the first time this Age, though both companies grew attached");
        Assert.AreEqual(Settings.conscription.attachmentEraScore, bank.eraScore);
        var side = roster.Muster(stack.id, Settings, n => Legend(n));
        Assert.AreEqual(1, side.sections[0].bonds["Aldric"]);

        for (int i = 0; i < 3; i++) Fight(roster, stack, bank, 20 + i);
        Assert.AreEqual(1, warden.Stars("Aldric"), "six battles: still one star");
        Fight(roster, stack, bank, 23);
        Assert.AreEqual(2, warden.Stars("Aldric"), "the seventh battle: two stars");
        Assert.AreEqual(1, bank.eraAwards);
        bank.age = 2;
        var fresh = roster.Conscript("grave-warden", bank, Settings);
        roster.Assign(fresh.id, stack.id);
        for (int i = 0; i < 3; i++) Fight(roster, stack, bank, 30 + i);
        Assert.AreEqual(2, bank.eraAwards, "a new Age pays again");
        for (int i = 0; i < 10; i++) Fight(roster, stack, bank, 40 + i);
        Assert.AreEqual(20, warden.Bond("Aldric").battles);
        Assert.AreEqual(2, warden.Stars("Aldric"));
        Fight(roster, stack, bank, 50);
        Assert.AreEqual(3, warden.Stars("Aldric"), "twenty-one battles: three stars");

        Assert.IsTrue(roster.Name(warden.id, "The Ashen Vow"));
        Assert.AreEqual("The Ashen Vow", warden.name);
        Assert.IsTrue(warden.named);
        Assert.AreEqual("Aldric", warden.namedFor);
    }

    [Test]
    public void Legends_InOneStackShareTheBattle()
    {
        var (roster, stack, bank) = Host("grave-warden", "wasteland-archer");
        roster.Lead(roster.units[0].id, "Mira");
        Fight(roster, stack, bank, 5);
        CollectionAssert.AreEquivalent(new[] { "Aldric", "Mira" }, bank.shared.Single());
        Assert.AreEqual(1, roster.units[0].Bond("Mira").battles, "the company's own leader grows attached too");
    }

    [Test]
    public void AttachedCompanies_AreHardToLoseButWeighMore()
    {
        BattleReport Lose(bool attached)
        {
            var side = OrchestralFormations.Raise(Settings.Template("hearth-guard"), Settings, 1, Legend("Aldric"));
            if (attached) foreach (var s in side.sections) s.bonds = new Dictionary<string, int> { ["Aldric"] = 3 };
            return BattleResolver.Resolve(new BattleSetup
            {
                attacker = side,
                defender = OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), Settings, 1, Legend("Kael", (LegendClass.Vanguard, 3))),
                field = new Battlefield { age = 1, magicAccess = 0.6f }, seed = 2,
            }, Settings);
        }
        var plain = Lose(false);
        var bound = Lose(true);
        Assert.Less(plain.winner, 0);
        Assert.Less(bound.winner, 0, "attachment does not win a hopeless battle");
        Assert.LessOrEqual(bound.attacker.destroyed + bound.attacker.captured, plain.attacker.destroyed + plain.attacker.captured, "its legend covers the escape");
        Assert.Greater(bound.attacker.integrityAfter, plain.attacker.integrityAfter, "more of the company gets away");
        var boundFate = bound.legends.Single(f => f.name == "Aldric");
        var plainFate = plain.legends.Single(f => f.name == "Aldric");
        Assert.Greater(boundFate.grief, 0f);
        Assert.Greater(boundFate.strain, plainFate.strain, "leaving them behind weighs on it far more");
    }

    [Test]
    public void MissingLegends_LoseTheirPosts()
    {
        var (roster, stack, bank) = Host("grave-warden");
        roster.Lead(roster.units[0].id, "Mira");
        var side = roster.Muster(stack.id, Settings, n => Legend(n));
        var strong = OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), Settings, 1);
        var report = BattleResolver.Resolve(new BattleSetup { attacker = side, defender = strong, field = new Battlefield { age = 1 }, seed = 1 }, Settings);
        Assert.Less(report.winner, 0);
        var lines = roster.Record(stack.id, side, report, attacker: true, bank, Settings);
        Assert.IsTrue(report.legends.All(f => f.missing));
        Assert.IsNull(roster.Stack(stack.id).commander);
        Assert.IsTrue(roster.units.All(u => u.leader == null));
        Assert.IsTrue(lines.Any(l => l.Contains("missing in action")));
    }

    [Test]
    public void Promotion_RaisesASoldierFromAVeteranCompany()
    {
        var (roster, stack, bank) = Host("grave-warden");
        var unit = roster.units[0];
        var offered = new List<PromotionCandidate>();
        roster.PromotionReady = offered.Add;
        Assert.IsEmpty(roster.PromotionCandidates(Settings));
        unit.merit = Settings.conscription.promotionMerit - 0.5f;
        Fight(roster, stack, bank, 7);
        Assert.AreEqual(unit.id, offered.Single().unitId, "the ledger announces a soldier ready to be raised");
        Assert.AreEqual(unit.id, roster.PromotionCandidates(Settings).Single().unitId);
        int people = unit.people;
        var soldier = roster.Promote(unit.id, Settings);
        Assert.IsNotNull(soldier);
        Assert.AreEqual(people - 1, unit.people);
        Assert.AreEqual(1, unit.promoted);
        Assert.IsEmpty(roster.PromotionCandidates(Settings), "the next soldier needs twice the merit");
    }
}
