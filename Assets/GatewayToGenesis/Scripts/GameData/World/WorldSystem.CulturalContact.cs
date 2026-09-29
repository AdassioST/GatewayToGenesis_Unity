using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Local cultures on the map (T02): which settlements are in contact by road, what ground each stands on, the customs a
/// cultural party carries, and who founded or rejoined a settlement. Contact reads the roads actually built
/// (<see cref="WorldMap.Routes"/>), never distance or territory; it is derived once per change of the map's roads,
/// settlements, authority or threats, not per custom or frame. A road enables contact only: nothing here moves land.
/// A road is interrupted where a threat's source stands on it or another authority holds a cell of it
/// (<see cref="LocalCultureTuning.roadDangerLimit"/> can add danger); interruption stops future contact, and nothing
/// already learned is lost.
/// </summary>
public partial class WorldSystem
{
    private ContactSnapshot _contact;
    private WorldMap _contactMap;
    private int _contactVersion = -1, _contactThreats = -1, _contactRoutes = -1;
    private readonly Dictionary<int, (int version, string ground)> _grounds = new Dictionary<int, (int, string)>();

    /// <summary>Contact between your settlements now (cached until the roads, settlements, authority or threats change).</summary>
    public ContactSnapshot CulturalContact()
    {
        if (Map == null) return ContactSnapshot.Empty;
        if (_contact != null && _contactMap == Map && _contactVersion == Map.CivilizationVersion && _contactThreats == Map.Threats.Count && _contactRoutes == Map.Routes.Count) return _contact;
        _contactMap = Map;
        _contactVersion = Map.CivilizationVersion;
        _contactThreats = Map.Threats.Count;
        _contactRoutes = Map.Routes.Count;
        _contact = CulturalContactMap.Build(Map, CultureSystem.LocalDefaults.roadDangerLimit, Place);
        return _contact;
    }

    /// <summary>Forget the cached contact and grounds (a load, a new world).</summary>
    public void ResetCulturalContact()
    {
        _contact = null;
        _contactMap = null;
        _grounds.Clear();
    }

    /// <summary>The words of a settlement's ground (<see cref="CulturalContactMap.Ground"/>), cached until the map changes.</summary>
    public string SettlementGround(Settlement s)
    {
        if (s == null || Map == null) return string.Empty;
        if (_grounds.TryGetValue(s.id, out var cached) && cached.version == Map.CivilizationVersion) return cached.ground;
        string ground = CulturalContactMap.Ground(Map, s);
        _grounds[s.id] = (Map.CivilizationVersion, ground);
        return ground;
    }

    // ===== CULTURAL PARTIES' REPERTOIRES =====

    /// <summary>The customs the party could take up where it stands (kept there), with why not (null: it can).</summary>
    public List<(string practice, string why)> CarryChoices(WorldUnit unit)
    {
        var list = new List<(string, string)>();
        var culture = CultureSystem.Instance;
        var s = SettlementAt(unit);
        if (culture == null || s == null || !WorldUnits.Can(SpecOf(unit), UnitAbility.Celebrate)) return list;
        foreach (var p in culture.LocalProfile(s.id).practices.Where(p => p.stage == LocalPracticeStage.Practiced))
            list.Add((p.practice, culture.WhyNotCarry(unit, s, p.practice)));
        return list;
    }

    /// <summary>Why the party cannot take up <paramref name="practice"/> where it stands, or null.</summary>
    public string WhyNotCarryPractice(WorldUnit unit, string practice)
    {
        if (unit == null) return "No party selected.";
        if (!WorldUnits.Can(SpecOf(unit), UnitAbility.Celebrate)) return "Only a cultural party carries customs.";
        if (unit.Moving) return "Stop the party first.";
        var culture = CultureSystem.Instance;
        if (culture == null || !CultureSystem.IsFounded) return "There is no culture to carry.";
        return culture.WhyNotCarry(unit, SettlementAt(unit), practice);
    }

    /// <summary>The party takes up a custom kept where it stands, to perform wherever it holds its next festivals.</summary>
    public bool CarryPractice(WorldUnit unit, string practice)
    {
        string why = WhyNotCarryPractice(unit, practice);
        if (why != null) { Say(why); return false; }
        var s = SettlementAt(unit);
        CultureSystem.Instance.Carry(unit, s, practice);
        Say($"{unit.name} takes up {LocalPracticeCatalog.NameOf(practice)} as {s.name} keeps it.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>The party sets a custom down (it no longer performs it).</summary>
    public bool DropPractice(WorldUnit unit, string practice)
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !culture.Drop(unit, practice)) return false;
        Say($"{unit.name} sets down {LocalPracticeCatalog.NameOf(practice)}.");
        Changed?.Invoke();
        return true;
    }

