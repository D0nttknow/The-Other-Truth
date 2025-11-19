using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthBarUI
/// - Existing healthbar logic (fill smoothing, visibility)
/// - Added support for status icons (e.g. bleeding) shown in a container under the bar
/// </summary>
[DisallowMultipleComponent]
public class HealthBarUI : MonoBehaviour
{
    [Header("UI References")]
    public Image fillImage;            // foreground fill (type: Filled, Fill Method: Horizontal)
    public CanvasGroup canvasGroup;    // for fade in/out or hide

    [Header("Options")]
    public float smoothSpeed = 8f;     // smoothing speed for fill transitions

    [Header("Status Icons (optional)")]
    [Tooltip("Parent transform under the healthbar where status icons will be instantiated")]
    public RectTransform statusIconContainer;
    [Tooltip("Prefab for a single status icon. Should be a GameObject with an Image component.")]
    public GameObject statusIconPrefab;

    private int currentHp;
    private int maxHp;
    private float displayedFill = 1f;

    // keyed by status id (e.g. "bleeding")
    private Dictionary<string, GameObject> _icons = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        float targetFill = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * smoothSpeed);
        if (fillImage != null) fillImage.fillAmount = displayedFill;
    }

    /// <summary>
    /// Set health values and update visibility.
    /// - If hp > 0 -> ensure UI is visible.
    /// - If hp <= 0 -> hide UI.
    /// Also bump displayedFill immediately to avoid waiting for lerp to show correct value.
    /// </summary>
    public void SetHealth(int hp, int max)
    {
        currentHp = hp;
        maxHp = max;

        // Immediately update displayedFill to reflect new values (so it doesn't stay at 0 while hidden)
        displayedFill = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        if (fillImage != null) fillImage.fillAmount = displayedFill;

        // Show when HP > 0, hide when HP <= 0
        if (currentHp <= 0)
            SetVisible(false);
        else
            SetVisible(true);
    }

    public void SetVisible(bool v)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
        }
        else
        {
            gameObject.SetActive(v);
        }
    }

    // ---------------- Status icons API ----------------

    /// <summary>
    /// Add or update a status icon for this healthbar.
    /// If an icon with the same id already exists, it will update its sprite.
    /// </summary>
    public void AddOrUpdateStatusIcon(string id, Sprite iconSprite, string tooltipText = null)
    {
        if (string.IsNullOrEmpty(id) || iconSprite == null) return;
        if (statusIconContainer == null || statusIconPrefab == null)
        {
            Debug.LogWarning("[HealthBarUI] statusIconContainer or statusIconPrefab not set on " + gameObject.name);
            return;
        }

        if (_icons.TryGetValue(id, out var existing))
        {
            var img = existing.GetComponent<Image>();
            if (img != null) img.sprite = iconSprite;
            // Optionally update tooltip metadata here if you have a tooltip component
            return;
        }

        var go = Instantiate(statusIconPrefab, statusIconContainer, false);
        go.name = "StatusIcon_" + id;
        var image = go.GetComponent<Image>();
        if (image != null) image.sprite = iconSprite;
        _icons[id] = go;

        // Optionally set up a tooltip component if present
        // Example if you have a Tooltip script with a 'text' field:
        // var tooltip = go.GetComponentInChildren<Tooltip>();
        // if (tooltip != null && !string.IsNullOrEmpty(tooltipText)) tooltip.text = tooltipText;
    }

    /// <summary>
    /// Remove status icon by id
    /// </summary>
    public void RemoveStatusIcon(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_icons.TryGetValue(id, out var go))
        {
            if (go != null) Destroy(go);
            _icons.Remove(id);
        }
    }

    /// <summary>
    /// Remove all status icons
    /// </summary>
    public void ClearStatusIcons()
    {
        foreach (var kv in _icons) if (kv.Value != null) Destroy(kv.Value);
        _icons.Clear();
    }

    /// <summary>
    /// Optional: query whether an icon exists
    /// </summary>
    public bool HasStatusIcon(string id) => _icons.ContainsKey(id);
}