using UnityEngine;
using UnityEngine.UI;

/// <summary>Where a lesson can be shown.</summary>
public enum TutorialPlace { Capital, World, Anywhere }

/// <summary>
/// One mini tutorial (<see cref="Tutorials"/> shows them), spoken by the Auric Aria (<see cref="AuricWords"/>). She
/// teaches only when the player has been idle a few seconds (<see cref="Tutorials.IdleSeconds"/>) and falls silent the
/// moment they act; she comes back at the next pause while the lesson still applies. Two kinds:
/// - a <b>lesson</b> (<see cref="IsHint"/> false: the founding): an announcement in the middle of the screen, at every
///   pause for as long as the game state calls for it;
/// - a <b>hint</b> (the rest): one line of hers low on the screen, a subtitle, at pauses until learnt: once the player
///   does the thing (<see cref="Done"/>), presses its glowing control or ringed spot, or has heard it for
///   <see cref="Seconds"/> in all. Learnt hints are remembered across playthroughs; one at a time, with a short pause
///   after each is learnt.
/// What happens in the moment (a completion, a loss) is not taught but said at once: <see cref="Watch"/> may call
/// <see cref="AuricAria.Announce"/> or <see cref="AuricAria.Say"/>.
/// A lesson reads the game's own state to know when it applies. Add one by deriving from this and listing it in
/// <see cref="Tutorials.Lessons"/>.
/// </summary>
public abstract class TutorialLesson
{
    /// <summary>A stable name: the key under which a hint is remembered as learnt.</summary>
    public abstract string Id { get; }

    /// <summary>The words to teach now (their id names the utterance and its recorded voice), or false when there is nothing to say.</summary>
    public abstract bool Speak(out AuricWords words);

    /// <summary>Looked at every few frames during play, spoken or not: the place to notice a moment and say it at once.</summary>
    public virtual void Watch() { }

    /// <summary>The control to ring with gold while she speaks (its art is traced, so an odd shape glows as it is drawn), or null.</summary>
    public virtual Graphic Target() => null;

    /// <summary>A point of the world map to ring with gold (map units), while the map is open.</summary>
    public virtual bool WorldTarget(out Vector2 point)
    {
        point = default;
        return false;
    }

    /// <summary>A hint (true) or a lesson (false).</summary>
    public virtual bool IsHint => true;

    public virtual TutorialPlace Place => TutorialPlace.Anywhere;

    /// <summary>The player has done what the hint is about: it is remembered as learnt, spoken or not.</summary>
    public virtual bool Done() => false;

    /// <summary>A hint whose lesson is any action at all (look around, zoom): acting while she speaks it learns it.</summary>
    public virtual bool AnsweredByAnyInput => false;

    /// <summary>Real seconds a hint is heard, over all its pauses, before it counts as learnt.</summary>
    public virtual float Seconds => 10f;
}
