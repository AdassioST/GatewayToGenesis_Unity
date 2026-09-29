using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What a conscripted company's life is measured in: merit toward promotion (the path from a nameless soldier to a
/// Legend, owner's note Sept 28 2026). All proposals.
/// </summary>
[Serializable]
public class ConscriptionTuning
{
    [Tooltip("Merit a company earns for each battle it stands through, each victory, each defeat survived, and each Mind Break it came back from.")]
    public float meritPerBattle = 1f, meritPerVictory = 2f, meritPerDefeatSurvived = 1f, meritPerMindBreakEndured = 1f;
    [Tooltip("Merit at which one of its soldiers is ready to be raised as a legend (PromotionCandidates).")]
    public float promotionMerit = 12f;

    [Header("Attachment (CompanyBond)")]
    [Tooltip("Battles a legend must fight with a company (as its stack's commander or its leader) for 1, 2 and 3 stars of attachment.")]
    public int[] attachmentBattles = { 3, 7, 21 };
    [Tooltip("Fragments a legend earns with each new star of attachment to a company.")]
    public List<FragmentAward> attachmentFragments = new List<FragmentAward> { new FragmentAward(FragmentKind.Defiance, 3), new FragmentAward(FragmentKind.Meaning, 3) };
    [Tooltip("Era Score the first time in each Age a legend grows attached to a company.")]
    public int attachmentEraScore = 2;
}

/// <summary>
/// One conscripted company: a section raised from the population (Grave Wardens, Wasteland Archers...), nameless
/// soldiers who carry their wounds and their merit from battle to battle. Saved by the roster's owner.
/// </summary>
[Serializable]
public class ConscriptUnit
{
    public string id;
    public string specId;
    public string name;
    /// <summary>Soldiers alive in it now, and how many were raised.</summary>
    public int people, raisedPeople;
    /// <summary>Integrity it carries into its next battle (wounds heal back into it; the dead do not).</summary>
    public float integrity;
    /// <summary>The legend leading it, or empty.</summary>
    public string leader;
    /// <summary>The stack it marches in, or empty.</summary>
    public string stack;
    public int battles, victories;
    public float merit;
    public List<string> deeds = new List<string>();
    /// <summary>Soldiers already raised from it as legends.</summary>
    public int promoted;
    /// <summary>The legends attached to it, and how far (<see cref="CompanyBond"/>).</summary>
    public List<CompanyBond> bonds = new List<CompanyBond>();
    /// <summary>Named by the player (a legend's attachment asks for it), and by whose attachment.</summary>
    public bool named;
    public string namedFor;

    public CompanyBond Bond(string legend) => bonds.FirstOrDefault(b => b != null && b.legend == legend);
    public int Stars(string legend) => Bond(legend)?.stars ?? 0;
}

/// <summary>A stack: companies marching together under one legend, its commander (the Battle Conductor).</summary>
[Serializable]
public class ArmyStack
{
    public string id;
    public string name;
    public string commander;
    public SpellTempo tempo = SpellTempo.Staccato;
    public List<string> units = new List<string>();
}

/// <summary>What conscription draws on: the people, the stores, the technologies and the Age.</summary>
public interface IConscriptionBank
{
    int People { get; }
    int Age { get; }
    bool Has(string technology);
    float Amount(string resource);
    void Spend(string resource, float amount);
    /// <summary>People leave the population to serve.</summary>
    void Enlist(int people, string company);
    /// <summary>Soldiers come home.</summary>
    void Discharge(int people, string company);
    /// <summary>Soldiers fell in battle (for the death records and the council's grief).</summary>
    void Fallen(int people, string cause);
    /// <summary>A legend earns fragments (a company's attachment).</summary>
    void AwardFragments(string legend, List<FragmentAward> reward, string deed);
    void AwardEraScore(int points, string reason);
    /// <summary>Legends who fought together in one stack share the battle (affection, <see cref="LegendProgress.ShareExperience"/>).</summary>
    void ShareBattle(IEnumerable<string> legends, string key, string memory, bool wound);
    /// <summary>A legend grew attached to a company: the player is asked to name it (<see cref="ArmyRoster.Name"/>).</summary>
    void RequestName(ConscriptUnit unit, string legend);
}

