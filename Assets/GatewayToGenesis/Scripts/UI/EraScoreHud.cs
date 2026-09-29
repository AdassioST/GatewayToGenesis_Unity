using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Binds the lower HUD's sun (LowerHUD/EraScore: its Amount label and Icon) to the Age's Era Score
/// (<see cref="AgeProgression.EraScore"/>), with a tooltip breaking it down by Act of Fate and listing the last deeds
/// that earned it. Found by name at runtime, so the scene is not edited; created by <see cref="GenesisLoop"/>.
/// </summary>
public class EraScoreHud : MonoBehaviour
{
    private TextMeshProUGUI _amount;
    private TooltipTrigger _trigger;
    private AgeProgression _ages;
    private float _pulseUntil;
    private Vector3 _baseScale = Vector3.one;

    private void Start()
    {
        var root = GameObject.Find("LowerHUD/EraScore") ?? FindByName("EraScore");
        if (root == null)
        {
            GameLog.Warning("No LowerHUD/EraScore in the scene: the Era Score has no sun to show on.", LogChannel.Ages);
            enabled = false;
            return;
        }
        var amount = root.transform.Find("Amount");
        _amount = amount != null ? amount.GetComponent<TextMeshProUGUI>() : root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (_amount != null) _baseScale = _amount.transform.localScale;
        var icon = root.transform.Find("Icon");
        _trigger = TooltipTrigger.Ensure(icon != null ? icon.gameObject : root);
        if (_amount != null) TooltipTrigger.Ensure(_amount.gameObject);
        // Clicking the sun opens the Chronicle, the whole timeline of Era Score.
        foreach (var go in new[] { icon != null ? icon.gameObject : root, _amount != null ? _amount.gameObject : null })
        {
            if (go == null) continue;
            var graphic = go.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
            var button = go.GetComponent<UnityEngine.UI.Button>() ?? go.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(EraTimelineWindow.Toggle);
        }
        _ages = AgeProgression.Instance;
        if (_ages != null)
        {
            _ages.Changed += Refresh;
            _ages.EraScoreAwarded += OnAwarded;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (_ages == null) return;
        _ages.Changed -= Refresh;
        _ages.EraScoreAwarded -= OnAwarded;
    }

    private static GameObject FindByName(string name) =>
        Resources.FindObjectsOfTypeAll<RectTransform>().Where(t => t.name == name && t.gameObject.scene.IsValid()).Select(t => t.gameObject).FirstOrDefault();

    private void OnAwarded(int points, string reason) => _pulseUntil = Time.unscaledTime + 0.6f;

    private void Update()
    {
        if (_amount == null) return;
        // A brief swell when Era Score is earned.
        float t = GameSettings.ReduceMotion ? 0f : Mathf.Clamp01((_pulseUntil - Time.unscaledTime) / 0.6f);
        _amount.transform.localScale = _baseScale * (1f + 0.35f * Mathf.Sin(t * Mathf.PI));
    }

    private void Refresh()
    {
        if (_ages == null || _ages.Current == null) return;
        if (_amount != null) _amount.text = _ages.EraScore.ToString();
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Row("This Age", _ages.EraScore.ToString()));
        for (int act = 0; act < _ages.EraScoreByAct.Count; act++)
            text.AppendLine(TooltipText.Bullet($"{_ages.Current.ActLabel(act)}: {_ages.EraScoreByAct[act]}{(act == _ages.Act ? TooltipText.Muted(" (now)") : string.Empty)}"));
        text.AppendLine();
        text.AppendLine(TooltipText.Muted("Earned by great deeds: Event Technologies, new towns and outposts, a settled Trade Nexus, Major Settlements, suzerainties, landmarks and Sacred Sites found. Era Score beyond the need eases the Age Crisis."));
        if (_ages.EraScoreLog.Count > 0)
        {
            text.AppendLine();
            foreach (var line in _ages.EraScoreLog.Take(6)) text.AppendLine(TooltipText.Bullet(line));
        }
        text.AppendLine();
        text.AppendLine(TooltipText.Muted("Click for the Chronicle: every award since the world began, by Cycle, Echo and Phase."));
        if (_trigger != null) _trigger.SetCustom("Era Score", KeywordMarkup.SafeGlyphs(text.ToString().TrimEnd()));
        if (_amount != null) TooltipTrigger.Ensure(_amount.gameObject).SetCustom("Era Score", KeywordMarkup.SafeGlyphs(text.ToString().TrimEnd()));
    }
}
