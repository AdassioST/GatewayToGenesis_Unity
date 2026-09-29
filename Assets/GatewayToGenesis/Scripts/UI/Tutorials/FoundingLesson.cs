using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The first lesson, in the Auric Aria's voice: while the founders wait at the gates (<see cref="PopGrowthLogic.FoundersWaiting"/>),
/// every pause brings her back to ask that they be fed from what glows in gold (the Vital Resource; every twelve Food
/// brings one home for good). When the last founder comes in during play she says a farewell at the next pause, once:
/// from now on, new mouths must be fed.
/// </summary>
public class FoundingLesson : TutorialLesson
{
    private const float FarewellSeconds = 7f;

    private bool _sawFounders;
    private Graphic _button;

    public override string Id => "founding";
    public override bool IsHint => false;
    public override TutorialPlace Place => TutorialPlace.Capital;

    public override bool Speak(out TutorialCard card)
    {
        card = default;
        var pop = PopGrowthLogic.Instance;
        if (pop == null || GameUnitsLogic.Instance == null) return false;
        int waiting = pop.FoundersWaiting, total = Mathf.Max(waiting, pop.Growth.foundingMigrants);
        if (waiting > 0)
        {
            _sawFounders = true;
            card = new TutorialCard("A broken world awaits your voice", $"Feed Your {total} Founding Members", "Gather food from what glows in gold...");
            return true;
        }
        // The last founder came in while playing (not a loaded save): a farewell, heard once.
        if (!_sawFounders) return false;
        card = new TutorialCard("Listen... the hearths are lit",
            $"Your {total} Founders Are Home",
            "They will never hunger again. Every soul who follows must be fed from what you gather.", FarewellSeconds);
        return true;
    }

    // The button that gathers Food (the capital's Vital Resource), found once.
    public override Graphic Target()
    {
        if (PopGrowthLogic.Instance == null || PopGrowthLogic.Instance.FoundersWaiting <= 0) return null;
        if (_button != null) return _button;
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        foreach (var click in Object.FindObjectsByType<ClickLogic>(FindObjectsInactive.Exclude))
        {
            if (click.currentMode != ClickLogic.ClickMode.AddResource || click.activeResource != food) continue;
            var button = click.GetComponent<Button>();
            _button = button != null && button.targetGraphic != null ? button.targetGraphic : click.GetComponent<Graphic>();
            if (_button != null) break;
        }
        return _button;
    }
}