/// <summary>
/// A soldier whose company has earned enough merit to raise one of its own as a legend. The legend-earning system
/// (not built yet) takes it with <see cref="ArmyRoster.Promote"/>; the roster only keeps the ledger.
/// </summary>
public sealed class PromotionCandidate
{
    public string unitId, unitName, specId;
    public float merit;
    public int battles, victories;
    public List<string> deeds = new List<string>();
}

/// <summary>
/// The civilization's conscripted companies and the stacks they march in, with no scene state (tested in
/// <c>ConscriptionTests</c>). Companies are raised from the population at a section kind's cost
/// (<see cref="CombatSectionSpec.conscripted"/>: Grave Warden, Wasteland Archer), each can be led by a legend (it lends its
/// Greats to that company), and each stack has a legend as commander. <see cref="Muster"/> turns a stack into a
/// <see cref="BattleSide"/>; <see cref="Record"/> writes the battle back: the fallen, the wounds, the captured and the
/// cut-down companies, legends who went missing losing their posts, and merit toward promotion. A legend holds one post
/// at a time. The roster is plain data: its owner saves it.
/// </summary>
[Serializable]
public class ArmyRoster
{
    public List<ConscriptUnit> units = new List<ConscriptUnit>();
    public List<ArmyStack> stacks = new List<ArmyStack>();
    public int nextId = 1;
    /// <summary>Ages in which a company attachment already paid its Era Score.</summary>
    public List<int> attachmentAges = new List<int>();

    /// <summary>A company's merit reached promotion (fired once per soldier's worth of merit).</summary>
    [NonSerialized] public Action<PromotionCandidate> PromotionReady;

    public ConscriptUnit Unit(string id) => units.FirstOrDefault(u => u != null && u.id == id);
    public ArmyStack Stack(string id) => stacks.FirstOrDefault(s => s != null && s.id == id);

    /// <summary>The section kinds that can be conscripted (in this Age, with these technologies, when a bank is given).</summary>
    public static IEnumerable<CombatSectionSpec> Conscriptable(CombatSettings settings, IConscriptionBank bank = null) =>
        (settings ?? new CombatSettings()).Sections.Where(s => s != null && s.conscripted &&
            (bank == null || (s.minAge <= bank.Age && (string.IsNullOrEmpty(s.technology) || bank.Has(s.technology)))));

    /// <summary>Why a company of <paramref name="specId"/> cannot be raised now, or null when it can.</summary>
    public static string WhyNotConscript(string specId, IConscriptionBank bank, CombatSettings settings)
    {
        var spec = (settings ?? new CombatSettings()).Section(specId);
        if (spec == null) return $"No such unit: {specId}";
        if (!spec.conscripted) return $"{spec.name} is not raised from the population";
        if (bank == null) return "Nothing to raise it from";
        if (spec.minAge > bank.Age) return $"{spec.name} cannot be raised before Age {AgeMagic.Numeral(spec.minAge)}";
        if (!string.IsNullOrEmpty(spec.technology) && !bank.Has(spec.technology)) return $"{spec.name} needs {spec.technology}";
        if (bank.People < Math.Max(1, spec.people)) return $"{spec.name} needs {Math.Max(1, spec.people)} people to enlist; there are {bank.People}";
        foreach (var c in spec.cost ?? new List<ResourceAmount>())
            if (c != null && c.amount > 0f && bank.Amount(c.resource) < c.amount) return $"{spec.name} costs {c.amount:0.#} {c.resource}";
        return null;
    }

