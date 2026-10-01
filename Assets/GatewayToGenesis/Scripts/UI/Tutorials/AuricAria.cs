using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>The two ways the Auric Aria speaks (<see cref="AuricWords"/>).</summary>
public enum AuricForm
{
    /// <summary>Large words a little above the middle of the screen: a whisper, a title, a line under it. For what begins or is completed.</summary>
    Announcement,
    /// <summary>A subtitle low on the screen: one or two short sentences, stage directions in parentheses. Her own voice.</summary>
    Line,
}

/// <summary>
/// One thing the Auric Aria says. <see cref="id"/> is its stable name: the key of its recorded voice
/// (Resources/Audio/AuricAria/&lt;id&gt;, <see cref="AuricVoiceOver"/>) and of what the game remembers about it.
/// An announcement has a small <see cref="whisper"/>, a large <see cref="title"/> and a small <see cref="text"/>; a line
/// has only <see cref="text"/>, where "(sob)" and other parenthesised directions are drawn fainter and in italics.
/// <see cref="seconds"/> holds words spoken at once for that long (0: long enough to read them, or to hear the voice).
/// </summary>
public struct AuricWords
{
    public string id;
    public AuricForm form;
    public string whisper, title, text;
    public float seconds;

    public static AuricWords Announcement(string id, string whisper, string title, string text = null, float seconds = 0f) =>
        new AuricWords { id = id, form = AuricForm.Announcement, whisper = whisper, title = title, text = text, seconds = seconds };

    public static AuricWords Line(string id, string text, float seconds = 0f) =>
        new AuricWords { id = id, form = AuricForm.Line, text = text, seconds = seconds };

    /// <summary>Nothing to say: an announcement without a title, or a line without words.</summary>
    public bool IsEmpty => form == AuricForm.Announcement ? string.IsNullOrEmpty(title) : string.IsNullOrEmpty(text);

    /// <summary>All of it as plain text, in speaking order (logs, tests, a transcript).</summary>
    public string Transcript => string.Join(" ", new[] { whisper, title, text }).Trim();

    private static readonly Regex Direction = new Regex(@"\(([^()]*)\)", RegexOptions.Compiled);

    /// <summary>Stage directions ("(sob)") set apart as TMP rich text: italic, smaller and fainter than the words.</summary>
    public static string Styled(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : Direction.Replace(text, "<i><size=86%><alpha=#8C>($1)<alpha=#FF></size></i>");
}

/// <summary>
/// The Auric Aria's voice, for anything in the game that wants her to speak at once (the tutorial host,
/// <see cref="Tutorials"/>, draws and times it). Two forms (<see cref="AuricForm"/>):
/// - <see cref="Announce"/>: something begins or is completed ("Your 21 Founders Are Home"), large and centred;
/// - <see cref="Say"/>: her own line as a subtitle ("(sob) ... It's okay, sometimes things die..."), low on the screen.
/// Words spoken here do not wait for the player to pause (the teaching lessons do) and the player's clicks do not hush
/// them: they hold until read (<see cref="HoldSeconds"/>). She says one thing at a time; the rest wait their turn, and
/// all of it waits while a story is told or a window covers the view. Nothing is said while the Tutorial Voice is off.
/// For recorded voice: <see cref="Began"/> when her first letter appears, <see cref="Ended"/> when she falls silent, and
/// <see cref="VoiceSeconds"/> so a subtitle stays up while its voice plays. Every word she says is a proposal.
/// </summary>
public static class AuricAria
{
    /// <summary>Reading pace for how long words stay (words a second), after the letters have condensed.</summary>
    public const float WordsPerSecond = 3f;
    // The letters take about this long to appear; the words linger this long once read; nothing is shorter than these.
    private const float RevealSeconds = 1.4f, Linger = 1.2f, ShortestLine = 3.5f, ShortestAnnouncement = 5.5f, AfterVoice = 0.6f;
    // Words waiting their turn beyond this are old news: the oldest are dropped.
    private const int MostWaiting = 4;

    /// <summary>She begins to say these words (their first letter appears): start their voice here.</summary>
    public static event Action<AuricWords> Began;

