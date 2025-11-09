using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InventoryUI (ปรับปรุง)
/// - แสดง/ซ่อน panel ด้วย Open/Close/Toggle
/// - จะเรียก RefreshUI เมื่อเปิดเพื่อแสดงข้อมูลล่าสุด
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public RectTransform contentParent;
    public GameObject slotPrefab;
    public ItemDefinition[] itemDatabase; // lookup

    Dictionary<string, ItemDefinition> lookup = new Dictionary<string, ItemDefinition>();
    List<GameObject> spawnedSlots = new List<GameObject>();

    bool isOpen = true; // ถ้าต้องการให้เริ่มปิด ให้ตั้งเป็น false

    void Awake()
    {
        // build lookup
        lookup.Clear();
        if (itemDatabase != null)
        {
            foreach (var it in itemDatabase)
                if (it != null && !string.IsNullOrEmpty(it.id))
                    lookup[it.id] = it;
        }

        // start closed by default (ถ้าต้องการให้เปิดตอนเริ่ม ให้เปลี่ยนเป็น true)
        gameObject.SetActive(isOpen);
    }

    void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;

        // If starting open, refresh immediately
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
        // only refresh if the panel is visible (optional)
        if (!gameObject.activeSelf) return;

        // clear existing slots
        foreach (var go in spawnedSlots) if (go != null) Destroy(go);
        spawnedSlots.Clear();

        if (InventoryManager.Instance == null || contentParent == null || slotPrefab == null) return;

        foreach (var e in InventoryManager.Instance.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;

            var go = Instantiate(slotPrefab, contentParent);
            var slot = go.GetComponent<InventorySlotUI>();
            Sprite icon = null;
            if (lookup.TryGetValue(e.id, out var def)) icon = def.icon;
            slot.Setup(e.id, icon, e.count);
            spawnedSlots.Add(go);
        }
    }
}