    /// <summary>Raises a company: spends its cost, enlists its people, names it ("1st Grave Wardens"). Null when it cannot.</summary>
    public ConscriptUnit Conscript(string specId, IConscriptionBank bank, CombatSettings settings, string name = null)
    {
        if (WhyNotConscript(specId, bank, settings) != null) return null;
        var spec = settings.Section(specId);
        foreach (var c in spec.cost ?? new List<ResourceAmount>()) if (c != null && c.amount > 0f) bank.Spend(c.resource, c.amount);
        int people = Math.Max(1, spec.people);
        int ordinal = units.Count(u => u.specId == spec.id) + 1;
        var unit = new ConscriptUnit
        {
            id = "unit-" + nextId++, specId = spec.id, name = string.IsNullOrEmpty(name) ? $"{Ordinal(ordinal)} {Plural(spec.name)}" : name,
            people = people, raisedPeople = people, integrity = spec.integrity,
        };
        bank.Enlist(people, unit.name);
        unit.deeds.Add($"Raised in Age {AgeMagic.Numeral(bank.Age)}");
        units.Add(unit);
        return unit;
    }

    /// <summary>Disbands a company: its soldiers come home; its leader is free again.</summary>
    public bool Disband(string unitId, IConscriptionBank bank)
    {
        var unit = Unit(unitId);
        if (unit == null) return false;
        bank?.Discharge(unit.people, unit.name);
        Remove(unit);
        return true;
    }

    private void Remove(ConscriptUnit unit)
    {
        units.Remove(unit);
        foreach (var s in stacks) s.units.Remove(unit.id);
    }

    /// <summary>The legend's post (a company it leads, or a stack it commands), or null when free.</summary>
    public string PostOf(string legend)
    {
        if (string.IsNullOrEmpty(legend)) return null;
        var stack = stacks.FirstOrDefault(s => s.commander == legend);
        if (stack != null) return "commands " + stack.name;
        var unit = units.FirstOrDefault(u => u.leader == legend);
        return unit != null ? "leads " + unit.name : null;
    }

    /// <summary>Every legend holding a post in the army.</summary>
    public IEnumerable<string> LegendsInService =>
        stacks.Select(s => s.commander).Concat(units.Select(u => u.leader)).Where(n => !string.IsNullOrEmpty(n)).Distinct();

    /// <summary>A legend takes command of a company (empty or null: it goes unled). A legend holds one post at a time.</summary>
    public bool Lead(string unitId, string legend)
    {
        var unit = Unit(unitId);
        if (unit == null) return false;
        if (!string.IsNullOrEmpty(legend) && PostOf(legend) != null && unit.leader != legend) return false;
        unit.leader = string.IsNullOrEmpty(legend) ? null : legend;
        return true;
    }

    /// <summary>Forms a stack of companies (each leaves any stack it marched in) under a commander (may be empty).</summary>
    public ArmyStack FormStack(string name, string commander, IEnumerable<string> unitIds, SpellTempo tempo = SpellTempo.Staccato)
    {
        if (!string.IsNullOrEmpty(commander) && PostOf(commander) != null) return null;
        var stack = new ArmyStack { id = "stack-" + nextId++, name = string.IsNullOrEmpty(name) ? $"{Ordinal(stacks.Count + 1)} Host" : name, commander = commander, tempo = tempo };
        stacks.Add(stack);
        foreach (var id in unitIds ?? Enumerable.Empty<string>()) Assign(id, stack.id);
        return stack;
    }

    /// <summary>A legend takes command of a stack (empty or null: it goes without one).</summary>
    public bool Command(string stackId, string legend)
    {
        var stack = Stack(stackId);
        if (stack == null) return false;
        if (!string.IsNullOrEmpty(legend) && PostOf(legend) != null && stack.commander != legend) return false;
        stack.commander = string.IsNullOrEmpty(legend) ? null : legend;
        return true;
    }

    /// <summary>Moves a company into a stack (null: out of every stack).</summary>
    public bool Assign(string unitId, string stackId)
    {
        var unit = Unit(unitId);
        if (unit == null) return false;
        foreach (var s in stacks) s.units.Remove(unitId);
        var stack = Stack(stackId);
        unit.stack = stack?.id;
        if (stack != null) stack.units.Add(unitId);
        return true;
    }

