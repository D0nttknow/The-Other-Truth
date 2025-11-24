using System.Collections;
using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// WeaponHandler - Component to manage weapon effects and temporary stat buffs.
    /// 
    /// WIRING INSTRUCTIONS:
    /// 1. Attach to the same GameObject as CharacterEquipment
    /// 2. Assign a WeaponDefinition in the Inspector (or leave empty for no weapon)
    /// 3. Call OnUse() before executing attacks to apply weapon effects
    /// 
    /// INTEGRATION WITH COMBAT:
    /// In PerCharacterUIController or your combat system, before calling DoNormalAttack():
    ///   var weaponHandler = playerEquipment.GetComponent<WeaponHandler>();
    ///   if (weaponHandler != null) weaponHandler.OnUse();
    ///   playerEquipment.DoNormalAttack(...);
    /// 
    /// The weapon handler will:
    /// - Apply damage multiplier (you need to read currentDamageMultiplier in your attack calculation)
    /// - Apply speed buff/debuff temporarily via coroutine
    /// - Automatically reset effects after duration
    /// </summary>
    public class WeaponHandler : MonoBehaviour
    {
        [Header("Weapon Configuration")]
        [Tooltip("Current weapon equipped (assign a WeaponDefinition ScriptableObject)")]
        public WeaponDefinition currentWeapon;

        [Header("Runtime State")]
        [Tooltip("Current damage multiplier (modified by weapon)")]
        public float currentDamageMultiplier = 1.0f;

        [Tooltip("Current speed modifier percentage (modified by weapon)")]
        public float currentSpeedModPercent = 0f;

        private PlayerStat _playerStat;
        private CharacterEquipment _characterEquipment;
        private Coroutine _speedBuffCoroutine;
        private int _turnsRemaining = 0;
        private float _originalSpeed = 0f;
        private bool _buffActive = false;

        void Awake()
        {
            _playerStat = GetComponent<PlayerStat>();
            _characterEquipment = GetComponent<CharacterEquipment>();
        }

        void Start()
        {
            Debug.Log($"[WeaponHandler] Initialized on {gameObject.name} with weapon: {(currentWeapon != null ? currentWeapon.displayName : "None")}");
            
            if (currentWeapon != null)
            {
                currentDamageMultiplier = currentWeapon.damageMultiplier;
            }
        }

        /// <summary>
        /// Swap to a different weapon definition
        /// </summary>
        public void SwapWeapon(WeaponDefinition newWeapon)
        {
            if (currentWeapon == newWeapon)
            {
                Debug.Log($"[WeaponHandler] Already equipped with {newWeapon.displayName}");
                return;
            }

            // Clear any active buffs
            if (_buffActive)
            {
                ResetSpeedBuff();
            }

            currentWeapon = newWeapon;
            
            if (currentWeapon != null)
            {
                currentDamageMultiplier = currentWeapon.damageMultiplier;
                Debug.Log($"[WeaponHandler] Swapped to {currentWeapon.displayName} (Damage: {currentDamageMultiplier}x, Speed: {currentWeapon.speedModPercent}%)");
            }
            else
            {
                currentDamageMultiplier = 1.0f;
                Debug.Log("[WeaponHandler] Unequipped weapon");
            }
        }

        /// <summary>
        /// Called before using attack or skill. Applies weapon effects.
        /// </summary>
        public void OnUse()
        {
            if (currentWeapon == null)
            {
                Debug.Log("[WeaponHandler] No weapon equipped, no effects applied.");
                return;
            }

            Debug.Log($"[WeaponHandler] {currentWeapon.displayName} activated! Damage: {currentDamageMultiplier}x");

            // Apply damage multiplier (this should be read by combat system)
            currentDamageMultiplier = currentWeapon.damageMultiplier;

            // Apply speed modifier if not zero
            if (currentWeapon.speedModPercent != 0)
            {
                ApplySpeedBuff(currentWeapon.speedModPercent, currentWeapon.durationTurns);
            }
        }

        /// <summary>
        /// Apply temporary speed buff/debuff
        /// </summary>
        private void ApplySpeedBuff(float speedPercent, int turns)
        {
            // Cancel any existing buff
            if (_speedBuffCoroutine != null)
            {
                StopCoroutine(_speedBuffCoroutine);
                ResetSpeedBuff();
            }

            currentSpeedModPercent = speedPercent;
            _turnsRemaining = turns;

            // Try to apply speed modification
            // Note: This requires your PlayerStat to have a speed field
            if (_playerStat != null)
            {
                try
                {
                    var speedField = _playerStat.GetType().GetField("speed");
                    if (speedField != null)
                    {
                        _originalSpeed = (float)(int)speedField.GetValue(_playerStat);
                        float modifier = 1.0f + (speedPercent / 100f);
                        int newSpeed = Mathf.RoundToInt(_originalSpeed * modifier);
                        speedField.SetValue(_playerStat, newSpeed);
                        _buffActive = true;

                        Debug.Log($"[WeaponHandler] Speed modified by {speedPercent}% for {turns} turn(s). {_originalSpeed} -> {newSpeed}");

                        // Start coroutine to reset after duration
                        // Note: This is a time-based reset. For turn-based, you'd need to hook into TurnBaseSystem
                        float durationSeconds = turns * 3.0f; // Assume ~3 seconds per turn
                        _speedBuffCoroutine = StartCoroutine(SpeedBuffCoroutine(durationSeconds));
                    }
                    else
                    {
                        Debug.LogWarning("[WeaponHandler] Could not find speed field on PlayerStat. Speed modifier not applied.");
                        Debug.Log($"[WeaponHandler] Would apply {speedPercent}% speed modifier for {turns} turn(s)");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[WeaponHandler] Error applying speed buff: {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"[WeaponHandler] No PlayerStat found. Speed modifier {speedPercent}% would be applied for {turns} turn(s)");
            }
        }

        /// <summary>
        /// Coroutine to reset speed buff after duration
        /// </summary>
        private IEnumerator SpeedBuffCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ResetSpeedBuff();
        }

        /// <summary>
        /// Reset speed to original value
        /// </summary>
        private void ResetSpeedBuff()
        {
            if (!_buffActive) return;

            if (_playerStat != null)
            {
                try
                {
                    var speedField = _playerStat.GetType().GetField("speed");
                    if (speedField != null && _originalSpeed > 0)
                    {
                        speedField.SetValue(_playerStat, (int)_originalSpeed);
                        Debug.Log($"[WeaponHandler] Speed buff expired. Reset to {_originalSpeed}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[WeaponHandler] Error resetting speed: {ex.Message}");
                }
            }

            currentSpeedModPercent = 0f;
            _buffActive = false;
            _turnsRemaining = 0;
            _speedBuffCoroutine = null;
        }

        /// <summary>
        /// Subscribe to TurnBaseSystem events to reset on player turn end (optional enhancement)
        /// </summary>
        public void OnTurnEnd()
        {
            if (_turnsRemaining > 0)
            {
                _turnsRemaining--;
                Debug.Log($"[WeaponHandler] Turn ended. Buff remaining: {_turnsRemaining} turn(s)");
                
                if (_turnsRemaining <= 0)
                {
                    ResetSpeedBuff();
                }
            }
        }

        /// <summary>
        /// Get current effective damage for an attack
        /// </summary>
        public float GetEffectiveDamageMultiplier()
        {
            return currentDamageMultiplier;
        }

        #region Debug Methods

        [ContextMenu("Debug: Trigger Weapon Effect")]
        public void DebugTriggerEffect()
        {
            OnUse();
        }

        [ContextMenu("Debug: Equip Sword")]
        public void DebugEquipSword()
        {
            SwapWeapon(WeaponDefinition.CreateSword());
        }

        [ContextMenu("Debug: Equip Cudgel")]
        public void DebugEquipCudgel()
        {
            SwapWeapon(WeaponDefinition.CreateCudgel());
        }

        #endregion
    }
}
