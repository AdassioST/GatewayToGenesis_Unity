using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>The crowd's look and pace, on <see cref="GlobalCharacterManager"/> (every value a proposal).</summary>
[Serializable]
public class CrowdSettings
{
    [Header("How many are shown")]
    [Tooltip("Villagers walking close: one per person up to this many.")]
    public int nearCap = 40;
    [Tooltip("Figures in the distance at most (the people beyond the near villagers, grown by doublings).")]
    public int farCap = 160;
    [Tooltip("People at which the distance is full.")]
    public int farFullAt = 300000;
    [Tooltip("The first extra people each add about one figure; past this many, figures come by doublings.")]
    public float farUnit = 20f;
    [Tooltip("Figures that may arrive or leave each second while the crowd catches up with the people.")]
    public float changesPerSecond = 6f;

    [Header("Walking")]
    public float walkSpeed = 0.32f;
    [Tooltip("Pause between walks, seconds (min, max).")]
    public Vector2 idleSeconds = new Vector2(1.5f, 7f);
    [Tooltip("How far a walk goes at most (half as far up and down: the valley is seen at a slant).")]
    public float wanderRadius = 2.2f;
    [Tooltip("Seconds to fade in on arriving and out on leaving.")]
    public float fadeSeconds = 0.9f;
    public float bobHeight = 0.018f;
    public float stepsPerSecond = 7f;

    [Header("Depth")]
    [Tooltip("Share of the area, from its far (upper) edge, where the distant crowd walks.")]
    [Range(0.1f, 1f)] public float farBand = 0.4f;
    [Tooltip("Scale at the far edge relative to the near edge (the valley's perspective).")]
    [Range(0.3f, 1f)] public float depthScale = 0.8f;
    [Tooltip("A distant figure's size relative to a near villager.")]
    [Range(0.2f, 1f)] public float farScale = 0.58f;
    public float farSpeed = 0.35f;
    [Tooltip("The air the distant crowd is seen through.")]
    public Color haze = new Color(0.42f, 0.29f, 0.24f, 1f);
    [Range(0f, 1f)] public float farHaze = 0.55f;
    [Range(0f, 1f)] public float farAlpha = 0.9f;

    [Header("Vagrants")]
    public Color vagrantTint = new Color(0.72f, 0.68f, 0.64f, 1f);
    [Tooltip("A vagrant walks this much slower and rests this much longer.")]
    [Range(0.2f, 1f)] public float vagrantPace = 0.6f;
}

/// <summary>
/// The people walking the settlement, derived from <see cref="PopGrowthLogic"/> every frame so it never drifts from the
/// count. A small people is shown exactly, one villager per person; past <see cref="CrowdSettings.nearCap"/> the rest
/// become the crowd in the distance, smaller hazy figures in the far part of the area whose number grows by doublings
/// (<see cref="CrowdRules"/>). Vagrants walk among them in their share, greyer and slower. Arrivals fade in and the dead
/// fade out; figures are pooled and moved by one loop, and their depth (z from y) sorts them.
/// </summary>
public class GlobalCharacterManager : SingletonBehaviour<GlobalCharacterManager>
{
    private const LogChannel Log = LogChannel.Population;
    private const string DefaultPopulationChild = "Population";
    private const string VillagerUnit = "Villager";
    private const int MaxSpawnAttempts = 30;
    private const float DepthPerUnit = 0.01f;

    [Tooltip("Spawn area. Its PolygonCollider2D is the region characters walk in.")]
    [SerializeField] private Transform characterParent;
    [Tooltip("Parent of spawned characters. Defaults to the spawn area's 'Population' child, then the spawn area itself.")]
    [SerializeField] private Transform populationParent;

    [SerializeField] private List<GameUnit> characterUnits; // GameUnits available for characters
    [SerializeField] private GameObject characterPrefab; // A generic prefab for any character
    [SerializeField] private CrowdSettings crowd = new CrowdSettings();