    /// <summary>
    /// A stack as a side of battle: each company a section at its carried Integrity, led by its legend, the stack's
    /// commander at its head. <paramref name="legendOf"/> turns a legend's name into its battle self (null: absent).
    /// </summary>
    public BattleSide Muster(string stackId, CombatSettings settings, Func<string, BattleLegend> legendOf = null)
    {
        settings = settings ?? new CombatSettings();
        var stack = Stack(stackId);
        if (stack == null) return null;
        var side = new BattleSide
        {
            name = stack.name, tempo = stack.tempo,
            conductor = string.IsNullOrEmpty(stack.commander) ? null : legendOf?.Invoke(stack.commander),
        };
        foreach (var unit in stack.units.Select(Unit).Where(u => u != null && u.people > 0))
        {
            var spec = settings.Section(unit.specId);
            if (spec == null) continue;
            var sec = CombatSection.Raise(spec);
            sec.name = unit.name;
            sec.unitId = unit.id;
            sec.count = unit.people;
            sec.integrity = Mathf.Clamp(unit.integrity, 0f, sec.maxIntegrity);
            sec.leader = string.IsNullOrEmpty(unit.leader) ? null : legendOf?.Invoke(unit.leader);
            sec.bonds = unit.bonds.Where(b => b != null && b.stars > 0).ToDictionary(b => b.legend, b => b.stars);
            side.sections.Add(sec);
        }
        return side;
    }

    /// <summary>
    /// Writes a battle back into the stack's companies: wounds heal back into Integrity, the dead are gone
    /// (<see cref="IConscriptionBank.Fallen"/>), companies cut down or taken are struck from the roster, legends who went
    /// missing lose their posts, merit is earned. Returns what changed, for the log. Fires <see cref="PromotionReady"/>.
    /// </summary>
    public List<string> Record(string stackId, BattleSide side, BattleReport report, bool attacker, IConscriptionBank bank, CombatSettings settings)
    {
        settings = settings ?? new CombatSettings();
        var tuning = settings.conscription ?? new ConscriptionTuning();
        var lines = new List<string>();
        var stack = Stack(stackId);
        if (stack == null || side == null || report == null) return lines;
        bool won = attacker ? report.winner > 0 : report.winner < 0;
        bool lost = attacker ? report.winner <= 0 : report.winner > 0;
        foreach (var sec in side.sections.Where(s => !string.IsNullOrEmpty(s.unitId)))
        {
            var unit = Unit(sec.unitId);
            if (unit == null) continue;
            unit.battles++;
            if (sec.destroyed || sec.captured)
            {
                int gone = sec.captured ? Math.Max(0, unit.people - sec.Alive) : unit.people;
                if (gone > 0) bank?.Fallen(gone, $"battle ({unit.name})");
                lines.Add(sec.captured ? $"{unit.name} was taken captive ({sec.Alive} alive in enemy hands)." : $"{unit.name} was cut down to the last.");
                Remove(unit);
                continue;
            }
            // Wounds heal back into the company; the dead do not come back.
            unit.integrity = Mathf.Clamp(Math.Max(0f, sec.integrity) + sec.wounded, 0f, sec.maxIntegrity);
            int alive = Math.Max(1, (int)Math.Round(unit.raisedPeople * unit.integrity / Math.Max(1f, sec.maxIntegrity)));
            int fallen = Math.Max(0, unit.people - alive);
            unit.people = Math.Min(unit.people, alive);
            if (fallen > 0) { bank?.Fallen(fallen, $"battle ({unit.name})"); lines.Add($"{unit.name} lost {fallen} of its own."); }
            unit.merit += tuning.meritPerBattle + (won ? tuning.meritPerVictory : 0f) + (lost ? tuning.meritPerDefeatSurvived : 0f) +
                          (sec.timesMindBroken > 0 ? tuning.meritPerMindBreakEndured : 0f);
            if (won) unit.victories++;
            unit.deeds.Add($"{(won ? "Won" : lost ? "Survived a defeat" : "Held")} against {(attacker ? report.defender.name : report.attacker.name)}");
            OfferPromotion(unit, tuning);
            GrowBonds(unit, new[] { side.conductor, sec.leader }, bank, tuning, lines);
        }
        // Legends who fought in one stack share the battle: affection grows between them (a lost one binds them harder).
        var together = side.Legends.Select(l => l.name).Distinct().ToList();
        if (together.Count > 1) bank?.ShareBattle(together, $"battle:{stack.id}:{report.seed}:{report.measures}", $"Fought side by side in {stack.name} against {(attacker ? report.defender.name : report.attacker.name)}", lost);
        // Legends who left a doomed field lose their posts until they walk home.
        foreach (var fate in report.legends.Where(f => f.attacker == attacker && f.missing))
        {
            if (stack.commander == fate.name) stack.commander = null;
            foreach (var u in units.Where(u => u.leader == fate.name)) u.leader = null;
            lines.Add($"{fate.name} is missing in action and gives up the post.");
        }
        return lines;
    }

