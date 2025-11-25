using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurnOrderIcon (ปรับให้ auto-assign และมี debug)
/// - auto-assign iconImage ถ้ายังว่าง
/// - log เมื่อ SetData ถูกเรียก (ช่วยดีบักว่ามี sprite เข้ามาหรือไม่)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TurnOrderIcon : MonoBehaviour
{
    [Tooltip("Image component used as the icon sprite")]
    public Image iconImage;

    [Tooltip("Optional Text component to show name/HP")]
    public Text nameText;

    [Tooltip("Optional highlight GameObject (e.g. border) that shows when this is the current turn")]
    public GameObject highlight;

    [Tooltip("Scale applied to current icon")]
    public float currentScale = 1.15f;

    Vector3 defaultScale;

    void Awake()
    {
        // try to auto-assign the iconImage: first search assigned, then child Image, then own Image component
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        defaultScale = transform.localScale;
    }

    public void SetData(string displayName, Sprite sprite, bool isCurrent)
    {
        // debug: tell us what's coming in
        if (sprite != null)
            Debug.Log($"[TurnOrderIcon] SetData for '{displayName}' with sprite='{sprite.name}' (isCurrent={isCurrent}) on '{gameObject.name}'");
        else
            Debug.Log($"[TurnOrderIcon] SetData for '{displayName}' with sprite=NULL (isCurrent={isCurrent}) on '{gameObject.name}'");

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }
        else
        {
            Debug.LogWarning($"[TurnOrderIcon] iconImage is NULL on '{gameObject.name}'. Add an Image component or assign iconImage in Inspector.");
        }

        if (nameText != null) nameText.text = displayName;

        if (highlight != null) highlight.SetActive(isCurrent);

        // simple scale highlight
        transform.localScale = isCurrent ? defaultScale * currentScale : defaultScale;
    }
}