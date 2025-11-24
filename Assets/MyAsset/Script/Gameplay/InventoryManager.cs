using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Inventory singleton เก็บไอเท็ม (consumables)
/// - รองรับ AddItem(ItemBase) และ AddItem(string id, int count) เพื่อความเข้ากันกับโค้ดเดิม
/// - เพิ่ม property entries เพื่อให้โค้ด UI ที่คาด entries ยังคงทำงานได้
/// - มี itemDatabase (optional) สำหรับแม็ป id -> ItemBase (ตั้งใน Inspector)
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Serializable]
    public class InventoryEntry
    {
        public ItemBase item;
        public int count = 1;

        // compatibility helpers used by InventoryUI / other callers
        public string id { get { return item != null ? item.itemId : string.Empty; } }
        public string displayName { get { return item != null ? item.displayName : id; } }
    }

    // Inspector: สถานที่เก็บไอเท็มที่เป็นฐานข้อมูล (optional)
    // เติมรายการ ItemBase assets ที่มีไว้ที่นี่ (แนะนำถ้าไอเท็มไม่ได้อยู่ใน Resources/)
    [Header("Optional: item database for lookup by id (set in Inspector)")]
    public List<ItemBase> itemDatabase = new List<ItemBase>();

    // internal storage
    [Header("Inventory storage")]
    public List<InventoryEntry> items = new List<InventoryEntry>();

    // compatibility property expected by some UI code
    public List<InventoryEntry> entries { get { return items; } }

    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    // Existing AddItem(ItemBase) behaviour (kept for compatibility)
    public void AddItem(ItemBase it)
    {
        AddItem(it, 1);
    }

    // New overload: AddItem(ItemBase, count)
    public void AddItem(ItemBase it, int count)
    {
        if (it == null) return;
        if (count <= 0) return;

        var entry = items.Find(e => e.item == it);
        if (entry == null)
        {
            items.Add(new InventoryEntry() { item = it, count = count });
        }
        else
        {
            entry.count += count;
        }
        Debug.LogFormat("[Inventory] AddItem: {0} x{1} (total {2})", it.displayName, count, items.Find(e => e.item == it).count);
        OnInventoryChanged?.Invoke();
    }

    // New overload: AddItem by itemId (string) for callers that pass id directly
    public void AddItem(string itemId, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        var it = FindItemById(itemId);
        if (it == null)
        {
            Debug.LogWarningFormat("[Inventory] AddItem: item with id '{0}' not found in database/Resources.", itemId);
            return;
        }
        AddItem(it, count);
    }

    // Remove by index
    public void RemoveItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        OnInventoryChanged?.Invoke();
    }

    // Use item at index on target (GameObject)
    public bool UseItemAt(int index, GameObject target)
    {
        if (index < 0 || index >= items.Count) return false;
        var entry = items[index];
        if (entry == null || entry.item == null) return false;
        bool consumed = entry.item.Use(target);
        if (consumed)
        {
            entry.count--;
            if (entry.count <= 0) items.RemoveAt(index);
            OnInventoryChanged?.Invoke();
        }
        return consumed;
    }

    // Try to find an ItemBase asset by id. First check itemDatabase (Inspector), then try Resources.LoadAll (fallback).
    public ItemBase FindItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        // 1) check explicit database
        if (itemDatabase != null && itemDatabase.Count > 0)
        {
            var found = itemDatabase.FirstOrDefault(x => x != null && x.itemId == itemId);
            if (found != null) return found;
        }

        // 2) try existing items in inventory (maybe already added previously)
        var inInv = items.FirstOrDefault(e => e != null && e.item != null && e.item.itemId == itemId);
        if (inInv != null) return inInv.item;

#if UNITY_EDITOR
        // In editor, try to find assets of type ItemBase (Editor-only faster lookup)
        try
        {
            var all = UnityEditor.AssetDatabase.FindAssets("t:ItemBase")
                .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBase>(path))
                .Where(x => x != null && x.itemId == itemId)
                .FirstOrDefault();
            if (all != null) return all;
        }
        catch { }
#endif

        // 3) runtime fallback: Resources folder search (requires ItemBase assets to be in a Resources folder)
        try
        {
            var loaded = Resources.LoadAll<ItemBase>("");
            if (loaded != null && loaded.Length > 0)
            {
                var r = Array.Find(loaded, x => x != null && x.itemId == itemId);
                if (r != null) return r;
            }
        }
        catch { }

        return null;
    }
}