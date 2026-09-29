using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Bestiary (vault: Arcanorian Ecology.md, "Knowledge of Creatures"): every creature the civilization has identified, with its card
/// (its AECOR subgroup, how it lives, its Auric Structure and Pure Light) and the places its dens were found, then the
/// Fauna sighted but not yet surveyed, one by one, as "unidentified fauna". It never names, lists or counts a species no
/// one has identified (<see cref="SpeciesKnowledge"/>). A card grows with its level (<see cref="SpeciesLore"/>): once
/// Observed, how the creature treats your people and whether its numbers grow or fall; once Understood, its numbers, what
/// newcomers meet, and what it hunts and what hunts it. Until then the Bestiary asks what the people cannot see at a
/// glance (<see cref="SpeciesHypotheses"/>): the player clicks a guess, and watching and hunting test it. Creatures only
/// rumoured (<see cref="WorldRumours"/>) are listed as the hunters tell of them. Opened from the capital's
/// <see cref="BestiaryHud"/>; Esc or Close shuts it. Built in code (<see cref="CodeUI"/>), like the Chronicle.
/// </summary>
public class BestiaryWindow : MonoBehaviour
{
    private static BestiaryWindow _instance;
    private TooltipTheme _theme;
    private RectTransform _root;
    private TextMeshProUGUI _body;
    private ScrollRect _scroll;
    private float _refreshAt;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    /// <summary>The frame it last closed on (so the Esc that closed it closes nothing else).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>Open the Bestiary, or close it when it is open.</summary>
    public static void Toggle()
    {
        if (IsOpen) { _instance.Close(); return; }
        if (!BestiaryHud.Unlocked) return;
        if (_instance == null) _instance = new GameObject("Bestiary Window").AddComponent<BestiaryWindow>();
        _instance.Open();
    }

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(BestiaryWindow));
        var canvas = CodeUI.Canvas(transform, "Bestiary Canvas", 2, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        // A dim backdrop that closes the Bestiary when clicked.
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(980f, 820f);
        CodeUI.Plate(book, _theme, scaler);
        var title = CodeUI.Label(book, "Title", "Bestiary", _theme.titleSize + 6f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -84f), new Vector2(-180f, -26f));
        var close = CodeUI.TextButton(book, "Close", Close, _theme);
        close.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -80f), new Vector2(-36f, -30f));

        var area = CodeUI.Panel(book, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(36f, 36f);
        area.offsetMax = new Vector2(-30f, -96f);
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var fitter = content.gameObject.AddComponent<VerticalLayoutGroup>();
        fitter.padding = new RectOffset(12, 12, 10, 16);
        fitter.childControlHeight = fitter.childControlWidth = true;
        fitter.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _body = CodeUI.Label(content, "Body", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        _body.alignment = TextAlignmentOptions.TopLeft;
        var bar = CodeUI.Panel(area, "Scrollbar", new Vector2(1f, 0f), Vector2.one);
        bar.offsetMin = new Vector2(-7f, 0f);
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        var handle = CodeUI.Solid(bar, "Handle", new Color(0.72f, 0.62f, 0.4f, 0.85f), true);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.viewport = viewport;
        _scroll.content = content;
        _scroll.verticalScrollbar = scrollbar;
        _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void Open()
    {
        if (_root == null) Build();
        _root.gameObject.SetActive(true);
        Refresh();
        _scroll.verticalNormalizedPosition = 1f;
    }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;

    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Close();
    }

    private void Update()
    {
        if (!IsOpen) return;
        // A story or the world map takes the screen: the Bestiary steps aside.
        bool telling = EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive;
        if (telling || WorldView.IsOpen) { Close(); return; }
        // A click on a guess (a link in the text) chooses it.
        if (InputUtils.LeftClickDown && ClickGuess()) { Refresh(); return; }
        // Expeditions keep surveying while it is open.
        if (Time.unscaledTime < _refreshAt) return;
        Refresh();
    }

    // The guess under the pointer, chosen: a link "hyp|species|question|answer".
    private bool ClickGuess()
    {
        var pointer = InputUtils.MousePosition;
        if (_scroll == null || !RectTransformUtility.RectangleContainsScreenPoint(_scroll.viewport, pointer, null)) return false;
        int index = TMP_TextUtilities.FindIntersectingLink(_body, pointer, null);
        if (index < 0 || index >= _body.textInfo.linkCount) return false;
        var parts = _body.textInfo.linkInfo[index].GetLinkID().Split('|');
        if (parts.Length != 4 || parts[0] != "hyp" || SpeciesLoreKeeper.Instance == null) return false;
        if (!System.Enum.TryParse(parts[2], out HypothesisQuestion question)) return false;
        return SpeciesLoreKeeper.Instance.Guess(parts[1], question, parts[3]);
    }

    private void Refresh()
    {
        _refreshAt = Time.unscaledTime + 1f;
        _body.text = KeywordMarkup.SafeGlyphs(Compose(WorldSystem.Instance));
    }

    /// <summary>The Bestiary's text: identified creatures and their cards, then the unidentified sightings.</summary>
    public static string Compose(WorldSystem world)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Muted("Every creature your people have identified, and where its dens were found. Expeditions sight dens as they travel; surveying one (or walking all its hexes) tells what lives there."));
        if (world == null || world.Map == null) return text.ToString().TrimEnd();
        var map = world.Map;
        var gen = world.Settings.generation;
        var lore = SpeciesLoreKeeper.View;
        var known = SpeciesKnowledge.Identified(map, gen, lore);
        var sightings = SpeciesKnowledge.UnidentifiedSightings(map, gen);
        var rumoured = WorldRumours.RumouredCreatures(RumourKeeper.Current);
        if (known.Count == 0 && sightings.Count == 0 && rumoured.Count == 0)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Muted("No creature has been sighted yet."));
            return text.ToString().TrimEnd();
        }
        var identified = new HashSet<string>(known.Select(k => k.species.id), System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in known)
        {
            text.AppendLine();
            AppendCard(entry, map, gen, lore, identified, text);
        }
        if (sightings.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading("Unidentified sightings"));
            foreach (var site in sightings)
                text.AppendLine(TooltipText.Bullet($"Unidentified {WorldResources.KindWord(site.kind)}, {SpeciesKnowledge.Place(map, gen, site)} {TooltipText.Muted($"({WorldResources.Hint(site.kind)})")}"));
        }
        // Creatures only heard of: as the hunters tell it, never by name (WorldRumours).
        if (rumoured.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading("Rumoured creatures", TooltipText.Muted("heard of, never seen")));
            foreach (var r in rumoured) text.AppendLine(TooltipText.Bullet(r.text));
        }
        return text.ToString().TrimEnd();
    }

    // One identified creature: the same card its identified den shows on the map, and where its dens were found; then
    // what watching it taught (Observed) and what study made of that (Understood).
    private static void AppendCard(KnownSpecies entry, WorldMap map, WorldGenSettings gen, LoreView lore, HashSet<string> identified, StringBuilder text)
    {
        var species = entry.species;
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        text.AppendLine(TooltipText.Heading(species.name));
        if (profile != null)
        {
            text.AppendLine(TooltipText.Row("Subgroup", profile.Name));
            text.AppendLine(TooltipText.Muted(profile.summary));
        }
        if (!string.IsNullOrEmpty(species.description)) text.AppendLine(TooltipText.Quote($"<i>{species.description}</i>"));
        var (min, max) = CreatureTaxonomy.GroupOf(species);
        text.AppendLine(TooltipText.Row("Lives", $"{CreatureTaxonomy.SizeWord(species.size)}, {CreatureTaxonomy.GroupWords(min, max)}, breeds {CreatureTaxonomy.PaceWord(CreatureTaxonomy.PaceOf(species))}"));
        text.AppendLine(TooltipText.Row("Nature", $"Auric Structure {species.structure:P0}, Pure Light {1f - species.structure:P0}{(CreatureTaxonomy.IsPureLightBeing(species) ? ": a Pure Light being" : string.Empty)}"));
        string organ = CreatureTaxonomy.BindingWords(species.binding);
        if (organ != null) text.AppendLine(TooltipText.Row("Magic", organ));
        if (species.commensal >= 0.5f) text.AppendLine(TooltipText.Row("Near people", "it lives off people's works, in granaries and middens, and thrives beside settlements"));
        var places = SpeciesKnowledge.Places(map, gen, entry.dens).Select(p => p.dens > 1 ? $"{p.place} ({p.dens} dens)" : p.place).ToList();
        if (places.Count > 0) text.AppendLine(TooltipText.Row(entry.dens.Count > 1 ? "Dens found" : "Den found", string.Join("; ", places)));
        if (entry.formerPlaces.Count > 0) text.AppendLine(TooltipText.Row("Once found", $"{string.Join("; ", entry.formerPlaces)} {TooltipText.Muted("(no den known to stand there now)")}"));
        // The Great Plague's vector, once its part is known and before it is understood (E9: the reveal comes midway).
        var world = WorldSystem.Instance;
        if (entry.level < SpeciesLevel.Understood && species.vector > 0f && world != null && world.KnownVector(species.id))
            text.AppendLine(TooltipText.Warn("It carries the plague: near your settlements it feeds Disease Burden. Culling it near them eases it."));

        AppendHypotheses(entry, lore, text);

        if (entry.level < SpeciesLevel.Observed)
        {
            int seen = SpeciesLore.Observations(lore?.Of(species.id), map, lore?.tuning), needed = lore?.tuning?.observeAt ?? 3;
            text.AppendLine(TooltipText.Muted($"Its ways are not known yet ({seen}/{needed}): a den of it near your land, a hunt, or another den found teaches more."));
            return;
        }
        AppendObserved(species, entry, map, gen, lore, text);
        if (entry.level < SpeciesLevel.Understood)
        {
            text.AppendLine(TooltipText.Muted($"Research that carries {SpeciesLore.StudiesUnlock}, or the scholars of an Auric Enclave, would make sense of what your people have seen."));
            return;
        }
        AppendUnderstood(species, map, gen, identified, text);
        // Mastered: kept by a Domestication Enclave you hold as suzerain (WorldEnclaveEcology).
        if (entry.level >= SpeciesLevel.Mastered)
            text.AppendLine(TooltipText.Row("Mastered", $"kept by {string.Join(", ", WorldEnclaveEcology.Keepers(map, gen, species.id).Select(e => e.name))}, who share all they know"));
    }

    // Observed: how it treats your people, how often they hunted it, and whether its numbers grow or fall where its dens stand.
    // What your people think (SpeciesHypotheses): each question with its answer once worked out, or the guesses to click,
    // the one being tested underlined and those ruled out struck through. Understood species need none of it.
    private static void AppendHypotheses(KnownSpecies entry, LoreView lore, StringBuilder text)
    {
        var keeper = SpeciesLoreKeeper.Instance;
        var species = entry.species;
        var record = lore?.Of(species.id);
        if (keeper == null || record == null || (entry.level >= SpeciesLevel.Understood && !record.deduced)) return;
        text.AppendLine(TooltipText.Row("What your people think", record.deduced ? TooltipText.Good("worked out, every question") : TooltipText.Muted("click a guess to test it")));
        foreach (var q in SpeciesHypotheses.Questions(species))
        {
            var h = SpeciesHypotheses.Of(record, q);
            var options = keeper.OptionsFor(species, q);
            if (h != null && h.confirmed)
            {
                string answer = options.FirstOrDefault(o => o.key == h.guess).label ?? h.guess;
                text.AppendLine(TooltipText.Bullet($"{SpeciesHypotheses.Ask(q)} {TooltipText.Good(answer)}{(h.insight ? TooltipText.Muted(" (insight: right the first time)") : string.Empty)}"));
                continue;
            }
            var parts = options.Select(o =>
                h != null && h.ruledOut.Contains(o.key) ? TooltipText.Muted($"<s>{o.label}</s>")
                : h != null && h.guess == o.key ? TooltipText.Link($"hyp|{species.id}|{q}|{o.key}", $"<u><b>{o.label}</b></u>")
                : TooltipText.Link($"hyp|{species.id}|{q}|{o.key}", o.label));
            text.AppendLine(TooltipText.Bullet($"{SpeciesHypotheses.Ask(q)} {string.Join(TooltipText.Separator, parts)}"));
            string guess = h != null && !string.IsNullOrEmpty(h.guess) ? options.FirstOrDefault(o => o.key == h.guess).label : null;
            text.AppendLine(TooltipText.Muted($"      {(guess != null ? $"Testing: {guess}" : "No guess yet")}; {SpeciesHypotheses.HowTested(q)}."));
        }
    }

    private static void AppendObserved(SpeciesSpec species, KnownSpecies entry, WorldMap map, WorldGenSettings gen, LoreView lore, StringBuilder text)
    {
        var nature = WorldBehavior.Nature(species);
        var toward = WorldBehavior.Toward(map, gen, species, WorldAuthority.Player);
        string learned = toward == nature ? string.Empty
            : WorldBehavior.Harsher(nature, toward) ? $" {TooltipText.Bad("(warier than its nature: it remembers)")}" : $" {TooltipText.Good("(gentler than its nature)")}";
        text.AppendLine(TooltipText.Row("Toward your people", WorldBehavior.Words(toward) + learned));
        int hunts = SpeciesLore.HuntsByYou(map, species.id);
        if (hunts > 0) text.AppendLine(TooltipText.Row("Hunted by your people", hunts == 1 ? "once" : $"{hunts} times"));
        // Its rhythm and harmonic climate (E10): once worked out (SpeciesHypotheses) or understood.
        var record = lore?.Of(species.id);
        string niche = SpeciesHypotheses.Knows(record, HypothesisQuestion.Niche, entry.level) ? CreatureTaxonomy.NicheWords(species.niche) : null;
        if (niche != null) text.AppendLine(TooltipText.Row("Niche", niche));
        int breeding = SpeciesHypotheses.Knows(record, HypothesisQuestion.Breeding, entry.level) ? CreatureTaxonomy.BreedingEchoOf(species) : 0;
        if (breeding > 0 && WorldRhythm.EchoName(gen, breeding) is string echo) text.AppendLine(TooltipText.Row("Breeds", $"in the {echo}"));
        foreach (var (p, biome) in Populations(entry, map, gen))
        {
            string trend = WorldEcology.TrendWord(p);
            text.AppendLine(TooltipText.Row(biome, gen.ecology != null ? WorldEcology.AbundanceWord(p, gen.ecology) + (trend != null ? $", {trend}" : string.Empty) : trend ?? "steady"));
        }
    }

    // Understood: the numbers, what newcomers meet, and what it hunts and what hunts it (identified creatures only).
    private static void AppendUnderstood(SpeciesSpec species, WorldMap map, WorldGenSettings gen, HashSet<string> identified, StringBuilder text)
    {
        float groups = 0f, capacity = 0f;
        foreach (var p in map.Populations.Where(p => p != null && string.Equals(p.species, species.id, System.StringComparison.OrdinalIgnoreCase)))
        {
            groups += p.groups;
            capacity += p.capacity;
        }
        if (capacity > 0f) text.AppendLine(TooltipText.Row("Numbers", $"about {groups:0.#} groups where the land could hold {capacity:0.#} ({SpeciesLore.PopulationPercent(map, species.id):0}%)"));
        text.AppendLine(TooltipText.Row("Newcomers meet", WorldBehavior.Words(WorldBehavior.Overall(map, gen, species))));
        var prey = (species.prey ?? new List<string>()).Where(id => !string.IsNullOrEmpty(id)).ToList();
        if (prey.Count > 0) text.AppendLine(TooltipText.Row("Hunts", Names(prey, gen, identified)));
        var hunters = (gen.species ?? new List<SpeciesSpec>()).Where(s => s != null && s.prey != null && s.prey.Any(id => string.Equals(id, species.id, System.StringComparison.OrdinalIgnoreCase))).Select(s => s.id).ToList();
        if (hunters.Count > 0) text.AppendLine(TooltipText.Row("Hunted by", Names(hunters, gen, identified)));
        // Its ills (E10): the sickness it carries to people, and the resonance plagues tuned to it.
        if (species.vector > 0f) text.AppendLine(TooltipText.Warn($"It carries sickness: near your settlements it feeds Disease Burden{(species.vector >= 0.5f ? ", strongly" : string.Empty)}. Hunting it down near them eases it."));
        foreach (var plague in WorldPlagues.PlaguesOf(gen, species.id))
        {
            text.AppendLine(TooltipText.Row("Vulnerable to", plague.name + (string.IsNullOrEmpty(plague.folklore) ? string.Empty : TooltipText.Muted($" (once called {plague.folklore})"))));
            if (!string.IsNullOrEmpty(plague.description)) text.AppendLine(TooltipText.Muted(plague.description));
        }
    }

    // The populations of a species where its identified dens stand, with their Macro Biome's name (each Macro Biome once).
    private static IEnumerable<(Population population, string biome)> Populations(KnownSpecies entry, WorldMap map, WorldGenSettings gen)
    {
        var slots = new HashSet<int>();
        foreach (var den in entry.dens)
        {
            int slot = map[den.center].habitatSlot;
            if (!slots.Add(slot)) continue;
            var p = WorldEcology.Find(map, slot, entry.species.id);
            if (p != null) yield return (p, WorldEcology.MacroBiomeOf(map, slot)?.name ?? "The wilds");
        }
    }

    // Species names, identified ones only; the rest are "creatures not yet identified" (never counted).
    private static string Names(List<string> ids, WorldGenSettings gen, HashSet<string> identified)
    {
        var names = ids.Where(identified.Contains).Select(id => gen.Species(id)?.name ?? id).ToList();
        if (ids.Any(id => !identified.Contains(id))) names.Add(TooltipText.Muted("creatures not yet identified"));
        return string.Join(", ", names);
    }
}
