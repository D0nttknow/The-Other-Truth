using UnityEngine;

/// <summary>
/// Singleton that remembers the "previous scene" across scene loads (DontDestroyOnLoad).
/// Usage:
/// - Call SceneFlowManager.SetPreviousScene(SceneManager.GetActiveScene().name) BEFORE loading the fighting scene.
/// - Read SceneFlowManager.Instance.previousSceneName (or SceneFlowManager.GetPreviousScene()) when you need it.
/// This implementation will auto-create itself if you call the static API and no instance exists.
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    // public so other systems can read it; prefer GetPreviousScene() for null-safe access
    public string previousSceneName;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Instance method to set previous scene name.
    /// </summary>
    public void SetPreviousScene(string name)
    {
        previousSceneName = name;
        Debug.Log($"[SceneFlowManager] previousSceneName set to '{previousSceneName}'");
    }

    /// <summary>
    /// Static helper — creates the singleton GameObject if needed and sets the previous scene.
    /// Call this from anywhere before loading the fight scene:
    /// SceneFlowManager.SetPreviousSceneStatic(SceneManager.GetActiveScene().name);
    /// </summary>
    public static void SetPreviousSceneStatic(string name)
    {
        EnsureInstanceExists();
        Instance.SetPreviousScene(name);
    }

    /// <summary>
    /// Get the saved previous scene (null or empty if none).
    /// </summary>
    public static string GetPreviousScene()
    {
        return Instance != null ? Instance.previousSceneName : null;
    }

    /// <summary>
    /// Clears stored previous scene.
    /// </summary>
    public static void ClearPreviousScene()
    {
        if (Instance != null) Instance.previousSceneName = null;
    }

    /// <summary>
    /// Ensure an Instance exists in the scene. If none, create a GameObject and attach this component.
    /// </summary>
    static void EnsureInstanceExists()
    {
        if (Instance != null) return;
        var go = new GameObject("SceneFlowManager");
        Instance = go.AddComponent<SceneFlowManager>();
        DontDestroyOnLoad(go);
        Debug.Log("[SceneFlowManager] Auto-created instance (DontDestroyOnLoad).");
    }
}