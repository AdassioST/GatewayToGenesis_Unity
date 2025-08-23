using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using System.Text.RegularExpressions;
using System.Linq;

/// <summary>
/// Automatically creates EventVolumes from Ink files in the Resources/Events folder
/// This eliminates the need for separate C# scripts for each volume
/// </summary>
public class InkDrivenEventSetup : MonoBehaviour
{
    [Header("Auto Setup Settings")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private string eventsFolderPath = "Events";
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupAllVolumesFromInk();
            // Precompile knot contents and choices for all knots (for runtime-free rendering)
            BuildKnotContentIndex();
            // Build a static index of choice metadata for all chorus knots at startup
            BuildChoiceMetadataIndex();
        }
    }
    
    /// <summary>
    /// Automatically create EventVolumes from all Ink files in the Resources/Events folder
    /// </summary>
    public void SetupAllVolumesFromInk()
    {
        // Load all text assets from Resources/Events (both .ink and compiled .json)
        TextAsset[] assets = Resources.LoadAll<TextAsset>(eventsFolderPath);

        // Group by base name (Unity strips extensions)
        var grouped = new Dictionary<string, List<TextAsset>>();
        foreach (var ta in assets)
        {
            if (!grouped.ContainsKey(ta.name)) grouped[ta.name] = new List<TextAsset>();
            grouped[ta.name].Add(ta);
        }

        int created = 0;
        foreach (var kvp in grouped)
        {
            TextAsset jsonAsset = null;
            TextAsset inkAsset = null;

            foreach (var ta in kvp.Value)
            {
                string text = ta.text?.TrimStart();
                if (!string.IsNullOrEmpty(text) && text.StartsWith("{"))
                {
                    // Likely compiled Ink JSON
                    jsonAsset = ta;
                }
                else
                {
                    // Likely raw ink file
                    inkAsset = ta;
                }
            }

            // Create a volume if we have at least one asset
            CreateVolumeFromAssets(baseName: kvp.Key, compiledJson: jsonAsset ?? inkAsset, inkSource: inkAsset);
            created++;
        }

        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Created {created} volumes from Events folder", "InkDrivenEventSetup");
    }

    // ===== CHOICE METADATA INDEX =====
    public static Dictionary<string, List<ChorusChoiceData>> ChoiceMetadataByKnot { get; private set; } = new Dictionary<string, List<ChorusChoiceData>>();

    // Deferred consequences keyed by verse/bridge/outro knot. Applied on verse button press.
    public static Dictionary<string, List<EventConsequence>> DeferredConsequencesByKnot { get; private set; } = new Dictionary<string, List<EventConsequence>>(System.StringComparer.OrdinalIgnoreCase);

    private static void AppendDeferred(string knotName, List<EventConsequence> consequences)
    {
        if (string.IsNullOrEmpty(knotName) || consequences == null || consequences.Count == 0) return;
        if (!DeferredConsequencesByKnot.TryGetValue(knotName, out var list))
        {
            list = new List<EventConsequence>();
            DeferredConsequencesByKnot[knotName] = list;
        }
        list.AddRange(consequences);
    }

    public static List<EventConsequence> GetAndClearDeferredConsequences(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return new List<EventConsequence>();
        if (!DeferredConsequencesByKnot.TryGetValue(knotName, out var list) || list == null)
        {
            return new List<EventConsequence>();
        }
        var copy = new List<EventConsequence>(list);
        DeferredConsequencesByKnot.Remove(knotName);
        return copy;
    }

    public static List<ChorusChoiceData> GetChoicesForKnot(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return new List<ChorusChoiceData>();
        return ChoiceMetadataByKnot.TryGetValue(knotName, out var list) ? list : new List<ChorusChoiceData>();
    }

    private void BuildChoiceMetadataIndex()
    {
        ChoiceMetadataByKnot.Clear();
        int compiledChorusKnots = 0;
        // Primary path: compiled stories from Resources/Events (precompiled JSON)
        var compiledAssets = Resources.LoadAll<TextAsset>(eventsFolderPath);
        foreach (var json in compiledAssets)
        {
            if (json == null || string.IsNullOrEmpty(json.text)) continue;
            // Heuristic: compiled Ink JSON starts with '{'
            if (!json.text.TrimStart().StartsWith("{")) continue;
            Story story = null;
            try { story = new Story(json.text); } catch { continue; }
            var named = story?.mainContentContainer?.namedContent;
            if (named == null || named.Count == 0) continue;
            foreach (var kv in named)
            {
                string knotName = kv.Key;
                if (string.IsNullOrEmpty(knotName)) continue;
                // Only chorus knots
                if (knotName.IndexOf("_chorus", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                // Skip if already populated from raw ink
                if (ChoiceMetadataByKnot.ContainsKey(knotName) && ChoiceMetadataByKnot[knotName].Count > 0) continue;
                // Build a temporary story to jump and read choices
                Story s = null;
                try { s = new Story(json.text); } catch { continue; }
                try
                {
                    s.ChoosePathString(knotName);
                    // Continue until choices appear or content ends
                    while (s.canContinue) s.Continue();
                    var ch = s.currentChoices;
                    EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Choice scan: knot='{knotName}', choices={(ch!=null?ch.Count:0)}", "InkDrivenEventSetup");
                    if (ch != null && ch.Count > 0)
                    {
                        var list = new List<ChorusChoiceData>();
                        foreach (var c in ch)
                        {
                            var data = ParseChoiceTextFromCompiled(c.text);
                            if (data != null)
                            {
                                list.Add(data);
                                // Register deferred consequences to target paths to apply at verse screens
                                if (data.successConsequences != null && !string.IsNullOrEmpty(data.successPath))
                                {
                                    AppendDeferred(NormalizeKnotName(data.successPath) ?? data.successPath, data.successConsequences);
                                }
                                if (data.failureConsequences != null && !string.IsNullOrEmpty(data.failurePath))
                                {
                                    AppendDeferred(NormalizeKnotName(data.failurePath) ?? data.failurePath, data.failureConsequences);
                                }
                                if (data.critSuccessConsequences != null && !string.IsNullOrEmpty(data.critSuccessPath))
                                {
                                    AppendDeferred(NormalizeKnotName(data.critSuccessPath) ?? data.critSuccessPath, data.critSuccessConsequences);
                                }
                                if (data.critFailureConsequences != null && !string.IsNullOrEmpty(data.critFailurePath))
                                {
                                    AppendDeferred(NormalizeKnotName(data.critFailurePath) ?? data.critFailurePath, data.critFailureConsequences);
                                }
                                if (data.rareEventConsequences != null && !string.IsNullOrEmpty(data.rareEventPath))
                                {
                                    AppendDeferred(NormalizeKnotName(data.rareEventPath) ?? data.rareEventPath, data.rareEventConsequences);
                                }

                                // Unconditional consequences (apply on the verse reached by this choice)
                                if (data.consequences != null && data.consequences.Count > 0)
                                {
                                    string attachPath = null;
                                    if (!string.IsNullOrEmpty(data.successPath)) attachPath = data.successPath;
                                    else if (!string.IsNullOrEmpty(data.failurePath)) attachPath = data.failurePath;
                                    else if (!string.IsNullOrEmpty(data.rareEventPath)) attachPath = data.rareEventPath;
                                    else if (!string.IsNullOrEmpty(data.critSuccessPath)) attachPath = data.critSuccessPath;
                                    else if (!string.IsNullOrEmpty(data.critFailurePath)) attachPath = data.critFailurePath;
                                    if (!string.IsNullOrEmpty(attachPath))
                                    {
                                        AppendDeferred(NormalizeKnotName(attachPath) ?? attachPath, data.consequences);
                                    }
                                }
                            }
                        }
                        if (list.Count > 0)
                        {
                            ChoiceMetadataByKnot[knotName] = list;
                            compiledChorusKnots++;
                            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Stored {list.Count} choices for '{knotName}'", "InkDrivenEventSetup");
                        }
                    }
                }
                catch { /* skip invalid knots */ }
            }
        }
        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Built choice metadata index for {ChoiceMetadataByKnot.Count} knot(s) (compiled knots {compiledChorusKnots})", "InkDrivenEventSetup");
    }

    // ===== PRECOMPILED KNOT CONTENT/CHOICES INDEX =====
    [System.Serializable]
    public class PrecompiledChoice
    {
        public string text;
        public string targetPath;
        // Extended metadata for outcomes (optional)
        public string critSuccessPath;
        public string critFailurePath;
        public string rareEventPath;
    }

    [System.Serializable]
    public class PrecompiledKnot
    {
        public string knotName;
        public string content;
        public List<PrecompiledChoice> choices = new List<PrecompiledChoice>();
        // When a knot has no choices and diverts to another knot, store the next knot name
        public string nextKnotIfNoChoices;
    }

    public static Dictionary<string, PrecompiledKnot> KnotIndexByKnot { get; private set; } = new Dictionary<string, PrecompiledKnot>();

    public static bool TryGetKnot(string knotName, out PrecompiledKnot knot)
    {
        return KnotIndexByKnot.TryGetValue(knotName, out knot);
    }

    private void BuildKnotContentIndex()
    {
        KnotIndexByKnot.Clear();
        var compiledAssets = Resources.LoadAll<TextAsset>(eventsFolderPath);
        foreach (var json in compiledAssets)
        {
            if (json == null || string.IsNullOrEmpty(json.text)) continue;
            if (!json.text.TrimStart().StartsWith("{")) continue; // compiled JSON only
            Story story;
            try { story = new Story(json.text); } catch { continue; }
            var named = story?.mainContentContainer?.namedContent;
            if (named == null || named.Count == 0) continue;
            foreach (var kv in named)
            {
                string knotName = kv.Key;
                if (string.IsNullOrEmpty(knotName)) continue;
                // Build content and choices for this knot using a fresh Story instance to avoid cross-state
                Story s;
                try { s = new Story(json.text); } catch { continue; }
                try
                {
                    s.ChoosePathString(knotName);
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    bool leftStartKnot = false;
                    string nextKnot = null;
                    while (s.canContinue)
                    {
                        // If we've already diverted to another top-level knot, stop collecting content
                        string currentTop = GetTopLevelKnotNameFromPath(s.state.currentPathString);
                        if (!string.IsNullOrEmpty(currentTop) && currentTop != knotName)
                        {
                            leftStartKnot = true;
                            nextKnot = currentTop;
                            break;
                        }

                        string line = s.Continue();
                        if (!string.IsNullOrEmpty(line)) sb.Append(line);
                        if (s.currentChoices != null && s.currentChoices.Count > 0) break;
                    }
                    var pk = new PrecompiledKnot { knotName = knotName, content = sb.ToString() };
                    EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] KnotIndex: '{knotName}': contentLen={pk.content?.Length ?? 0}, hasChoices={(s.currentChoices!=null ? s.currentChoices.Count:0)}, fallthrough={(leftStartKnot ? nextKnot:"-")}", "InkDrivenEventSetup");
                    if (leftStartKnot)
                    {
                        pk.nextKnotIfNoChoices = nextKnot;
                    }
                    else if (s.currentChoices != null && s.currentChoices.Count > 0)
                    {
                        int count = s.currentChoices.Count;
                        // For chorus knots, do not pre-resolve targets; navigation depends on challenge outcome
                        bool isChorus = knotName.IndexOf("_chorus", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        for (int i = 0; i < count; i++)
                        {
                            string choiceText = s.currentChoices[i].text;
                            string resolvedTop = null;
                            if (!isChorus)
                            {
                                resolvedTop = ResolveChoiceTargetTopLevel(json.text, knotName, i);
                            }
                            pk.choices.Add(new PrecompiledChoice { text = choiceText, targetPath = resolvedTop });
                            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup]   Choice[{i}] '{choiceText}' -> '{(resolvedTop ?? "-")}'", "InkDrivenEventSetup");
                        }
                    }
                    else
                    {
                        // Post-loop divert detection: if we ended with no choices but path moved to another knot
                        string currentTop = GetTopLevelKnotNameFromPath(s.state.currentPathString);
                        if (!string.IsNullOrEmpty(currentTop) && currentTop != knotName)
                        {
                            pk.nextKnotIfNoChoices = currentTop;
                            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup]   Next-if-no-choices='{pk.nextKnotIfNoChoices}' for '{knotName}'", "InkDrivenEventSetup");
                        }
                    }
                    KnotIndexByKnot[knotName] = pk;
                }
                catch { /* skip invalid knot */ }
            }
        }
        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Built knot content index for {KnotIndexByKnot.Count} knot(s)", "InkDrivenEventSetup");
    }

    public static string GetTopLevelKnotNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        int dot = path.IndexOf('.');
        return dot >= 0 ? path.Substring(0, dot) : path;
    }

    /// <summary>
    /// Normalize any Ink path string to a top-level knot name our system expects
    /// Example: "hollow_caravan_verse_1.0.c-0" -> "hollow_caravan_verse_1"
    /// </summary>
    public static string NormalizeKnotName(string path)
    {
        return GetTopLevelKnotNameFromPath(path);
    }

    private static string ResolveChoiceTargetTopLevel(string storyJson, string startKnot, int choiceIndex)
    {
        if (string.IsNullOrEmpty(storyJson) || string.IsNullOrEmpty(startKnot)) return null;
        try
        {
            Story sim = new Story(storyJson);
            sim.ChoosePathString(startKnot);
            while (sim.canContinue && (sim.currentChoices == null || sim.currentChoices.Count == 0))
            {
                sim.Continue();
            }
            if (sim.currentChoices == null || sim.currentChoices.Count <= choiceIndex) return null;
            // First try the direct targetPath if provided by Ink
            string directRaw = null;
            try { directRaw = sim.currentChoices[choiceIndex].targetPath != null ? sim.currentChoices[choiceIndex].targetPath.ToString() : null; } catch { directRaw = null; }
            string directTop = NormalizeKnotName(directRaw);
            if (!string.IsNullOrEmpty(directTop) && !string.Equals(directTop, startKnot))
            {
                return directTop;
            }
            sim.ChooseChoiceIndex(choiceIndex);
            string initialTop = startKnot;
            string lastTop = GetTopLevelKnotNameFromPath(sim.state.currentPathString);
            int safety = 0;
            // Important: advance first, then read the new path to catch immediate diverts
            while (safety++ < 128 && (sim.canContinue || (sim.currentChoices != null && sim.currentChoices.Count > 0)))
            {
                if (sim.canContinue)
                {
                    sim.Continue();
                }
                string currentTop = GetTopLevelKnotNameFromPath(sim.state.currentPathString);
                if (!string.IsNullOrEmpty(currentTop)) lastTop = currentTop;
                if (!string.IsNullOrEmpty(currentTop) && currentTop != initialTop)
                {
                    return currentTop;
                }
                // If reached choices without continue, break
                if (!sim.canContinue)
                {
                    // If we have arrived at a different top-level knot (e.g., chorus), return it immediately
                    string curTop = GetTopLevelKnotNameFromPath(sim.state.currentPathString);
                    if (!string.IsNullOrEmpty(curTop) && curTop != initialTop)
                    {
                        return curTop;
                    }
                    // As a last resort, if we have new choices, try to infer top-level from their target paths
                    if (sim.currentChoices != null && sim.currentChoices.Count > 0)
                    {
                        string raw = null;
                        try { raw = sim.currentChoices[0].targetPath != null ? sim.currentChoices[0].targetPath.ToString() : null; } catch { raw = null; }
                        string inferred = NormalizeKnotName(raw);
                        if (!string.IsNullOrEmpty(inferred)) return inferred;
                    }
                    break;
                }
            }
            return lastTop;
        }
        catch { return null; }
    }

    private void ParseInkForChoiceMetadata(string inkText)
    {
        if (string.IsNullOrEmpty(inkText)) return;
        string text = inkText.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = text.Split('\n');
        string currentKnot = null;
        List<ChorusChoiceData> currentKnotChoices = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].Trim();
            if (l.StartsWith("=== "))
            {
                // Extract knot name between === ... ===
                int start = l.IndexOf("=== ") + 4;
                int end = l.IndexOf("===", start);
                // Flush previous knot log if it had choices
                if (!string.IsNullOrEmpty(currentKnot) && currentKnotChoices != null && currentKnotChoices.Count > 0)
                {
                    string summary = string.Join(", ", currentKnotChoices.Select(c => $"{c.choiceId} (pillar:{(c.hasChallenge ? c.challengePillar : "-")}, str:{(c.hasChallenge ? c.challengeStrength : 0)}, succ:{c.successPath}, fail:{c.failurePath})"));
                    EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Extracted choices for '{currentKnot}': {currentKnotChoices.Count} -> [{summary}]", "InkDrivenEventSetup");
                }
                currentKnot = end > start ? l.Substring(start, end - start).Trim() : l.Trim('=', ' ').Trim();
                currentKnotChoices = new List<ChorusChoiceData>();
                continue;
            }
            if (currentKnot == null) continue;
            if (l.StartsWith("* "))
            {
                string choiceLine = l;
                // Support multi-line choice: accumulate following '&C ' lines and/or destination '-> path'
                if (!choiceLine.Contains("->") || choiceLine.IndexOf("&C ", System.StringComparison.Ordinal) >= 0)
                {
                    int j = i + 1;
                    while (j < lines.Length)
                    {
                        string next = lines[j].Trim();
                        if (string.IsNullOrEmpty(next)) { j++; continue; }
                        // Accumulate inline consequences lines
                        if (next.StartsWith("&C "))
                        {
                            choiceLine = choiceLine + " " + next;
                            j++;
                            continue;
                        }
                        // Accumulate destination line placed on its own
                        if (next.StartsWith("-> "))
                        {
                            choiceLine = choiceLine + " " + next;
                            i = j; // advance outer loop to destination line
                        }
                        break;
                    }
                }
                if (choiceLine.Contains("->"))
                {
                    var cd = ParseChoiceLineAsData(choiceLine);
                    if (cd != null)
                    {
                        if (!ChoiceMetadataByKnot.ContainsKey(currentKnot)) ChoiceMetadataByKnot[currentKnot] = new List<ChorusChoiceData>();
                        ChoiceMetadataByKnot[currentKnot].Add(cd);
                        if (currentKnotChoices != null) currentKnotChoices.Add(cd);
                    }
                }
            }
        }
        // Flush last knot's summary
        if (!string.IsNullOrEmpty(currentKnot) && currentKnotChoices != null && currentKnotChoices.Count > 0)
        {
            string summary = string.Join(", ", currentKnotChoices.Select(c => $"{c.choiceId} (pillar:{(c.hasChallenge ? c.challengePillar : "-")}, str:{(c.hasChallenge ? c.challengeStrength : 0)}, succ:{c.successPath}, fail:{c.failurePath})"));
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Extracted choices for '{currentKnot}': {currentKnotChoices.Count} -> [{summary}]", "InkDrivenEventSetup");
        }
    }

    private ChorusChoiceData ParseChoiceLineAsData(string choiceLine)
    {
        try
        {
            string[] parts = choiceLine.Split(new[] { "->" }, 2, System.StringSplitOptions.None);
            if (parts.Length != 2) return null;
            string choicePart = parts[0].Trim();
            string destination = parts[1].Trim();
            if (choicePart.StartsWith("* ")) choicePart = choicePart.Substring(2);
            // Extract text, description (&D) and inline consequences/metadata (&C)
            string[] choiceAndMetaHash = choicePart.Split('#', 2); // legacy fallback
            string choiceTextFull = choiceAndMetaHash[0].Trim();
            string legacyHashMeta = choiceAndMetaHash.Length > 1 ? choiceAndMetaHash[1].Trim() : string.Empty;

            // Identify &D and &C markers within the full choice text
            int dIdx = choiceTextFull.IndexOf("&D ", System.StringComparison.Ordinal);
            int cIdx = choiceTextFull.IndexOf("&C ", System.StringComparison.Ordinal);

            // Compute plain text before markers (type/title area)
            string textBeforeMarkers;
            if (dIdx >= 0)
            {
                textBeforeMarkers = choiceTextFull.Substring(0, dIdx).Trim();
            }
            else if (cIdx >= 0)
            {
                textBeforeMarkers = choiceTextFull.Substring(0, cIdx).Trim();
            }
            else
            {
                textBeforeMarkers = choiceTextFull;
            }

            // Description is from &D to &C (if present) or to end
            string description = "Make your choice.";
            if (dIdx >= 0)
            {
                int descStart = dIdx + 3;
                int descEnd = (cIdx > descStart) ? cIdx : choiceTextFull.Length;
                description = choiceTextFull.Substring(descStart, descEnd - descStart).Trim();
            }

            // Inline metadata string is what's after &C, otherwise fallback to legacy '#'
            string metadata = string.Empty;
            if (cIdx >= 0)
            {
                metadata = choiceTextFull.Substring(cIdx + 3).Trim();
            }
            else
            {
                metadata = legacyHashMeta;
            }

            // Left side: "Type. Title..."
            string rawType = textBeforeMarkers;
            string title = textBeforeMarkers;
            int firstDot = textBeforeMarkers.IndexOf('.');
            if (firstDot >= 0)
            {
                rawType = textBeforeMarkers.Substring(0, firstDot).Trim();
                title = textBeforeMarkers.Substring(firstDot + 1).Trim();
            }

            string choiceId = DetermineChoiceTypeLocal(rawType);
            var meta = ParseMetadataSimple(metadata);

            ChorusChoiceData data = new ChorusChoiceData();
            data.choiceId = choiceId;
            data.title = title;
            data.description = description;
            data.hoverDescription = title;
            data.destinationPath = destination;

            if (meta.ContainsKey("requirements"))
            {
                data.requirements = ParseRequirements(meta["requirements"]);
                data.hasRequirements = data.requirements.Count > 0;
            }
            if (meta.ContainsKey("requirements:cost"))
            {
                data.requirementsCost = ParseRequirements(meta["requirements:cost"]);
            }

            if (meta.ContainsKey("success")) data.successPath = meta["success"];
            if (meta.ContainsKey("failure")) data.failurePath = meta["failure"];

            if (meta.ContainsKey("success:consequences")) data.successConsequences = ParseConsequences(meta["success:consequences"]);
            if (meta.ContainsKey("failure:consequences")) data.failureConsequences = ParseConsequences(meta["failure:consequences"]);
            if (meta.ContainsKey("consequences")) data.consequences = ParseConsequences(meta["consequences"]);

            // Extended: crit/rare
            if (meta.ContainsKey("crit_success")) data.critSuccessPath = meta["crit_success"];
            if (meta.ContainsKey("crit_failure")) data.critFailurePath = meta["crit_failure"];
            if (meta.ContainsKey("crit_success:consequences")) data.critSuccessConsequences = ParseConsequences(meta["crit_success:consequences"]);
            if (meta.ContainsKey("crit_failure:consequences")) data.critFailureConsequences = ParseConsequences(meta["crit_failure:consequences"]);
            if (meta.ContainsKey("rare_event")) data.rareEventPath = meta["rare_event"];
            if (meta.ContainsKey("rare_event_percent")) int.TryParse(meta["rare_event_percent"], out data.rareEventPercent);
            if (meta.ContainsKey("rare_event:consequences")) data.rareEventConsequences = ParseConsequences(meta["rare_event:consequences"]);

            if (meta.ContainsKey("pillar"))
            {
                data.challengePillar = meta["pillar"];
                int st = 10; if (meta.ContainsKey("strength")) int.TryParse(meta["strength"], out st);
                if (st <= 0) st = 10;
                data.challengeStrength = st;
                data.hasChallenge = true;
            }
            else if (meta.ContainsKey("challenge"))
            {
                // Allow compact form: challenge:pillar:strength
                // e.g., challenge:waltz:15
                var seg = meta["challenge"].Split(':');
                if (seg.Length >= 2)
                {
                    data.challengePillar = seg[0].Trim();
                    int st = 10;
                    if (seg.Length >= 3) int.TryParse(seg[2].Trim(), out st);
                    if (st <= 0) st = 10;
                    data.challengeStrength = st;
                    data.hasChallenge = !string.IsNullOrEmpty(data.challengePillar);
                }
            }

            data.validationType = (data.hasChallenge && data.hasRequirements) ? ChoiceValidationType.Both :
                                  (data.hasChallenge ? ChoiceValidationType.Challenge :
                                  (data.hasRequirements ? ChoiceValidationType.Requirements : ChoiceValidationType.None));
            return data;
        }
        catch
        {
            return null;
        }
    }

    // Parse from compiled choice text (no leading '*', no '->', but inline &D and &C preserved)
    private ChorusChoiceData ParseChoiceTextFromCompiled(string choiceTextFull)
    {
        try
        {
            if (string.IsNullOrEmpty(choiceTextFull)) return null;
            int dIdx = choiceTextFull.IndexOf("&D ", System.StringComparison.Ordinal);
            int cIdx = choiceTextFull.IndexOf("&C ", System.StringComparison.Ordinal);

            string textBeforeMarkers;
            if (dIdx >= 0) textBeforeMarkers = choiceTextFull.Substring(0, dIdx).Trim();
            else if (cIdx >= 0) textBeforeMarkers = choiceTextFull.Substring(0, cIdx).Trim();
            else textBeforeMarkers = choiceTextFull.Trim();

            string description = "Make your choice.";
            if (dIdx >= 0)
            {
                int descStart = dIdx + 3;
                int descEnd = (cIdx > descStart) ? cIdx : choiceTextFull.Length;
                description = choiceTextFull.Substring(descStart, descEnd - descStart).Trim();
            }

            string metadata = cIdx >= 0 ? choiceTextFull.Substring(cIdx + 3).Trim() : string.Empty;

            string rawType = textBeforeMarkers;
            string title = textBeforeMarkers;
            int firstDot = textBeforeMarkers.IndexOf('.');
            if (firstDot >= 0)
            {
                rawType = textBeforeMarkers.Substring(0, firstDot).Trim();
                title = textBeforeMarkers.Substring(firstDot + 1).Trim();
            }

            string choiceId = DetermineChoiceTypeLocal(rawType);
            var meta = ParseMetadataSimple(metadata);

            // Heuristic: accumulate requirements blocks even when authors split entries across tokens
            if (!meta.ContainsKey("requirements") || !meta.ContainsKey("requirements:cost"))
            {
                var parts = (metadata ?? string.Empty).Split(';');
                bool inReq = false, inReqCost = false;
                System.Text.StringBuilder req = null, reqCost = null;
                foreach (var raw in parts)
                {
                    string item = raw.Trim();
                    if (string.IsNullOrEmpty(item)) continue;
                    string lower = item.ToLower();
                    if (lower.StartsWith("requirements:cost:"))
                    {
                        inReq = false; inReqCost = true;
                        string after = item.Substring("requirements:cost:".Length).Trim();
                        if (!string.IsNullOrEmpty(after))
                        {
                            if (reqCost == null) reqCost = new System.Text.StringBuilder();
                            if (reqCost.Length > 0) reqCost.Append(';');
                            reqCost.Append(after);
                        }
                        continue;
                    }
                    if (lower.StartsWith("requirements:"))
                    {
                        inReq = true; inReqCost = false;
                        string after = item.Substring("requirements:".Length).Trim();
                        if (!string.IsNullOrEmpty(after))
                        {
                            if (req == null) req = new System.Text.StringBuilder();
                            if (req.Length > 0) req.Append(';');
                            req.Append(after);
                        }
                        continue;
                    }
                    // Break out of requirements capture on new top-level sections
                    if (lower.StartsWith("pillar:") || lower.StartsWith("strength:") || lower.StartsWith("challenge:")
                        || lower.StartsWith("success:") || lower.StartsWith("failure:")
                        || lower.StartsWith("crit_success:") || lower.StartsWith("crit_failure:")
                        || lower.StartsWith("rare_event:") || lower.StartsWith("rare_event_percent:"))
                    {
                        inReq = false; inReqCost = false;
                    }
                    else if (inReq)
                    {
                        if (req == null) req = new System.Text.StringBuilder();
                        if (req.Length > 0) req.Append(';');
                        req.Append(item);
                    }
                    else if (inReqCost)
                    {
                        if (reqCost == null) reqCost = new System.Text.StringBuilder();
                        if (reqCost.Length > 0) reqCost.Append(';');
                        reqCost.Append(item);
                    }
                }
                if (req != null && req.Length > 0 && !meta.ContainsKey("requirements")) meta["requirements"] = req.ToString();
                if (reqCost != null && reqCost.Length > 0 && !meta.ContainsKey("requirements:cost")) meta["requirements:cost"] = reqCost.ToString();
            }

            var data = new ChorusChoiceData
            {
                choiceId = choiceId,
                title = title,
                description = description,
                hoverDescription = title
            };

            if (meta.ContainsKey("requirements"))
            {
                data.requirements = ParseRequirements(meta["requirements"]);
                data.hasRequirements = data.requirements.Count > 0;
            }
            if (meta.ContainsKey("requirements:cost"))
            {
                data.requirementsCost = ParseRequirements(meta["requirements:cost"]);
            }

            if (meta.ContainsKey("success")) data.successPath = meta["success"];
            if (meta.ContainsKey("failure")) data.failurePath = meta["failure"];
            if (meta.ContainsKey("success:consequences")) data.successConsequences = ParseConsequences(meta["success:consequences"]);
            if (meta.ContainsKey("failure:consequences")) data.failureConsequences = ParseConsequences(meta["failure:consequences"]);
            if (meta.ContainsKey("consequences")) data.consequences = ParseConsequences(meta["consequences"]);

            // Extended outcomes (optional)
            if (meta.ContainsKey("crit_success")) data.critSuccessPath = meta["crit_success"];
            if (meta.ContainsKey("crit_failure")) data.critFailurePath = meta["crit_failure"];
            if (meta.ContainsKey("crit_success:consequences")) data.critSuccessConsequences = ParseConsequences(meta["crit_success:consequences"]);
            if (meta.ContainsKey("crit_failure:consequences")) data.critFailureConsequences = ParseConsequences(meta["crit_failure:consequences"]);
            if (meta.ContainsKey("rare_event")) data.rareEventPath = meta["rare_event"];
            if (meta.ContainsKey("rare_event_percent")) int.TryParse(meta["rare_event_percent"], out data.rareEventPercent);
            if (meta.ContainsKey("rare_event:consequences")) data.rareEventConsequences = ParseConsequences(meta["rare_event:consequences"]);

            if (meta.ContainsKey("pillar"))
            {
                data.challengePillar = meta["pillar"];
                int st = 10; if (meta.ContainsKey("strength")) int.TryParse(meta["strength"], out st);
                if (st <= 0) st = 10;
                data.challengeStrength = st;
                data.hasChallenge = true;
            }
            else if (meta.ContainsKey("challenge"))
            {
                var seg = meta["challenge"].Split(':');
                if (seg.Length >= 2)
                {
                    data.challengePillar = seg[0].Trim();
                    int st = 10; if (seg.Length >= 3) int.TryParse(seg[2].Trim(), out st);
                    if (st <= 0) st = 10;
                    data.challengeStrength = st;
                    data.hasChallenge = !string.IsNullOrEmpty(data.challengePillar);
                }
            }

            data.validationType = (data.hasChallenge && data.hasRequirements) ? ChoiceValidationType.Both :
                                   (data.hasChallenge ? ChoiceValidationType.Challenge :
                                   (data.hasRequirements ? ChoiceValidationType.Requirements : ChoiceValidationType.None));
            return data;
        }
        catch { return null; }
    }

    private string DetermineChoiceTypeLocal(string title)
    {
        string lt = (title ?? string.Empty).ToLower();
        if (lt.Contains("idealism") || lt.Contains("idealistic")) return "idealism";
        if (lt.Contains("realism") || lt.Contains("realistic")) return "realism";
        if (lt.Contains("pragmatism") || lt.Contains("pragmatic")) return "pragmatism";
        return lt.Replace(" ", "_");
    }

    private Dictionary<string, string> ParseMetadataSimple(string metadata)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(metadata)) return result;
        string[] items = metadata.Split(';');
        string activeKey = null;
        System.Text.StringBuilder active = null;
        // Accumulators to support multiple entries
        System.Text.StringBuilder reqBuilder = null;
        System.Text.StringBuilder reqCostBuilder = null;
        bool inRequirements = false;
        bool inRequirementsCost = false;
        foreach (var raw in items)
        {
            string item = raw.Trim();
            if (string.IsNullOrEmpty(item)) continue;
            int colon = item.IndexOf(':');
            if (colon <= 0)
            {
                if (activeKey != null)
                {
                    if (active.Length > 0) active.Append(';');
                    active.Append(item);
                }
                continue;
            }
            string left = item.Substring(0, colon).Trim();
            string rest = item.Substring(colon + 1).Trim();

            // Handle requirements and requirements:cost explicitly to avoid key clobbering
            if (left.Equals("requirements", System.StringComparison.OrdinalIgnoreCase))
            {
                // requirements:cost:foo or regular requirements:bar
                if (rest.StartsWith("cost:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string afterCost = rest.Substring("cost:".Length).Trim();
                    if (!string.IsNullOrEmpty(afterCost))
                    {
                        if (reqCostBuilder == null) reqCostBuilder = new System.Text.StringBuilder();
                        if (reqCostBuilder.Length > 0) reqCostBuilder.Append(';');
                        reqCostBuilder.Append(afterCost);
                    }
                    inRequirements = false;
                    inRequirementsCost = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(rest))
                    {
                        if (reqBuilder == null) reqBuilder = new System.Text.StringBuilder();
                        if (reqBuilder.Length > 0) reqBuilder.Append(';');
                        reqBuilder.Append(rest);
                    }
                    inRequirements = true;
                    inRequirementsCost = false;
                }
                continue;
            }

            // Enter consequences group
            if ((left.Equals("success", System.StringComparison.OrdinalIgnoreCase)
                || left.Equals("failure", System.StringComparison.OrdinalIgnoreCase)
                || left.Equals("crit_success", System.StringComparison.OrdinalIgnoreCase)
                || left.Equals("crit_failure", System.StringComparison.OrdinalIgnoreCase)
                || left.Equals("rare_event", System.StringComparison.OrdinalIgnoreCase))
                && rest.StartsWith("consequences:", System.StringComparison.OrdinalIgnoreCase))
            {
                // flush previous
                if (activeKey != null)
                {
                    result[activeKey] = active.ToString();
                }
                activeKey = left.ToLower() + ":consequences";
                active = new System.Text.StringBuilder();
                int second = rest.IndexOf(':');
                string firstVal = second > 0 ? rest.Substring(second + 1).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(firstVal)) active.Append(firstVal);
                continue;
            }
            if (activeKey != null)
            {
                // keep accumulating until a non-consequence key appears
                string lowerItem = item.ToLower();
                if (lowerItem.StartsWith("score:") || lowerItem.StartsWith("resource:") || lowerItem.StartsWith("production:") || lowerItem.StartsWith("stat:") || lowerItem.StartsWith("population:") || lowerItem.StartsWith("housing:") || lowerItem.StartsWith("vagrants:") || lowerItem.StartsWith("deaths:") || lowerItem.StartsWith("death_records_revision:") || lowerItem.StartsWith("technology:"))
                {
                    if (active.Length > 0) active.Append(';');
                    active.Append(item);
                    continue;
                }
                // close group and fall-through
                result[activeKey] = active.ToString();
                activeKey = null;
                active = null;
            }

            // Accumulate requirement tokens if we are within a requirements group
            if (inRequirements || inRequirementsCost)
            {
                string lowerLeft = left.ToLower();
                bool isTopLevel = lowerLeft == "pillar" || lowerLeft == "strength" || lowerLeft == "challenge" || lowerLeft == "success" || lowerLeft == "failure" || lowerLeft == "crit_success" || lowerLeft == "crit_failure" || lowerLeft == "rare_event" || lowerLeft == "rare_event_percent";
                if (!isTopLevel)
                {
                    if (lowerLeft == "score" || lowerLeft == "resource" || lowerLeft == "technology" || lowerLeft == "stat" || lowerLeft == "population" || lowerLeft == "housing" || lowerLeft == "vagrants" || lowerLeft == "deaths" || lowerLeft == "vagrant_deaths" || lowerLeft == "true_deaths" || lowerLeft == "death_records_revision")
                    {
                        if (inRequirements)
                        {
                            if (reqBuilder == null) reqBuilder = new System.Text.StringBuilder();
                            if (reqBuilder.Length > 0) reqBuilder.Append(';');
                            reqBuilder.Append(item);
                            continue;
                        }
                        if (inRequirementsCost)
                        {
                            if (reqCostBuilder == null) reqCostBuilder = new System.Text.StringBuilder();
                            if (reqCostBuilder.Length > 0) reqCostBuilder.Append(';');
                            reqCostBuilder.Append(item);
                            continue;
                        }
                    }
                }
                // Close requirements grouping on top-level section key
                inRequirements = false;
                inRequirementsCost = false;
                // fall-through
            }

            // Default behavior: simple key:value
            if (result.ContainsKey(left))
            {
                // merge duplicate keys with ';'
                result[left] = string.IsNullOrEmpty(result[left]) ? rest : (result[left] + ";" + rest);
            }
            else
            {
                result[left] = rest;
            }
        }
        if (activeKey != null && active != null)
        {
            result[activeKey] = active.ToString();
        }
        if (reqBuilder != null)
        {
            result["requirements"] = reqBuilder.ToString();
        }
        if (reqCostBuilder != null)
        {
            result["requirements:cost"] = reqCostBuilder.ToString();
        }
        return result;
    }

    private List<EventCondition> ParseRequirements(string requirementsString)
    {
        var list = new List<EventCondition>();
        if (string.IsNullOrEmpty(requirementsString)) return list;
        var parts = requirementsString.Split(';');
        foreach (var p in parts)
        {
            string s = p.Trim(); if (string.IsNullOrEmpty(s)) continue;
            var cond = ParseRequirement(s);
            if (cond != null) list.Add(cond);
        }
        return list;
    }

    private EventCondition ParseRequirement(string requirementStr)
    {
        string[] parts = requirementStr.Split(':');
        if (parts.Length != 2) return null;
        string type = parts[0].Trim();
        string condition = parts[1].Trim();
        // With comparison (support multi-word targets)
        if (condition.Contains(">=") || condition.Contains("<=") || condition.Contains("==") || condition.Contains("!=") || condition.Contains(">") || condition.Contains("<"))
        {
            var tokens = condition.Split(' ');
            if (tokens.Length < 3) return null;
            // operator should be the token before the last; value should be the last token; target is everything before operator
            string valueToken = tokens[tokens.Length - 1].Trim();
            string opToken = tokens[tokens.Length - 2].Trim();
            if (!int.TryParse(valueToken, out int val)) return null;
            string target = string.Join(" ", tokens, 0, tokens.Length - 2).Trim();
            if (string.IsNullOrEmpty(target)) return null;
            return new EventCondition
            {
                type = GetConditionType(type),
                targetName = target,
                requiredValue = val,
                comparison = ParseOp(opToken)
            };
        }
        // Implicit numeric form without operator: "ResourceName 40" -> treat as ">= 40"
        var simpleTokens = condition.Split(' ');
        if (simpleTokens.Length >= 2)
        {
            string last = simpleTokens[simpleTokens.Length - 1].Trim();
            if (int.TryParse(last, out int implicitVal))
            {
                string target = string.Join(" ", simpleTokens, 0, simpleTokens.Length - 1).Trim();
                if (!string.IsNullOrEmpty(target))
                {
                    return new EventCondition
                    {
                        type = GetConditionType(type),
                        targetName = target,
                        requiredValue = implicitVal,
                        comparison = ComparisonOperator.GreaterThanOrEqual
                    };
                }
            }
        }
        // Simple (implicit equals 1 or bare name)
        return new EventCondition
        {
            type = GetConditionType(type),
            targetName = condition,
            requiredValue = 1,
            comparison = ComparisonOperator.Equals
        };
    }

    private EventCondition.ConditionType GetConditionType(string type)
    {
        switch (type.ToLower())
        {
            case "score": return EventCondition.ConditionType.ScoreCheck;
            case "resource": return EventCondition.ConditionType.ResourceCheck;
            case "technology": return EventCondition.ConditionType.TechnologyCheck;
            case "stat": return EventCondition.ConditionType.StatCheck;
            case "seventh": return EventCondition.ConditionType.SeventhCheck;
            case "phase": return EventCondition.ConditionType.PhaseCheck;
            case "echo": return EventCondition.ConditionType.EchoCheck;
            case "cycle": return EventCondition.ConditionType.CycleCheck;
            case "ritual_seventh": return EventCondition.ConditionType.RitualSeventhCheck;
            case "population": return EventCondition.ConditionType.PopulationCheck;
            case "housing": return EventCondition.ConditionType.HousingCheck;
            case "vagrants": return EventCondition.ConditionType.VagrantsCheck;
            case "deaths": return EventCondition.ConditionType.DeathsCheck;
            case "vagrant_deaths": return EventCondition.ConditionType.VagrantDeathsCheck;
            case "true_deaths": return EventCondition.ConditionType.TrueDeathsCheck;
            case "no_event_in_sevenths": return EventCondition.ConditionType.NoEventInSeventhsCheck;
            default: return EventCondition.ConditionType.ScoreCheck;
        }
    }

    private ComparisonOperator ParseOp(string op)
    {
        switch (op)
        {
            case "==": return ComparisonOperator.Equals;
            case "!=": return ComparisonOperator.NotEquals;
            case ">=": return ComparisonOperator.GreaterThanOrEqual;
            case "<=": return ComparisonOperator.LessThanOrEqual;
            case ">": return ComparisonOperator.GreaterThan;
            case "<": return ComparisonOperator.LessThan;
            default: return ComparisonOperator.Equals;
        }
    }

    private List<EventConsequence> ParseConsequences(string s)
    {
        var list = new List<EventConsequence>();
        if (string.IsNullOrEmpty(s)) return list;
        var items = s.Split(';');
        foreach (var raw in items)
        {
            string item = raw.Trim(); if (string.IsNullOrEmpty(item)) continue;
            var ec = ParseSingleConsequence(item);
            if (ec != null) list.Add(ec);
        }
        return list;
    }

    private EventConsequence ParseSingleConsequence(string str)
    {
        string[] parts = str.Split(':');
        if (parts.Length != 2) return null;
        string type = parts[0].Trim();
        string body = parts[1].Trim();
        if (body.Contains("enlightened"))
        {
            return new EventConsequence { type = EventConsequence.ConsequenceType.TechnologyEnlightened, targetName = body.Replace("enlightened", "").Trim(), value = 0 };
        }
        // Optional duration: allow syntax "... value; duration:sevenths:N"
        // Basic parse: extract final numeric token as value; remaining body is target
        int last = body.LastIndexOf(' ');
        if (last <= 0) return null;
        string target = body.Substring(0, last).Trim();
        string valStr = body.Substring(last + 1).Trim();
        int v;
        if (!int.TryParse(valStr, out v)) return null;
        var ec = new EventConsequence
        {
            type = GetConsequenceType(type),
            targetName = target,
            value = v,
            durationSevenths = 0
        };
        return ec;
    }

    private EventConsequence.ConsequenceType GetConsequenceType(string type)
    {
        switch (type.ToLower())
        {
            case "score": return EventConsequence.ConsequenceType.ScoreChange;
            case "resource": return EventConsequence.ConsequenceType.ResourceChange;
            case "production": return EventConsequence.ConsequenceType.ProductionUnitChange;
            case "stat": return EventConsequence.ConsequenceType.StatChange;
            case "population": return EventConsequence.ConsequenceType.PopulationChange;
            case "housing": return EventConsequence.ConsequenceType.HousingChange;
            case "vagrants": return EventConsequence.ConsequenceType.VagrantsChange;
            case "deaths": return EventConsequence.ConsequenceType.DeathsChange;
            case "death_records_revision": return EventConsequence.ConsequenceType.DeathRecordsRevision;
            case "technology": return EventConsequence.ConsequenceType.TechnologyEnlightened;
            case "production_percent": return EventConsequence.ConsequenceType.ProductionPercentChange;
            case "production_percent_section": return EventConsequence.ConsequenceType.ProductionPercentChangeSection;
            case "click_power": return EventConsequence.ConsequenceType.ClickPowerChange;
            case "click_power_percent": return EventConsequence.ConsequenceType.ClickPowerPercentChange;
            case "click_power_section": return EventConsequence.ConsequenceType.ClickPowerChangeSection;
            case "click_power_percent_section": return EventConsequence.ConsequenceType.ClickPowerPercentChangeSection;
            default: return EventConsequence.ConsequenceType.ScoreChange;
        }
    }
    
    /// <summary>
    /// Create a single EventVolume from an Ink file
    /// </summary>
    private void CreateVolumeFromAssets(string baseName, TextAsset compiledJson, TextAsset inkSource)
    {
        if (compiledJson == null)
        {
            Debug.LogWarning($"[InkDrivenEventSetup] No compiled Ink JSON found for '{baseName}'. Ensure the .ink is compiled to .json in Resources.");
        }

        EventVolume volume = new EventVolume
        {
            volumeName = baseName,
            inkMasterfile = compiledJson, // Prefer compiled JSON for runtime Story
            isUnlocked = true,
            priority = 5
        };

        // Prefer building nodes from compiled story JSON (Ink-native), fallback to raw ink parsing
        bool builtFromStory = false;
        if (compiledJson != null)
        {
            try
            {
                BuildStoryNodesFromCompiledStory(volume, compiledJson.text);
                builtFromStory = volume.storyNodes.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to build nodes from compiled story for '{baseName}': {ex.Message}");
            }
        }
        if (!builtFromStory)
        {
            string parseText = inkSource != null ? inkSource.text : compiledJson != null ? compiledJson.text : string.Empty;
            ParseInkFileForStoryNodes(volume, parseText);
        }

        // Register the volume
        EventSystemLogic.Instance?.AddVolume(volume);
    }

    /// <summary>
    /// Build StoryNodes using Ink's compiled Story data: enumerate knots and read tags with Story.TagsForContentAtPath
    /// </summary>
    private void BuildStoryNodesFromCompiledStory(EventVolume volume, string compiledJsonText)
    {
        if (string.IsNullOrEmpty(compiledJsonText)) return;
        Story story = new Story(compiledJsonText);

        // The main container holds named knots in namedContent
        var named = story.mainContentContainer?.namedContent;
        if (named == null || named.Count == 0)
        {
            EventSystemLogic.Instance.LogEvent("[InkDrivenEventSetup] Compiled story has no named content", "InkDrivenEventSetup");
            return;
        }

        foreach (var kv in named)
        {
            string knotName = kv.Key;
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Processing knot: '{knotName}'", "InkDrivenEventSetup");
            
            // Get tags for this knot (metadata lines imported as tags)
            List<string> tags = story.TagsForContentAtPath(knotName) ?? new List<string>();
            // Hide deprecated screen_flow from debug output to reduce confusion
            var filtered = tags.FindAll(t => !t.TrimStart().StartsWith("screen_flow:"));
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Found {filtered.Count} tags for knot '{knotName}': {string.Join(", ", filtered)}", "InkDrivenEventSetup");

            string title = null, description = null, conditions = null, consequences = null, screenFlow = null;
            int cooldown = 0;
            Dictionary<string, string> uiMetadata = new Dictionary<string, string>();

            foreach (string tag in tags)
            {
                string t = tag.Trim();
                if (t.StartsWith("title:")) title = t.Substring(6).Trim();
                else if (t.StartsWith("description:")) description = t.Substring(12).Trim();
                else if (t.StartsWith("conditions:")) conditions = t.Substring(11).Trim();
                else if (t.StartsWith("consequences:")) consequences = t.Substring(13).Trim();
                else if (t.StartsWith("cooldown:")) int.TryParse(t.Substring(9).Trim(), out cooldown);
                // Ignore deprecated screen_flow metadata (dynamic flow now)
                else if (t.StartsWith("screen_flow:")) { /* deprecated */ }
                else if (t.StartsWith("splash_art:")) uiMetadata["splash_art"] = t.Substring(11).Trim();
                else if (t.StartsWith("event_type:")) uiMetadata["event_type"] = t.Substring(11).Trim();
                else if (t.StartsWith("priority:")) uiMetadata["priority"] = t.Substring(9).Trim();
                else if (t.StartsWith("event_color:")) uiMetadata["event_color"] = t.Substring(12).Trim();
                else if (t.StartsWith("button_text:")) uiMetadata["button_text"] = t.Substring(12).Trim();
                else if (t.StartsWith("background:")) uiMetadata["background"] = t.Substring(11).Trim();
                else if (t.StartsWith("speaker:")) uiMetadata["speaker"] = t.Substring(9).Trim();
                else if (t.StartsWith("portrait:")) uiMetadata["portrait"] = t.Substring(10).Trim();
                else if (t.StartsWith("layout:")) uiMetadata["layout"] = t.Substring(8).Trim();
            }
            
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Extracted metadata for '{knotName}': title='{title}', conditions='{conditions}', event_type='{uiMetadata.GetValueOrDefault("event_type")}'", "InkDrivenEventSetup");

            // Only create story nodes for knots that have the essential metadata (title and conditions)
            // This prevents internal story flow knots from being treated as separate events
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(conditions) && !string.IsNullOrEmpty(uiMetadata.GetValueOrDefault("event_type")))
            {
                StoryNode node = CreateStoryNodeFromParsedData(
                    knotName,
                    title,
                    description,
                    conditions,
                    consequences,
                    screenFlow,
                    uiMetadata,
                    cooldown
                );

                volume.storyNodes.Add(node);
                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] (Compiled) Added story node '{knotName}' with title '{node.storyTitle}' and {node.storyConditions.Count} conditions", "InkDrivenEventSetup");
            }
            else
            {
                string missingFields = "";
                if (string.IsNullOrEmpty(title)) missingFields += "title, ";
                if (string.IsNullOrEmpty(conditions)) missingFields += "conditions, ";
                if (string.IsNullOrEmpty(uiMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                missingFields = missingFields.TrimEnd(',', ' ');
                
                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] (Compiled) Skipped internal knot '{knotName}' - missing: {missingFields}", "InkDrivenEventSetup");
            }
        }

        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] (Compiled) Parsed {volume.storyNodes.Count} story node(s) for volume '{volume.volumeName}'", "InkDrivenEventSetup");
    }
    
    /// <summary>
    /// Parse Ink file content to find story nodes and their metadata
    /// </summary>
    private void ParseInkFileForStoryNodes(EventVolume volume, string inkContent)
    {
        string[] lines = inkContent.Split('\n');
        string currentNodeName = null;
        string currentTitle = "";
        string currentDescription = "";
        string currentConditions = "";
        string currentConsequences = "";
        string currentScreenFlow = "";
        int currentCooldown = 0;
        Dictionary<string, string> currentUIMetadata = new Dictionary<string, string>();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            // Check for knot definition (robust): lines like "=== name ===" or with extra spaces
            if (trimmedLine.StartsWith("==="))
            {
                // Save previous node if exists
                if (!string.IsNullOrEmpty(currentNodeName))
                {
                    // Only create story nodes for knots that have the essential metadata (title and conditions)
                    // This prevents internal story flow knots from being treated as separate events
                    if (!string.IsNullOrEmpty(currentTitle) && !string.IsNullOrEmpty(currentConditions) && !string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type")))
                    {
                        StoryNode storyNode = CreateStoryNodeFromParsedData(
                            currentNodeName, 
                            currentTitle, 
                            currentDescription, 
                            currentConditions, 
                            currentConsequences, 
                            currentScreenFlow, 
                            currentUIMetadata,
                            currentCooldown
                        );
                        volume.storyNodes.Add(storyNode);
                        
                        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Added story node '{currentNodeName}' with title '{currentTitle}' and {storyNode.storyConditions.Count} conditions", "InkDrivenEventSetup");
                    }
                    else
                    {
                        string missingFields = "";
                        if (string.IsNullOrEmpty(currentTitle)) missingFields += "title, ";
                        if (string.IsNullOrEmpty(currentConditions)) missingFields += "conditions, ";
                        if (string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                        missingFields = missingFields.TrimEnd(',', ' ');
                        
                        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Skipped internal knot '{currentNodeName}' - missing: {missingFields}", "InkDrivenEventSetup");
                    }
                }

                // Start new node: extract between leading/trailing '='
                // Use regex to capture the name between === ... ===
                var match = Regex.Match(trimmedLine, "^=+\\s*(.*?)\\s*=+$");
                currentNodeName = match.Success ? match.Groups[1].Value : trimmedLine.Trim('=').Trim();
                currentTitle = "";
                currentDescription = "";
                currentConditions = "";
                currentConsequences = "";
                currentScreenFlow = "";
                currentCooldown = 0;
                currentUIMetadata = new Dictionary<string, string>();

                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Found knot: '{currentNodeName}'", "InkDrivenEventSetup");
            }
            // Check for metadata lines
            else if (trimmedLine.StartsWith("#"))
            {
                ParseMetadataLine(trimmedLine, ref currentTitle, ref currentDescription, 
                    ref currentConditions, ref currentConsequences, ref currentScreenFlow, ref currentUIMetadata, ref currentCooldown);
            }
        }

        // Save the last node
        if (!string.IsNullOrEmpty(currentNodeName))
        {
            // Only create story nodes for knots that have the essential metadata (title and conditions)
            // This prevents internal story flow knots from being treated as separate events
            if (!string.IsNullOrEmpty(currentTitle) && !string.IsNullOrEmpty(currentConditions) && !string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type")))
            {
                StoryNode storyNode = CreateStoryNodeFromParsedData(
                    currentNodeName, 
                    currentTitle, 
                    currentDescription, 
                    currentConditions, 
                    currentConsequences, 
                    currentScreenFlow, 
                    currentUIMetadata,
                    currentCooldown
                );
                volume.storyNodes.Add(storyNode);
                
                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Added story node '{currentNodeName}' with title '{currentTitle}' and {storyNode.storyConditions.Count} conditions", "InkDrivenEventSetup");
            }
            else
            {
                string missingFields = "";
                if (string.IsNullOrEmpty(currentTitle)) missingFields += "title, ";
                if (string.IsNullOrEmpty(currentConditions)) missingFields += "conditions, ";
                if (string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                missingFields = missingFields.TrimEnd(',', ' ');
                
                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Skipped internal knot '{currentNodeName}' - missing: {missingFields}", "InkDrivenEventSetup");
            }
        }

        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Parsed {volume.storyNodes.Count} story node(s) for volume '{volume.volumeName}'", "InkDrivenEventSetup");
        for (int i = 0; i < volume.storyNodes.Count; i++)
        {
            var sn = volume.storyNodes[i];
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Node[{i}]: name='{sn.nodeName}', title='{sn.storyTitle}', conditions={sn.storyConditions.Count}, flowSteps={sn.screenFlow.Count}", "InkDrivenEventSetup");
        }
    }
    
    /// <summary>
    /// Parse metadata line and extract information
    /// </summary>
    private void ParseMetadataLine(string line, ref string title, ref string description, ref string conditions, ref string consequences, ref string screenFlow, ref Dictionary<string, string> uiMetadata, ref int cooldown)
    {
        line = line.Trim();
        
        if (line.StartsWith("# title:"))
        {
            title = line.Substring(8).Trim();
        }
        else if (line.StartsWith("# description:"))
        {
            description = line.Substring(14).Trim();
        }
        else if (line.StartsWith("# conditions:"))
        {
            conditions = line.Substring(13).Trim();
        }
        else if (line.StartsWith("# consequences:"))
        {
            consequences = line.Substring(15).Trim();
        }
        else if (line.StartsWith("# screen_flow:"))
        {
            screenFlow = line.Substring(15).Trim();
        }
        else if (line.StartsWith("# cooldown:"))
        {
            int.TryParse(line.Substring(11).Trim(), out cooldown);
        }
        // New UI metadata parsing
        else if (line.StartsWith("# splash_art:"))
        {
            uiMetadata["splash_art"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# event_type:"))
        {
            uiMetadata["event_type"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# event_color:"))
        {
            uiMetadata["event_color"] = line.Substring(13).Trim();
        }
        else if (line.StartsWith("# button_text:"))
        {
            uiMetadata["button_text"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# background:"))
        {
            uiMetadata["background"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# speaker:"))
        {
            uiMetadata["speaker"] = line.Substring(9).Trim();
        }
        else if (line.StartsWith("# portrait:"))
        {
            uiMetadata["portrait"] = line.Substring(10).Trim();
        }
        else if (line.StartsWith("# layout:"))
        {
            uiMetadata["layout"] = line.Substring(8).Trim();
        }
        else if (line.StartsWith("# priority:"))
        {
            uiMetadata["priority"] = line.Substring(10).Trim();
        }
    }
    
    /// <summary>
    /// Create a story node from parsed data
    /// </summary>
    private StoryNode CreateStoryNodeFromParsedData(string nodeName, string title, string description, string conditions, string consequences, string screenFlow, Dictionary<string, string> uiMetadata, int cooldown = 0)
    {
        StoryNode storyNode = new StoryNode();
        storyNode.nodeName = nodeName;
        storyNode.storyTitle = title ?? nodeName;
        storyNode.storyDescription = description ?? "";
        storyNode.isUnlocked = true;
        storyNode.cooldownSevenths = cooldown;
        
        // Parse priority from metadata, default to 0 if not specified
        if (uiMetadata.ContainsKey("priority") && int.TryParse(uiMetadata["priority"], out int parsedPriority))
        {
            storyNode.priority = parsedPriority;
        }
        else
        {
            storyNode.priority = 0; // Default priority for events without explicit priority
        }

        // Parse conditions and consequences
        if (!string.IsNullOrEmpty(conditions))
        {
            storyNode.storyConditions = ParseConditions(conditions);
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Node '{nodeName}' parsed {storyNode.storyConditions.Count} condition(s): '{conditions}'", "InkDrivenEventSetup");
        }

        if (!string.IsNullOrEmpty(consequences))
        {
            storyNode.storyConsequences = ParseConsequences(consequences);
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Node '{nodeName}' parsed {storyNode.storyConsequences.Count} consequence(s): '{consequences}'", "InkDrivenEventSetup");
        }

        // Generate screen flow (ignore deprecated custom screen_flow metadata)
        storyNode.screenFlow = GenerateScreenFlowFromNodeName(nodeName, null);

        // Store UI metadata in the story node for later use
        storyNode.uiMetadata = uiMetadata;

        return storyNode;
    }
    
    /// <summary>
    /// Parse condition strings into EventCondition objects
    /// </summary>
    private List<EventCondition> ParseConditions(string conditionString)
    {
        List<EventCondition> conditions = new List<EventCondition>();
        
        if (string.IsNullOrEmpty(conditionString))
            return conditions;
        
        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Parsing conditions: '{conditionString}'", "InkDrivenEventSetup");
        
        // Support multiple conditions separated by ';'
        string[] parts = conditionString.Split(';');
        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Processing condition part: '{trimmed}'", "InkDrivenEventSetup");
            
            EventCondition condition = ParseConditionString(trimmed);
            if (condition != null)
            {
                conditions.Add(condition);
                EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Successfully parsed condition: {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue}", "InkDrivenEventSetup");
            }
            else
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse condition part: '{trimmed}'");
            }
        }
        
        EventSystemLogic.Instance.LogEvent($"[InkDrivenEventSetup] Parsed {conditions.Count} conditions successfully", "InkDrivenEventSetup");
        
        return conditions;
    }
    
    /// <summary>
    /// Parse a single condition string
    /// </summary>
    private EventCondition ParseConditionString(string conditionStr)
    {
        // Example formats:
        // "score:quest_progress >= 1"
        // "resource:gold >= 100"
        // "technology:agriculture == 1"
        // "ritual_seventh == 1"
        // "no_event_in_sevenths:2" (special case - no comparison operator)
        
        string[] parts = conditionStr.Split(':');
        if (parts.Length != 2) return null;
        
        string type = parts[0].Trim();
        string condition = parts[1].Trim();
        
        // Special handling for no_event_in_sevenths condition (no comparison operator needed)
        if (type.ToLower() == "no_event_in_sevenths")
        {
            if (int.TryParse(condition, out int sevenths))
            {
                EventCondition noEventCondition = new EventCondition
                {
                    type = EventCondition.ConditionType.NoEventInSeventhsCheck,
                    targetName = "sevenths", // Not used for this condition type
                    requiredValue = sevenths,
                    comparison = ComparisonOperator.GreaterThanOrEqual // Default to >= for time-based checks
                };
                return noEventCondition;
            }
            else
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse sevenths value '{condition}' from condition string: '{conditionStr}'");
                return null;
            }
        }
        
        // Parse the condition part (e.g., "quest_progress >= 1")
        string[] conditionParts = condition.Split(' ');
        
        // Special handling for technology conditions (no comparison operator needed)
        if (type.ToLower() == "technology")
        {
            // Technology conditions just check if the technology is unlocked
            // Format: technology:Technology Name
            EventCondition techCondition = new EventCondition
            {
                targetName = condition.Trim(), // Use the full technology name
                requiredValue = 1, // Always check for unlocked (1 = true)
                comparison = ComparisonOperator.Equals
            };
            techCondition.type = EventCondition.ConditionType.TechnologyCheck;
            return techCondition;
        }
        
        // For all other condition types, expect the standard format
        if (conditionParts.Length < 3) return null;
        
        string targetName = conditionParts[0];
        string operatorStr = conditionParts[1];
        
        // Use TryParse instead of Parse to avoid exceptions
        if (!int.TryParse(conditionParts[2], out int value))
        {
            Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse condition value '{conditionParts[2]}' from condition string: '{conditionStr}'");
            return null;
        }
        
        EventCondition eventCondition = new EventCondition
        {
            targetName = targetName,
            requiredValue = value,
            comparison = ParseComparisonOperator(operatorStr)
        };
        
        // Set the condition type based on the prefix
        switch (type.ToLower())
        {
            case "score":
                eventCondition.type = EventCondition.ConditionType.ScoreCheck;
                break;
            case "resource":
                eventCondition.type = EventCondition.ConditionType.ResourceCheck;
                break;
            case "technology":
                eventCondition.type = EventCondition.ConditionType.TechnologyCheck;
                break;
            case "stat":
                eventCondition.type = EventCondition.ConditionType.StatCheck;
                break;
            case "seventh":
                eventCondition.type = EventCondition.ConditionType.SeventhCheck;
                break;
            case "phase":
                eventCondition.type = EventCondition.ConditionType.PhaseCheck;
                break;
            case "echo":
                eventCondition.type = EventCondition.ConditionType.EchoCheck;
                break;
            case "cycle":
                eventCondition.type = EventCondition.ConditionType.CycleCheck;
                break;
            case "ritual_seventh":
                eventCondition.type = EventCondition.ConditionType.RitualSeventhCheck;
                break;
            case "population":
                eventCondition.type = EventCondition.ConditionType.PopulationCheck;
                break;
            case "housing":
                eventCondition.type = EventCondition.ConditionType.HousingCheck;
                break;
            case "vagrants":
                eventCondition.type = EventCondition.ConditionType.VagrantsCheck;
                break;
            case "deaths":
                eventCondition.type = EventCondition.ConditionType.DeathsCheck;
                break;
            case "vagrant_deaths":
                eventCondition.type = EventCondition.ConditionType.VagrantDeathsCheck;
                break;
            case "true_deaths":
                eventCondition.type = EventCondition.ConditionType.TrueDeathsCheck;
                break;
            default:
                Debug.LogWarning($"[InkDrivenEventSetup] Unknown condition type '{type}' in condition string: '{conditionStr}'");
                return null;
        }
        
        return eventCondition;
    }
    
    /// <summary>
    /// Parse comparison operator string
    /// </summary>
    private ComparisonOperator ParseComparisonOperator(string operatorStr)
    {
        switch (operatorStr)
        {
            case "==": return ComparisonOperator.Equals;
            case "!=": return ComparisonOperator.NotEquals;
            case ">=": return ComparisonOperator.GreaterThanOrEqual;
            case "<=": return ComparisonOperator.LessThanOrEqual;
            case ">": return ComparisonOperator.GreaterThan;
            case "<": return ComparisonOperator.LessThan;
            default: return ComparisonOperator.Equals;
        }
    }
    
    // Use existing consequence parsing earlier in this class to avoid duplication
    
    /// <summary>
    /// Generate screen flow based on node name and optional custom screen flow
    /// This allows for truly flexible screen sequences per node
    /// </summary>
    private List<ScreenFlowStep> GenerateScreenFlowFromNodeName(string nodeName, string customScreenFlow = null)
    {
        List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>();
        
        // Minimal default flow: a single Splash screen that shows the knot content and first choice.
        screenFlow.Add(new ScreenFlowStep
        {
            flowType = ScreenFlowStep.FlowType.Splash,
            screenId = $"{nodeName}_splash",
            inkKnot = nodeName,
            displayDuration = 3f,
            waitForInput = false
        });
        
        return screenFlow;
    }
    
    /// <summary>
    /// Parse custom screen flow from metadata string
    /// </summary>
    private List<ScreenFlowStep> ParseCustomScreenFlow(string nodeName, string screenFlowString)
    {
        List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>();
        
        // Parse screen flow like "splash,verse,verse,chorus,bridge,chorus,verse,outro"
        string[] screenTypes = screenFlowString.Split(',');
        
        for (int i = 0; i < screenTypes.Length; i++)
        {
            string screenType = screenTypes[i].Trim().ToLower();
            ScreenFlowStep step = new ScreenFlowStep();
            
            switch (screenType)
            {
                case "splash":
                    step.flowType = ScreenFlowStep.FlowType.Splash;
                    step.screenId = $"{nodeName}_splash";
                    step.inkKnot = nodeName; // splash uses main node content
                    step.displayDuration = 3f;
                    step.waitForInput = false;
                    break;
                    
                case "verse":
                    step.flowType = ScreenFlowStep.FlowType.Verse;
                    step.screenId = $"{nodeName}_verse_{GetVerseNumber(screenFlow, screenType)}";
                    step.inkKnot = $"{nodeName}_verse_{GetVerseNumber(screenFlow, screenType)}";
                    step.waitForInput = true;
                    break;
                    
                case "chorus":
                    step.flowType = ScreenFlowStep.FlowType.Chorus;
                    step.screenId = $"{nodeName}_chorus_{GetChorusNumber(screenFlow, screenType)}";
                    // Optional: allow content via Ink if provided
                    step.inkKnot = $"{nodeName}_chorus_{GetChorusNumber(screenFlow, screenType)}";
                    step.waitForInput = true;
                    break;
                    
                case "bridge":
                    step.flowType = ScreenFlowStep.FlowType.Bridge;
                    step.screenId = $"{nodeName}_bridge_{GetBridgeNumber(screenFlow, screenType)}";
                    step.inkKnot = $"{nodeName}_bridge_{GetBridgeNumber(screenFlow, screenType)}";
                    step.displayDuration = 2f;
                    step.waitForInput = false;
                    break;
                    
                case "outro":
                    step.flowType = ScreenFlowStep.FlowType.Outro;
                    step.screenId = $"{nodeName}_outro";
                    step.inkKnot = $"{nodeName}_outro";
                    step.displayDuration = 2f;
                    step.waitForInput = false;
                    break;
                    
                default:
                    // Unknown screen type, skip
                    continue;
            }
            
            screenFlow.Add(step);
        }
        
        return screenFlow;
    }
    
    /// <summary>
    /// Get the verse number for this verse screen
    /// </summary>
    private int GetVerseNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Verse) + 1;
    }
    
    /// <summary>
    /// Get the chorus number for this chorus screen
    /// </summary>
    private int GetChorusNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Chorus) + 1;
    }
    
    /// <summary>
    /// Get the bridge number for this bridge screen
    /// </summary>
    private int GetBridgeNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Bridge) + 1;
    }
} 