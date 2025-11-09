using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthBarManager: creates healthbar prefabs and subscribes to IHpProvider.OnHpChanged.
/// - Auto-assigns missing fill Image / CanvasGroup on instantiated prefab if possible.
/// - Ensures Image is Filled (so fillAmount works) and assigns default sprite if provided.
/// - Adds CreateForAllFromTurnManager helper for convenience.
/// </summary>
public class HealthBarManager : MonoBehaviour
{
    public static HealthBarManager Instance { get; private set; }

    [Header("Assign")]
    public Canvas uiCanvas;               // Canvas (Screen Space - Overlay recommended)
    public GameObject healthBarPrefab;    // prefab that contains HealthBarUI + HealthBarFollower

    [Header("Optional")]
    public Sprite defaultFillSprite;      // if HP fill Image has no sprite, use this (assign a simple bar sprite)
    public bool forceSetFilledType = true;// ensure Image.Type = Filled so fillAmount works
    public bool autoCreateFromTurnManager = false; // call CreateForAllFromTurnManager at Start if true

    // map character -> healthbar instance
    Dictionary<GameObject, GameObject> created = new Dictionary<GameObject, GameObject>();

    // map character -> subscription delegate (so we can unsubscribe later)
    Dictionary<GameObject, Action<int, int>> subscriptions = new Dictionary<GameObject, Action<int, int>>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (autoCreateFromTurnManager)
        {
            CreateForAllFromTurnManager();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Create a healthbar for the given character (if not already created).
    /// Returns the instantiated healthbar GameObject or null on failure.
    /// </summary>
    public GameObject CreateFor(GameObject character, Transform headTransform = null)
    {
        if (character == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateFor aborted: character is null.");
            return null;
        }
        if (healthBarPrefab == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateFor aborted: healthBarPrefab is not assigned in the inspector.");
            return null;
        }
        if (uiCanvas == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateFor aborted: uiCanvas is not assigned in the inspector.");
            return null;
        }

        if (created.ContainsKey(character))
            return created[character];

        // safety: prevent instantiating a prefab that contains a HealthBarManager (could cause recursion)
        if (healthBarPrefab.GetComponentInChildren<HealthBarManager>(true) != null)
        {
            Debug.LogError("[HealthBarManager] healthBarPrefab contains HealthBarManager component. This can cause recursion. Use a plain healthbar prefab.");
            return null;
        }

        GameObject hb = null;
        try
        {
            hb = Instantiate(healthBarPrefab, uiCanvas.transform);
            hb.name = healthBarPrefab.name + "_Instance";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HealthBarManager] Exception instantiating healthBarPrefab: {ex}");
            return null;
        }

        Debug.Log($"[HealthBarManager] Instantiated '{hb.name}' for character '{character.name}'");

        // ensure the instance is active and visible (some prefabs are disabled by default)
        hb.SetActive(true);
        var cgCheck = hb.GetComponent<CanvasGroup>() ?? hb.GetComponentInChildren<CanvasGroup>(true);
        if (cgCheck == null)
        {
            cgCheck = hb.AddComponent<CanvasGroup>();
            Debug.Log($"[HealthBarManager] Added CanvasGroup to '{hb.name}' for visibility control.");
        }
        cgCheck.alpha = 1f;
        cgCheck.interactable = true;
        cgCheck.blocksRaycasts = true;

        // try to find HealthBarFollower and configure
        var follower = hb.GetComponent<HealthBarFollower>();
        if (follower != null)
        {
            follower.target = headTransform != null ? headTransform : character.transform;

            // assign camera sensibly
            if (follower.uiCamera == null)
            {
                if (uiCanvas.renderMode != RenderMode.WorldSpace)
                    follower.uiCamera = Camera.main;
                else if (uiCanvas.worldCamera != null)
                    follower.uiCamera = uiCanvas.worldCamera;
            }

            Debug.Log($"[HealthBarManager] Follower configured: target={(follower.target != null ? follower.target.name : "null")}, uiCamera={(follower.uiCamera != null ? follower.uiCamera.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[HealthBarManager] HealthBarFollower component not found on prefab instance.");
        }

        // find HealthBarUI and ensure its fillImage / canvasGroup are assigned; if not, try auto-bind
        var ui = hb.GetComponent<HealthBarUI>() ?? hb.GetComponentInChildren<HealthBarUI>(true);

        if (ui != null)
        {
            // auto-assign fillImage if empty
            if (ui.fillImage == null)
            {
                Image found = null;
                var imgs = hb.GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    var n = img.gameObject.name.ToLower();
                    if (n.Contains("fill") || n.Contains("hp"))
                    {
                        found = img;
                        break;
                    }
                }
                if (found != null)
                {
                    ui.fillImage = found;
                    Debug.Log($"[HealthBarManager] Auto-assigned fillImage on '{hb.name}' to child Image '{found.gameObject.name}'");
                }
                else
                {
                    Debug.LogWarning($"[HealthBarManager] Could not auto-assign fillImage for '{hb.name}'. Make sure HealthBarUI.fillImage is assigned in prefab.");
                }
            }

            // if fillImage exists, ensure it can be used with fillAmount
            if (ui.fillImage != null)
            {
                // assign default sprite if none
                if (ui.fillImage.sprite == null && defaultFillSprite != null)
                {
                    ui.fillImage.sprite = defaultFillSprite;
                    Debug.Log($"[HealthBarManager] Assigned defaultFillSprite to '{ui.fillImage.gameObject.name}' in '{hb.name}'");
                }

                if (forceSetFilledType)
                {
                    ui.fillImage.type = Image.Type.Filled;
                    ui.fillImage.fillMethod = Image.FillMethod.Horizontal;
                    ui.fillImage.fillAmount = 1f;
                }
            }

        }
        else
        {
            Debug.LogWarning($"[HealthBarManager] HealthBarUI component not found on '{hb.name}'.");
        }

