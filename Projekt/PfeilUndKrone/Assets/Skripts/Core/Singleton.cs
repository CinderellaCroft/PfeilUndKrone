using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

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
            Debug.LogWarning($"[Singleton] Duplicate {typeof(T).Name} instance detected and destroyed! This indicates a scene setup issue.");
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

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
