using System.Collections.Generic;
using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// LootGenerator - Helper class to generate random item drops from monsters.
    /// 
    /// WIRING INSTRUCTIONS:
    /// 1. Create consumable items in the Unity Editor:
    ///    - Right-click > Create > Gameplay/Consumable Heal (save as "HealPotion")
    ///    - Right-click > Create > Gameplay/Consumable ATK (save as "AtkPotion")
    ///    - Right-click > Create > Gameplay/Consumable DEF (save as "DefPotion")
    /// 2. Assign these items to a LootGenerator instance or use the static methods
    /// 3. Call from monster death logic to add items to inventory
    /// 
    /// INTEGRATION EXAMPLE:
    /// In your monster's OnDeath() method:
    ///   var loot = LootGenerator.GenerateRandomConsumable();
    ///   if (loot != null && InventoryManager.Instance != null)
    ///   {
    ///       InventoryManager.Instance.AddItem(loot);
    ///   }
    /// 
    /// Or for multiple items:
    ///   var loots = LootGenerator.GenerateRandomConsumables(3);
    ///   foreach (var item in loots)
    ///   {
    ///       InventoryManager.Instance.AddItem(item);
    ///   }
    /// </summary>
    public class LootGenerator : MonoBehaviour
    {
        [Header("Consumable Item Pool")]
        [Tooltip("Assign your ConsumableHeal assets here")]
        public List<ConsumableHeal> healPotions = new List<ConsumableHeal>();

        [Tooltip("Assign your ConsumableAtk assets here")]
        public List<ConsumableAtk> atkPotions = new List<ConsumableAtk>();

        [Tooltip("Assign your ConsumableDef assets here")]
        public List<ConsumableDef> defPotions = new List<ConsumableDef>();

        [Header("Drop Rates (%)")]
        [Range(0, 100)]
        [Tooltip("Chance to drop a consumable item on death")]
        public float dropChance = 50f;

        [Header("Drop Weights")]
        [Tooltip("Weight for HP potion drops (higher = more common)")]
        public int healWeight = 50;

        [Tooltip("Weight for ATK potion drops")]
        public int atkWeight = 25;

        [Tooltip("Weight for DEF potion drops")]
        public int defWeight = 25;

        // Static instance for easy access
        public static LootGenerator Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[LootGenerator] Multiple instances detected!");
            }
        }

        /// <summary>
        /// Generate a single random consumable item
        /// </summary>
        public ItemBase GenerateRandomItem()
        {
            // Check drop chance
            if (Random.Range(0f, 100f) > dropChance)
            {
                Debug.Log("[LootGenerator] No loot dropped (failed drop chance)");
                return null;
            }

            // Calculate total weight
            int totalWeight = healWeight + atkWeight + defWeight;
            if (totalWeight <= 0)
            {
                Debug.LogWarning("[LootGenerator] Total weight is 0, cannot generate loot!");
                return null;
            }

            // Weighted random selection
            int roll = Random.Range(0, totalWeight);
            
            if (roll < healWeight)
            {
                return GetRandomFromList(healPotions);
            }
            else if (roll < healWeight + atkWeight)
            {
                return GetRandomFromList(atkPotions);
            }
            else
            {
                return GetRandomFromList(defPotions);
            }
        }

        /// <summary>
        /// Generate multiple random consumable items
        /// </summary>
        public List<ItemBase> GenerateRandomItems(int count)
        {
            var items = new List<ItemBase>();
            for (int i = 0; i < count; i++)
            {
                var item = GenerateRandomItem();
                if (item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        /// <summary>
        /// Get a random item from a list
        /// </summary>
        private ItemBase GetRandomFromList<T>(List<T> list) where T : ItemBase
        {
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[LootGenerator] Empty loot pool for type {typeof(T).Name}");
                return null;
            }

            int index = Random.Range(0, list.Count);
            return list[index];
        }

        #region Static Methods (for use without instance)

        /// <summary>
        /// Static method to generate a random consumable using the instance
        /// </summary>
        public static ItemBase GenerateRandomConsumable()
        {
            if (Instance != null)
            {
                return Instance.GenerateRandomItem();
            }
            else
            {
                Debug.LogWarning("[LootGenerator] No instance found! Generating fallback runtime item.");
                return GenerateFallbackItem();
            }
        }

        /// <summary>
        /// Static method to generate multiple random consumables
        /// </summary>
        public static List<ItemBase> GenerateRandomConsumables(int count)
        {
            if (Instance != null)
            {
                return Instance.GenerateRandomItems(count);
            }
            else
            {
                Debug.LogWarning("[LootGenerator] No instance found! Generating fallback items.");
                var items = new List<ItemBase>();
                for (int i = 0; i < count; i++)
                {
                    items.Add(GenerateFallbackItem());
                }
                return items;
            }
        }

        /// <summary>
        /// Generate a fallback item at runtime (for when no LootGenerator instance exists)
        /// </summary>
        private static ItemBase GenerateFallbackItem()
        {
            int roll = Random.Range(0, 3);
            
            ItemBase item;
            switch (roll)
            {
                case 0:
                    item = ScriptableObject.CreateInstance<ConsumableHeal>();
                    item.itemID = "heal_potion_runtime";
                    item.displayName = "Heal Potion";
                    item.description = "Restores HP.";
                    break;
                case 1:
                    item = ScriptableObject.CreateInstance<ConsumableAtk>();
                    item.itemID = "atk_potion_runtime";
                    item.displayName = "ATK Potion";
                    item.description = "Increases ATK.";
                    break;
                default:
                    item = ScriptableObject.CreateInstance<ConsumableDef>();
                    item.itemID = "def_potion_runtime";
                    item.displayName = "DEF Potion";
                    item.description = "Increases DEF.";
                    break;
            }
            
            Debug.Log($"[LootGenerator] Generated fallback item: {item.displayName}");
            return item;
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug: Generate 1 Random Item")]
        public void DebugGenerateOne()
        {
            var item = GenerateRandomItem();
            if (item != null)
            {
                Debug.Log($"[LootGenerator] Generated: {item.displayName}");
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(item);
                }
            }
        }

        [ContextMenu("Debug: Generate 5 Random Items")]
        public void DebugGenerateFive()
        {
            var items = GenerateRandomItems(5);
            Debug.Log($"[LootGenerator] Generated {items.Count} items");
            if (InventoryManager.Instance != null)
            {
                foreach (var item in items)
                {
                    InventoryManager.Instance.AddItem(item);
                }
            }
        }

        #endregion
    }
}
