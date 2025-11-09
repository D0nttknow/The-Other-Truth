using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple InventoryManager (singleton)
/// - Stores item id -> count
/// - API: AddItem, RemoveItem, GetCount, HasItem
/// - Event OnInventoryChanged invoked when inventory changes
/// - Simple persistence via PlayerPrefs (JSON)
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // Serializable entry for saving/loading
    [Serializable]
    public class Entry { public string id; public int count; public Entry(string i, int c) { id = i; count = c; } }

    [Header("Data")]
    // runtime storage (keeps insertion order)
    public List<Entry> entries = new List<Entry>();

    // quick lookup
    private Dictionary<string, int> map = new Dictionary<string, int>();

    public event Action OnInventoryChanged;

    const string SaveKey = "Inventory_v1";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // Add item by id (from ItemDefinition.id), returns true if added
    public bool AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;

        if (map.ContainsKey(itemId))
        {
            map[itemId] += amount;
            // update entries list
            var e = entries.Find(x => x.id == itemId);
            if (e != null) e.count = map[itemId];
        }
        else
        {
            map[itemId] = amount;
            entries.Add(new Entry(itemId, amount));
        }

        OnInventoryChanged?.Invoke();
        Save();
        return true;
    }

    // Remove up to amount, return true if at least one removed
    public bool RemoveItem(string itemId, int amount = 1)
    {
        if (!map.ContainsKey(itemId) || amount <= 0) return false;

        int have = map[itemId];
        int remove = Mathf.Min(have, amount);
        have -= remove;
        if (have <= 0)
        {
            map.Remove(itemId);
            entries.RemoveAll(x => x.id == itemId);
        }
        else
        {
            map[itemId] = have;
            var e = entries.Find(x => x.id == itemId);
            if (e != null) e.count = have;
        }

        OnInventoryChanged?.Invoke();
        Save();
        return remove > 0;
    }

    public int GetCount(string itemId)
    {
        if (map.TryGetValue(itemId, out int c)) return c;
        return 0;
    }

    public bool HasItem(string itemId, int minAmount = 1)
    {
        return GetCount(itemId) >= minAmount;
    }

    public void Clear()
    {
        entries.Clear();
        map.Clear();
        OnInventoryChanged?.Invoke();
        Save();
    }

    // persistence (simple)
    [System.Serializable]
    class SaveData { public List<Entry> items = new List<Entry>(); }

    public void Save()
    {
        try
        {
            var sd = new SaveData();
            sd.items = entries;
            string json = JsonUtility.ToJson(sd);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            // Debug.Log("[Inventory] Saved " + json);
        }
        catch (System.Exception ex) { Debug.LogWarning("[Inventory] Save failed: " + ex); }
    }

    public void Load()
    {
        try
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            string json = PlayerPrefs.GetString(SaveKey);
            var sd = JsonUtility.FromJson<SaveData>(json);
            if (sd?.items != null)
            {
                entries = new List<Entry>(sd.items);
                map.Clear();
                foreach (var e in entries) map[e.id] = e.count;
                OnInventoryChanged?.Invoke();
            }
        }
        catch (System.Exception ex) { Debug.LogWarning("[Inventory] Load failed: " + ex); }
    }
}