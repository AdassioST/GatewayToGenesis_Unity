using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws the world (roadmap WG11) with no GameObject per cell: one quad whose shader (WorldTerrain) reads the
/// micro, meso and macro colours from point-sampled textures keyed by hex coordinates, plus two batched meshes
/// (WorldMarks) for rivers and leylines, and for markers. Knowledge, selection and overlays are small textures
/// rewritten on change; zooming only moves the camera and sets the shader's reading level. The world lives far
/// behind the capital's camera (<see cref="Depth"/>) and has its own camera, enabled only in the world view.
/// </summary>
public class WorldRenderer
{
    public const float Depth = 20000f;

    private static readonly Color VoidColor = new Color(0.015f, 0.02f, 0.04f);
    private static readonly Color FogColor = new Color(0.04f, 0.035f, 0.055f);
    private static readonly Color LineColor = new Color(0.015f, 0.012f, 0.02f);
    private static readonly Color Shore = new Color(0.78f, 0.72f, 0.55f);
    private static readonly Color RiverColor = new Color(0.32f, 0.54f, 0.78f, 0.95f);
    private static readonly Color SilverColor = new Color(0.86f, 0.9f, 0.98f, 1f);
    private static readonly Color LeylineColor = new Color(0.8f, 0.58f, 1f, 0.85f);
    private static readonly Color ForecastColor = new Color(1f, 0.84f, 0.45f, 0.6f);
    private static readonly Color CapitalColor = new Color32(0xFF, 0xD8, 0x6A, 0xFF);
    private static readonly Color SacredColor = new Color(0.97f, 0.97f, 1f, 1f);
    private static readonly Color ConvergenceColor = new Color(0.8f, 0.6f, 1f, 1f);
    private static readonly Color BasinColor = new Color(1f, 0.75f, 1f, 1f);
    private static readonly Color ExpeditionColor = new Color32(0xFF, 0xF6, 0xD2, 0xFF);
    private static readonly Color RoadColor = new Color(0.78f, 0.64f, 0.42f, 0.95f);
    private static readonly Color PreviousColor = new Color(0.6f, 0.58f, 0.66f, 0.5f);
    private static readonly Color TownColor = new Color(1f, 0.93f, 0.78f, 1f);
    private static readonly Color MajorColor = new Color32(0xFF, 0xC8, 0x5A, 0xFF);
    private static readonly Color OutpostColor = new Color(0.55f, 0.9f, 0.95f, 1f);
    private static readonly Color HavenColor = new Color(0.95f, 0.95f, 1f, 1f);
    private static readonly Color AnchorColor = new Color(0.75f, 0.55f, 1f, 1f);
    private static readonly Color EnclaveColor = new Color(0.98f, 0.66f, 0.3f, 1f);
    private static readonly Color ThreatColor = new Color(0.95f, 0.22f, 0.22f, 1f);
    private static readonly Color NexusColor = new Color(1f, 0.85f, 0.3f, 1f);
    private static readonly Color NodeColor = new Color(1f, 0.98f, 0.9f, 1f);
    private static readonly Color CragColor = new Color(0.36f, 0.33f, 0.31f, 1f);
    private static readonly Color CampColor = new Color(1f, 0.72f, 0.36f, 1f);
    private static readonly Color DistressColor = new Color(1f, 0.24f, 0.2f, 1f);

    /// <summary>The user's reference colours of the seven sectors (the Sectors overlay).</summary>
    private static readonly Dictionary<string, Color> SectorColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
    {
        { "S1", new Color32(0xA4, 0xC6, 0x3C, 0xFF) }, { "S2", new Color32(0x4E, 0x36, 0x32, 0xFF) },
        { "S3", new Color32(0x9C, 0x8C, 0xCC, 0xFF) }, { "S4", new Color32(0xE8, 0xC8, 0xC0, 0xFF) },
        { "S5", new Color32(0xF8, 0xC4, 0x14, 0xFF) }, { "S6", new Color32(0xB4, 0xC8, 0xEC, 0xFF) },
        { "S7", new Color32(0x74, 0x1C, 0x4C, 0xFF) },
    };

    public Camera Camera { get; private set; }
    public GameObject Root { get; private set; }

    private WorldMap _map;
    private WorldGenSettings _settings;
    private Material _terrain, _marks;
    private Texture2D _microTex, _mesoTex, _macroTex, _fogTex, _overlayTex, _stateTex;
    private Texture2D _reliefTex, _microFogTex;
    private Color32[] _fog, _overlay, _state;
    // What is known of each micro hex (the travel grid's fog), one byte each.
    private byte[] _microFog;
    // World units per screen pixel at the present zoom (SetLevel).
    private float _perPixel = 0.05f;
    private int _microSize, _microOffset, _mesoSize, _mesoOffset;
    private Mesh _lineMesh, _markerMesh, _unitMesh;
    private Color[] _graded;
    private WorldLens _lens = WorldLens.Normal;
    private float[] _potential;
    private SettlementRules _rules;
    private bool _showRivers = true, _showLeylines, _showPrevious;
    private List<Leyline> _forecast;

    // ===== BUILD =====

