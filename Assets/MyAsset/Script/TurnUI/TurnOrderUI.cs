using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turn order UI helper (robust):
/// - Call RefreshFromManager() to pull lists from the project's turn manager (supports TurnBaseSystem or TurnManager)
/// - Or set iconPrefab/container/defaultSprite in inspector and enable autoRefresh (convenience only)
/// - Safer reflection when reading private turnIndex and robust null-checks + debug logs
/// </summary>
public class TurnOrderUI : MonoBehaviour
{
    public static TurnOrderUI Instance;

    [Tooltip("Prefab for a single turn icon (must have TurnOrderIcon component)")]
    public GameObject iconPrefab;

    [Tooltip("Container (e.g. Content object under HorizontalLayoutGroup) to instantiate icons into")]
    public RectTransform container;

    [Tooltip("Optional default sprite to use if battler GameObject has no sprite/icon")]
    public Sprite defaultSprite;

    [Tooltip("If true, RefreshOrder will be called every frame for convenience (disable in production)")]
    public bool autoRefresh = false;

    // optional: how often to auto-refresh (avoid every-frame cost)
    public float autoRefreshInterval = 0.2f;
    private float _autoTimer = 0f;

    private List<GameObject> spawned = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        if (iconPrefab == null) Debug.LogWarning("[TurnOrderUI] iconPrefab not set in inspector.");
        if (container == null) Debug.LogWarning("[TurnOrderUI] container not set in inspector.");
    }

    void Update()
    {
        if (!autoRefresh) return;
        _autoTimer += Time.unscaledDeltaTime;
        if (_autoTimer >= autoRefreshInterval)
        {
            _autoTimer = 0f;
            RefreshFromManager();
        }
    }

    /// <summary>
    /// Convenience: ask whatever Turn manager exists in your project and refresh UI.
    /// Supports a singleton called TurnBaseSystem.Instance or TurnManager.Instance (or any similar type).
    /// This method uses reflection to find battlers/battlerObjects/turnIndex fields/properties.
    /// For reliability you should call RefreshOrder(...) directly from your turn manager when lists change.
    /// </summary>
    public void RefreshFromManager()
    {
        // Try some known singletons
        object tm = null;
        // Try TurnBaseSystem.Instance
        tm = GetSingletonInstanceByName("TurnBaseSystem");
        if (tm == null) tm = GetSingletonInstanceByName("TurnManager");
        if (tm == null)
        {
            // nothing to pull
            // Debug only — don't spam in production
            // Debug.Log("[TurnOrderUI] No Turn manager singleton found (TurnBaseSystem/TurnManager).");
            return;
        }

        // get battlers list and battlerObjects list (try property or field)
        var battlersObj = GetMemberValue(tm, "battlers") as System.Collections.IList;
        var battlerObjectsObj = GetMemberValue(tm, "battlerObjects") as System.Collections.IList;
        int currentIndex = GetTurnIndexFromManager(tm);

        if (battlersObj == null || battlerObjectsObj == null)
        {
            Debug.LogWarning("[TurnOrderUI] Turn manager found but battlers/battlerObjects lists missing or null. Type=" + tm.GetType().Name);
            return;
        }

        // Convert to strongly typed lists for RefreshOrder signature
        var battlers = new List<Battler>();
        var battlerObjects = new List<GameObject>();
        foreach (var item in battlersObj) battlers.Add(item as Battler);
        foreach (var item in battlerObjectsObj) battlerObjects.Add(item as GameObject);

        RefreshOrder(battlers, battlerObjects, currentIndex);
    }

    // safe helper to read a "turnIndex" value from manager
    int GetTurnIndexFromManager(object tm)
    {
        object val = GetMemberValue(tm, "turnIndex");
        if (val == null)
        {
            // try property named currentTurnIndex or turnIdx etc.
            val = GetMemberValue(tm, "currentTurnIndex") ?? GetMemberValue(tm, "turnIdx");
        }

        if (val == null) return 0;

        // unbox/convert robustly
        if (val is int) return (int)val;
        if (val is long) return Convert.ToInt32((long)val);
        if (val is short) return Convert.ToInt32((short)val);
        int parsed;
        if (int.TryParse(val.ToString(), out parsed)) return parsed;
        return 0;
    }

    // reflection helpers
    object GetMemberValue(object target, string name)
    {
        if (target == null) return null;
        var t = target.GetType();
        // try property
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (p != null) return p.GetValue(target);
        // try field
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (f != null) return f.GetValue(target);
        return null;
    }

    object GetSingletonInstanceByName(string typeName)
    {
        // Find a type with this name in loaded assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(typeName);
                if (t == null) continue;
                // look for static Instance or instance property
                var p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (p != null)
                {
                    var inst = p.GetValue(null);
                    if (inst != null) return inst;
                }
                var f = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (f != null)
                {
                    var inst2 = f.GetValue(null);
                    if (inst2 != null) return inst2;
                }
            }
            catch { }
        }
        return null;
    }

    public void RefreshOrder(List<Battler> battlers, List<GameObject> battlerObjects, int currentIndex)
    {
        // safety
        if (container == null || iconPrefab == null)
        {
            // helpful debug message
            // only warn once to reduce spam
            if (container == null) Debug.LogWarning("[TurnOrderUI] container is null. Assign a RectTransform in the inspector.");
            if (iconPrefab == null) Debug.LogWarning("[TurnOrderUI] iconPrefab is null. Assign a prefab with TurnOrderIcon component.");
            return;
        }

        // clear existing
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();

        if (battlers == null || battlerObjects == null || battlers.Count == 0) return;

        int n = battlers.Count;
        if (currentIndex < 0 || currentIndex >= n) currentIndex = 0;

        // Show from current turn then wrap — upcoming order
        for (int i = 0; i < n; i++)
        {
            int idx = (currentIndex + i) % n;
            var b = battlers[idx];
            var obj = battlerObjects[idx];

            var iconGO = Instantiate(iconPrefab, container, false);
            iconGO.name = $"TurnIcon_{idx}_{(b != null ? b.name : "null")}";
            spawned.Add(iconGO);

            var icon = iconGO.GetComponent<TurnOrderIcon>();
            if (icon != null)
            {
                Sprite s = GetSpriteFromGameObject(obj);
                icon.SetData(b != null ? b.name : "Unknown", s ?? defaultSprite, isCurrent: i == 0);
            }
            else
            {
                Debug.LogWarning("[TurnOrderUI] iconPrefab missing TurnOrderIcon component on " + iconGO.name);
            }
        }
    }

    Sprite GetSpriteFromGameObject(GameObject obj)
    {
        if (obj == null) return null;
        // Try common places for an icon: SpriteRenderer, Image (UI), or a custom IconProvider component
        var sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.sprite;

        var img = obj.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img != null) return img.sprite;

        var provider = obj.GetComponentInChildren<IconProvider>();
        if (provider != null && provider.icon != null) return provider.icon;

        return null;
    }
}

/// <summary>
/// Optional helper component to provide a Sprite for TurnOrderUI (attach to character prefab)
/// </summary>
public class IconProvider : MonoBehaviour
{
    public Sprite icon;
}