using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// WeaponDefinition - ScriptableObject defining weapon types and their properties.
    /// 
    /// USAGE:
    /// 1. Create via: Right-click in Project > Create > Gameplay/Weapon Definition
    /// 2. Configure weapon properties (type, damage multiplier, speed modifier, duration)
    /// 3. Assign to WeaponHandler on character
    /// 
    /// PREDEFINED WEAPONS:
    /// - Sword: damageMultiplier=1.25, speedModPercent=+25%, durationTurns=1
    /// - Cudgel/Hammer: damageMultiplier=2.5, speedModPercent=-25%, durationTurns=1
    /// 
    /// The speedModPercent is applied temporarily when the weapon is used.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Gameplay/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        public enum WeaponType
        {
            None,
            Sword,
            Cudgel,
            Hammer,
            Axe,
            Spear,
            Bow,
            Staff
        }

        [Header("Weapon Identity")]
        [Tooltip("Type of weapon")]
        public WeaponType weaponType = WeaponType.Sword;

        [Tooltip("Display name of the weapon")]
        public string displayName = "Weapon";

        [Header("Combat Properties")]
        [Tooltip("Damage multiplier applied to base attack (1.0 = normal, 1.25 = +25% damage)")]
        public float damageMultiplier = 1.0f;

        [Tooltip("Speed modifier percentage (-25 = -25% speed, +25 = +25% speed)")]
        [Range(-100f, 100f)]
        public float speedModPercent = 0f;

        [Tooltip("Number of turns the speed modifier lasts (typically 1)")]
        public int durationTurns = 1;

        [Header("Visual")]
        [Tooltip("Icon for UI display (optional)")]
        public Sprite icon;

        [Header("Description")]
        [TextArea(2, 4)]
        public string description = "A basic weapon.";

        /// <summary>
        /// Get formatted description with stats
        /// </summary>
        public string GetFormattedDescription()
        {
            string desc = $"{description}\n\n";
            desc += $"Damage: {damageMultiplier:F2}x\n";
            
            if (speedModPercent > 0)
                desc += $"Speed: +{speedModPercent}%\n";
            else if (speedModPercent < 0)
                desc += $"Speed: {speedModPercent}%\n";
            
            desc += $"Duration: {durationTurns} turn(s)";
            return desc;
        }

        #region Static Factory Methods

        /// <summary>
        /// Create a Sword weapon definition at runtime (for testing)
        /// </summary>
        public static WeaponDefinition CreateSword()
        {
            var sword = CreateInstance<WeaponDefinition>();
            sword.weaponType = WeaponType.Sword;
            sword.displayName = "Sword";
            sword.damageMultiplier = 1.25f;
            sword.speedModPercent = 25f;
            sword.durationTurns = 1;
            sword.description = "A balanced blade that increases attack speed.";
            return sword;
        }

        /// <summary>
        /// Create a Cudgel weapon definition at runtime (for testing)
        /// </summary>
        public static WeaponDefinition CreateCudgel()
        {
            var cudgel = CreateInstance<WeaponDefinition>();
            cudgel.weaponType = WeaponType.Cudgel;
            cudgel.displayName = "Cudgel";
            cudgel.damageMultiplier = 2.5f;
            cudgel.speedModPercent = -25f;
            cudgel.durationTurns = 1;
            cudgel.description = "A heavy weapon that deals massive damage but slows attack speed.";
            return cudgel;
        }

        #endregion
    }
}
