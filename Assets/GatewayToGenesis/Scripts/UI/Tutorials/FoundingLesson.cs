using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The first lesson, in the Auric Aria's voice: while the founders wait at the gates (<see cref="PopGrowthLogic.FoundersWaiting"/>),
/// every pause brings her back to ask that they be fed from what glows in gold (the Vital Resource; every twelve Food
/// brings one home for good). The moment the last founder comes in during play she announces it, once, without waiting
/// for a pause: from now on, new mouths must be fed.
/// </summary>
public class FoundingLesson : TutorialLesson
{
    private bool _sawFounders, _announced;
    private Graphic _button;

    public override string Id => "founding";
    public override bool IsHint => false;
    public override TutorialPlace Place => TutorialPlace.Capital;

    private static int Founders(PopGrowthLogic pop) => Mathf.Max(pop.FoundersWaiting, pop.Growth.foundingMigrants);

    public override bool Speak(out AuricWords words)
    {
        words = default;
        var pop = PopGrowthLogic.Instance;
        if (pop == null || GameUnitsLogic.Instance == null || pop.FoundersWaiting <= 0) return false;
        words = AuricWords.Announcement("founding.feed", "A broken world awaits your voice", $"Feed Your {Founders(pop)} Founding Members", "Gather food from what glows in gold...");
        return true;
    }

    // The last founder came in while playing (not a loaded save): announced at once.
    public override void Watch()
    {
        var pop = PopGrowthLogic.Instance;
        if (pop == null || _announced) return;
        if (pop.FoundersWaiting > 0)
        {
            _sawFounders = true;
            return;
        }
        if (!_sawFounders) return;
        _announced = true;
        AuricAria.Announce("founding.home", "Listen... the hearths are lit", $"Your {Founders(pop)} Founders Are Home", "Every soul who follows must be fed.");
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
