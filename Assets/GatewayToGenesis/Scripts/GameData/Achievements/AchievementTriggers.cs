using System;
using System.Collections.Generic;

/// <summary>Things that happen in the game that achievements listen for. Systems report them through <see cref="Achievements.Report"/>.</summary>
public enum AchievementSignal
{
    /// <summary>The White-Haven Library was opened.</summary>
    LibraryOpened,
    /// <summary>A chorus choice (a luck-based challenge) was resolved: <see cref="AchievementEvent.chorus"/>.</summary>
    ChorusResolved,
    /// <summary>An official death ledger was revised (the DeathRecordsRevision consequence): <see cref="AchievementEvent.amount"/> is the change.</summary>
    DeathLedgerRevised,
    /// <summary>The council changed: <see cref="AchievementEvent.amount"/> seated regular positions, <see cref="AchievementEvent.flag"/> a seated Head of State.</summary>
    CouncilChanged,
    /// <summary>An Age passed (its crisis resolved and the next Age began): <see cref="AchievementEvent.amount"/> is the number of the Age survived.</summary>
    AgeSurvived,
    /// <summary>A legend's Composure fell to a deeper state: <see cref="AchievementEvent.amount"/> is the <see cref="ComposureState"/> reached.</summary>
    ComposureFell,
    /// <summary>A legend's Motif Awakening embellished its Soul Leitmotif: <see cref="AchievementEvent.amount"/> is the Ornament (1 primary, 2 secondary),
    /// <see cref="AchievementEvent.flag"/> that it healed from Spiraling and one of its Legend Traits evolved.</summary>
    MotifAwakened,
    /// <summary>A legend reached the Catalytic Abyss of Emotion (the Awakened State).</summary>
    CatalyticAbyss,
    /// <summary>A settlement fell into ruins (<see cref="WorldRuins.Fall"/>): <see cref="AchievementEvent.flag"/> that it stood
    /// beyond your Administrative Authority (an Outpost or a detached settlement).</summary>
    SettlementLost,
    /// <summary>A civic left behind by the ruins of a fallen settlement was adopted (<see cref="WorldSystem.AdoptRuinCivic"/>).</summary>
    RuinCivicAdopted,
    /// <summary>A settlement was founded on the ruins of one of yours that fell: the lost settlement reclaimed.</summary>
    SettlementReclaimed,
    /// <summary>The culture was reformed (<see cref="CultureSystem.Reform"/>): <see cref="AchievementEvent.flag"/> that it digested the
    /// ways of a fallen settlement's ruins.</summary>
    CultureReformed,
    /// <summary>None of the civics in force when the culture was founded is left (<see cref="CultureSystem"/>).</summary>
    FoundingCivicsGone,
    /// <summary>Children were born in the Civilization (<see cref="PopGrowthLogic"/>): <see cref="AchievementEvent.amount"/> is the number born since the founding.</summary>
    PeopleBorn,
}

/// <summary>
/// One reported happening and what the triggers need to know about it. Report it only after the action it describes
/// has committed. <see cref="source"/> and <see cref="subject"/> are its evidence (kept in the world's reward ledger
/// as <see cref="AwardEvidence"/>): stable ids, never display text. New kinds of payload get typed fields here, reviewed
/// by the integration owner, rather than new meanings for <see cref="amount"/> and <see cref="flag"/>.
/// </summary>
public struct AchievementEvent
{
    /// <summary>Shape of the evidence written for an award; bump when <see cref="AwardEvidence"/> changes meaning.</summary>
    public const int EvidenceVersion = 1;

    public AchievementSignal signal;
    public ChorusResolution chorus;
    public int amount;
    public bool flag;
    /// <summary>Stable key of the committed happening, e.g. "age-passed:age-of-desolation:1".</summary>
    public string source;
    /// <summary>Stable id of the entity concerned (a legend, an Age, a story); null when none.</summary>
    public string subject;

    public static AchievementEvent Of(AchievementSignal signal, int amount = 0, bool flag = false) => new AchievementEvent { signal = signal, amount = amount, flag = flag };

    public static AchievementEvent Chorus(ChorusResolution resolution) => new AchievementEvent { signal = AchievementSignal.ChorusResolved, chorus = resolution };

    /// <summary>This happening with its evidence attached.</summary>
    public AchievementEvent From(string source, string subject = null)
    {
        var copy = this;
        copy.source = source;
        copy.subject = subject;
        return copy;
    }
}

/// <summary>
/// Which achievements the game can award today, and the rule for each. Keyed by <see cref="AchievementDefinition.id"/>
/// (the slug of the vault title): if a title changes in the vault, <c>ContentTests.EveryAchievementTriggerNamesAnAchievement</c>
/// fails until the key here follows, and the old id goes into <see cref="AchievementAliases"/> so earned unlocks carry
/// over. Every other achievement waits for a system that does not exist yet: Docs/Planning/ACHIEVEMENT_COVERAGE.csv
/// routes each one to its wave (<c>AchievementRegisterTests</c> keeps that register and this table in step).
/// </summary>
public static class AchievementTriggers
{
    /// <summary>Odds at or below this are the challenge slot's "Forsaken" tier.</summary>
    public const int ForsakenPercent = 10;

