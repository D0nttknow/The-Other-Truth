using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// Runtime loading controller used by SceneLoader to perform the async load.
/// Created at runtime and destroyed after load completes.
/// </summary>
public class RuntimeLoadingController : MonoBehaviour
{
    public float minDisplayTime = 0.2f;

    /// <summary>
    /// Start loading the target scene asynchronously.
    /// onComplete invoked when load finished and scene activated.
    /// </summary>
    public void StartLoad(string targetScene, Action onComplete = null)
    {
        StartCoroutine(DoLoad(targetScene, onComplete));
    }

    IEnumerator DoLoad(string targetScene, Action onComplete)
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("[RuntimeLoadingController] StartLoad called with empty targetScene.");
            onComplete?.Invoke();
            Destroy(gameObject);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"[RuntimeLoadingController] Target '{targetScene}' not in Build Settings.");
            onComplete?.Invoke();
            Destroy(gameObject);
            yield break;
        }

        float start = Time.realtimeSinceStartup;
        var op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        // Wait until progress reaches 0.9 (ready) — can show progress here
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // ensure min display time
        float elapsed = Time.realtimeSinceStartup - start;
        if (elapsed < minDisplayTime) yield return new WaitForSeconds(minDisplayTime - elapsed);

        // Activate and finish
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        onComplete?.Invoke();

        // Done — destroy this controller
        Destroy(gameObject);
    }
}