    /// <summary>She falls silent on these words (heard out, hushed by the player, or cut short by a story).</summary>
    public static event Action<AuricWords> Ended;

    /// <summary>Seconds of recorded voice for an id (0 or null: none); the words are held at least that long.</summary>
    public static Func<string, float> VoiceSeconds;

    private static readonly List<AuricWords> _waiting = new List<AuricWords>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _waiting.Clear();
        Began = null;
        Ended = null;
        VoiceSeconds = null;
    }

    /// <summary>Words waiting to be said, oldest first.</summary>
    public static IReadOnlyList<AuricWords> Waiting => _waiting;

    /// <summary>Announce something begun or completed: a whisper, a large title and a line under it.</summary>
    public static void Announce(string id, string whisper, string title, string text = null, float seconds = 0f) =>
        Speak(AuricWords.Announcement(id, whisper, title, text, seconds));

    /// <summary>Say a line of her own, as a subtitle ("(softly) ..." directions allowed).</summary>
    public static void Say(string id, string text, float seconds = 0f) => Speak(AuricWords.Line(id, text, seconds));

    /// <summary>Queue words to be said as soon as she may. The same id already waiting is not said twice.</summary>
    public static void Speak(AuricWords words)
    {
        if (words.IsEmpty || !Tutorials.VoiceOn) return;
        _waiting.RemoveAll(w => w.id == words.id);
        // Announcements go before lines: what was completed is heard before what she feels about it.
        int at = _waiting.Count;
        if (words.form == AuricForm.Announcement)
            for (at = 0; at < _waiting.Count && _waiting[at].form == AuricForm.Announcement; at++) { }
        _waiting.Insert(at, words);
        while (_waiting.Count > MostWaiting) _waiting.RemoveAt(_waiting.Count - 1);
    }

    /// <summary>Take the next words to say (the host calls this when she is free to speak).</summary>
    public static bool TryNext(out AuricWords words)
    {
        words = default;
        if (_waiting.Count == 0) return false;
        words = _waiting[0];
        _waiting.RemoveAt(0);
        return true;
    }

    /// <summary>Put words back at the head of the queue (they were cut short before most of them was heard).</summary>
    public static void Resume(AuricWords words)
    {
        _waiting.RemoveAll(w => w.id == words.id);
        _waiting.Insert(0, words);
    }

    /// <summary>Forget every word waiting (a new scene, or the voice turned off).</summary>
    public static void Clear() => _waiting.Clear();

    /// <summary>
    /// How long spoken words stay: their own <see cref="AuricWords.seconds"/>, or else long enough for the letters to
    /// appear and the words to be read, and never shorter than their recorded voice.
    /// </summary>
    public static float HoldSeconds(AuricWords words, float voiceSeconds)
    {
        if (words.seconds > 0f) return Mathf.Max(words.seconds, voiceSeconds > 0f ? voiceSeconds + AfterVoice : 0f);
        float reading = RevealSeconds + CountWords(words.Transcript) / WordsPerSecond + Linger;
        float least = words.form == AuricForm.Announcement ? ShortestAnnouncement : ShortestLine;
        return Mathf.Max(least, reading, voiceSeconds > 0f ? voiceSeconds + AfterVoice : 0f);
    }

    /// <summary><see cref="HoldSeconds(AuricWords, float)"/> with the recorded voice, if any.</summary>
    public static float HoldSeconds(AuricWords words) => HoldSeconds(words, VoiceSeconds != null && !string.IsNullOrEmpty(words.id) ? VoiceSeconds(words.id) : 0f);

    private static int CountWords(string text)
    {
        int count = 0;
        bool inWord = false;
        foreach (char c in text ?? string.Empty)
        {
            bool letter = char.IsLetterOrDigit(c);
            if (letter && !inWord) count++;
            inWord = letter;
        }
        return count;
    }

    internal static void RaiseBegan(AuricWords words) => Began?.Invoke(words);
    internal static void RaiseEnded(AuricWords words) => Ended?.Invoke(words);
}
