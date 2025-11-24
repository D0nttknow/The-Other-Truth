using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory singleton เก็บไอเท็ม (consumables)
/// - AddItem(ItemBase) เพิ่มเข้า inventory
/// - UseItemAt(index, target) เรียก Use บนไอเท็ม ถ้าถูก consume จะลดจำนวน/ลบ
/// - UI สามารถ subscribe OnInventoryChanged เพื่ออัปเดต
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Serializable]
    public class InventoryEntry
    {
        public ItemBase item;
        public int count = 1;
    }

    public List<InventoryEntry> items = new List<InventoryEntry>();
    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    public void AddItem(ItemBase it)
    {
        if (it == null) return;
        var e = items.Find(x => x.item == it);
        if (e == null)
        {
            items.Add(new InventoryEntry() { item = it, count = 1 });
        }
        else
        {
            e.count++;
        }
        Debug.LogFormat("[Inventory] AddItem {0} -> count={1}", it.displayName, items.Find(x => x.item == it).count);
        OnInventoryChanged?.Invoke();
    }

    public void RemoveItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        OnInventoryChanged?.Invoke();
    }

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
}