        // subscribe to IHpProvider if present
        var hpProv = character.GetComponent(typeof(IHpProvider)) as IHpProvider;
        if (ui != null && hpProv != null)
        {
            // set initial state
            try
            {
                ui.SetHealth(hpProv.CurrentHp, hpProv.MaxHp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HealthBarManager] Exception calling SetHealth on '{hb.name}': {ex}");
            }

            Action<int, int> handler = (cur, max) =>
            {
                try
                {
                    ui.SetHealth(cur, max);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HealthBarManager] Exception while updating UI SetHealth: {ex}");
                }
            };

            hpProv.OnHpChanged += handler;
            subscriptions[character] = handler;
            Debug.Log($"[HealthBarManager] Subscribed OnHpChanged for character '{character.name}'");
        }
        else
        {
            if (hpProv == null)
                Debug.LogWarning($"[HealthBarManager] Character '{character.name}' does not implement IHpProvider (hpProv null).");
            if (ui == null)
                Debug.LogWarning($"[HealthBarManager] UI component missing on '{hb.name}'; will not subscribe.");
        }

        created[character] = hb;
        return hb;
    }

    /// <summary>
    /// Create healthbars for every character found in TurnManager.Instance.characterObjects.
    /// Useful to call after TurnManager has populated its list to avoid ordering issues.
    /// </summary>
    public void CreateForAllFromTurnManager()
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateForAllFromTurnManager aborted: TurnManager.Instance is null.");
            return;
        }

        var chars = TurnManager.Instance.characterObjects;
        if (chars == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateForAllFromTurnManager aborted: characterObjects is null.");
            return;
        }

        Debug.Log($"[HealthBarManager] CreateForAllFromTurnManager: creating for {chars.Count} characters.");
        foreach (var go in chars)
        {
            if (go == null) continue;
            // only create if IHpProvider exists (otherwise UI wouldn't get updates)
            var prov = go.GetComponent(typeof(IHpProvider)) as IHpProvider;
            if (prov == null)
            {
                Debug.Log($"[HealthBarManager] skipping '{go.name}' (no IHpProvider)");
                continue;
            }
            if (!created.ContainsKey(go))
            {
                var instance = CreateFor(go, go.transform);
                Debug.Log($"[HealthBarManager] CreateForAll created: {(instance != null ? instance.name : "null")} for '{go.name}'");
            }
        }
    }

    public void RemoveFor(GameObject character)
    {
        if (character == null) return;

        if (subscriptions.TryGetValue(character, out var handler))
        {
            var hpProv = character.GetComponent(typeof(IHpProvider)) as IHpProvider;
            if (hpProv != null)
            {
                try { hpProv.OnHpChanged -= handler; } catch { }
            }
            subscriptions.Remove(character);
        }

        if (created.TryGetValue(character, out var go))
        {
            if (go != null) Destroy(go);
            created.Remove(character);
        }
    }

    public void ClearAll()
    {
        foreach (var kv in subscriptions)
        {
            var character = kv.Key;
            var handler = kv.Value;
            if (character != null)
            {
                var hpProv = character.GetComponent(typeof(IHpProvider)) as IHpProvider;
                if (hpProv != null)
                {
                    try { hpProv.OnHpChanged -= handler; } catch { }
                }
            }
        }
        subscriptions.Clear();

        foreach (var kv in created)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        created.Clear();
    }
}