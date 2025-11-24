using System;
using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// PlayerLevel - Manages player leveling, XP, and stat point allocation.
    /// 
    /// WIRING INSTRUCTIONS:
    /// 1. Attach to main character GameObject (same GameObject that has CharacterEquipment/PlayerStat)
    /// 2. Configure in Inspector: maxLevel (default 20), pointsPerLevel (default 1), expBase, expGrowth
    /// 3. To grant XP when monster dies, call: GetComponent<PlayerLevel>().AddExp(xpAmount)
    /// 4. To manually allocate stat points: AllocatePoint("HP"), AllocatePoint("ATK"), AllocatePoint("DEF")
    /// 5. For auto-allocation, enable autoAllocate and set priorities (hp/atk/def Priority fields)
    /// 
    /// Example integration with monster death:
    ///   In your monster's OnDeath() method:
    ///   var playerLevel = FindMainCharacter().GetComponent<PlayerLevel>();
    ///   if (playerLevel != null) playerLevel.AddExp(monsterExpValue);
    /// </summary>
    public class PlayerLevel : MonoBehaviour
    {
        [Header("Level Configuration")]
        [Tooltip("Current player level (starts at 1)")]
        public int Level = 1;
        
        [Tooltip("Maximum level (default 20)")]
        public int MaxLevel = 20;
        
        [Tooltip("Stat points awarded per level up")]
        public int pointsPerLevel = 1;

        [Header("Experience Configuration")]
        [Tooltip("Current accumulated experience")]
        public int CurrentExp = 0;
        
        [Tooltip("Base XP required for level 2 (formula: baseExp * expGrowth^(level-1))")]
        public int expBase = 100;
        
        [Tooltip("Exponential growth multiplier per level (1.2 = 20% more XP each level)")]
        public float expGrowth = 1.2f;

        [Header("Stat Points")]
        [Tooltip("Available stat points to allocate")]
        public int AvailableStatPoints = 0;

        [Header("Auto Allocation")]
        [Tooltip("If true, stat points are allocated automatically on level up")]
        public bool autoAllocate = false;
        
        [Tooltip("Priority for HP allocation (higher = more likely)")]
        [Range(0, 100)]
        public int hpPriority = 33;
        
        [Tooltip("Priority for ATK allocation (higher = more likely)")]
        [Range(0, 100)]
        public int atkPriority = 33;
        
        [Tooltip("Priority for DEF allocation (higher = more likely)")]
        [Range(0, 100)]
        public int defPriority = 34;

        // Events
        public event Action<int> OnLevelUp; // Invoked with new level
        public event Action<int, int> OnExpChanged; // Invoked with (currentExp, expToNext)
        public event Action<string, int> OnStatAllocated; // Invoked with (stat name, new value)

        private PlayerStat _playerStat;
        private CharacterEquipment _characterEquipment;

        /// <summary>
        /// XP required to reach next level from current level
        /// </summary>
        public int ExpToNext
        {
            get
            {
                if (Level >= MaxLevel) return int.MaxValue;
                double val = expBase * Math.Pow(expGrowth, Level - 1);
                return Mathf.Max(1, (int)Math.Round(val));
            }
        }

        void Awake()
        {
            _playerStat = GetComponent<PlayerStat>();
            _characterEquipment = GetComponent<CharacterEquipment>();
        }

        void Start()
        {
            Debug.Log($"[PlayerLevel] Initialized on {gameObject.name}. Level={Level}, MaxLevel={MaxLevel}, AvailablePoints={AvailableStatPoints}");
        }

        /// <summary>
        /// Add experience points. Returns number of levels gained.
        /// </summary>
        public int AddExp(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerLevel] AddExp called with invalid amount: {amount}");
                return 0;
            }

            if (Level >= MaxLevel)
            {
                Debug.Log($"[PlayerLevel] Already at max level {MaxLevel}, cannot gain more XP");
                return 0;
            }

            CurrentExp += amount;
            int levelsGained = 0;

            Debug.Log($"[PlayerLevel] {gameObject.name} gained {amount} XP. CurrentExp={CurrentExp}/{ExpToNext}");

            // Process level ups
            while (Level < MaxLevel && CurrentExp >= ExpToNext)
            {
                CurrentExp -= ExpToNext;
                Level++;
                levelsGained++;
                
                // Grant stat points
                AvailableStatPoints += pointsPerLevel;
                
                Debug.Log($"[PlayerLevel] LEVEL UP! {gameObject.name} reached Level {Level}! Gained {pointsPerLevel} stat points. Total available: {AvailableStatPoints}");
                
                OnLevelUp?.Invoke(Level);

                // Auto-allocate if enabled
                if (autoAllocate && AvailableStatPoints > 0)
                {
                    AutoAllocate();
                }
            }

            // Clamp exp if at max level
            if (Level >= MaxLevel)
            {
                CurrentExp = 0;
                Debug.Log($"[PlayerLevel] Reached max level {MaxLevel}!");
            }

            OnExpChanged?.Invoke(CurrentExp, ExpToNext);
            return levelsGained;
        }

        /// <summary>
        /// Manually allocate a stat point to HP, ATK, or DEF.
        /// </summary>
        public bool AllocatePoint(string stat)
        {
            if (AvailableStatPoints <= 0)
            {
                Debug.LogWarning($"[PlayerLevel] No stat points available to allocate!");
                return false;
            }

            if (_characterEquipment == null)
            {
                Debug.LogWarning($"[PlayerLevel] CharacterEquipment not found on {gameObject.name}");
                return false;
            }

            stat = stat.ToUpper();
            AvailableStatPoints--;

            // Apply stat increase based on stat type
            switch (stat)
            {
                case "HP":
                    if (_playerStat != null)
                    {
                        // Increase max HP (you may need to adjust this based on your PlayerStat implementation)
                        // For now, we'll just log it
                        Debug.Log($"[PlayerLevel] Allocated 1 point to HP (implementation depends on PlayerStat structure)");
                        OnStatAllocated?.Invoke("HP", 1);
                    }
                    break;

                case "ATK":
                case "ATTACK":
                    Debug.Log($"[PlayerLevel] Allocated 1 point to ATK (implementation depends on stat structure)");
                    OnStatAllocated?.Invoke("ATK", 1);
                    break;

                case "DEF":
                case "DEFENSE":
                    Debug.Log($"[PlayerLevel] Allocated 1 point to DEF (implementation depends on stat structure)");
                    OnStatAllocated?.Invoke("DEF", 1);
                    break;

                default:
                    Debug.LogWarning($"[PlayerLevel] Unknown stat type: {stat}. Valid options: HP, ATK, DEF");
                    AvailableStatPoints++; // Refund the point
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Automatically allocate all available stat points based on priority settings.
        /// </summary>
        public void AutoAllocate()
        {
            if (AvailableStatPoints <= 0) return;

            int totalPriority = hpPriority + atkPriority + defPriority;
            if (totalPriority <= 0)
            {
                Debug.LogWarning($"[PlayerLevel] Auto-allocate enabled but all priorities are 0!");
                return;
            }

            Debug.Log($"[PlayerLevel] Auto-allocating {AvailableStatPoints} stat points...");

            while (AvailableStatPoints > 0)
            {
                // Weighted random selection based on priorities
                int roll = UnityEngine.Random.Range(0, totalPriority);
                
                if (roll < hpPriority)
                {
                    AllocatePoint("HP");
                }
                else if (roll < hpPriority + atkPriority)
                {
                    AllocatePoint("ATK");
                }
                else
                {
                    AllocatePoint("DEF");
                }
            }

            Debug.Log($"[PlayerLevel] Auto-allocation complete.");
        }

        /// <summary>
        /// For debugging: grant a level up
        /// </summary>
        [ContextMenu("Debug: Level Up")]
        public void DebugLevelUp()
        {
            AddExp(ExpToNext);
        }

        /// <summary>
        /// For debugging: grant 5 levels
        /// </summary>
        [ContextMenu("Debug: Add 5 Levels")]
        public void DebugAdd5Levels()
        {
            for (int i = 0; i < 5; i++)
            {
                AddExp(ExpToNext);
            }
        }
    }
}
