using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// The hints of a first playthrough, in the Auric Aria's voice (<see cref="Tutorials"/>): a whisper, a title and what to
// do, spoken at pauses until learnt, skipped for good when the player already did the thing. Mostly the world map,
// plus the Era Score sun. Every hint is learnt by a Done, a press on
// what glows (its control or ringed spot), any act (world-look), or by being heard out. Wording is a proposal.

/// <summary>Shared reads for the hints.</summary>
internal static class HintWorld
{
    public static WorldSystem World => WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance : null;

    public static bool At(WorldUnit unit, out Vector2 point)
    {
        point = default;
        var world = World;
        if (world == null || unit == null || unit.Missing) return false;
        var (x, y) = WorldUnits.Position(world.Map, world.Settings.generation, unit);
        point = new Vector2(x, y);
        return true;
    }

    public static bool At(HexCoord cell, out Vector2 point)
    {
        point = default;
        var tile = World?.Map.Get(cell);
        if (tile == null) return false;
        point = new Vector2(tile.x, tile.y);
        return true;
    }

    /// <summary>The selected unit when it is an expedition out on the map.</summary>
    public static WorldUnit SelectedParty
    {
        get
        {
            var unit = WorldView.SelectedUnit;
            return unit != null && !unit.Missing && World != null && World.IsExpedition(unit) ? unit : null;
        }
    }

    /// <summary>It stands outside your settlements.</summary>
    public static bool InTheField(WorldUnit unit) => unit != null && (World?.Map.Get(unit.coord)?.settlement ?? 0) < 0;
}

/// <summary>Pathfinder Training opened the map: how to get there.</summary>
public class WorldOpensHint : TutorialLesson
{
    public override string Id => "world-opens";
    public override TutorialPlace Place => TutorialPlace.Capital;
    public override float Seconds => 14f;
    public override bool Done() => WorldView.IsOpen;

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Beyond the walls, the world holds its breath", "The World Is Open", "Press M, or keep scrolling out, and walk it with me.");
        return HintWorld.World != null && HintWorld.World.MapUnlocked;
    }
}

/// <summary>The first look at the world: moving about it and going home.</summary>
public class WorldLookHint : TutorialLesson
{
    public override string Id => "world-look";
    public override TutorialPlace Place => TutorialPlace.World;
    public override float Seconds => 9f;
    public override bool AnsweredByAnyInput => true;

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("So much was lost. So much still sings.", "Behold the Land", "Drag to look around and scroll to zoom. Esc brings you home.");
        return true;
    }
}

/// <summary>No party is selected: its token leads it.</summary>
public class LeadPartyHint : TutorialLesson
{
    private WorldUnit _party;

    public override string Id => "lead-party";
    public override TutorialPlace Place => TutorialPlace.World;

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Someone out there awaits your word", "Your Expedition", "Click its token to lead it.");
        var world = HintWorld.World;
        _party = world != null && WorldView.SelectedUnit == null && WorldView.SelectedTile == null ? world.ExpeditionUnits.FirstOrDefault(u => !u.Missing) : null;
        return _party != null;
    }

    public override bool WorldTarget(out Vector2 point) => HintWorld.At(_party, out point);
}

/// <summary>A party is selected and idle: a right click sends it.</summary>
public class SendPartyHint : TutorialLesson
{
    public override string Id => "send-party";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => HintWorld.World != null && HintWorld.World.ExpeditionUnits.Any(u => u.Moving && !u.autoExplore && !u.returning && !u.surveying);

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Where shall they wander?", "Send Them Onward", "Right-click the land to send them there. Zoomed in, they walk to the very hex.");
        return WorldUnits.AwaitsOrders(HintWorld.SelectedParty);
    }
}

/// <summary>A party beyond the stores runs short of rations.</summary>
public class RationsHint : TutorialLesson
{
    private WorldUnit _party;

