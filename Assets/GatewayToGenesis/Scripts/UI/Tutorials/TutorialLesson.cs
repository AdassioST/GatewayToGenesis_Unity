using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the Auric Aria says this moment, in three layers: a small <see cref="whisper"/> on top (her aside), the large
/// <see cref="title"/> (what matters now) and a small <see cref="body"/> under it (what to do). The title names the
/// utterance: a new title is a new utterance. A lesson's utterance can be spoken only for so long
/// (<see cref="retireAfter"/>, real seconds heard in all; 0 = as long as it applies).
/// </summary>
public struct TutorialCard
{
    public string whisper, title, body;
    public float retireAfter;

    public TutorialCard(string whisper, string title, string body, float retireAfter = 0f)
    {
        this.whisper = whisper;
        this.title = title;
        this.body = body;
        this.retireAfter = retireAfter;
    }
}

/// <summary>Where a lesson can be shown.</summary>
public enum TutorialPlace { Capital, World, Anywhere }

/// <summary>
/// One mini tutorial (<see cref="Tutorials"/> shows them), spoken by the Auric Aria as floating golden words
/// (<see cref="AuricVoice"/>). She speaks only when the player has been idle a few seconds
/// (<see cref="Tutorials.IdleSeconds"/>) and falls silent the moment they act; she comes back at the next pause while
/// the lesson still applies. Two kinds:
/// - a <b>lesson</b> (<see cref="IsHint"/> false: the founding): larger words in the middle of the screen, spoken at
///   every pause for as long as the game state calls for it;
/// - a <b>hint</b> (the rest): smaller words under the Age banner (or above its spot on the world map), spoken at pauses
///   until learnt: once the player does the thing (<see cref="Done"/>), presses its glowing control or ringed spot, or
///   has heard it for <see cref="Seconds"/> in all. Learnt hints are remembered across playthroughs; one hint at a time,
///   with a short pause after each is learnt.
/// A lesson reads the game's own state to know when it applies. Add one by deriving from this and listing it in
/// <see cref="Tutorials.Lessons"/>.
/// </summary>
public abstract class TutorialLesson
{
    /// <summary>A stable name: the key under which a hint is remembered as learnt.</summary>
    public abstract string Id { get; }

    /// <summary>The words to speak now, or false when the lesson has nothing to say.</summary>
    public abstract bool Speak(out TutorialCard card);

    /// <summary>The control to ring with gold while she speaks (its art is traced, so an odd shape glows as it is drawn), or null.</summary>
    public virtual Graphic Target() => null;

    /// <summary>A point of the world map to ring with gold (map units), while the map is open.</summary>
    public virtual bool WorldTarget(out Vector2 point)
    {
        point = default;
        return false;
    }

    /// <summary>A hint (true) or a centred lesson (false).</summary>
    public virtual bool IsHint => true;

    public virtual TutorialPlace Place => TutorialPlace.Anywhere;

    /// <summary>The player has done what the hint is about: it is remembered as learnt, spoken or not.</summary>
    public virtual bool Done() => false;

    /// <summary>A hint whose lesson is any action at all (look around, zoom): acting while she speaks it learns it.</summary>
    public virtual bool AnsweredByAnyInput => false;

    /// <summary>Real seconds a hint is heard, over all its pauses, before it counts as learnt.</summary>
    public virtual float Seconds => 10f;
}
