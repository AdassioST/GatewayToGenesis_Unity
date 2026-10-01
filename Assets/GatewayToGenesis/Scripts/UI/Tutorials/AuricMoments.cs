using System;

/// <summary>
/// The moments the Auric Aria answers at once, whatever the player is doing (<see cref="AuricAria"/>): what is completed
/// is announced, what is felt gets a line of her own. She is the mother of all this, fond, curious and a little guilty,
/// and her lines should sound like it. Watched by the tutorial host (<see cref="Tutorials"/>) every few frames while a
/// game is played. Nothing here is saved: a moment is answered only when it is seen happening in play (a loaded save's
/// past is not news). The founders' homecoming lives with its lesson (<see cref="FoundingLesson.Watch"/>). Every word is
/// a proposal.
/// </summary>
internal sealed class AuricMoments : IDisposable
{
    private static readonly string[] Fallen =
    {
        "(a breath catches) ...Hush, {0}. Even cities fall asleep.",
        "(quietly) I heard every hearth in {0} go out.",
    };

    private int _deathsBefore = -1, _fallenSaid;
    private WorldSystem _world;
    private LegendProgress _legends;

    public AuricMoments() => GameTechnologySlot.Researched += OnResearched;

    public void Dispose()
    {
        GameTechnologySlot.Researched -= OnResearched;
        Follow(null, null);
    }

    public void Watch()
    {
        Follow(WorldSystem.Instance, LegendProgress.Instance);
        FirstDeath();
    }

    // The world and the legends come and go with the scene: listen to the ones there now.
    private void Follow(WorldSystem world, LegendProgress legends)
    {
        if (world != _world)
        {
            if (_world != null) _world.SettlementFell -= OnSettlementFell;
            _world = world;
            if (_world != null) _world.SettlementFell += OnSettlementFell;
        }
        if (legends != _legends)
        {
            if (_legends != null) _legends.Lost -= OnLegendLost;
            _legends = legends;
            if (_legends != null) _legends.Lost += OnLegendLost;
        }
    }

    // The first of her children to die in this world (seen from the first look on, so a loaded save's dead are old grief).
    private void FirstDeath()
    {
        var pop = PopGrowthLogic.Instance;
        if (pop == null) return;
        int deaths = pop.trueDeaths;
        if (_deathsBefore == 0 && deaths > 0) AuricAria.Say("moment.first-death", "(sob) ... It's okay, sometimes things die...");
        _deathsBefore = deaths;
    }

    // Lookout Towers: the map opens (research only; a loaded save never raises it).
    private static void OnResearched(GameTechnologySlot slot)
    {
        var world = WorldSystem.Instance;
        if (world == null || world.Settings == null || slot?.gameUnit == null || slot.gameUnit.name != world.Settings.mapTechnology) return;
        AuricAria.Announce("moment.world-opens", "Beyond the walls, the world holds its breath", "The World Is Open", "Press M, and walk it with me.");
    }

    private void OnSettlementFell(Ruin ruin)
    {
        if (ruin == null) return;
        AuricAria.Say("moment.settlement-fell." + (_fallenSaid % Fallen.Length), string.Format(Fallen[_fallenSaid % Fallen.Length], ruin.name));
        _fallenSaid++;
    }

    private static void OnLegendLost(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        AuricAria.Say("moment.legend-lost", $"(softly) {name}'s song broke into Dissonance... I'll keep its last note.");
    }
}
