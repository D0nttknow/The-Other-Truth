using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// InventoryManager - Singleton for managing player inventory with ItemBase objects.
    /// 
    /// WIRING INSTRUCTIONS:
    /// 1. Create an empty GameObject in your scene named "InventoryManager"
    /// 2. Attach this script to it
    /// 3. The object will persist across scenes (DontDestroyOnLoad)
    /// 4. Access via InventoryManager.Instance from anywhere
    /// 
    /// USAGE:
    /// - Add items: InventoryManager.Instance.AddItem(itemBase)
    /// - Remove items: InventoryManager.Instance.RemoveItem(index)
    /// - Use items: InventoryManager.Instance.UseItem(index, targetCharacter)
    /// 
    /// INTEGRATION:
    /// - Subscribe to OnInventoryChanged event to update UI
    /// - When monster dies, call AddItem with a random consumable (see LootGenerator)
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [Tooltip("Maximum number of item slots")]
        public int maxSlots = 20;

        [Header("Current Inventory")]
        [Tooltip("List of items in inventory (can be same item multiple times)")]
        public List<ItemBase> items = new List<ItemBase>();

        // Events
        public event Action OnInventoryChanged;
        public event Action<ItemBase, bool> OnItemUsed; // (item, success)

        void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InventoryManager] Multiple instances detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[InventoryManager] Initialized successfully.");
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Add an item to the inventory. Returns true if successful.
        /// </summary>
        public bool AddItem(ItemBase item)
        {
            if (item == null)
            {
                Debug.LogWarning("[InventoryManager] Attempted to add null item!");
                return false;
            }

            if (items.Count >= maxSlots)
            {
                Debug.LogWarning($"[InventoryManager] Inventory full! Cannot add {item.displayName}");
                return false;
            }

            items.Add(item);
            Debug.Log($"[InventoryManager] Added {item.displayName} to inventory. Total items: {items.Count}/{maxSlots}");
            
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Remove an item at the specified index. Returns true if successful.
        /// </summary>
        public bool RemoveItem(int index)
        {
            if (index < 0 || index >= items.Count)
            {
                Debug.LogWarning($"[InventoryManager] Invalid index {index} for removal!");
                return false;
            }

            var item = items[index];
            items.RemoveAt(index);
            Debug.Log($"[InventoryManager] Removed {item.displayName} from inventory. Total items: {items.Count}/{maxSlots}");
            
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Use an item at the specified index on a target character.
        /// Returns true if the item was used successfully.
        /// </summary>
        public bool UseItem(int index, CharacterEquipment target)
        {
            if (index < 0 || index >= items.Count)
            {
                Debug.LogWarning($"[InventoryManager] Invalid index {index} for use!");
                return false;
            }

            if (target == null)
            {
                Debug.LogWarning("[InventoryManager] Target is null!");
                return false;
            }

            var item = items[index];
            bool success = item.Use(target);

            if (success)
            {
                Debug.Log($"[InventoryManager] Successfully used {item.displayName} on {target.gameObject.name}");
                RemoveItem(index);
            }
            else
            {
                Debug.LogWarning($"[InventoryManager] Failed to use {item.displayName}");
            }

            OnItemUsed?.Invoke(item, success);
            return success;
        }

        /// <summary>
        /// Get item at index (for UI display)
        /// </summary>
        public ItemBase GetItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return null;
            return items[index];
        }

        /// <summary>
        /// Get number of items in inventory
        /// </summary>
        public int GetItemCount()
        {
            return items.Count;
        }

        /// <summary>
        /// Clear all items from inventory
        /// </summary>
        public void ClearInventory()
        {
            items.Clear();
            Debug.Log("[InventoryManager] Inventory cleared.");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Check if inventory has space
        /// </summary>
        public bool HasSpace(int count = 1)
        {
            return (items.Count + count) <= maxSlots;
        }

        #region Debug Methods

        [ContextMenu("Debug: Print Inventory")]
        public void DebugPrintInventory()
        {
            Debug.Log($"[InventoryManager] === Inventory ({items.Count}/{maxSlots}) ===");
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Debug.Log($"  [{i}] {item.displayName} ({item.itemID})");
            }
        }

        [ContextMenu("Debug: Clear Inventory")]
        public void DebugClearInventory()
        {
            ClearInventory();
        }

        #endregion
    }
}
