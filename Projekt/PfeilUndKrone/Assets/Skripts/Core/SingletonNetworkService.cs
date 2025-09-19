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
