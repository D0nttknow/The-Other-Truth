using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton scene loader. Keeps one instance (DontDestroyOnLoad).
/// Behavior:
/// - Call LoadScene(target) to request a scene load (this sets TargetScene and loads the loadingScene).
/// - The loader subscribes to sceneLoaded and will only react when the loadingScene is loaded.
/// - After the loadingScene triggers the actual async load, TargetScene is cleared to avoid re-triggering.
/// - Adds a one-frame delay before starting the runtime loader so the LoadingScene UI has a chance to render.
/// - Guards against double-starting the runtime loader.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [HideInInspector] public string TargetScene;
    [SerializeField] string loadingSceneName = "LoadingScene";

    // Prevent starting multiple runtime loaders at once
    bool isLoadingInProgress = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Public: request to load a scene via the loading scene.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneLoader] LoadScene called with empty name.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] Target scene '{sceneName}' is not in Build Settings. Aborting.");
            return;
        }

        // Save previous scene for BattleEndUIManager
        string current = SceneManager.GetActiveScene().name;
        SceneFlowManager.SetPreviousSceneStatic(current);
        PlayerPrefs.SetString("BattleEndUIManager_PreviousScene", current);
        PlayerPrefs.Save();

        // Set target and load loading scene
        TargetScene = sceneName;
        Debug.Log($"[SceneLoader] Request load: target='{TargetScene}', previous='{current}'. Loading '{loadingSceneName}'");
        SceneManager.LoadScene(loadingSceneName);
    }

    /// <summary>
    /// Called whenever any scene is loaded. We only react if:
    /// - the newly loaded scene is the loadingSceneName
    /// - and TargetScene is set (non-empty)
    /// When reacting, we start a delayed coroutine that creates the runtime loading controller.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != loadingSceneName) return;
        if (string.IsNullOrEmpty(TargetScene))
        {
            // No target to load — do nothing (prevents accidental re-loads)
            Debug.Log("[SceneLoader] LoadingScene loaded but TargetScene is empty — nothing to do.");
            return;
        }

        if (isLoadingInProgress)
        {
            Debug.Log("[SceneLoader] Loading already in progress, skipping OnSceneLoaded.");
            return;
        }

        // Start a coroutine to delay one frame so LoadingScene UI can render
        StartCoroutine(DelayedStartRuntimeLoader(TargetScene));
    }

    IEnumerator DelayedStartRuntimeLoader(string target)
    {
        // Wait one frame so the loading scene has a chance to render its UI
        yield return null;

        // Re-check validity and avoid races
        if (string.IsNullOrEmpty(TargetScene) || !string.Equals(TargetScene, target))
        {
            Debug.LogWarning($"[SceneLoader] Delayed loader aborted: TargetScene changed (expected '{target}', actual '{TargetScene}').");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError($"[SceneLoader] Delayed loader: target '{target}' not in Build Settings. Abort.");
            TargetScene = null;
            yield break;
        }

        isLoadingInProgress = true;
        Debug.Log($"[SceneLoader] Starting runtime loader for '{target}' after one frame delay.");

        var go = new GameObject("RuntimeLoadingController");
        DontDestroyOnLoad(go); // optional: keep controller alive while loading
        var ctrl = go.AddComponent<RuntimeLoadingController>();
        ctrl.StartLoad(target, () =>
        {
            // callback when finished loading the target
            Debug.Log($"[SceneLoader] Finished loading target '{target}'. Clearing TargetScene and state.");
            TargetScene = null;
            isLoadingInProgress = false;

            // remove the temporary runtime loading controller object if it's still around
            if (ctrl != null && ctrl.gameObject != null)
            {
                Destroy(ctrl.gameObject);
            }
        });
    }
}