    public override string Id => "rations";
    public override TutorialPlace Place => TutorialPlace.World;
    public override float Seconds => 14f;
    public override bool Done() => HintWorld.World != null && HintWorld.World.ExpeditionUnits.Any(u => u.returning || (u.Camping && !u.resting));

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Their packs grow light, and the road grows long", "Rations Run Short", "Make camp on fertile ground, or bring them home for rations.");
        var world = HintWorld.World;
        _party = world?.ExpeditionUnits.FirstOrDefault(u =>
        {
            var spec = world.SpecOf(u);
            return !u.Missing && !u.Camping && !u.returning && HintWorld.InTheField(u) && spec != null && spec.supplyCapacity > 0f && u.supplies < spec.supplyCapacity * 0.35f;
        });
        return _party != null;
    }

    public override Graphic Target() => _party != null && WorldView.SelectedUnit == _party ? WorldView.OrderButton("Make camp") : null;

    public override bool WorldTarget(out Vector2 point)
    {
        point = default;
        return WorldView.SelectedUnit != _party && HintWorld.At(_party, out point);
    }
}

/// <summary>The selected party stands in a cell with hexes left to survey.</summary>
public class SurveyHint : TutorialLesson
{
    public override string Id => "survey";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => HintWorld.World != null && HintWorld.World.ExpeditionUnits.Any(u => u.surveying);

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Every hex hums a note of its own", "Survey the Land", "Walk every hex of this cell and learn what grows and dwells there.");
        var party = HintWorld.SelectedParty;
        return party != null && !party.surveying && !party.Moving && HintWorld.InTheField(party) && Target() != null;
    }

    public override Graphic Target() => WorldView.OrderButton("Survey here");
}

/// <summary>The selected party stands at a site it can harvest or hunt.</summary>
public class HarvestHint : TutorialLesson
{
    public override string Id => "harvest";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => HintWorld.World != null && HintWorld.World.ExpeditionUnits.Any(u => u.task == UnitTask.Harvest);

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("A gift, if you will carry it", "Harvest", "Gather what is found here, then deliver it at a settlement.");
        return HintWorld.SelectedParty != null && Target() != null;
    }

    public override Graphic Target() => WorldView.OrderButton("Harvest") ?? WorldView.OrderButton("Hunt");
}

/// <summary>Wilderness bordering your land can be claimed.</summary>
public class ClaimHint : TutorialLesson
{
    public override string Id => "claim";
    public override TutorialPlace Place => TutorialPlace.World;
    public override float Seconds => 14f;
    public override bool Done() => HintWorld.World != null && HintWorld.World.Map.Claims.Count + HintWorld.World.Map.Claiming.Count > 0;

    public override bool Speak(out TutorialCard card)
    {
        var world = HintWorld.World;
        if (Target() != null)
        {
            card = new TutorialCard("The wild ground is listening", "Claim the Land", "Land bordering yours can be claimed, paid for from your stored food.");
            return true;
        }
        card = new TutorialCard("The wild ground is listening", "Claim the Land", "Select the wilderness beside your borders to learn what it asks of you.");
        return world != null && WorldView.SelectedUnit == null && WorldView.SelectedTile == null && world.ClaimableCount() > 0;
    }

    public override Graphic Target() => WorldView.OrderButton("Claim");
}

/// <summary>A free expedition slot and a free legend: the capital sends out another.</summary>
public class FormExpeditionHint : TutorialLesson
{
    public override string Id => "form-expedition";
    public override TutorialPlace Place => TutorialPlace.World;
    public override float Seconds => 14f;
    public override bool Done() => HintWorld.World != null && HintWorld.World.ExpeditionUnits.Count() >= 2;

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Another heart is ready to wander", "A New Expedition", "Select the capital to choose who leads it.");
        var world = HintWorld.World;
        return world != null && world.ExpeditionUnits.Any() && world.FreeExpeditionSlots > 0 && world.Candidates().Count > 0;
    }

    public override Graphic Target() => (WorldView.SelectedTile?.settlement ?? -1) >= 0 ? WorldView.OrderButton("Form ") : null;

    public override bool WorldTarget(out Vector2 point)
    {
        point = default;
        var world = HintWorld.World;
        return world != null && (WorldView.SelectedTile?.settlement ?? -1) < 0 && HintWorld.At(world.Map.Capital, out point);
    }
}

