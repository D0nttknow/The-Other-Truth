using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สร้าง UI แสดงลำดับเทิร์น (icon list) โดยเรียก RefreshOrder จาก TurnManager
/// - ต้องมี iconPrefab ที่มี TurnOrderIcon component (Image + optional Text)
/// - container: parent (UI) ที่มี HorizontalLayoutGroup / Vertical layout
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

    private List<GameObject> spawned = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (autoRefresh && TurnManager.Instance != null)
        {
            RefreshOrder(TurnManager.Instance.battlers, TurnManager.Instance.battlerObjects, GetTurnIndexSafe());
        }
    }

    int GetTurnIndexSafe()
    {
        if (TurnManager.Instance == null) return 0;
        return Mathf.Clamp(TurnManager.Instance != null ? TurnManager.Instance.GetType().GetField("turnIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(TurnManager.Instance) as int? ?? 0 : 0, 0, Mathf.Max(0, TurnManager.Instance.battlers.Count - 1));
    }

    public void RefreshOrder(List<Battler> battlers, List<GameObject> battlerObjects, int currentIndex)
    {
        // safety
        if (container == null || iconPrefab == null) return;

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

            var iconGO = Instantiate(iconPrefab, container);
            iconGO.name = $"TurnIcon_{idx}_{b.name}";
            spawned.Add(iconGO);

            var icon = iconGO.GetComponent<TurnOrderIcon>();
            if (icon != null)
            {
                Sprite s = GetSpriteFromGameObject(obj);
                icon.SetData(b.name, s ?? defaultSprite, isCurrent: i == 0);
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