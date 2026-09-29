using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// The capital HUD's Bestiary button (built in code, <see cref="CodeUI"/>, below Ballads &amp; Bonds): hidden until a
/// researched technology carries the <see cref="SpeciesKnowledge.BestiaryUnlock"/> Special unlockable (the way Horology
/// unlocks time-keeping and Pathfinder Training the map), then opens the <see cref="BestiaryWindow"/>. Hidden while a
/// story is told and while the world map is open. Created by <see cref="GenesisLoop"/>.
/// </summary>
public class BestiaryHud : MonoBehaviour
{
    private TooltipTheme _theme;
    private RectTransform _plate;
    private TextMeshProUGUI _label;
    private float _refreshAt;
    private int _shownCount = -1;

    /// <summary>A researched technology opens the Bestiary. Read from the technologies themselves, so a load needs nothing.</summary>
    public static bool Unlocked
    {
        get
        {
            var units = GameUnitsLogic.Instance;
            if (units == null) return false;
            return GameCatalog.Technologies.All.Any(t => t != null && t.gameUnit != null && OpensBestiary(t) && units.IsTechnologyUnlocked(t.gameUnit.name));
        }
    }

    /// <summary>The technology carries the Bestiary's Special unlockable.</summary>
    public static bool OpensBestiary(TechnologyData tech) =>
        tech != null && tech.techUnlockables != null && tech.techUnlockables.Any(u => u != null && u.unlockableType == TechUnlockableType.Special && u.name == SpeciesKnowledge.BestiaryUnlock);

    private void Start()
    {
        _theme = CodeUI.Theme(nameof(BestiaryHud));
        var canvas = CodeUI.Canvas(transform, "Bestiary Button", 5, out var scaler);
        _plate = CodeUI.Panel(canvas.transform, "Bestiary Button", new Vector2(0f, 1f), new Vector2(0f, 1f));
        _plate.pivot = new Vector2(0f, 1f);
        _plate.anchoredPosition = new Vector2(24f, -302f);
        _plate.sizeDelta = new Vector2(264f, 48f);
        CodeUI.Plate(_plate, _theme, scaler);
        _label = CodeUI.TextButton(_plate, "Bestiary", BestiaryWindow.Toggle, _theme, _theme.bodySize);
        var rect = (RectTransform)_label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 5f);
        rect.offsetMax = new Vector2(-18f, -5f);
        _plate.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_plate == null || Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.5f;
        bool telling = EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive;
        bool show = !telling && !WorldView.IsOpen && !BestiaryWindow.IsOpen && Unlocked;
        if (_plate.gameObject.activeSelf != show) _plate.gameObject.SetActive(show);
        if (show) RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        var world = WorldSystem.Instance;
        int count = world != null && world.Map != null ? SpeciesKnowledge.IdentifiedCount(world.Map, world.Settings.generation, SpeciesLoreKeeper.View) : 0;
        if (count == _shownCount) return;
        _shownCount = count;
        string body = TooltipText.Row("Creatures identified", count.ToString()) + "\n\n"
            + TooltipText.Muted("Every creature your people have identified, and where its dens were found. Survey a den to learn what lives there.");
        TooltipTrigger.Ensure(_label.gameObject).SetCustom("Bestiary", KeywordMarkup.SafeGlyphs(body));
    }
}
