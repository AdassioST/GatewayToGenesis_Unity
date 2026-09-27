using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The views of a list shown under one container (seats, legends in a pool, civics). <see cref="Show{TItem}"/> keeps
/// exactly one view per item: existing views are re-bound in place, and only the difference is instantiated or
/// destroyed. A view keeps its event subscriptions (made once in <c>onCreate</c>) for its whole life.
/// </summary>
public class ViewList<TView> where TView : Component
{
    private readonly List<TView> _views = new List<TView>();
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly Action<TView> _onCreate;

    public ViewList(GameObject prefab, Transform parent, Action<TView> onCreate = null)
    {
        _prefab = prefab;
        _parent = parent;
        _onCreate = onCreate;
    }

    public IReadOnlyList<TView> Views => _views;

    /// <summary>Show <paramref name="items"/> in order, binding view i to item i.</summary>
    public void Show<TItem>(IReadOnlyList<TItem> items, Action<TView, TItem> bind)
    {
        _views.RemoveAll(view => view == null); // destroyed from outside
        if (_prefab == null || _parent == null) return;

        while (_views.Count < items.Count)
        {
            var view = Object.Instantiate(_prefab, _parent).GetComponent<TView>();
            if (view == null)
            {
                GameLog.Error($"Prefab '{_prefab.name}' has no {typeof(TView).Name}; nothing is shown under '{_parent.name}'.", LogChannel.UI);
                return;
            }
            _onCreate?.Invoke(view);
            _views.Add(view);
        }
        while (_views.Count > items.Count)
        {
            int last = _views.Count - 1;
            Object.Destroy(_views[last].gameObject);
            _views.RemoveAt(last);
        }
        for (int i = 0; i < items.Count; i++) bind(_views[i], items[i]);
    }

    public void Clear() => Show(Array.Empty<object>(), (TView _, object __) => { });
}
