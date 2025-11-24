using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// ConsumableDef - Increases DEF stat by 1-3 points.
    /// 
    /// USAGE:
    /// 1. Create via: Right-click in Project > Create > Gameplay/Consumable DEF
    /// 2. Set itemID, displayName, and icon
    /// 3. Use from inventory by calling Use(CharacterEquipment)
    /// 
    /// WIRING:
    /// - Requires the target to have stat tracking (PlayerStat or similar)
    /// - Modify the Use() method below to match your actual stat system
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableDef", menuName = "Gameplay/Consumable DEF")]
    public class ConsumableDef : ItemBase
    {
        [Header("DEF Boost Settings")]
        [Tooltip("Minimum DEF increase")]
        public int minBoost = 1;
        
        [Tooltip("Maximum DEF increase")]
        public int maxBoost = 3;

        public override bool Use(CharacterEquipment target)
        {
            if (target == null)
            {
                Debug.LogWarning("[ConsumableDef] Target is null!");
                return false;
            }

            // Calculate boost amount
            int boostAmount = Random.Range(minBoost, maxBoost + 1);

            // Try to apply DEF boost
            // Note: This is a placeholder implementation since we don't have direct access to DEF stat
            // You'll need to adjust this based on your actual stat system
            
            var playerStat = target.GetComponent<PlayerStat>();
            if (playerStat != null)
            {
                try
                {
                    // Try to find def or defense field
                    var defField = playerStat.GetType().GetField("def") ?? 
                                   playerStat.GetType().GetField("defense") ??
                                   playerStat.GetType().GetField("DEF") ??
                                   playerStat.GetType().GetField("Defense");
                    
                    if (defField != null)
                    {
                        int currentDef = (int)defField.GetValue(playerStat);
                        int newDef = currentDef + boostAmount;
                        defField.SetValue(playerStat, newDef);
                        
                        Debug.Log($"[ConsumableDef] {displayName} used on {target.gameObject.name}! DEF increased by {boostAmount} ({currentDef} -> {newDef})");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("[ConsumableDef] Could not find DEF field on PlayerStat. Please adjust the implementation.");
                        Debug.Log($"[ConsumableDef] {displayName} would increase DEF by {boostAmount} (implementation needed)");
                        return true; // Return true anyway so item is consumed
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ConsumableDef] Error applying DEF boost: {ex.Message}");
                    Debug.Log($"[ConsumableDef] {displayName} would increase DEF by {boostAmount} (implementation needed)");
                    return true;
                }
            }
            else
            {
                Debug.LogWarning($"[ConsumableDef] No PlayerStat found on {target.gameObject.name}");
                Debug.Log($"[ConsumableDef] {displayName} would increase DEF by {boostAmount} (implementation needed)");
                return true;
            }
        }

        public override string GetFormattedDescription()
        {
            return $"{description}\nIncreases DEF by {minBoost}-{maxBoost} permanently.";
        }
    }
}
