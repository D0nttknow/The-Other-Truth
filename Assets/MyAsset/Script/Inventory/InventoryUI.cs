using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InventoryUI - updated to use InventoryManager entries (ItemBase) directly
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public RectTransform contentParent;
    public GameObject slotPrefab;

    List<GameObject> spawnedSlots = new List<GameObject>();

    bool isOpen = true;

    void Awake()
    {
        gameObject.SetActive(isOpen);
    }

    void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;

        if (isOpen) RefreshUI();
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        isOpen = true;
        RefreshUI();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        isOpen = false;
    }

    public void RefreshUI()
    {
        if (!gameObject.activeSelf) return;

        foreach (var go in spawnedSlots) if (go != null) Destroy(go);
        spawnedSlots.Clear();

        if (InventoryManager.Instance == null || contentParent == null || slotPrefab == null) return;

        foreach (var e in InventoryManager.Instance.entries)
        {
            if (e == null || e.item == null) continue;

            var go = Instantiate(slotPrefab, contentParent);
            var slot = go.GetComponent<InventorySlotUI>();
            Sprite icon = e.item.icon;
            slot.Setup(e.item.itemId, icon, e.count);
            spawnedSlots.Add(go);
        }
    }
}