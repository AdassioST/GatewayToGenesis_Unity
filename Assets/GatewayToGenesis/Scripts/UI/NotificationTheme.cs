using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The look of the capital's notices (Resources/UI/NotificationTheme): the rhombus prefab stacked in the HUD's
/// NotificationGrid, its three plates and an icon per topic. Read by <see cref="NotificationFeed"/>.
/// </summary>
[CreateAssetMenu(fileName = "NotificationTheme", menuName = "Game Object/Notification Theme", order = 12)]
public class NotificationTheme : ScriptableObject
{
    [Tooltip("One notice: a rhombus Image + Button with an 'Icon' child Image (Prefabs/UI/Notification).")]
    public GameObject prefab;

    [Header("Plates")]
    [Tooltip("Issues that stay until they are solved (an empty council, no research...).")]
    public Sprite issue;
    [Tooltip("News from the world: discoveries, units, land.")]
    public Sprite world;
    [Tooltip("Other news: research done, Era Score, legends, the Age.")]
    public Sprite news;

    [Header("Icons")]
    public List<TopicIcon> icons = new List<TopicIcon>();

    [Header("Stack")]
    [Tooltip("Most notices shown at once (issues first); the NotificationGrid holds about nine.")]
    public int maxShown = 9;
    [Tooltip("Most news kept waiting to be dismissed (the oldest goes first).")]
    public int maxNews = 6;
    [Tooltip("Seconds a notice takes to pop in or out.")]
    public float popSeconds = 0.25f;

    public Sprite Icon(NotificationFeed.Topic topic)
    {
        foreach (var entry in icons) if (entry != null && entry.topic == topic) return entry.icon;
        return null;
    }
}

[Serializable]
public class TopicIcon
{
    public NotificationFeed.Topic topic;
    public Sprite icon;
}