    /// <summary>Six regular council positions and a Head of State (vault: "6 Legends and a Head of State").</summary>
    public const int FullCouncilSeats = 6;

    public static readonly IReadOnlyDictionary<string, Func<AchievementEvent, bool>> Rules = new Dictionary<string, Func<AchievementEvent, bool>>
    {
        // Meet The White-Touched Archivist: "Find the entrance to an impossible library and a very peculiar narrator."
        ["an-eccentric-madman"] = e => e.signal == AchievementSignal.LibraryOpened,

        // Fail a high-stakes decision as a Critical Failure for a luck-based challenge.
        ["heads-you-vanish"] = e => e.signal == AchievementSignal.ChorusResolved && e.chorus.outcome == ChorusOutcome.CriticalFailure,

        // Win ... as a Critical Success ... hitting a threshold of Forsaken with 10% or less probability of success.
        ["butterfly-survives-the-storm"] = e => e.signal == AchievementSignal.ChorusResolved && e.chorus.outcome == ChorusOutcome.CriticalSuccess
                                               && e.chorus.successPercent <= ForsakenPercent,

        // Have Piety trigger the saving grace of a decision that should've failed in a luck-based challenge.
        ["the-aria-was-listening-that-day"] = e => e.signal == AchievementSignal.ChorusResolved && e.chorus.savedByRoll,

        // Revise the Death Count of any official ledger in your Civilization.
        ["the-trolley-that-kept-moving"] = e => e.signal == AchievementSignal.DeathLedgerRevised && e.amount != 0,

        // Fill all the slots of a Civilization's Government Council by appointing 6 Legends and a Head of State.
        ["a-league-of-legends"] = e => e.signal == AchievementSignal.CouncilChanged && e.amount >= FullCouncilSeats && e.flag,

        // Survive through Ages 0: "Escape The Inescapable Hunger." (Every famine outcome reaches the Age of Renewal.)
        ["the-brown-auric-peach"] = e => e.signal == AchievementSignal.AgeSurvived && e.amount == 0,

        // Survive through Ages I: "Survive the Great Plague."
        ["it-s-the-moths-the-moths"] = e => e.signal == AchievementSignal.AgeSurvived && e.amount == 1,

        // Witness any Legend reach the Spiraling Composure level (falling past it to Surrender reaches it too).
        ["let-s-go-to-therapy"] = e => e.signal == AchievementSignal.ComposureFell && e.amount >= (int)ComposureState.Spiraling,

        // Witness the Motif Awakening of a Legend to any Ornament.
        ["alchemical-pelican"] = e => e.signal == AchievementSignal.MotifAwakened && e.amount >= 1,

        // Witness a Legend recover from Spiraling Composure and as result evolve one of their Legend Traits alongside a new Motif Awakening.
        ["you-are-filled-with-determination"] = e => e.signal == AchievementSignal.MotifAwakened && e.flag,

        // Witness any Legend reach the Catalytic Abyss of Emotion.
        ["the-crux-of-nigredo"] = e => e.signal == AchievementSignal.CatalyticAbyss,

        // Reform a culture or adopt a Civic from the ruins of a fallen settlement: "The ruins of the past fuel the roots of the present."
        ["digestive-rebirth"] = e => e.signal == AchievementSignal.RuinCivicAdopted || (e.signal == AchievementSignal.CultureReformed && e.flag),

        // Have none of your original starting Civics: "Wait, at what point did we change culture?"
        ["ship-of-theseus"] = e => e.signal == AchievementSignal.FoundingCivicsGone,

        // Lose any settlement beyond the Administrative Authority of your borders.
        ["404"] = e => e.signal == AchievementSignal.SettlementLost && e.flag,

        // Reclaim a lost settlement back into your Civilization.
        ["welcome-back-traitors"] = e => e.signal == AchievementSignal.SettlementReclaimed,

        // Witness the first birth of your Civilization: "There is Beauty in That". Founding survivors arriving are migrants, not births.
        ["there-is-beauty-in-that"] = e => e.signal == AchievementSignal.PeopleBorn && e.amount >= 1,
    };

    /// <summary>The achievements <paramref name="happening"/> earns (ids, in the table's order).</summary>
    public static IEnumerable<string> Earned(AchievementEvent happening)
    {
        foreach (var rule in Rules)
        {
            if (rule.Value(happening)) yield return rule.Key;
        }
    }

    public static bool CanBeEarned(string id) => id != null && Rules.ContainsKey(id);
}
