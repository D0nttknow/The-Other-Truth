using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// ItemBase - Base ScriptableObject for all items in the game.
    /// 
    /// USAGE:
    /// 1. Create items via: Right-click in Project > Create > Gameplay/ItemBase
    /// 2. For consumables, use the specific consumable types (ConsumableHeal, etc.)
    /// 3. Assign unique IDs to each item for inventory tracking
    /// 4. Set display name, description, and icon as needed
    /// 
    /// This is a simple base class that can be extended for different item types.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Gameplay/ItemBase")]
    public class ItemBase : ScriptableObject
    {
        [Header("Item Identification")]
        [Tooltip("Unique ID for this item (e.g., 'potion_hp_small')")]
        public string itemID = "item_001";

        [Tooltip("Display name shown in UI")]
        public string displayName = "Item";

        [Header("Visual")]
        [Tooltip("Icon displayed in inventory UI (optional)")]
        public Sprite icon;

        [Header("Description")]
        [TextArea(3, 5)]
        [Tooltip("Description of item effects")]
        public string description = "A basic item.";

        /// <summary>
        /// Virtual method for item use logic. Override in derived classes.
        /// Returns true if item was consumed/used successfully.
        /// </summary>
        public virtual bool Use(CharacterEquipment target)
        {
            Debug.Log($"[ItemBase] Used {displayName} (base implementation - does nothing)");
            return false;
        }

        /// <summary>
        /// Get a formatted description for display in UI
        /// </summary>
        public virtual string GetFormattedDescription()
        {
            return description;
        }
    }
}