    /// <summary>A legend grew attached to a company: (company, legend, stars).</summary>
    [NonSerialized] public Action<ConscriptUnit, string, int> Attached;

    // Each battle fought with a company (as its stack's commander or its own leader) draws a legend closer to it.
    private void GrowBonds(ConscriptUnit unit, IEnumerable<BattleLegend> present, IConscriptionBank bank, ConscriptionTuning tuning, List<string> lines)
    {
        foreach (string legend in present.Where(l => l != null).Select(l => l.name).Distinct().ToList())
        {
            var bond = unit.Bond(legend);
            if (bond == null) unit.bonds.Add(bond = new CompanyBond { legend = legend });
            bond.battles++;
            int stars = CompanyBond.StarsFor(bond.battles, tuning.attachmentBattles);
            if (stars <= bond.stars) continue;
            bond.stars = stars;
            lines.Add(stars == 1 ? $"{legend} has grown attached to {unit.name}." : $"{legend}'s attachment to {unit.name} deepens ({stars}★).");
            bank?.AwardFragments(legend, tuning.attachmentFragments, $"Grew attached to {unit.name} ({stars}★)");
            if (stars == 1)
            {
                if (!unit.named) bank?.RequestName(unit, legend);
                if (bank != null && tuning.attachmentEraScore > 0 && !attachmentAges.Contains(bank.Age))
                {
                    attachmentAges.Add(bank.Age);
                    bank.AwardEraScore(tuning.attachmentEraScore, $"{legend} grew attached to {unit.name}");
                }
            }
            Attached?.Invoke(unit, legend, stars);
        }
    }

    /// <summary>The player names a company (a legend's attachment asks for it). False for an empty name or no such company.</summary>
    public bool Name(string unitId, string name)
    {
        var unit = Unit(unitId);
        if (unit == null || string.IsNullOrWhiteSpace(name)) return false;
        string old = unit.name;
        unit.name = name.Trim();
        unit.named = true;
        unit.namedFor = unit.bonds.Where(b => b.stars > 0).OrderByDescending(b => b.stars).ThenByDescending(b => b.battles).Select(b => b.legend).FirstOrDefault();
        unit.deeds.Add($"Named {unit.name} (once {old})");
        return true;
    }

    private void OfferPromotion(ConscriptUnit unit, ConscriptionTuning tuning)
    {
        if (tuning.promotionMerit <= 0f || unit.people <= 0) return;
        if (unit.merit < tuning.promotionMerit * (unit.promoted + 1)) return;
        PromotionReady?.Invoke(Candidate(unit));
    }

    private static PromotionCandidate Candidate(ConscriptUnit u) => new PromotionCandidate
    {
        unitId = u.id, unitName = u.name, specId = u.specId, merit = u.merit, battles = u.battles, victories = u.victories, deeds = new List<string>(u.deeds),
    };

    /// <summary>Companies with a soldier ready to be raised as a legend, the most merit first.</summary>
    public IEnumerable<PromotionCandidate> PromotionCandidates(CombatSettings settings)
    {
        var tuning = (settings ?? new CombatSettings()).conscription ?? new ConscriptionTuning();
        return units.Where(u => u.people > 0 && tuning.promotionMerit > 0f && u.merit >= tuning.promotionMerit * (u.promoted + 1))
            .OrderByDescending(u => u.merit).Select(Candidate);
    }

