using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI for a single inventory slot prefab
/// - expects an Image for icon and a Text for count (assign in inspector)
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    public Image iconImage;
    public Text countText;

    string itemId;

    public void Setup(string id, Sprite icon, int count)
    {
        itemId = id;
        if (iconImage != null) iconImage.sprite = icon;
        if (iconImage != null) iconImage.enabled = (icon != null);
        if (countText != null) countText.text = count > 1 ? count.ToString() : "";
    }

    // optional click handler
    public void OnClick()
    {
        Debug.Log($"Clicked slot {itemId}");
        // implement use / equip / drop logic here or call InventoryManager
    }
}