    // One figure on screen: a pooled villager and where it is going.
    private sealed class Walker
    {
        public GameCharacterLogic character;
        public Transform transform;
        public SpriteRenderer renderer;
        public bool far, vagrant, leaving;
        public Vector2 position, goal;
        public float wait, alpha, phase, pace;
        public Color tint;
    }

    private readonly List<GameCharacterLogic> activeCharacters = new List<GameCharacterLogic>();
    private readonly List<Walker> _walkers = new List<Walker>();
    private readonly Stack<Walker> _pool = new Stack<Walker>();
    private PolygonCollider2D spawnArea;
    private Bounds _bounds;
    private float _changeBudget;
    private bool _synced;

    public IReadOnlyList<GameCharacterLogic> ActiveCharacters => activeCharacters;

    public int Count => activeCharacters.Count;
    public int AllocatedCount => _walkers.Count + _pool.Count;
    public int FigureLimit => Mathf.Clamp(crowd.nearCap, 0, 200) + Mathf.Clamp(crowd.farCap, 0, 400);

    /// <summary>Figures on screen now: (villagers close, figures in the distance), leaving ones aside.</summary>
    public (int near, int far) Shown
    {
        get
        {
            int near = 0, far = 0;
            foreach (var w in _walkers) if (!w.leaving) { if (w.far) far++; else near++; }
            return (near, far);
        }
    }

    protected override void OnSingletonAwake()
    {
        if (characterParent == null)
        {
            GameLog.Warning("No spawn area (characterParent) assigned; no characters will appear.", Log);
            return;
        }
        spawnArea = characterParent.GetComponent<PolygonCollider2D>();
        if (spawnArea == null) GameLog.Warning($"'{characterParent.name}' has no PolygonCollider2D; no characters will appear.", Log);
        else _bounds = spawnArea.bounds;
        if (populationParent == null) populationParent = characterParent.Find(DefaultPopulationChild);
        if (populationParent == null) populationParent = characterParent;
    }

    // ===== THE CROWD =====

    /// <summary>Show the people as they are now at once, with no arrivals or departures (after a load).</summary>
    public void Resync()
    {
        for (int i = _walkers.Count - 1; i >= 0; i--) Release(i);
        _synced = false;
    }