    /// <summary>
    /// Takes one soldier out of a company to become a legend (the legend-earning system creates the legend itself).
    /// Returns the soldier's record, or null when the company has no one ready.
    /// </summary>
    public PromotionCandidate Promote(string unitId, CombatSettings settings)
    {
        var unit = Unit(unitId);
        var tuning = (settings ?? new CombatSettings()).conscription ?? new ConscriptionTuning();
        if (unit == null || unit.people <= 0 || unit.merit < tuning.promotionMerit * (unit.promoted + 1)) return null;
        var candidate = Candidate(unit);
        unit.promoted++;
        unit.people--;
        unit.raisedPeople = Math.Max(unit.people, unit.raisedPeople - 1);
        unit.deeds.Add("One of its own was raised as a legend");
        if (unit.people <= 0) Remove(unit);
        return candidate;
    }

    private static string Ordinal(int n)
    {
        int mod100 = n % 100, mod10 = n % 10;
        string suffix = mod100 >= 11 && mod100 <= 13 ? "th" : mod10 == 1 ? "st" : mod10 == 2 ? "nd" : mod10 == 3 ? "rd" : "th";
        return n + suffix;
    }

    private static string Plural(string name) => string.IsNullOrEmpty(name) ? "Company" : name.EndsWith("s") ? name : name + "s";
}

/// <summary>The live game as a conscription bank: the population, the resource slots, the researched technologies, the Age.</summary>
public sealed class LiveConscriptionBank : IConscriptionBank
{
    public int People => PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.TotalPeople : 0;
    public int Age => GameAge.Number;
    public bool Has(string technology) => TechnologyTreeLogic.IsResearched(technology);

    public float Amount(string resource)
    {
        var slot = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(resource) : null;
        return slot != null ? slot.amount : 0f;
    }

    public void Spend(string resource, float amount)
    {
        var slot = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(resource) : null;
        if (slot != null) slot.ChangeAmount(-amount);
    }

    public void Enlist(int people, string company) { if (PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.Enlist(people, company); }
    public void Discharge(int people, string company) { if (PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.Discharge(people, company); }
    public void Fallen(int people, string cause) { if (PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.ReviseDeathRecords(people); }

    public void AwardFragments(string legend, List<FragmentAward> reward, string deed)
    {
        if (LegendProgress.Instance != null) LegendProgress.Instance.Award(legend, reward, deed);
    }

    public void AwardEraScore(int points, string reason) => AgeProgression.Award(points, reason);

    public void ShareBattle(IEnumerable<string> legends, string key, string memory, bool wound)
    {
        if (LegendProgress.Instance != null) LegendProgress.Instance.ShareExperience(legends, key, memory, direction: 1, thread: "Cindergale", wound: wound);
    }

    public void RequestName(ConscriptUnit unit, string legend) =>
        NotificationFeed.Push($"{legend} is attached to {unit.name}", $"{legend} has fought beside {unit.name} long enough to call them their own. Give the company a name.",
            NotificationFeed.Topic.Council, key: "name-company:" + unit.id);
}

/// <summary>
/// A legend's attachment to one company (owner, Sept 28 2026): after enough battles fought with it (as its stack's
/// commander or its own leader) it grows attached, 1 to 3 stars. With the legend on the field the company fights harder,
/// and in a rout the legend covers its escape; losing it, or leaving it behind, weighs on the legend far more.
/// </summary>
[Serializable]
public class CompanyBond
{
    public string legend;
    public int battles;
    public int stars;

    /// <summary>Stars (0-3) for battles fought together.</summary>
    public static int StarsFor(int battles, int[] thresholds)
    {
        var t = thresholds != null && thresholds.Length >= 3 ? thresholds : new[] { 3, 7, 21 };
        int stars = 0;
        for (int i = 0; i < 3; i++) if (battles >= t[i]) stars = i + 1;
        return stars;
    }
}
