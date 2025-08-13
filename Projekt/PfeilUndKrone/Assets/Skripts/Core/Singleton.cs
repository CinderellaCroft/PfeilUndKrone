using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    // Optional: Legt fest, ob diese Singleton-Instanz nur im Editor leben darf
    protected virtual bool EditorOnly => false;

    // Optional: Persistenz über Szenen hinweg
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

        Instance = this as T;

        if (Persistent)
        {
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
    }
}