/// <summary>Known ruins not yet investigated.</summary>
public class RuinsHint : TutorialLesson
{
    private Ruin _ruin;

    public override string Id => "ruins";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => HintWorld.World != null && HintWorld.World.Map.Ruins.Any(r => r.investigated);

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("The fallen left their echoes behind", "Ruins", "Send an expedition to investigate what they kept.");
        var world = HintWorld.World;
        _ruin = world?.Map.Ruins.FirstOrDefault(r => !r.investigated && (world.Map.Get(r.coord)?.known ?? false));
        return _ruin != null && world.ExpeditionUnits.Any();
    }

    public override bool WorldTarget(out Vector2 point)
    {
        point = default;
        return _ruin != null && HintWorld.At(_ruin.coord, out point);
    }
}

/// <summary>After a while on the map: the lenses.</summary>
public class LensesHint : TutorialLesson
{
    private const float After = 90f;
    private float _inWorld, _lastAt = -1f;

    public override string Id => "lenses";
    public override TutorialPlace Place => TutorialPlace.World;

    public override bool Done()
    {
        // Counts the time spent on the map (Done is asked every few frames while the hint is unseen).
        float now = Time.unscaledTime;
        if (_lastAt >= 0f && WorldView.Current == WorldView.Mode.World) _inWorld += Mathf.Min(1f, now - _lastAt);
        _lastAt = now;
        return WorldView.OpenPanel == "Map";
    }

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Listen to the land another way", "Map Lenses", "The Map button shows fertility, danger, authority and more.");
        return _inWorld >= After && Target() != null;
    }

    public override Graphic Target() => WorldView.DockButton("Map");
}

/// <summary>Two or more expeditions: the roster.</summary>
public class PartiesHint : TutorialLesson
{
    public override string Id => "parties";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => WorldView.OpenPanel == "Units";

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("I keep count of every wanderer", "Your Parties", "Every expedition in the field, listed. The period key finds the next idle one.");
        return HintWorld.World != null && HintWorld.World.ExpeditionUnits.Count() >= 2 && Target() != null;
    }

    public override Graphic Target() => WorldView.DockButton("Parties");
}

/// <summary>The realm has begun to grow: its administration.</summary>
public class RealmHint : TutorialLesson
{
    public override string Id => "realm";
    public override TutorialPlace Place => TutorialPlace.World;
    public override bool Done() => WorldView.OpenPanel == "Realm";

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("A realm is a song that must be held in tune", "Your Realm", "See how much land your administration can hold before it strains.");
        var world = HintWorld.World;
        return world != null && world.Map.Claims.Count + world.Map.Adopted.Count > 0 && Target() != null;
    }

    public override Graphic Target() => WorldView.DockButton("Realm");
}

/// <summary>The first Era Score: the sun and its Chronicle.</summary>
public class EraScoreHint : TutorialLesson
{
    private Graphic _sun;

    public override string Id => "era-score";
    public override TutorialPlace Place => TutorialPlace.Capital;
    public override bool Done() => EraTimelineWindow.IsOpen;

    public override bool Speak(out TutorialCard card)
    {
        card = new TutorialCard("Your deeds are being remembered", "Era Score", "Great deeds earn Era Score. Click the sun to read the Chronicle of them all.");
        return AgeProgression.Instance != null && AgeProgression.Instance.EraScore > 0 && Target() != null;
    }

    public override Graphic Target()
    {
        if (_sun != null) return _sun;
        var root = GameObject.Find("LowerHUD/EraScore");
        var icon = root != null ? root.transform.Find("Icon") : null;
        _sun = icon != null ? icon.GetComponent<Graphic>() : root != null ? root.GetComponentInChildren<Graphic>() : null;
        return _sun;
    }
}
