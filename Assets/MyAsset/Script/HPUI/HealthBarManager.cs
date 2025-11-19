using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthBarManager: creates healthbar prefabs and subscribes to IHpProvider.OnHpChanged.
/// - Auto-assigns missing fill Image / CanvasGroup on instantiated prefab if possible.
/// - Ensures Image is Filled (so fillAmount works) and assigns default sprite if provided.
/// - Adds CreateForAllFromTurnManager helper for convenience (robust to ordering issues).
/// - Added status-icon API: AddStatusIconFor / RemoveStatusIconFor, with pending queue support.
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

    // pending status icons for targets that don't have a healthbar yet
    private Dictionary<GameObject, List<PendingIcon>> _pendingIcons = new Dictionary<GameObject, List<PendingIcon>>();

    // internal retry coroutine handle
    private Coroutine _createRetryCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (autoCreateFromTurnManager)
        {
            // use defensive call that will retry a few frames if TurnBaseSystem hasn't populated lists yet
            CreateForAllFromTurnManager();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- PendingIcon helper ----------------
    private class PendingIcon
    {
        public string id;
        public Sprite sprite;
        public string tooltip;
        public PendingIcon(string id, Sprite sprite, string tooltip)
        {
            this.id = id;
            this.sprite = sprite;
            this.tooltip = tooltip;
        }
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
            Debug.LogError("[HealthBarManager] Exception instantiating healthBarPrefab: " + ex);
            return null;
        }

        Debug.Log("[HealthBarManager] Instantiated '" + hb.name + "' for character '" + character.name + "'");

        // ensure the instance is active and visible (some prefabs are disabled by default)
        hb.SetActive(true);
        var cgCheck = hb.GetComponent<CanvasGroup>() ?? hb.GetComponentInChildren<CanvasGroup>(true);
        if (cgCheck == null)
        {
            cgCheck = hb.AddComponent<CanvasGroup>();
            Debug.Log("[HealthBarManager] Added CanvasGroup to '" + hb.name + "' for visibility control.");
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

            Debug.Log("[HealthBarManager] Follower configured: target=" + (follower.target != null ? follower.target.name : "null") + ", uiCamera=" + (follower.uiCamera != null ? follower.uiCamera.name : "null"));
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
                    Debug.Log("[HealthBarManager] Auto-assigned fillImage on '" + hb.name + "' to child Image '" + found.gameObject.name + "'");
                }
                else
                {
                    Debug.LogWarning("[HealthBarManager] Could not auto-assign fillImage for '" + hb.name + "'. Make sure HealthBarUI.fillImage is assigned in prefab.");
                }
            }

            // if fillImage exists, ensure it can be used with fillAmount
            if (ui.fillImage != null)
            {
                // assign default sprite if none
                if (ui.fillImage.sprite == null && defaultFillSprite != null)
                {
                    ui.fillImage.sprite = defaultFillSprite;
                    Debug.Log("[HealthBarManager] Assigned defaultFillSprite to '" + ui.fillImage.gameObject.name + "' in '" + hb.name + "'");
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
            Debug.LogWarning("[HealthBarManager] HealthBarUI component not found on '" + hb.name + "'.");
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
                Debug.LogError("[HealthBarManager] Exception calling SetHealth on '" + hb.name + "': " + ex);
            }

            Action<int, int> handler = (cur, max) =>
            {
                try
                {
                    ui.SetHealth(cur, max);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[HealthBarManager] Exception while updating UI SetHealth: " + ex);
                }
            };

            hpProv.OnHpChanged += handler;
            subscriptions[character] = handler;
            Debug.Log("[HealthBarManager] Subscribed OnHpChanged for character '" + character.name + "'");
        }
        else
        {
            if (hpProv == null)
                Debug.LogWarning("[HealthBarManager] Character '" + character.name + "' does not implement IHpProvider (hpProv null).");
            if (ui == null)
                Debug.LogWarning("[HealthBarManager] UI component missing on '" + hb.name + "'; will not subscribe.");
        }

        created[character] = hb;

        // process any pending icons queued for this character
        if (_pendingIcons.TryGetValue(character, out var pendList))
        {
            foreach (var p in pendList)
            {
                // this will find the created[character] and add the icon immediately
                AddStatusIconFor(character, p.id, p.sprite, p.tooltip);
            }
            _pendingIcons.Remove(character);
        }

        return hb;
    }

    /// <summary>
    /// Create healthbars for every character found in TurnBaseSystem.Instance.characterObjects (or battlerObjects).
    /// This method is defensive about timing: if TurnBaseSystem isn't ready yet it will retry a few frames.
    /// </summary>
    public void CreateForAllFromTurnManager()
    {
        // prefer TurnBaseSystem.Instance directly (avoid forced TurnManager attachment during startup)
        var tbs = TurnBaseSystem.Instance;
        if (tbs == null)
        {
            Debug.LogWarning("[HealthBarManager] CreateForAllFromTurnManager aborted: TurnBaseSystem.Instance is null. Scheduling retry.");
            StartRetryCreate();
            return;
        }

        // try characterObjects first
        var chars = tbs.characterObjects;
        if (chars == null || chars.Count == 0)
        {
            // fallback to battlerObjects if available
            var battlers = tbs.battlerObjects;
            if (battlers != null && battlers.Count > 0)
            {
                Debug.Log("[HealthBarManager] CreateForAllFromTurnManager: characterObjects empty; using battlerObjects (" + battlers.Count + ")");
                CreateForList(battlers);
                return;
            }

            Debug.LogWarning("[HealthBarManager] CreateForAllFromTurnManager: characterObjects is null or empty. Scheduling retry.");
            StartRetryCreate();
            return;
        }

        Debug.Log("[HealthBarManager] CreateForAllFromTurnManager: creating for " + chars.Count + " characters.");
        CreateForList(chars);
    }

    private void CreateForList(List<GameObject> list)
    {
        foreach (var go in list)
        {
            if (go == null) continue;
            var prov = go.GetComponent(typeof(IHpProvider)) as IHpProvider;
            if (prov == null)
            {
                Debug.Log("[HealthBarManager] skipping '" + go.name + "' (no IHpProvider)");
                continue;
            }
            if (!created.ContainsKey(go))
            {
                var instance = CreateFor(go, go.transform);
                Debug.Log("[HealthBarManager] CreateForAll created: " + (instance != null ? instance.name : "null") + " for '" + go.name + "'");
            }
        }
    }

    private void StartRetryCreate()
    {
        if (_createRetryCoroutine == null)
            _createRetryCoroutine = StartCoroutine(RetryCreateCoroutine(10, 0.1f));
    }

    private IEnumerator RetryCreateCoroutine(int attempts, float delaySeconds)
    {
        int tries = 0;
        while (tries < attempts)
        {
            yield return new WaitForSeconds(delaySeconds);
            tries++;
            var tbs = TurnBaseSystem.Instance;
            if (tbs != null)
            {
                var chars = tbs.characterObjects;
                if (chars != null && chars.Count > 0)
                {
                    Debug.Log("[HealthBarManager] RetryCreateCoroutine: found characterObjects on attempt " + tries + " (count=" + chars.Count + "). Creating now.");
                    CreateForList(chars);
                    _createRetryCoroutine = null;
                    yield break;
                }
                var battlers = tbs.battlerObjects;
                if (battlers != null && battlers.Count > 0)
                {
                    Debug.Log("[HealthBarManager] RetryCreateCoroutine: found battlerObjects on attempt " + tries + " (count=" + battlers.Count + "). Creating now.");
                    CreateForList(battlers);
                    _createRetryCoroutine = null;
                    yield break;
                }
            }
        }
        Debug.LogWarning("[HealthBarManager] RetryCreateCoroutine: gave up after " + attempts + " attempts; no characters found.");
        _createRetryCoroutine = null;
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

        // clear any pending icons queued
        if (_pendingIcons.ContainsKey(character)) _pendingIcons.Remove(character);
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

        _pendingIcons.Clear();
    }

    // ---------------- Status icon API ----------------

    /// <summary>
    /// Add or update a status icon on the healthbar that follows target.
    /// id: unique id for this status (e.g. "bleeding")
    /// icon: Sprite to show
    /// tooltip: optional text to show on hover (if tooltip implemented)
    /// </summary>
    public void AddStatusIconFor(GameObject target, string id, Sprite icon, string tooltip = null)
    {
        if (target == null || string.IsNullOrEmpty(id) || icon == null) return;

        if (created.TryGetValue(target, out var hb))
        {
            var ui = hb.GetComponent<HealthBarUI>() ?? hb.GetComponentInChildren<HealthBarUI>(true);
            if (ui != null)
            {
                ui.AddOrUpdateStatusIcon(id, icon, tooltip);
                return;
            }
            else
            {
                Debug.LogWarning("[HealthBarManager] AddStatusIconFor: HealthBarUI missing on instance for " + target.name);
            }
        }

        // healthbar not present yet — queue it
        if (!_pendingIcons.TryGetValue(target, out var list))
        {
            list = new List<PendingIcon>();
            _pendingIcons[target] = list;
        }

        // avoid duplicating same pending id
        if (!list.Exists(p => p.id == id))
            list.Add(new PendingIcon(id, icon, tooltip));

        Debug.Log("[HealthBarManager] AddStatusIconFor: queued icon '" + id + "' for target '" + target.name + "' (healthbar not yet created)");
    }

    /// <summary>
    /// Remove status icon for the given target & id
    /// </summary>
    public void RemoveStatusIconFor(GameObject target, string id)
    {
        if (target == null || string.IsNullOrEmpty(id)) return;

        // if instance exists, remove immediately
        if (created.TryGetValue(target, out var hb))
        {
            var ui = hb.GetComponent<HealthBarUI>() ?? hb.GetComponentInChildren<HealthBarUI>(true);
            if (ui != null)
            {
                ui.RemoveStatusIcon(id);
                return;
            }
        }

        // else remove from pending queue if present
        if (_pendingIcons.TryGetValue(target, out var list))
        {
            list.RemoveAll(p => p.id == id);
            if (list.Count == 0) _pendingIcons.Remove(target);
        }
    }

    /// <summary>
    /// Helper: find the instantiated healthbar GameObject for a target.
    /// Uses created dictionary (reliable) rather than scene search.
    /// </summary>
    public GameObject GetHealthBarForTarget(GameObject target)
    {
        if (target == null) return null;
        if (created.TryGetValue(target, out var hb)) return hb;
        return null;
    }
}