using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What the Bestiary asks of an identified species until the people know the answer.</summary>
public enum HypothesisQuestion
{
    /// <summary>The Echo its births crowd into, or none of its own (<see cref="SpeciesSpec.breedingEcho"/>).</summary>
    Breeding,
    /// <summary>The harmonic climate it lives in (<see cref="SpeciesSpec.niche"/>).</summary>
    Niche,
    /// <summary>One creature it hunts, or none (<see cref="SpeciesSpec.prey"/>). Asked only of meat-eaters.</summary>
    Prey,
}

/// <summary>What watching or hunting a species can test.</summary>
public enum Evidence { Watch, Hunt }

/// <summary>The outcome of testing a guess against what was seen.</summary>
public enum Verdict { Untested, Confirmed, Refuted }

/// <summary>The people's guess at one question about a species (saved in <see cref="SpeciesRecord.hypotheses"/>).</summary>
[Serializable]
public class Hypothesis
{
    public HypothesisQuestion question;
    /// <summary>The answer the player is testing (an option's key), or null while none is chosen.</summary>
    public string guess;
    public bool confirmed;
    /// <summary>Guesses the evidence refuted, in the order they fell.</summary>
    public List<string> ruledOut = new List<string>();
    /// <summary>Confirmed with no guess refuted first: insight, paid once in Era Score.</summary>
    public bool insight;
}

/// <summary>A choice the player can make for a question: its key and how it reads.</summary>
public struct HypothesisOption
{
    public string key;
    public string label;
}

/// <summary>
/// Hypotheses about creatures, with no scene state (tested in <c>SpeciesHypothesesTests</c>). Once a species is identified
/// the Bestiary asks what the people cannot see at a glance: when it breeds, the harmonic climate it lives in, and (for a
/// meat-eater) what it hunts. The player chooses a guess; watching and hunting test only the guess made (the evidence
/// answers the question asked, nothing more), and each question is tested its own way:
/// <list type="bullet">
/// <item>Breeding: watched in the Echo guessed (young crowd its dens then, or they do not). "No season of its own" is
///   confirmed once it has been watched through all four Echoes with no crowding, refuted in the Echo its young crowd into.</item>
/// <item>Niche: every Echo its dens are watched.</item>
/// <item>Prey: every hunt of it by your people (what it had eaten tells).</item>
/// </list>
/// A refuted guess is ruled out (information the Bestiary keeps). A guess confirmed with nothing ruled out first is
/// insight (Era Score). Every question answered is understanding without the research (<see cref="SpeciesLevel.Understood"/>).
/// The loop: Docs/Planning/DISCOVERY_LOOP.md. Every number is a proposal.
/// </summary>
public static class SpeciesHypotheses
{
    public const string None = "none";

    /// <summary>The questions the Bestiary asks of a species (prey only of meat-eaters: the diet is known once identified).</summary>
    public static List<HypothesisQuestion> Questions(SpeciesSpec species)
    {
        var list = new List<HypothesisQuestion> { HypothesisQuestion.Breeding, HypothesisQuestion.Niche };
        if (species != null && (species.diet == CreatureDiet.Carnivore || species.diet == CreatureDiet.Omnivore)) list.Add(HypothesisQuestion.Prey);
        return list;
    }

    /// <summary>"When does it breed?"</summary>
    public static string Ask(HypothesisQuestion q)
    {
        switch (q)
        {
            case HypothesisQuestion.Breeding: return "When does it breed?";
            case HypothesisQuestion.Niche: return "Where does it thrive?";
            default: return "What does it hunt?";
        }
    }

    /// <summary>How a guess at the question is tested, for the Bestiary.</summary>
    public static string HowTested(HypothesisQuestion q)
    {
        switch (q)
        {
            case HypothesisQuestion.Breeding: return "tested when its dens are watched in the Echo you guessed";
            case HypothesisQuestion.Niche: return "tested each Echo its dens are watched from your land";
            default: return "tested each time your people hunt it";
        }
    }

