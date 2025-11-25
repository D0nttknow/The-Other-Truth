using UnityEngine;

public enum WeaponCategory { Sword, Hammer }

[CreateAssetMenu(fileName = "WeaponItem", menuName = "Game/Items/Weapon")]
public class WeaponItem : ScriptableObject
{
    [Header("Identity")]
    public string id; // unique id for save/load
    public string displayName;

    [Header("Visual")]
    public Sprite icon;
    public GameObject weaponPrefab; // optional visual prefab for mounting
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Category")]
    public WeaponCategory category = WeaponCategory.Sword;

    [Header("Gameplay")]
    public int baseDamage = 5;

    // Sword-specific defaults
    [Range(0f, 1f)] public float normalBleedChance = 0.3f;
    [Range(0f, 1f)] public float skillBleedChance = 0.3f;
    public int swordSkillCooldownTurns = 2;

    // bleed fields (used by WeaponController / SwordWeapon)
    [Header("Bleed Settings")]
    public int bleedDuration = 2;
    public int bleedDmgPerTurn = 2;

    // Hammer-specific defaults
    [Header("Hammer Settings")]
    [Range(0f, 1f)] public float hammerStunChance = 0.5f;
    public int hammerSkillCooldownTurns = 2;
    public int hammerSkillTargetCount = 1; // number of enemies to attempt to stun (here 1)

    // generic skill damage if you want separate values:
    [Header("Skill")]
    public int skillDamage = 0;
}