using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the capital HUD's "Your Nation" slot (the scene's NationSlot, found by name so the scene is not edited) to the
/// culture: a click opens the <see cref="CultureWindow"/>, and its tooltip names the nation, its citizens, its culture,
/// its character and national foods. Keeps <see cref="CultureField.Name"/> (the Culture lens's word) current. Created by
/// <see cref="GenesisLoop"/>.
/// </summary>
public class CultureHud : MonoBehaviour
{
    private const string SlotName = "NationSlot";
    private GameObject _slot;
    private CultureSystem _culture;
    private TextMeshProUGUI _opportunity;

    private void Start()
    {
        _slot = FindByName(SlotName);
        if (_slot == null) GameLog.Warning($"No {SlotName} in the scene: the Culture window opens only from the world view.", LogChannel.UI);
        else
        {
            var button = _slot.GetComponent<Button>() ?? _slot.AddComponent<Button>();
            button.onClick.AddListener(CultureWindow.Toggle);
            var graphic = _slot.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }
        _culture = CultureSystem.Instance;
        if (_culture != null) _culture.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_culture != null) _culture.Changed -= Refresh;
    }

    private static GameObject FindByName(string name) =>
        Resources.FindObjectsOfTypeAll<RectTransform>().Where(t => t.name == name && t.gameObject.scene.IsValid()).Select(t => t.gameObject).FirstOrDefault();

    private void Refresh()
    {
        CultureField.Name = CultureSystem.IsNamed ? CultureSystem.Adjective : "Your people's";
        if (_slot == null) return;
        TooltipTrigger.Ensure(_slot).SetCustom(CultureSystem.NationName, Flavour(), "Nation", Summary());
        if (_opportunity == null)
        {
            _opportunity = CodeUI.TextButton(_slot.transform, "Culture atlas", () => CultureAtlasWindow.Open(), CodeUI.Theme(nameof(CultureHud)), 16f);
            CodeUI.Place(_opportunity.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -36), new Vector2(0, -4));
            _opportunity.textWrappingMode = TextWrappingModes.NoWrap;
            _opportunity.overflowMode = TextOverflowModes.Ellipsis;
            _opportunity.richText = false;
        }
        _opportunity.text = Opportunity(_culture);
        TooltipTrigger.Ensure(_opportunity.gameObject).SetCustom("Cultural atlas", _opportunity.text);
    }

    public static string Opportunity(CultureSystem c)
    {
        if (c == null || !c.State.founded) return "Culture atlas · founding awaits";
        var dispute = c.OpenDisputes().FirstOrDefault();
        if (dispute != null) return "Culture atlas · answer " + dispute.traditionName;
        var tradition = c.PendingTraditionChoices().FirstOrDefault();
        return tradition != null ? "Culture atlas · decide on " + tradition.name : "Culture atlas · explore your history";
    }

    private static string Flavour()
    {
        var myth = CultureSystem.Instance != null ? CultureSystem.Instance.Myth : null;
        return myth != null ? myth.answer : "A people not yet founded.";
    }

    /// <summary>The nation in a few lines (the HUD slot and the world banner share it).</summary>
    public static string Summary()
    {
        var culture = CultureSystem.Instance;
        var text = new StringBuilder();
        if (culture == null || !CultureSystem.IsFounded)
        {
            text.AppendLine(TooltipText.Muted("Research Horology to tell how your people were founded and give them a name."));
        }
        else
        {
            if (CultureSystem.IsNamed)
            {
                text.AppendLine(TooltipText.Row("Citizens", CultureSystem.Demonym));
                text.AppendLine(TooltipText.Row("Culture", CultureSystem.Adjective));
            }
            else text.AppendLine(TooltipText.Warn("Your people wait for a name."));
            text.AppendLine(TooltipText.Row("Character", culture.Character));
            text.AppendLine(TooltipText.Row("Rootedness", CultureRules.Percent(culture.Cohesion)));
            var national = culture.NationalFoods.Select(f => f.resource).ToList();
            if (national.Count > 0) text.AppendLine(TooltipText.Row("National table", string.Join(", ", national)));
            if (!string.IsNullOrEmpty(culture.PendingFood)) text.AppendLine(TooltipText.Warn($"{culture.PendingFood} is becoming theirs."));
            text.AppendLine(TooltipText.Row("Happiness", $"{culture.Happiness:0} ({CultureLifeRules.MoodWord(culture.Happiness)})"));
            text.AppendLine(TooltipText.Row("Unity", $"{culture.UnityHeld:0} ({culture.UnityPerSeventh().Total:+0.#;-0.#;0} a Seventh)"));
            var (next, until) = culture.NextHoliday();
            if (next != null) text.AppendLine(TooltipText.Row("Next holiday", until == 0 ? $"{next.name}, today" : $"{next.name}, in {until} Seventh{(until == 1 ? "" : "s")}"));
        }
        text.AppendLine();
        text.AppendLine(TooltipText.Muted("Click to open your nation: its myth, character, how its people live, Unity, holidays, kitchen, foodways, land and heritage."));
        return KeywordMarkup.SafeGlyphs(text.ToString().TrimEnd());
    }
}
