using System.Collections.Generic;
using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// PartyAutoAttack - Enables automatic attacks for non-player party members.
    /// 
    /// WIRING INSTRUCTIONS:
    /// 1. Attach to TurnBaseSystem GameObject or create a separate PartyManager GameObject
    /// 2. Enable/disable autoAttackEnabled to toggle auto-attack behavior
    /// 3. Enable autoLevelUp to automatically allocate stat points when player levels
    /// 
    /// INTEGRATION:
    /// This component should be called during the turn system's update loop:
    /// - In TurnBaseSystem, when a non-player party member's turn starts, call ProcessPartyMemberTurn()
    /// - For auto-level-up, subscribe to PlayerLevel.OnLevelUp event
    /// 
    /// Example in TurnBaseSystem:
    ///   var autoAttack = GetComponent<PartyAutoAttack>();
    ///   if (autoAttack != null && !IsPlayerControlled(currentBattler))
    ///       autoAttack.ProcessPartyMemberTurn(currentBattler);
    /// </summary>
    public class PartyAutoAttack : MonoBehaviour
    {
        [Header("Auto-Attack Settings")]
        [Tooltip("Enable automatic attacks for party members")]
        public bool autoAttackEnabled = true;

        [Header("Auto-Level-Up Settings")]
        [Tooltip("Enable automatic stat allocation on level up")]
        public bool autoLevelUpEnabled = false;

        [Tooltip("Reference to main character's PlayerLevel component")]
        public PlayerLevel playerLevel;

        private TurnBaseSystem _turnBaseSystem;

        void Awake()
        {
            _turnBaseSystem = GetComponent<TurnBaseSystem>();
            if (_turnBaseSystem == null)
            {
                _turnBaseSystem = TurnBaseSystem.Instance;
            }
        }

        void Start()
        {
            Debug.Log($"[PartyAutoAttack] Initialized. AutoAttack: {autoAttackEnabled}, AutoLevelUp: {autoLevelUpEnabled}");

            // Subscribe to player level up if enabled
            if (autoLevelUpEnabled && playerLevel != null)
            {
                playerLevel.OnLevelUp += OnPlayerLevelUp;
                Debug.Log("[PartyAutoAttack] Subscribed to PlayerLevel.OnLevelUp");
            }
            else if (autoLevelUpEnabled && playerLevel == null)
            {
                Debug.LogWarning("[PartyAutoAttack] AutoLevelUp enabled but PlayerLevel reference is not set!");
            }
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (playerLevel != null)
            {
                playerLevel.OnLevelUp -= OnPlayerLevelUp;
            }
        }

        /// <summary>
        /// Process a party member's turn with auto-attack logic
        /// </summary>
        public void ProcessPartyMemberTurn(GameObject partyMember)
        {
            if (!autoAttackEnabled)
            {
                Debug.Log("[PartyAutoAttack] Auto-attack is disabled.");
                return;
            }

            if (partyMember == null)
            {
                Debug.LogWarning("[PartyAutoAttack] Party member is null!");
                return;
            }

            Debug.Log($"[PartyAutoAttack] Processing auto-attack for {partyMember.name}");

            // Get CharacterEquipment component
            var equipment = partyMember.GetComponent<CharacterEquipment>();
            if (equipment == null)
            {
                Debug.LogWarning($"[PartyAutoAttack] No CharacterEquipment found on {partyMember.name}");
                return;
            }

            // Find available monsters to attack
            GameObject targetMonster = FindTargetMonster();
            if (targetMonster == null)
            {
                Debug.Log($"[PartyAutoAttack] No available monsters to attack for {partyMember.name}");
                return;
            }

            // Execute normal attack
            Debug.Log($"[PartyAutoAttack] {partyMember.name} auto-attacking {targetMonster.name}");
            
            try
            {
                // Try to call DoNormalAttack if it exists
                var method = equipment.GetType().GetMethod("DoNormalAttack");
                if (method != null)
                {
                    // Attempt to invoke DoNormalAttack with the target
                    method.Invoke(equipment, new object[] { targetMonster, null });
                    Debug.Log($"[PartyAutoAttack] {partyMember.name} executed normal attack on {targetMonster.name}");
                }
                else
                {
                    Debug.LogWarning($"[PartyAutoAttack] DoNormalAttack method not found on {equipment.GetType().Name}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PartyAutoAttack] Error executing auto-attack: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the best target monster for auto-attack
        /// Priority: lowest HP monster that is alive
        /// </summary>
        private GameObject FindTargetMonster()
        {
            if (_turnBaseSystem == null)
            {
                Debug.LogWarning("[PartyAutoAttack] TurnBaseSystem reference is null!");
                return null;
            }

            GameObject bestTarget = null;
            int lowestHp = int.MaxValue;

            // Search through battlers for monsters (non-player characters)
            foreach (var battlerObj in _turnBaseSystem.battlerObjects)
            {
                if (battlerObj == null) continue;

                // Skip if it's a player character (has PlayerStat component)
                if (battlerObj.GetComponent<PlayerStat>() != null)
                    continue;

                // Try to get HP from various possible components
                int hp = GetMonsterHP(battlerObj);
                if (hp > 0 && hp < lowestHp)
                {
                    lowestHp = hp;
                    bestTarget = battlerObj;
                }
            }

            if (bestTarget != null)
            {
                Debug.Log($"[PartyAutoAttack] Selected target: {bestTarget.name} (HP: {lowestHp})");
            }

            return bestTarget;
        }

        /// <summary>
        /// Try to get HP from a monster GameObject
        /// </summary>
        private int GetMonsterHP(GameObject monster)
        {
            if (monster == null) return 0;

            // Try EnemyStats component
            var enemyStats = monster.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                return enemyStats.hp;
            }

            // Try to find hp field via reflection
            var components = monster.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                var hpField = comp.GetType().GetField("hp");
                if (hpField != null)
                {
                    try
                    {
                        return (int)hpField.GetValue(comp);
                    }
                    catch { }
                }

                var monsterHpField = comp.GetType().GetField("monsterHp");
                if (monsterHpField != null)
                {
                    try
                    {
                        return (int)monsterHpField.GetValue(comp);
                    }
                    catch { }
                }
            }

            return 0;
        }

        /// <summary>
        /// Called when player levels up (if auto-level-up is enabled)
        /// </summary>
        private void OnPlayerLevelUp(int newLevel)
        {
            if (!autoLevelUpEnabled || playerLevel == null)
                return;

            Debug.Log($"[PartyAutoAttack] Player reached level {newLevel}! Auto-allocating stat points...");
            
            // Trigger auto-allocation
            if (playerLevel.autoAllocate)
            {
                playerLevel.AutoAllocate();
            }
            else
            {
                Debug.LogWarning("[PartyAutoAttack] AutoLevelUp enabled but PlayerLevel.autoAllocate is false!");
            }
        }

        #region Public API

        /// <summary>
        /// Enable/disable auto-attack at runtime
        /// </summary>
        public void SetAutoAttackEnabled(bool enabled)
        {
            autoAttackEnabled = enabled;
            Debug.Log($"[PartyAutoAttack] Auto-attack {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Enable/disable auto-level-up at runtime
        /// </summary>
        public void SetAutoLevelUpEnabled(bool enabled)
        {
            autoLevelUpEnabled = enabled;
            Debug.Log($"[PartyAutoAttack] Auto-level-up {(enabled ? "enabled" : "disabled")}");
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug: Process Current Battler")]
        public void DebugProcessCurrentBattler()
        {
            if (_turnBaseSystem != null && _turnBaseSystem.CurrentBattlerObject != null)
            {
                ProcessPartyMemberTurn(_turnBaseSystem.CurrentBattlerObject);
            }
            else
            {
                Debug.LogWarning("[PartyAutoAttack] No current battler to process!");
            }
        }

        #endregion
    }
}
