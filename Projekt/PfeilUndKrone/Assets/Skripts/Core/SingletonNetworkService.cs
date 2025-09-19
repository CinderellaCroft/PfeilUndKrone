using UnityEngine;

public abstract class SingletonNetworkService<TSelf> : NetworkServiceBase
    where TSelf : NetworkServiceBase
{
    public static TSelf Instance { get; private set; }

    protected virtual bool EditorOnly => false;
    protected virtual bool Persistent => true;

    protected virtual void Awake()
    {
#if !UNITY_EDITOR
        if (EditorOnly)
        {
            Destroy(gameObject);
            return;
        }
#endif
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[SingletonNetworkService] Duplicate {typeof(TSelf).Name} instance detected and destroyed! This indicates a scene setup issue.");
            Destroy(gameObject);
            return;
        }

        Instance = (NetworkServiceBase)this as TSelf;

        if (Persistent)
        {
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