    public bool Build(WorldMap map, WorldGenSettings settings, Transform parent, SettlementRules rules = null)
    {
        _rules = rules ?? new SettlementRules();
        var terrainShader = Resources.Load<Shader>("World/Shaders/WorldTerrain");
        var marksShader = Resources.Load<Shader>("World/Shaders/WorldMarks");
        if (map == null || terrainShader == null || marksShader == null)
        {
            GameLog.Warning("The world view's shaders (Resources/World/Shaders) are missing: the world cannot be drawn.", LogChannel.World);
            return false;
        }
        _map = map;
        _settings = settings;

        Root = new GameObject("World View");
        Root.transform.SetParent(parent, false);
        Root.transform.position = new Vector3(0f, 0f, Depth);

        var cameraGo = new GameObject("World Camera");
        cameraGo.transform.SetParent(Root.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 0f, -100f);
        Camera = cameraGo.AddComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = WorldZoom.MicroSize;
        Camera.clearFlags = CameraClearFlags.SolidColor;
        Camera.backgroundColor = VoidColor;
        Camera.nearClipPlane = 1f;
        Camera.farClipPlane = 200f;
        Camera.depth = (UnityEngine.Camera.main != null ? UnityEngine.Camera.main.depth : 0f) + 5f;
        Camera.enabled = false;

        BuildTextures();
        _terrain = new Material(terrainShader) { name = "World Terrain", renderQueue = 2000 };
        _terrain.SetTexture("_MicroTex", _microTex);
        _terrain.SetTexture("_MesoTex", _mesoTex);
        _terrain.SetTexture("_MacroTex", _macroTex);
        _terrain.SetTexture("_FogTex", _fogTex);
        _terrain.SetTexture("_OverlayTex", _overlayTex);
        _terrain.SetTexture("_StateTex", _stateTex);
        _terrain.SetTexture("_ReliefTex", _reliefTex);
        _terrain.SetTexture("_MicroFogTex", _microFogTex);
        _terrain.SetVector("_HoverMicro", Vector4.zero);
        _terrain.SetVector("_TargetMicro", Vector4.zero);
        _terrain.SetVector("_MicroInfo", new Vector4(_microOffset, _microSize, 0f, 0f));
        _terrain.SetVector("_MesoInfo", new Vector4(_mesoOffset, _mesoSize, 0f, 0f));
        _terrain.SetColor("_FogColor", FogColor);
        _terrain.SetColor("_VoidColor", VoidColor);
        _terrain.SetColor("_LineColor", LineColor);
        _terrain.SetColor("_SelectColor", new Color32(0xFF, 0xF6, 0xD2, 0xFF));
        _terrain.SetColor("_FrontierColor", new Color32(0xE6, 0xBC, 0x93, 0xFF));
        _terrain.SetColor("_ExpeditionColor", ExpeditionColor);
        _terrain.SetFloat("_OverlayAlpha", 0f);

        _marks = new Material(marksShader) { name = "World Marks", renderQueue = 3000 };
        _marks.SetTexture("_FogTex", _fogTex);
        _marks.SetVector("_MesoInfo", new Vector4(_mesoOffset, _mesoSize, 0f, 0f));

        float margin = 60f;
        var quad = new Mesh { name = "World Ground" };
        quad.vertices = new[]
        {
            new Vector3(map.minX - margin, map.minY - margin, 0f), new Vector3(map.maxX + margin, map.minY - margin, 0f),
            new Vector3(map.maxX + margin, map.maxY + margin, 0f), new Vector3(map.minX - margin, map.maxY + margin, 0f),
        };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f));
        Renderer("Ground", quad, _terrain, 0f, 0);

        _lineMesh = new Mesh { name = "World Lines", indexFormat = IndexFormat.UInt32 };
        _markerMesh = new Mesh { name = "World Markers", indexFormat = IndexFormat.UInt32 };
        Renderer("Lines", _lineMesh, _marks, -0.1f, 1);
        Renderer("Markers", _markerMesh, _marks, -0.2f, 2);
        _unitMesh = new Mesh { name = "World Units", indexFormat = IndexFormat.UInt32 };
        Renderer("Units", _unitMesh, _marks, -0.3f, 3);

        RefreshKnowledge();
        RefreshLines();
        return true;
    }

    private void Renderer(string name, Mesh mesh, Material material, float z, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Root.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static int PowerOfTwo(int n)
    {
        int p = 16;
        while (p < n) p *= 2;
        return p;
    }

    private static Texture2D Texture(int size, string name) =>
        new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

    private int MesoIndex(HexCoord c) => (c.r + _mesoOffset) * _mesoSize + (c.q + _mesoOffset);

    /// <summary>
    /// The cinematic grade of the ground, once per world: each cell's colour pushed to high contrast and saturation,
    /// hill-shaded by a low light from the north-west (relief reads at a glance), cold and pale toward the peaks, and
    /// water graded by depth from glowing teal shallows to ink-black deeps.
    /// </summary>
    private void GradeGround()
    {
        _graded = new Color[_map.Count];
        float sea = _settings.seaLevel;
        const float lx = -0.62f, ly = 0.78f; // light from the north-west
        foreach (var t in _map.Tiles)
        {
            Color c = RawColor(t);
            if (t.water && !t.lake)
            {
                float depth = Mathf.Clamp01((sea - t.elevation) / 0.26f);
                c = Color.Lerp(Color.Lerp(c, new Color(0.08f, 0.32f, 0.36f), 0.55f), new Color(0.01f, 0.03f, 0.08f), Mathf.SmoothStep(0f, 1f, depth));
                _graded[t.index] = c;
                continue;
            }
            if (t.lake)
            {
                _graded[t.index] = Color.Lerp(new Color(0.34f, 0.43f, 0.49f), new Color(0.035f, 0.08f, 0.15f), Mathf.Clamp01(t.waterDepth / 0.16f));
                continue;
            }
            // Relief: the slope toward the light brightens, away from it darkens.
            float gx = 0f, gy = 0f;
            foreach (var n in _map.NeighboursOf(t))
            {
                float dx = n.x - t.x, dy = n.y - t.y, len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len <= 0f) continue;
                float dz = (n.water && !n.lake ? sea : n.elevation) - t.elevation;
                gx += dz * dx / len;
                gy += dz * dy / len;
            }
            float facing = -(gx * lx + gy * ly);
            float shade = Mathf.Clamp(1f + 3.2f * facing, 0.62f, 1.35f);
            // Contrast and saturation, then height: high ground pales and cools.
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * 1.25f);
            v = Mathf.Clamp01((v - 0.45f) * 1.25f + 0.43f);
            c = Color.HSVToRGB(h, s, v);
            float high = Mathf.Clamp01((t.elevation - 0.62f) / 0.3f);
            c = Color.Lerp(c, new Color(0.78f, 0.8f, 0.88f), high * 0.35f);
            c *= shade;
            _graded[t.index] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        }
    }

    private void BuildTextures()
    {
        GradeGround();
        int mesoMax = 1, microMax = 1;
        foreach (var t in _map.Tiles)
        {
            mesoMax = Math.Max(mesoMax, Math.Max(Math.Abs(t.coord.q), Math.Abs(t.coord.r)));
            var center = HexHierarchy.ChildCenter(t.coord);
            microMax = Math.Max(microMax, Math.Max(Math.Abs(center.q), Math.Abs(center.r)) + 1);
        }
        _mesoSize = PowerOfTwo(2 * mesoMax + 2);
        _mesoOffset = _mesoSize / 2;
        _microSize = PowerOfTwo(2 * microMax + 2);
        _microOffset = _microSize / 2;

        var micro = new Color32[_microSize * _microSize];
        var meso = new Color32[_mesoSize * _mesoSize];
        var macro = new Color32[_mesoSize * _mesoSize];
        var relief = new Color32[_mesoSize * _mesoSize];
        int seed = MicroNavigation.GroundSeed(_map);

        foreach (var t in _map.Tiles)
        {
            Color ground = TerrainColor(t);
            bool fresh = t.lake || t.river || _map.NeighboursOf(t).Any(n => n.lake || n.river);
            relief[MesoIndex(t.coord)] = new Color(Mathf.Clamp01(t.escarpment * 10f),
                fresh ? 0.7f : 0f, t.terrain == "auric-meadow" ? 1f : 0f, 1f);
            meso[MesoIndex(t.coord)] = WithAlpha(ground, t.water ? 0.6f : 1f);
            var center = HexHierarchy.ChildCenter(t.coord);
            bool walkable = MicroNavigation.Walkable(t, _settings);
            for (int k = 0; k < MicroNavigation.PerCell; k++)
            {
                var offset = HexHierarchy.ChildOffsets[k];
                var hex = center + offset;
                Color c = ground;
                double h = WorldNoise.Hash01(seed, hex.q, hex.r);
                if (offset != HexCoord.Zero)
                {
                    // Rim hexes may carry the neighbouring cell's ground (seams at micro scale are ragged, not
                    // straight): between walkable lands exactly as the travel grid reads them (MicroNavigation.GroundCell),
                    // so what a hex looks like is what walking it costs; never across a shore or a mountain wall.
                    var beyond = _map.Get(HexHierarchy.Parent(hex + offset));
                    if (beyond != null && beyond != t && beyond.terrain != t.terrain)
                    {
                        bool beyondWalkable = MicroNavigation.Walkable(beyond, _settings);
                        if (t.water != beyond.water)
                        {
                            if (!t.water && h < 0.4) c = Color.Lerp(ground, Shore, 0.55f);
                            else if (t.water && h < 0.3) c = Color.Lerp(ground, TerrainColor(beyond), 0.35f);
                        }
                        else if (walkable && beyondWalkable)
                        {
                            if (MicroNavigation.GroundCell(_map, _settings, hex, seed) == beyond) c = Color.Lerp(ground, TerrainColor(beyond), 0.8f);
                        }
                        else if (!walkable && !beyondWalkable && h < MicroNavigation.BorrowChance) c = Color.Lerp(ground, TerrainColor(beyond), 0.8f);
                    }
                }
                // Crags: broken rock no one climbs, darker and stonier (the shader hatches them).
                bool crag = (t.microBlockedMask & (1 << k)) != 0;
                if (crag) c = Color.Lerp(c * 0.6f, CragColor, 0.45f);
                float shade = 0.94f + 0.12f * (float)WorldNoise.Hash01(seed + 7, hex.q, hex.r);
                c = new Color(c.r * shade, c.g * shade, c.b * shade, crag ? 0.5f : 1f);
                int mi = (hex.r + _microOffset) * _microSize + (hex.q + _microOffset);
                if (mi >= 0 && mi < micro.Length) micro[mi] = c;
            }
        }

        // The atlas: each aggregate's leading biome, shaded by each cell's own (a mixture, not a majority label).
        foreach (var group in _map.Tiles.GroupBy(t => WorldMap.MacroOf(t.coord)))
        {
            var land = group.Where(t => !t.water || t.lake).ToList();
            var lead = land.Where(t => t.biome != null).GroupBy(t => t.biome).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
            Color leadColor = lead != null ? BiomeColor(lead.Key) : new Color(0.45f, 0.45f, 0.4f);
            foreach (var t in group)
            {
                Color c;
                if (t.water && !t.lake) c = Color.Lerp(TerrainColor(t), VoidColor, 0.15f);
                else if (t.lake) c = TerrainColor(t);
                else c = Color.Lerp(leadColor, t.biome != null ? BiomeColor(t.biome) : leadColor, 0.35f);
                macro[MesoIndex(t.coord)] = WithAlpha(c, 1f);
            }
        }

        _microTex = Texture(_microSize, "World Micro");
        _reliefTex = Texture(_mesoSize, "World Relief");
        _reliefTex.SetPixels32(relief);
        _reliefTex.Apply(false, false);
        _microTex.SetPixels32(micro);
        _microTex.Apply(false, false);
        _mesoTex = Texture(_mesoSize, "World Meso");
        _mesoTex.SetPixels32(meso);
        _mesoTex.Apply(false, false);
        _macroTex = Texture(_mesoSize, "World Macro");
        _macroTex.SetPixels32(macro);
        _macroTex.Apply(false, false);
        _fog = new Color32[_mesoSize * _mesoSize];
        _fogTex = Texture(_mesoSize, "World Knowledge");
        _microFog = new byte[_microSize * _microSize];
        _microFogTex = new Texture2D(_microSize, _microSize, TextureFormat.R8, false, true) { name = "World Micro Knowledge", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _overlay = new Color32[_mesoSize * _mesoSize];
        _overlayTex = Texture(_mesoSize, "World Overlay");
        _overlayTex.SetPixels32(_overlay);
        _overlayTex.Apply(false, false);
        _state = new Color32[_mesoSize * _mesoSize];
        _stateTex = Texture(_mesoSize, "World State");
        _stateTex.SetPixels32(_state);
        _stateTex.Apply(false, false);
    }

    private static Color32 WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>A seat's own colour in the Territory lens: the Capital gold, every other seat a steady hue from its key.</summary>
    public static Color SeatColor(TerritorySeat seat)
    {
        if (seat == null) return Color.gray;
        if (seat.kind == SeatKind.Capital) return new Color(1f, 0.85f, 0.4f);
        float hue = (float)WorldNoise.Hash01(WorldNoise.Stream(0, seat.key), 0, 0);
        return Color.HSVToRGB(hue, 0.55f, 0.95f);
    }

    private Color TerrainColor(WorldTile t) => _graded != null && t.index < _graded.Length ? _graded[t.index] : RawColor(t);

    private Color RawColor(WorldTile t) => _settings.Terrain(t.terrain)?.color ?? Color.gray;

    private Color BiomeColor(string biome) => _settings.Biome(biome)?.color ?? Color.gray;

    // ===== CAMERA AND LEVEL =====

    public void SetCamera(float x, float y, float size, bool enabled)
    {
        if (Camera == null) return;
        Camera.transform.localPosition = new Vector3(x, y, -100f);
        Camera.orthographicSize = size;
        Camera.enabled = enabled;
    }

    /// <summary>The camera's view as a world rectangle (x, y, width, height).</summary>
    public Rect View
    {
        get
        {
            if (Camera == null) return default;
            float h = Camera.orthographicSize, w = h * Camera.aspect;
            var p = Camera.transform.localPosition;
            return new Rect(p.x - w, p.y - h, 2 * w, 2 * h);
        }
    }

    /// <summary>The world point under a screen position.</summary>
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        if (Camera == null) return Vector2.zero;
        var world = Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 100f));
        var local = Root.transform.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    public void SetLevel(float level, float worldPerPixel, float pulse)
    {
        _perPixel = worldPerPixel;
        if (_terrain == null) return;
        _terrain.SetFloat("_Level", level);
        _terrain.SetFloat("_WorldPerPixel", worldPerPixel);
        _terrain.SetFloat("_Pulse", pulse);
        _marks.SetFloat("_Level", level);
        _marks.SetFloat("_WorldPerPixel", worldPerPixel);
        _marks.SetFloat("_Pulse", pulse);
    }

    // ===== KNOWLEDGE, STATE AND OVERLAYS =====

    /// <summary>Knowledge written for the shaders: the fog 0, unknown wilderness (seen) 100, known (a scout passed) 200, explored (surveyed) 255.</summary>
    public static byte KnowledgeByte(WorldTile t) => t.explored ? (byte)255 : t.known ? (byte)200 : t.revealed ? (byte)100 : (byte)0;

    /// <summary>
    /// Knowledge of one micro hex (bit <paramref name="child"/> of its cell) for the micro reading: surveyed 255,
    /// walked on or beside 200, unwalked in a known cell 160 (glimpsed), else its cell's.
    /// </summary>
    public static byte MicroKnowledgeByte(WorldTile t, int child)
    {
        int bit = 1 << child;
        if (t.explored || (t.microSurveyMask & bit) != 0) return 255;
        if ((t.microKnownMask & bit) != 0) return 200;
        return t.known ? (byte)160 : KnowledgeByte(t);
    }

    /// <summary>Rewrite the fog (both scales) and the markers (after exploration or a new Age's features).</summary>
    public void RefreshKnowledge()
    {
        if (_map == null) return;
        foreach (var t in _map.Tiles)
        {
            byte k = KnowledgeByte(t);
            _fog[MesoIndex(t.coord)] = new Color32(k, k, k, 255);
            var center = HexHierarchy.ChildCenter(t.coord);
            for (int c = 0; c < MicroNavigation.PerCell; c++)
            {
                var hex = center + HexHierarchy.ChildOffsets[c];
                int mi = (hex.r + _microOffset) * _microSize + (hex.q + _microOffset);
                if (mi >= 0 && mi < _microFog.Length) _microFog[mi] = MicroKnowledgeByte(t, c);
            }
        }
        _fogTex.SetPixels32(_fog);
        _fogTex.Apply(false, false);
        _microFogTex.SetPixelData(_microFog, 0);
        _microFogTex.Apply(false, false);
        RefreshMarkers(null);
    }

    /// <summary>
    /// Mark micro hexes on the ground: the one under the cursor (at the micro reading) and a selected unit's
    /// destination (a pulsing hex at every reading).
    /// </summary>
    public void SetMicroHighlight(HexCoord? hovered, HexCoord? target)
    {
        if (_terrain == null) return;
        _terrain.SetVector("_HoverMicro", hovered.HasValue ? new Vector4(hovered.Value.q, hovered.Value.r, 1f, 0f) : Vector4.zero);
        _terrain.SetVector("_TargetMicro", target.HasValue ? new Vector4(target.Value.q, target.Value.r, 1f, 0f) : Vector4.zero);
    }

    public void SetState(HexCoord? selected, HexCoord? selectedMacro, IEnumerable<HexCoord> frontier, IEnumerable<HexCoord> expeditions)
    {
        if (_map == null) return;
        Array.Clear(_state, 0, _state.Length);
        foreach (var c in frontier ?? Enumerable.Empty<HexCoord>()) Mark(c, 1);
        foreach (var c in expeditions ?? Enumerable.Empty<HexCoord>()) Mark(c, 2);
        if (selected.HasValue) Mark(selected.Value, 0);
        if (selectedMacro.HasValue) foreach (var t in _map.CellsOfMacro(selectedMacro.Value)) Mark(t.coord, 3);
        _stateTex.SetPixels32(_state);
        _stateTex.Apply(false, false);
        RefreshMarkers(expeditions);
    }

    private void Mark(HexCoord c, int channel)
    {
        if (!_map.InBounds(c)) return;
        int i = MesoIndex(c);
        var v = _state[i];
        if (channel == 0) v.r = 255;
        else if (channel == 1) v.g = 255;
        else if (channel == 2) v.b = 255;
        else v.a = 255;
        _state[i] = v;
    }

    public WorldLens Lens => _lens;

    /// <summary>
    /// Paint a lens over the ground. <paramref name="potential"/> is the Settle field
    /// (<see cref="CityDevelopment.PotentialField"/>); <paramref name="foundable"/> marks cells where a Developing Town
    /// could be founded now (drawn brighter in the Settle lens).
    /// </summary>
    public void SetLens(WorldLens lens, float[] potential = null, ICollection<int> foundable = null)
    {
        _lens = lens;
        if (potential != null) _potential = potential;
        if (_map == null) return;
        Array.Clear(_overlay, 0, _overlay.Length);
        if (lens != WorldLens.Normal)
            foreach (var t in _map.Tiles) _overlay[MesoIndex(t.coord)] = LensColor(t, lens, foundable != null && foundable.Contains(t.index));
        _overlayTex.SetPixels32(_overlay);
        _overlayTex.Apply(false, false);
        _terrain.SetFloat("_OverlayAlpha", lens == WorldLens.Normal ? 0f : 1f);
    }

    /// <summary>Re-read the fields the lens shows (after an Age moved the magic or the civilization changed).</summary>
    public void RefreshLens(float[] potential = null, ICollection<int> foundable = null) => SetLens(_lens, potential, foundable);

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    private Color32 LensColor(WorldTile t, WorldLens lens, bool foundable)
    {
        bool sea = t.water && !t.lake;
        float v = WorldLenses.Value(lens, _map, t, _potential);
        switch (lens)
        {
            case WorldLens.Weather:
                var weather = CelestialWeatherSystemLogic.Instance?.WeatherAt(t.coord);
                if (weather == null) return Clear;
                float hue = (float)WorldNoise.Hash01(WorldNoise.Stream(0, weather.name), 0, 0);
                return WithAlpha(Color.HSVToRGB(hue, 0.48f, 0.8f), 0.72f);
            case WorldLens.Settle:
                if (t.water || t.impassable) return WithAlpha(new Color(0.08f, 0.08f, 0.1f), 0.75f);
                // Red poor, amber fair, green rich (City Development 0-100 over a 0-60 useful range).
                float s = Mathf.Clamp01(v / 0.6f);
                Color c = s < 0.5f ? Color.Lerp(new Color(0.55f, 0.12f, 0.1f), new Color(0.9f, 0.7f, 0.2f), s * 2f) : Color.Lerp(new Color(0.9f, 0.7f, 0.2f), new Color(0.3f, 0.85f, 0.35f), (s - 0.5f) * 2f);
                return foundable ? WithAlpha(Color.Lerp(c, Color.white, 0.3f), 0.95f) : WithAlpha(c * 0.8f, 0.8f);
            case WorldLens.Authority:
                return WithAlpha(t.impassable ? new Color(0.22f, 0.2f, 0.2f) : t.water ? new Color(0.15f, 0.3f, 0.5f) :
                    t.authorityId == WorldAuthority.Player ? Color.Lerp(new Color(0.2f, 0.45f, 0.3f), new Color(0.6f, 0.95f, 0.65f), t.administrativeAuthority) :
                    t.authorityId == WorldAuthority.Outpost ? OutpostColor :
                    t.authorityId == WorldAuthority.Wilderness ? new Color(0.4f, 0.4f, 0.36f) :
                    t.authorityId.StartsWith("enclave:", StringComparison.Ordinal) ? EnclaveColor : new Color(0.85f, 0.45f, 0.2f), 0.85f);
            case WorldLens.Biomes:
                return sea || t.biome == null ? Clear : WithAlpha(BiomeColor(t.biome), 0.85f);
            case WorldLens.Composition:
                if (t.region == WorldRegion.Slot && t.sector != null && SectorColors.TryGetValue(t.sector, out var sc))
                    return WithAlpha(t.handmadeTile != null ? Color.Lerp(sc, Color.white, 0.35f) : t.seam ? Color.Lerp(sc, Color.black, 0.2f) : sc, 0.9f);
                return WithAlpha(sea ? new Color(0.36f, 0.43f, 0.62f) : Color.black, 0.9f);
            case WorldLens.Terrain:
                return WithAlpha(sea ? Color.Lerp(new Color(0.04f, 0.1f, 0.25f), new Color(0.3f, 0.6f, 0.75f), t.elevation) :
                    Color.Lerp(new Color(0.15f, 0.4f, 0.2f), Color.white, t.elevation), 0.85f);
            case WorldLens.LandFertility:
                return sea ? Clear : WithAlpha(Color.Lerp(new Color(0.3f, 0.18f, 0.09f), new Color(0.35f, 0.85f, 0.25f), v), 0.85f);
            case WorldLens.Coherence:
                if (t.sacred) return WithAlpha(Color.white, 0.9f);
                if (t.dissonance > t.coherence * 0.6f && t.dissonance > 0.05f) return WithAlpha(Color.Lerp(new Color(0.25f, 0.05f, 0.08f), new Color(0.9f, 0.2f, 0.2f), Mathf.Clamp01(t.dissonance * 2f)), 0.85f);
                return WithAlpha(Color.Lerp(new Color(0.08f, 0.06f, 0.16f), new Color(0.85f, 0.7f, 1f), v), 0.85f);
            case WorldLens.MagicalFertility:
                return WithAlpha(Color.Lerp(new Color(0.02f, 0.18f, 0.18f), new Color(1f, 0.8f, 0.25f), v), 0.85f);
            case WorldLens.Trade:
                if (t.nexus != null) return WithAlpha(NexusColor, 0.95f);
                if (t.tradeNode) return WithAlpha(NodeColor, 0.95f);
                if (t.road) return WithAlpha(RoadColor, 0.9f);
                return WithAlpha(sea ? new Color(0.08f, 0.14f, 0.24f) : new Color(0.12f, 0.12f, 0.12f), 0.6f);
            case WorldLens.Resources:
                if (t.grandfield >= 0 && t.grandfield < _map.Grandfields.Count)
                {
                    var spec = _settings.Grandfield(_map.Grandfields[t.grandfield].spec);
                    Color gc = spec != null ? spec.color : NexusColor;
                    return WithAlpha(Color.Lerp(gc * 0.45f, gc, t.grandfieldDensity), 0.95f);
                }
                return WithAlpha(new Color(0.1f, 0.1f, 0.1f), 0.6f);
            case WorldLens.Beauty:
                if (t.water) return sea ? Clear : WithAlpha(new Color(0.3f, 0.5f, 0.7f), 0.6f);
                // Murky brown hideous, grey plain, rose-gold beautiful.
                var plain = new Color(0.52f, 0.52f, 0.5f);
                return WithAlpha(t.beauty < 0f ? Color.Lerp(plain, new Color(0.28f, 0.2f, 0.08f), -t.beauty) : Color.Lerp(plain, new Color(1f, 0.74f, 0.78f), t.beauty), 0.85f);
            case WorldLens.Territory:
                if (t.water || t.impassable) return WithAlpha(new Color(0.08f, 0.08f, 0.1f), sea ? 0.35f : 0.7f);
                if (WorldAuthority.IsPlayers(t.authorityId))
                {
                    var seat = _map.territory?.Seat(t.pullSeat);
                    Color held = seat == null ? new Color(0.3f, 0.6f, 0.4f) : SeatColor(seat);
                    if (t.authorityId == WorldAuthority.Outpost) held = Color.Lerp(held, OutpostColor, 0.5f);
                    return WithAlpha(held * (0.55f + 0.45f * Mathf.Clamp01(t.pull)), 0.88f);
                }
                if (t.authorityId != WorldAuthority.Wilderness) return WithAlpha(EnclaveColor * 0.7f, 0.8f);
                if (t.rivalPull > t.pull && t.rivalPull > 0.05f) return WithAlpha(Color.Lerp(new Color(0.3f, 0.12f, 0.1f), new Color(0.9f, 0.25f, 0.2f), Mathf.Clamp01(t.rivalPull)), 0.8f);
                if (foundable) return WithAlpha(new Color(1f, 0.95f, 0.75f), 0.95f);
                if (t.pull > 0.01f) return WithAlpha(Color.Lerp(new Color(0.35f, 0.25f, 0.1f), new Color(0.95f, 0.65f, 0.2f), Mathf.Clamp01(t.pull / 0.8f)), 0.8f);
                return WithAlpha(new Color(0.15f, 0.15f, 0.15f), 0.6f);
            case WorldLens.Danger:
                if (sea) return Clear;
                return t.danger <= 0.01f ? WithAlpha(new Color(0.2f, 0.24f, 0.22f), 0.5f) : WithAlpha(Color.Lerp(new Color(0.45f, 0.3f, 0.2f), ThreatColor, Mathf.Clamp01(t.danger * 1.6f)), 0.9f);
            default: return Clear;
        }
    }

    // ===== LINES =====

    public void SetLayers(bool rivers, bool leylines, List<Leyline> forecast, bool previous = false)
    {
        _showRivers = rivers;
        _showLeylines = leylines;
        _forecast = forecast;
        _showPrevious = previous;
        RefreshLines();
        RefreshMarkers(null);
    }

    /// <summary>A unit as the map draws it: where it stands now (between hexes while walking), its colour and mark.</summary>
    public struct UnitMark
    {
        public Vector2 position;
        public Color color;
        public float shape;
        public bool selected;
        /// <summary>It is camped (a tent beside it).</summary>
        public bool camping;
        /// <summary>It is starving or close to collapse (a pulsing red ring).</summary>
        public bool distress;
    }

    /// <summary>The path colour for a road ahead the unit can walk, and one it cannot finish fed.</summary>
    public static readonly Color PathColor = new Color(1f, 0.95f, 0.8f, 0.75f), HungryPathColor = new Color(1f, 0.55f, 0.35f, 0.8f);

    /// <summary>
    /// Draw the units (called every frame while the world is open: a handful of quads) and the selected unit's road
    /// ahead: a dark token under a bright mark, a white ring when selected, a tent beside a camp, a red ring when it is
    /// starving or failing, and the path it will walk.
    /// </summary>
    public void SetUnits(IList<UnitMark> units, IList<Vector2> path, float pulse, Color? pathColor = null)
    {
        if (_unitMesh == null) return;
        var b = new Builder();
        if (path != null && path.Count > 1)
        {
            var color = pathColor ?? PathColor;
            Polyline(b, path.ToList(), path.Select(_ => color).ToList(), path.Select(_ => 0.22f).ToList(), 2f, new Vector2(-1f, 3f), 1f);
        }
        if (units != null)
        {
            // Sized to sit inside its micro hex up close (a hex is 1.73 across), never smaller than a few pixels.
            foreach (var u in units)
            {
                var anchor = u.position;
                if (u.distress) QuadAt(b, anchor, 2f, new Color(DistressColor.r, DistressColor.g, DistressColor.b, 0.35f + 0.5f * pulse), 2.5f, 24f);
                QuadAt(b, anchor, 1f, new Color(0.02f, 0.02f, 0.03f, 0.9f), 1.8f, 17f);
                QuadAt(b, anchor, u.shape, u.color, 1.35f, 13f);
                if (u.selected) QuadAt(b, anchor, 2f, new Color(1f, 1f, 1f, 0.6f + 0.4f * pulse), 2.3f, 22f);
                if (u.camping)
                {
                    // A tent beside it, clear of the rings at every zoom.
                    var tent = anchor + new Vector2(0.78f, -0.62f) * Mathf.Max(1.4f, 19f * _perPixel);
                    QuadAt(b, tent, 1f, new Color(0.02f, 0.02f, 0.03f, 0.85f), 1f, 12f);
                    QuadAt(b, tent, 6f, CampColor, 0.8f, 9f);
                }
            }
        }
        b.Apply(_unitMesh);
    }

    private static void QuadAt(Builder b, Vector2 anchor, float shape, Color color, float worldSize, float minPixels)
    {
        int start = b.vertices.Count;
        var info = new Vector4(worldSize, minPixels, shape, 1f);
        var levels = new Vector2(-1f, 3f);
        b.Vertex(anchor, new Vector2(-1f, -1f), color, info, levels, new Vector2(-1f, -1f));
        b.Vertex(anchor, new Vector2(1f, -1f), color, info, levels, new Vector2(1f, -1f));
        b.Vertex(anchor, new Vector2(1f, 1f), color, info, levels, new Vector2(1f, 1f));
        b.Vertex(anchor, new Vector2(-1f, 1f), color, info, levels, new Vector2(-1f, 1f));
        b.triangles.Add(start);
        b.triangles.Add(start + 2);
        b.triangles.Add(start + 1);
        b.triangles.Add(start);
        b.triangles.Add(start + 3);
        b.triangles.Add(start + 2);
    }

    /// <summary>Redraw what the civilization builds (roads, settlements, enclaves, threats) after it changed.</summary>
    public void RefreshCivilization()
    {
        RefreshLines();
        RefreshMarkers(null);
    }

    private class Builder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<Vector4> size = new List<Vector4>();
        public readonly List<Vector4> range = new List<Vector4>();
        public readonly List<Vector2> local = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        public void Vertex(Vector2 anchor, Vector2 offset, Color color, Vector4 sizeInfo, Vector2 levels, Vector2 local2)
        {
            vertices.Add(new Vector3(anchor.x, anchor.y, 0f));
            normals.Add(new Vector3(offset.x, offset.y, 0f));
            colors.Add(color);
            size.Add(sizeInfo);
            var cell = HexHierarchy.CellAt(anchor.x, anchor.y, HexHierarchy.Meso);
            range.Add(new Vector4(levels.x, levels.y, cell.q, cell.r));
            local.Add(local2);
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, size);
            mesh.SetUVs(1, range);
            mesh.SetUVs(2, local);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f));
        }
    }

    public void RefreshLines()
    {
        if (_map == null) return;
        var b = new Builder();
        if (_showRivers)
        {
            float major = Math.Max(1f, _settings.majorRiverThreshold);
            foreach (var river in _map.Rivers)
            {
                // A gentle, fixed meander between cell centres (WorldGeometry: the same line units ford).
                var points = WorldGeometry.RiverPoints(_map, river).Select(p => new Vector2(p.x, p.y)).ToList();
                var colors = river.cells.Select(i => _map[i].silver ? SilverColor : RiverColor).ToList();
                var widths = river.cells.Select(i => 0.18f + 0.5f * Mathf.Sqrt(Mathf.Min(1.5f, _map[i].flow / major))).ToList();
                Polyline(b, points, colors, widths, river.major ? 1.8f : 1.2f, river.major ? new Vector2(-1f, 3f) : new Vector2(-1f, 1.55f), 0f);
            }
        }
        // Roads: the same polyline at every zoom (the atlas keeps only the thicker look of a major river's width).
        foreach (var route in _map.Routes)
        {
            var points = route.cells.Select(c => _map.Get(c)).Where(t => t != null).Select(t => new Vector2(t.x, t.y)).ToList();
            Polyline(b, points, points.Select(_ => RoadColor).ToList(), points.Select(_ => 0.32f).ToList(), 1.6f, new Vector2(-1f, 2.2f), 1f);
        }
        if (_showPrevious && _map.Magic != null)
            foreach (var line in _map.Magic.PreviousLeylines) Leyline(b, line, PreviousColor, 0f);
        if (_showLeylines && _map.Magic != null)
        {
            foreach (var line in _map.Magic.Leylines) Leyline(b, line, LeylineColor, 0f);
            if (_forecast != null) foreach (var line in _forecast) Leyline(b, line, ForecastColor, 1f);
        }
        b.Apply(_lineMesh);
    }

    private void Leyline(Builder b, Leyline line, Color color, float fogMode)
    {
        var points = line.points.Count > 1 ? line.points.Select(p => new Vector2(p.x, p.y)).ToList() : line.cells.Select(i => new Vector2(_map[i].x, _map[i].y)).ToList();
        Polyline(b, points, points.Select(_ => color).ToList(), points.Select(_ => 0.55f).ToList(), 2.2f, new Vector2(-1f, 3f), fogMode);
    }

    // A smoothed strip with mitred joints; widths in world units, never thinner than minPixels.
    private static void Polyline(Builder b, List<Vector2> points, List<Color> colors, List<float> widths, float minPixels, Vector2 levels, float fogMode)
    {
        if (points.Count < 2) return;
        Chaikin(ref points, ref colors, ref widths);
        Chaikin(ref points, ref colors, ref widths);
        int start = b.vertices.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 prev = points[Math.Max(0, i - 1)], next = points[Math.Min(points.Count - 1, i + 1)];
            Vector2 tangent = (next - prev).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float miter = 1f;
            if (i > 0 && i < points.Count - 1)
            {
                Vector2 seg = (points[i + 1] - points[i]).normalized;
                miter = 1f / Mathf.Max(0.4f, Vector2.Dot(normal, new Vector2(-seg.y, seg.x)));
            }
            var info = new Vector4(widths[i], minPixels, 0f, fogMode);
            b.Vertex(points[i], normal * miter, colors[i], info, levels, new Vector2(0f, 1f));
            b.Vertex(points[i], -normal * miter, colors[i], info, levels, new Vector2(0f, -1f));
        }
        for (int i = 0; i < points.Count - 1; i++)
        {
            int a = start + 2 * i;
            b.triangles.Add(a);
            b.triangles.Add(a + 2);
            b.triangles.Add(a + 1);
            b.triangles.Add(a + 1);
            b.triangles.Add(a + 2);
            b.triangles.Add(a + 3);
        }
    }

    private static void Chaikin(ref List<Vector2> points, ref List<Color> colors, ref List<float> widths)
    {
        if (points.Count < 3) return;
        var p = new List<Vector2> { points[0] };
        var c = new List<Color> { colors[0] };
        var w = new List<float> { widths[0] };
        for (int i = 0; i < points.Count - 1; i++)
        {
            p.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
            p.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
            c.Add(colors[i]);
            c.Add(colors[i + 1]);
            w.Add(Mathf.Lerp(widths[i], widths[i + 1], 0.25f));
            w.Add(Mathf.Lerp(widths[i], widths[i + 1], 0.75f));
        }
        p.Add(points[points.Count - 1]);
        c.Add(colors[colors.Count - 1]);
        w.Add(widths[widths.Count - 1]);
        points = p;
        colors = c;
        widths = w;
    }

    // ===== MARKERS =====

    private IEnumerable<HexCoord> _expeditions = Enumerable.Empty<HexCoord>();

    private void RefreshMarkers(IEnumerable<HexCoord> expeditions)
    {
        if (_map == null) return;
        if (expeditions != null) _expeditions = expeditions.ToList();
        var b = new Builder();
        var capital = _map.Get(_map.Capital);
        foreach (var t in _map.Tiles)
        {
            if (!t.HasFeature || t == capital) continue;
            var feature = _settings.Feature(t.feature);
            if (feature == null) continue;
            // A site shows once a scout knows its ground (landmarks from afar); a ring until it is investigated.
            if (!t.known && !feature.visibleFromAfar) continue;
            var color = feature.color;
            color.a = t.explored ? 1f : 0.75f;
            bool landmark = feature.visibleFromAfar || feature.tag == "landmark";
            Marker(b, t, t.explored ? 1f : 2f, color, 1.9f, 10f, landmark ? new Vector2(-1f, 3f) : new Vector2(-1f, 1.7f), 1f);
        }
        if (_map.Magic != null)
        {
            foreach (int site in _map.Magic.SacredSites) Marker(b, _map[site], 3f, SacredColor, 2.3f, 11f, new Vector2(-1f, 3f), 0f);
            if (_showLeylines)
            {
                foreach (var junction in _map.Magic.Junctions)
                    Marker(b, _map[junction.center], junction.IsBasin ? 4f : 2f, junction.IsBasin ? BasinColor : ConvergenceColor, junction.IsBasin ? 2.8f : 1.8f, junction.IsBasin ? 13f : 9f, new Vector2(-1f, 3f), 0f);
            }
        }
        // Improved hotspots: a copper mark that grows with each level of work.
        foreach (var t in _map.Tiles.Where(t => t.improvement > 0 && t.explored))
            Marker(b, t, t.improvement >= 3 ? 4f : 2f, new Color(0.95f, 0.6f, 0.3f, 1f), 1.2f + 0.35f * t.improvement, 6f + 2f * t.improvement, new Vector2(-1f, 1.8f), 1f);
        // Gifted geography and threats, once seen; Trade Nodes on the roads.
        bool trade = _lens == WorldLens.Trade;
        foreach (int site in _map.NexusSites)
        {
            var t = _map[site];
            if (t.revealed || trade) Marker(b, t, 3f, NexusColor, trade ? 2.2f : 1.5f, trade ? 11f : 7f, new Vector2(-1f, trade ? 3f : 1.8f), trade ? 1f : 0f);
        }
        foreach (var t in _map.Tiles.Where(t => t.tradeNode)) Marker(b, t, 1f, NodeColor, 1.1f, 6f, new Vector2(-1f, 1.8f), 1f);
        bool danger = _lens == WorldLens.Danger;
        foreach (var threat in _map.Threats)
        {
            var t = _map[threat.cell];
            if (t.revealed || danger) Marker(b, t, 2f, ThreatColor, 2.4f, 11f, new Vector2(-1f, 3f), danger ? 1f : 0f);
        }
        foreach (var e in _map.Enclaves)
        {
            var t = _map.Get(e.coord);
            if (t == null || !t.revealed) continue;
            Marker(b, t, 3f, e.suzerain ? Color.Lerp(EnclaveColor, Color.white, 0.4f) : EnclaveColor, 2.6f, 13f, new Vector2(-1f, 3f), 0f);
        }
        // Settlements: dots for towns, a double ring for Major Settlements, diamonds for Outposts, rings for Havens;
        // a violet halo marks a Resonance Anchor.
        foreach (var s in _map.Settlements)
        {
            var t = _map.Get(s.coord);
            if (t == null || s.kind == SettlementKind.Capital) continue;
            if (s.anchor) Marker(b, t, 2f, AnchorColor, 3.6f, 18f, new Vector2(-1f, 3f), 1f);
            switch (s.kind)
            {
                case SettlementKind.Major: Marker(b, t, 4f, MajorColor, 3.2f, 17f, new Vector2(-1f, 3f), 1f); break;
                case SettlementKind.Town: Marker(b, t, 1f, TownColor, 2.4f, 12f, new Vector2(-1f, 3f), 1f); break;
                case SettlementKind.Outpost: Marker(b, t, 3f, OutpostColor, 2.2f, 11f, new Vector2(-1f, 3f), 1f); break;
                case SettlementKind.Haven: Marker(b, t, 2f, HavenColor, 2.4f, 12f, new Vector2(-1f, 3f), 1f); break;
            }
        }
        foreach (var c in _expeditions)
        {
            var t = _map.Get(c);
            if (t != null) Marker(b, t, 2f, ExpeditionColor, 2.6f, 12f, new Vector2(-1f, 3f), 1f);
        }
        if (capital != null) Marker(b, capital, 5f, CapitalColor, 3.4f, 20f, new Vector2(-1f, 3f), 1f);
        b.Apply(_markerMesh);
    }

    private static void Marker(Builder b, WorldTile t, float shape, Color color, float worldSize, float minPixels, Vector2 levels, float fogMode)
    {
        int start = b.vertices.Count;
        var info = new Vector4(worldSize, minPixels, shape, fogMode);
        var anchor = new Vector2(t.x, t.y);
        b.Vertex(anchor, new Vector2(-1f, -1f), color, info, levels, new Vector2(-1f, -1f));
        b.Vertex(anchor, new Vector2(1f, -1f), color, info, levels, new Vector2(1f, -1f));
        b.Vertex(anchor, new Vector2(1f, 1f), color, info, levels, new Vector2(1f, 1f));
        b.Vertex(anchor, new Vector2(-1f, 1f), color, info, levels, new Vector2(-1f, 1f));
        b.triangles.Add(start);
        b.triangles.Add(start + 2);
        b.triangles.Add(start + 1);
        b.triangles.Add(start);
        b.triangles.Add(start + 3);
        b.triangles.Add(start + 2);
    }

    // ===== TEAR DOWN =====

    public void Destroy()
    {
        foreach (var o in new UnityEngine.Object[] { _microTex, _mesoTex, _macroTex, _fogTex, _microFogTex, _overlayTex, _stateTex, _reliefTex, _terrain, _marks, _lineMesh, _markerMesh, _unitMesh })
            if (o != null) UnityEngine.Object.Destroy(o);
        if (Root != null) UnityEngine.Object.Destroy(Root);
    }
}