    /// <summary>
    /// The answers the player may choose. Prey: the species your people have identified (the unknown cannot be guessed),
    /// and "none". <paramref name="echoName"/> names Echo 1-4.
    /// </summary>
    public static List<HypothesisOption> Options(HypothesisQuestion q, SpeciesSpec species, IEnumerable<SpeciesSpec> identified, Func<int, string> echoName)
    {
        var list = new List<HypothesisOption>();
        switch (q)
        {
            case HypothesisQuestion.Breeding:
                for (int e = 1; e <= TimeSystemLogic.EchoesPerCycle; e++) list.Add(new HypothesisOption { key = e.ToString(), label = echoName?.Invoke(e) ?? $"Echo {e}" });
                list.Add(new HypothesisOption { key = None, label = "no season of its own" });
                break;
            case HypothesisQuestion.Niche:
                list.Add(new HypothesisOption { key = nameof(HarmonicNiche.Coherent), label = "where Coherence is high" });
                list.Add(new HypothesisOption { key = nameof(HarmonicNiche.Discordant), label = "where the Loom is torn" });
                list.Add(new HypothesisOption { key = nameof(HarmonicNiche.Leyline), label = "on the leylines and silver water" });
                break;
            default:
                foreach (var s in (identified ?? Enumerable.Empty<SpeciesSpec>()).Where(s => s != null && species != null && s.id != species.id).OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase))
                    list.Add(new HypothesisOption { key = s.id, label = s.name ?? s.id });
                list.Add(new HypothesisOption { key = None, label = "no creature: it forages" });
                break;
        }
        return list;
    }

    /// <summary>The true answer's key ("2", "none", "Leyline", or the prey ids for <see cref="HypothesisQuestion.Prey"/>).</summary>
    public static HashSet<string> Truth(HypothesisQuestion q, SpeciesSpec species)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (species == null) return set;
        switch (q)
        {
            case HypothesisQuestion.Breeding:
                int e = CreatureTaxonomy.BreedingEchoOf(species);
                set.Add(e > 0 ? e.ToString() : None);
                break;
            case HypothesisQuestion.Niche:
                set.Add(species.niche.ToString());
                break;
            default:
                foreach (string id in (species.prey ?? new List<string>()).Where(id => !string.IsNullOrEmpty(id))) set.Add(id);
                if (set.Count == 0) set.Add(None);
                break;
        }
        return set;
    }

    /// <summary>The hypothesis on a question, or null when none was ever made.</summary>
    public static Hypothesis Of(SpeciesRecord record, HypothesisQuestion q) => record?.hypotheses?.FirstOrDefault(h => h != null && h.question == q);

    /// <summary>
    /// Choose (or change) a guess. Refused (false) when the question is answered, the answer was already ruled out, or
    /// it is no option at all.
    /// </summary>
    public static bool Guess(SpeciesRecord record, HypothesisQuestion q, string key, IEnumerable<HypothesisOption> options)
    {
        if (record == null || string.IsNullOrEmpty(key)) return false;
        if (options != null && !options.Any(o => string.Equals(o.key, key, StringComparison.OrdinalIgnoreCase))) return false;
        if (record.hypotheses == null) record.hypotheses = new List<Hypothesis>();
        var h = Of(record, q);
        if (h == null) record.hypotheses.Add(h = new Hypothesis { question = q });
        if (h.confirmed || h.ruledOut.Contains(key, StringComparer.OrdinalIgnoreCase)) return false;
        h.guess = key;
        return true;
    }

    /// <summary>
    /// What one piece of evidence says of a guess: a watch in <paramref name="echo"/> (1-4) with the Echoes it has been
    /// watched in so far (<paramref name="echoMask"/>, bit e-1), or a hunt. Untested when the evidence does not bear on it.
    /// </summary>
    public static Verdict Test(Hypothesis h, SpeciesSpec species, Evidence evidence, int echo, int echoMask)
    {
        if (h == null || h.confirmed || string.IsNullOrEmpty(h.guess) || species == null) return Verdict.Untested;
        var truth = Truth(h.question, species);
        switch (h.question)
        {
            case HypothesisQuestion.Breeding:
                if (evidence != Evidence.Watch || echo < 1) return Verdict.Untested;
                string now = echo.ToString();
                if (h.guess == None)
                {
                    if (truth.Contains(now)) return Verdict.Refuted;
                    int all = (1 << TimeSystemLogic.EchoesPerCycle) - 1;
                    return truth.Contains(None) && (echoMask & all) == all ? Verdict.Confirmed : Verdict.Untested;
                }
                if (h.guess != now) return Verdict.Untested;
                return truth.Contains(now) ? Verdict.Confirmed : Verdict.Refuted;
            case HypothesisQuestion.Niche:
                if (evidence != Evidence.Watch) return Verdict.Untested;
                return truth.Contains(h.guess) ? Verdict.Confirmed : Verdict.Refuted;
            default:
                if (evidence != Evidence.Hunt) return Verdict.Untested;
                return truth.Contains(h.guess) ? Verdict.Confirmed : Verdict.Refuted;
        }
    }

    /// <summary>Apply a verdict: a confirmed guess is the answer (insight when nothing was ruled out first), a refuted one is ruled out.</summary>
    public static void Apply(Hypothesis h, Verdict verdict)
    {
        if (h == null || verdict == Verdict.Untested) return;
        if (verdict == Verdict.Confirmed)
        {
            h.confirmed = true;
            h.insight = h.ruledOut.Count == 0;
            return;
        }
        if (!h.ruledOut.Contains(h.guess, StringComparer.OrdinalIgnoreCase)) h.ruledOut.Add(h.guess);
        h.guess = null;
    }

    /// <summary>Every question of the species answered by the people's own watching and hunting.</summary>
    public static bool Deduced(SpeciesRecord record, SpeciesSpec species) =>
        record != null && species != null && Questions(species).All(q => Of(record, q)?.confirmed == true);

    /// <summary>The answer to a question is known: worked out, or understood by study (Understood or better).</summary>
    public static bool Knows(SpeciesRecord record, HypothesisQuestion q, SpeciesLevel level) =>
        level >= SpeciesLevel.Understood || Of(record, q)?.confirmed == true;

    /// <summary>Species whose every question was worked out (GameValues "species_deduced").</summary>
    public static int DeducedCount(SpeciesLoreState state, WorldGenSettings settings) =>
        state?.species.Count(r => r != null && r.identified && Deduced(r, settings?.Species(r.species))) ?? 0;

    /// <summary>What the people said when a guess was tested, for the notice.</summary>
    public static string Report(HypothesisQuestion q, string guessKey, string guessLabel, Verdict verdict, string speciesName)
    {
        bool right = verdict == Verdict.Confirmed, none = guessKey == None;
        string fact;
        switch (q)
        {
            case HypothesisQuestion.Breeding:
                fact = none ? (right ? "has no season of its own: its young come in every Echo alike" : "has a season of its own after all: its young crowded its dens this Echo")
                    : right ? $"breeds in the {guessLabel}" : $"does not breed in the {guessLabel}";
                break;
            case HypothesisQuestion.Niche:
                fact = right ? $"thrives {guessLabel}" : $"does not thrive {guessLabel}";
                break;
            default:
                fact = none ? (right ? "hunts no creature: it forages" : "hunts after all: something was in its belly")
                    : right ? $"hunts the {guessLabel}" : $"does not hunt the {guessLabel}";
                break;
        }
        return right ? $"Your people's guess held: the {speciesName} {fact}." : $"Your people's guess failed: the {speciesName} {fact}. The Bestiary rules it out.";
    }
}
