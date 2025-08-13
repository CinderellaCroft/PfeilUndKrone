using UnityEngine;

public abstract class SingletonNetworkService<TSelf> : NetworkServiceBase
    where TSelf : NetworkServiceBase
{
    public static TSelf Instance { get; private set; }

    // Optional: Nur im Editor erlauben (z. B. für den Simulator)
    protected virtual bool EditorOnly => false;

    // Optional: Persistenz über Szenen hinweg
    protected virtual bool Persistent => true;

    protected virtual void Awake()
    {
#if !UNITY_EDITOR
        if (EditorOnly)
        {
            // Im Build sofort entsorgen, falls Editor-only
            Destroy(gameObject);
            return;
        }
#endif
        if (Instance != null && Instance != this)
        {
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
}