    // ===== FOUNDERS, VISITS AND RETURNING SETTLERS (hooks called where these actually happen) =====

    // An expedition took settlers on in one of your settlements.
    private void CulturalSettlersTaken(WorldUnit unit, Settlement from) => CultureSystem.Instance?.NoteSettlers(unit, from);

    // The settlers founded a settlement: they remember the customs of where they were taken on.
    private void CulturalFounding(WorldUnit unit, Settlement s)
    {
        var culture = CultureSystem.Instance;
        if (culture == null || s == null) return;
        var origin = culture.TakeSettlersOrigin(unit);
        var from = origin != null ? WorldCivilization.Get(Map, origin.settlement) : null;
        culture.LocalFounding(s, from, from != null ? $"the settlers of {unit.name}" : null, $"founding:{s.id}");
    }

    // A hamlet raised on a hub's outskirts: its first people are the hub's.
    private void CulturalTributaryFounding(Settlement s, Settlement hub) =>
        CultureSystem.Instance?.LocalFounding(s, hub, $"the people of {hub?.name}", $"founding:{s?.id}");

    // Settlers who never founded anything rejoin your citizens where the expedition disbands: their origin, when known.
    private static string SettlersReturnOrigin(WorldUnit unit)
    {
        var origin = CultureSystem.Instance?.TakeSettlersOrigin(unit);
        return origin != null ? "settlement:" + origin.settlement : null;
    }

    // A festival is over: the host joins in its customs and sees those the party carried (once per festival).
    private string CompleteCulturalVisit(WorldUnit unit, Settlement s, IList<string> members) =>
        CultureSystem.Instance?.CompleteLocalVisit(s, unit, members);
}

/// <summary>Contact and ground read from a world map, with no scene (tested in <c>LocalCultureTests</c>).</summary>
public static class CulturalContactMap
{
    /// <summary>The contact between the map's settlements along its roads, each road open or interrupted.</summary>
    public static ContactSnapshot Build(WorldMap map, float dangerLimit = 1f, System.Func<WorldTile, string> place = null)
    {
        if (map == null) return ContactSnapshot.Empty;
        var threatCells = new HashSet<int>(map.Threats.Select(t => t.cell));
        var routes = map.Routes.Where(r => r != null).Select(r => new ContactRoute(r.id, r.from, r.to, Interruption(map, r, threatCells, dangerLimit, place)));
        return ContactSnapshot.Build(map.Settlements.Where(s => s != null).Select(s => (s.id, s.name)), routes);
    }

    /// <summary>Why a road cannot carry contact now, or null: a threat's source on it, another authority holding it, or danger.</summary>
    public static string Interruption(WorldMap map, TradeRoute route, ICollection<int> threatCells, float dangerLimit, System.Func<WorldTile, string> place = null)
    {
        foreach (var c in route.cells)
        {
            var t = map.Get(c);
            if (t == null) continue;
            string at = place != null ? place(t) : t.coord.ToString();
            if (threatCells.Contains(t.index)) return $"a threat at {at}";
            if (t.authorityId != WorldAuthority.Wilderness && !WorldAuthority.IsPlayers(t.authorityId)) return $"{at} is held by another authority";
            if (dangerLimit < 1f && t.danger >= dangerLimit) return $"danger at {at}";
        }
        return null;
    }

    /// <summary>
    /// The words of a settlement's ground (lower case): its cell's and neighbours' terrain, biome and landform, "river",
    /// "sacred", and the resource sites within two cells. A custom begins where one of its ground words appears.
    /// </summary>
    public static string Ground(WorldMap map, Settlement s)
    {
        var t = s != null && map != null ? map.Get(s.coord) : null;
        if (t == null) return string.Empty;
        var words = new StringBuilder();
        foreach (var coord in HexCoord.Spiral(s.coord, 2))
        {
            var c = map.Get(coord);
            if (c == null) continue;
            if (HexCoord.Distance(coord, s.coord) <= 1)
            {
                words.Append(' ').Append(c.terrain).Append(' ').Append(c.macroBiome).Append(' ').Append(c.landform);
                if (c.river) words.Append(" river");
                if (c.sacred) words.Append(" sacred");
            }
            if (c.resourceSite >= 0 && c.resourceSite < map.ResourceSites.Count)
            {
                var site = map.ResourceSites[c.resourceSite];
                if (site != null) words.Append(' ').Append(site.spec).Append(' ').Append(site.name);
            }
        }
        return words.ToString().ToLowerInvariant();
    }
}
