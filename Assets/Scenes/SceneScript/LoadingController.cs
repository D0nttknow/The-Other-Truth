using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingController : MonoBehaviour
{
    [Header("UI")]
    public Slider progressBar;
    public Text progressText; // optional

    // ปรับถ้าต้องการดีเลย์เพื่อแสดงโลโก้/animation
    public float minDisplayTime = 0.5f;

    // ชื่อ loading scene ที่ควรจะเรียกใช้ controller นี้ — ตรวจให้แน่ใจว่าตรงกับ Build Settings
    [Tooltip("The scene name that this controller should run in (usually 'LoadingScene').")]
    public string loadingSceneName = "LoadingScene";

    void Start()
    {
        // Defensive: only run the loading logic if we're actually in the loading scene.
        // This prevents a LoadingController accidentally placed in other scenes from starting a load.
        var current = SceneManager.GetActiveScene().name;
        if (!string.Equals(current, loadingSceneName, System.StringComparison.Ordinal))
        {
            Debug.LogWarning($"[LoadingController] Start: current scene is '{current}', expected '{loadingSceneName}'. Skipping load logic.");
            return;
        }

        StartCoroutine(LoadTarget());
    }

    IEnumerator LoadTarget()
    {
        string target = SceneLoader.Instance != null ? SceneLoader.Instance.TargetScene : "";

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogError("[LoadingController] TargetScene is empty! Nothing to load.");
            yield break;
        }

        float elapsed = 0f;
        var async = SceneManager.LoadSceneAsync(target);
        async.allowSceneActivation = false;

        while (!async.isDone)
        {
            // Unity async.progress goes 0..0.9 (0.9 means loaded), final 1.0 only when allowSceneActivation=true
            float progress = Mathf.Clamp01(async.progress / 0.9f);
            if (progressBar) progressBar.value = progress;
            if (progressText) progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            // เราอยากให้แสดง Loading หน่วงขั้นต่ำ
            elapsed += Time.deltaTime;
            if (async.progress >= 0.9f && elapsed >= minDisplayTime)
            {
                // อาจใส่ fade out effect ก่อน activate
                async.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}