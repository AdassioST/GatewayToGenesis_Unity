using UnityEngine;

/// <summary>
/// Scene-owned singleton. The first instance wins; later duplicates destroy their GameObject.
/// Subclasses put initialization in <see cref="OnSingletonAwake"/> and teardown in
/// <see cref="OnSingletonDestroy"/> instead of declaring Awake/OnDestroy.
/// </summary>
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    public static T Instance { get; private set; }

    protected void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate {typeof(T).Name} found on '{name}', destroying it.");
            Destroy(gameObject);
            return;
        }
        Instance = (T)this;
        OnSingletonAwake();
    }

    protected void OnDestroy()
    {
        if (Instance != this) return;
        OnSingletonDestroy();
        Instance = null;
    }

    protected virtual void OnSingletonAwake() { }

    protected virtual void OnSingletonDestroy() { }
}