    private void Update()
    {
        if (spawnArea == null || characterPrefab == null) return;
        if (SaveMenu.BlocksGameplay || Time.timeScale <= 0f) return;
        if (!populationParent.gameObject.activeInHierarchy) return;
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive()) return;
        var people = PopGrowthLogic.Instance;
        int citizens = people != null ? Mathf.Max(0, people.population) : 0, vagrants = people != null ? Mathf.Max(0, people.vagrants) : 0;
        Reconcile(citizens, vagrants, Time.deltaTime);
        if (!populationParent.gameObject.activeInHierarchy) return;
        float dt = Time.deltaTime, now = Time.time;
        for (int i = _walkers.Count - 1; i >= 0; i--) Step(i, dt, now);
    }

    // Brings the figures on screen toward what the people call for: near citizens, near vagrants, distant figures.
    private void Reconcile(int citizens, int vagrants, float dt)
    {
        int people = (int)Math.Min(int.MaxValue, (long)citizens + vagrants);
        int nearCap = Mathf.Clamp(crowd.nearCap, 0, 200);
        int farCap = Mathf.Clamp(crowd.farCap, 0, 400);
        int near = CrowdRules.Near(people, nearCap);
        int nearVagrants = CrowdRules.Share(near, vagrants, people);
        int far = CrowdRules.Far(people, nearCap, farCap, crowd.farUnit, crowd.farFullAt);
        int farVagrants = CrowdRules.Share(far, vagrants, people);

        // The first time (a new game or a load) the crowd is simply there; afterwards it changes a few figures a second.
        int budget;
        if (!_synced) { budget = int.MaxValue; _synced = true; }
        else
        {
            _changeBudget = Mathf.Min(_changeBudget + crowd.changesPerSecond * dt, Mathf.Max(1f, crowd.changesPerSecond));
            budget = (int)_changeBudget;
        }
        bool instant = budget == int.MaxValue;
        int used = 0;
        used += Match(false, false, near - nearVagrants, budget - used, instant);
        used += Match(false, true, nearVagrants, budget - used, instant);
        used += Match(true, false, far - farVagrants, budget - used, instant);
        used += Match(true, true, farVagrants, budget - used, instant);
        if (!instant) _changeBudget -= used;
    }

    // Adds or sends away figures of one kind until there are `wanted`, within the budget. Returns the changes made.
    private int Match(bool far, bool vagrant, int wanted, int budget, bool instant)
    {
        if (budget <= 0) return 0;
        int have = 0;
        foreach (var w in _walkers) if (w.far == far && w.vagrant == vagrant && !w.leaving) have++;
        int changes = 0;
        for (; have < wanted && changes < budget; have++, changes++) if (!Arrive(far, vagrant, instant)) break;
        for (; have > wanted && changes < budget; have--, changes++) SendAway(far, vagrant);
        return changes;
    }

    private bool Arrive(bool far, bool vagrant, bool instant)
    {
        // Departing figures count toward the limit too. Repeated plagues/loads never inflate the pool.
        if (_pool.Count == 0 && _walkers.Count >= FigureLimit) return false;
        var w = _pool.Count > 0 ? _pool.Pop() : Create();
        if (w == null) return false;
        if (w.character == null) return false;
        w.far = far;
        w.vagrant = vagrant;
        w.leaving = false;
        w.position = RandomPoint(far);
        w.goal = w.position;
        w.wait = Random.Range(0f, crowd.idleSeconds.y);
        w.alpha = instant ? 1f : 0f;
        w.phase = Random.value * 10f;
        w.pace = (far ? crowd.farSpeed : 1f) * (vagrant ? crowd.vagrantPace : 1f) * Random.Range(0.8f, 1.2f);
        w.tint = far ? Color.Lerp(vagrant ? crowd.vagrantTint : Color.white, crowd.haze, crowd.farHaze) : vagrant ? crowd.vagrantTint : Color.white;
        w.character.AssignRandomSprite();
        w.transform.gameObject.SetActive(true);
        activeCharacters.Add(w.character);
        _walkers.Add(w);
        Place(w);
        return true;
    }

    // The one to leave is picked at random among its kind; it fades where it stands.
    private void SendAway(bool far, bool vagrant)
    {
        int count = 0;
        foreach (var w in _walkers) if (w.far == far && w.vagrant == vagrant && !w.leaving) count++;
        int pick = Random.Range(0, count);
        foreach (var w in _walkers)
        {
            if (w.far != far || w.vagrant != vagrant || w.leaving) continue;
            if (pick-- > 0) continue;
            w.leaving = true;
            activeCharacters.Remove(w.character);
            return;
        }
    }

    private Walker Create()
    {
        var unit = FindUnit(VillagerUnit);
        var go = Instantiate(characterPrefab, populationParent);
        if (!go.TryGetComponent(out GameCharacterLogic character) || !go.TryGetComponent(out SpriteRenderer renderer))
        {
            GameLog.Error($"Character prefab '{characterPrefab.name}' has no GameCharacterLogic component.", Log);
            Destroy(go);
            return null;
        }
        character.Initialize(unit);
        return new Walker { character = character, transform = go.transform, renderer = renderer };
    }

    private void Release(int index)
    {
        var w = _walkers[index];
        _walkers.RemoveAt(index);
        activeCharacters.Remove(w.character);
        if (w.character == null) return;
        w.transform.gameObject.SetActive(false);
        _pool.Push(w);
    }

    // ===== WALKING =====

    private void Step(int index, float dt, float now)
    {
        var w = _walkers[index];
        if (w.character == null) { activeCharacters.Remove(w.character); _walkers.RemoveAt(index); return; }
        float fade = crowd.fadeSeconds > 0f ? dt / crowd.fadeSeconds : 1f;
        if (w.leaving)
        {
            w.alpha -= fade;
            if (w.alpha <= 0f) { Release(index); return; }
        }
        else if (w.alpha < 1f) w.alpha = Mathf.Min(1f, w.alpha + fade);

        bool walking = false;
        if (w.wait > 0f) w.wait -= dt;
        else
        {
            Vector2 to = w.goal - w.position;
            float step = crowd.walkSpeed * w.pace * DepthFactor(w.position.y) * dt;
            if (to.sqrMagnitude <= step * step)
            {
                w.position = w.goal;
                w.wait = Random.Range(crowd.idleSeconds.x, crowd.idleSeconds.y) / (w.vagrant ? crowd.vagrantPace : 1f);
                w.goal = NextGoal(w);
            }
            else
            {
                w.position += to.normalized * step;
                w.renderer.flipX = to.x < 0f;
                walking = true;
            }
        }
        Place(w, walking ? Mathf.Abs(Mathf.Sin((now + w.phase) * crowd.stepsPerSecond * Mathf.PI)) * crowd.bobHeight : 0f);
    }

    // Position (z from y, so lower figures stand in front), size by depth, and colour with its fade.
    private void Place(Walker w, float bob = 0f)
    {
        float depth = DepthFactor(w.position.y);
        w.transform.position = new Vector3(w.position.x, w.position.y + bob * depth, w.position.y * DepthPerUnit);
        float scale = depth * (w.far ? crowd.farScale : 1f);
        w.transform.localScale = new Vector3(scale * PrefabScale, scale * PrefabScale, 1f);
        var c = w.tint;
        c.a = w.alpha * (w.far ? crowd.farAlpha : 1f);
        w.renderer.color = c;
    }

    private float PrefabScale => characterPrefab != null ? characterPrefab.transform.localScale.x : 1f;

    // 1 at the near (lower) edge of the area, depthScale at its far (upper) edge.
    private float DepthFactor(float y)
    {
        float t = _bounds.size.y > 0f ? Mathf.InverseLerp(_bounds.min.y, _bounds.max.y, y) : 0f;
        return Mathf.Lerp(1f, crowd.depthScale, t);
    }

    // A nearby point in the walker's part of the area (half as far up and down), or anywhere there after a few misses.
    private Vector2 NextGoal(Walker w)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var offset = Random.insideUnitCircle * crowd.wanderRadius * (w.far ? crowd.farScale : 1f);
            var point = w.position + new Vector2(offset.x, offset.y * 0.5f);
            if (InBand(point.y, w.far) && WalkableSegment(w.position, point)) return point;
        }
        // A concave valley can have valid endpoints separated by water. Rest instead of walking across it.
        return w.position;
    }

    private bool WalkableSegment(Vector2 from, Vector2 to)
    {
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / 0.05f), 1, 128);
        for (int i = 1; i <= steps; i++)
            if (!spawnArea.OverlapPoint(Vector2.Lerp(from, to, i / (float)steps))) return false;
        return true;
    }

    private bool InBand(float y, bool far) => !far || y >= _bounds.max.y - _bounds.size.y * crowd.farBand;

    // Rejection sampling inside the polygon (its far band for the distant crowd). A disabled or degenerate collider
    // never reports a hit, so the attempts are capped and the last guess is pulled onto the polygon instead.
    private Vector2 RandomPoint(bool far)
    {
        float minY = far ? _bounds.max.y - _bounds.size.y * crowd.farBand : _bounds.min.y;
        Vector2 point = _bounds.center;
        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            point = new Vector2(Random.Range(_bounds.min.x, _bounds.max.x), Random.Range(minY, _bounds.max.y));
            if (spawnArea.OverlapPoint(point)) return point;
        }
        return spawnArea.ClosestPoint(point);
    }

    // Called by a character as it is destroyed, whoever destroyed it.
    internal void Unregister(GameCharacterLogic character) => activeCharacters.Remove(character);

    private GameUnit FindUnit(string unitName)
    {
        if (characterUnits == null) return null;
        foreach (var unit in characterUnits)
        {
            if (unit != null && string.Equals(unit.name, unitName, StringComparison.OrdinalIgnoreCase)) return unit;
        }
        return null;
    }
}
