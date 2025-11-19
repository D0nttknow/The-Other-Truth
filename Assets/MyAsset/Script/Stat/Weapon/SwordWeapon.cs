using System;
using System.Collections.Generic;
using UnityEngine;
using GameRaiwaa.Stat; // ถ้า BleedEffect/Status อยู่ที่นี่

/// <summary>
/// SwordWeapon: โค้ดตัวอย่างสำหรับการทำ damage + มีโอกาสทำให้ติด Bleed
/// - ใส่ Debug.Log เพื่อช่วยดีบัก flow
/// - พยายามใช้ reflection ในการเรียก StatusManager.ApplyStatus หาก type/ชื่อไม่ตรงกัน
/// - เรียก BleedingIntegrationExample.OnBleedingApplied / HealthBarManager เพื่อแสดงไอคอนบน healthbar
/// 
/// วิธีใช้:
/// - วางไฟล์นี้ใน Assets/.../Script/... (เช่น Assets/MyAsset/Script/Stat/Weapon/SwordWeapon.cs)
/// - แน่ใจว่า BleedEffect, StatusManager, HealthBarManager, BleedingIntegrationExample อยู่ในโปรเจค
/// - เชื่อมค่า WeaponItem ผ่าน ApplyWeaponData เมื่อ equip หรือ instantiate prefab
/// </summary>
public class SwordWeapon : MonoBehaviour
{
    [Tooltip("Raw damage dealt by the attack before statuses (if you use HP directly)")]
    public int baseDamage = 5;

    [Range(0f, 1f)]
    public float normalBleedChance = 0.3f;

    [Range(0f, 1f)]
    public float skillBleedChance = 0.5f;

    public int bleedDurationTurns = 2;
    public int bleedDamagePerTurn = 2;

    // optional: keep reference to applied WeaponItem (useful for UI, save, etc.)
    [NonSerialized] public WeaponItem appliedItem;

    // --- existing attack methods (NormalAttack/SkillAttack/DealDamageToTarget/ApplyBleed) ---
    public void NormalAttack(GameObject target)
    {
        if (target == null) return;

        Debug.Log($"[SwordWeapon] NormalAttack -> target={{target.name}}, baseDamage={{baseDamage}}");
        DealDamageToTarget(target, baseDamage);

        // roll bleed
        float r = UnityEngine.Random.value;
        Debug.Log($"[SwordWeapon] Normal bleed roll: rnd={{r}} vs chance={{normalBleedChance}}");
        if (r <= normalBleedChance)
        {
            ApplyBleed(target);
        }
    }

    public void SkillAttack(IEnumerable<GameObject> targets)
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t == null) continue;
            Debug.Log($"[SwordWeapon] SkillAttack -> target={{t.name}}, baseDamage={{baseDamage}}");
            DealDamageToTarget(t, baseDamage);

            float r = UnityEngine.Random.value;
            Debug.Log($"[SwordWeapon] Skill bleed roll for {{t.name}}: rnd={{r}} vs chance={{skillBleedChance}}");
            if (r <= skillBleedChance)
            {
                ApplyBleed(t);
            }
        }
    }

    void ApplyBleed(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[SwordWeapon] ApplyBleed called with null target.");
            return;
        }

        Debug.Log($"[SwordWeapon] ApplyBleed called on {{target.name}} (dur={{bleedDurationTurns}}, dmgPerTurn={{bleedDamagePerTurn}})");

        // Create BleedEffect instance (constructor from your StatusEffect implementation)
        var bleed = new BleedEffect(bleedDurationTurns, bleedDamagePerTurn);

        // Try to find a StatusManager component by type name (handles namespace differences)
        Component smComp = null;
        foreach (var mb in target.GetComponents<MonoBehaviour>())
        {
            var t = mb.GetType();
            if (t.Name == "StatusManager" || t.FullName == "GameRaiwaa.Stat.StatusManager" || t.FullName.EndsWith(".StatusManager"))
            {
                smComp = mb;
                break;
            }
        }

        // If not found, try to locate the type by name and add it (best-effort)
        if (smComp == null)
        {
            var smType = Type.GetType("GameRaiwaa.Stat.StatusManager") ?? Type.GetType("StatusManager");
            if (smType != null && typeof(Component).IsAssignableFrom(smType))
            {
                try
                {
                    smComp = target.AddComponent(smType);
                    Debug.Log($"[SwordWeapon] Added StatusManager ({{smType.FullName}}) to {{target.name}} via reflection.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SwordWeapon] Failed to add StatusManager via reflection: {{ex.Message}}");
                }
            }
        }

        if (smComp != null)
        {
            // Try common ApplyStatus signatures
            var applyMethod = smComp.GetType().GetMethod("ApplyStatus", new Type[] { typeof(StatusEffect) })
                              ?? smComp.GetType().GetMethod("ApplyStatus", new Type[] { typeof(object) })
                              ?? smComp.GetType().GetMethod("ApplyStatus", Type.EmptyTypes);

            if (applyMethod != null)
            {
                try
                {
                    applyMethod.Invoke(smComp, new object[] { bleed });
                    Debug.Log($"[SwordWeapon] Applied Bleed to {{target.name}} via {{smComp.GetType().Name}}.");
                    // Notify UI integration to show bleeding icon (if available)
                    try
                    {
                        // Prefer integration helper if present
                        var onBleed = typeof(BleedingIntegrationExample).GetMethod("OnBleedingApplied");
                        if (onBleed != null)
                        {
                            onBleed.Invoke(null, new object[] { target, 1 });
                        }
                        else
                        {
                            // fallback: direct call to HealthBarManager
                            var sprite = BleedingIntegrationExample.bleedingSprite ?? Resources.Load<Sprite>("StatusIcons/bleeding");
                            HealthBarManager.Instance?.AddStatusIconFor(target, "bleeding", sprite, "Bleeding");
                        }
                        Debug.Log($"[SwordWeapon] Requested bleeding icon for {{target.name}}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SwordWeapon] Failed to request bleeding icon: {{ex}}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SwordWeapon] Failed to invoke ApplyStatus on {{smComp.GetType().Name}}: {{ex}}");
                }
            }
            else
            {
                Debug.LogWarning($"[SwordWeapon] StatusManager found ({{smComp.GetType().FullName}}) but no suitable ApplyStatus method was found.");
            }
        }
        else
        {
            Debug.LogWarning("[SwordWeapon] No StatusManager available on target and could not add one. Bleed not applied.");
        }
    }

    void DealDamageToTarget(GameObject target, int dmg)
    {
        if (dmg <= 0 || target == null) return;

        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg);
            Debug.Log($"[SwordWeapon] IDamageable.TakeDamage({{dmg}}) on {{target.name}}");
            return;
        }

        var ps = target.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var meth = ps.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
            if (meth != null)
            {
                try { meth.Invoke(ps, new object[] { dmg }); Debug.Log($"[SwordWeapon] PlayerStat.TakeDamage invoked on {{target.name}}"); return; }
                catch { Debug.LogWarning($"[SwordWeapon] PlayerStat.TakeDamage invoked but failed on {{target.name}}."); }
            }

            var hpField = ps.GetType().GetField("hp");
            if (hpField != null)
            {
                try
                {
                    int hpVal = (int)hpField.GetValue(ps);
                    hpVal = Mathf.Max(0, hpVal - dmg);
                    hpField.SetValue(ps, hpVal);
                    Debug.Log($"[SwordWeapon] (fallback) Subtracted {{dmg}} hp from {{target.name}} (hp now {{hpVal}})");
                    return;
                }
                catch { }
            }

            var hpProp = ps.GetType().GetProperty("hp") ?? ps.GetType().GetProperty("HP") ?? ps.GetType().GetProperty("Hp");
            if (hpProp != null && hpProp.CanRead && hpProp.CanWrite)
            {
                try
                {
                    int hpVal = (int)hpProp.GetValue(ps);
                    hpVal = Mathf.Max(0, hpVal - dmg);
                    hpProp.SetValue(ps, hpVal);
                    Debug.Log($"[SwordWeapon] (fallback) Subtracted {{dmg}} hp from {{target.name}} (hp now {{hpVal}})");
                    return;
                }
                catch { }
            }

            Debug.LogWarning($"[SwordWeapon] Could not apply damage to PlayerStat on {{target.name}}.");
            return;
        }

        var ms = target.GetComponent<IMonsterStat>();
        if (ms != null)
        {
            try
            {
                var method = ms.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
                if (method != null)
                {
                    method.Invoke(ms, new object[] { dmg });
                    Debug.Log($"[SwordWeapon] IMonsterStat.TakeDamage({{dmg}}) on {{target.name}}");
                    return;
                }
            }
            catch { }
        }

        // fallback: TurnManager battler list (if you use TurnManager based health tracking)
        if (TurnManager.Instance != null)
        {
            var idx = TurnManager.Instance.battlerObjects.IndexOf(target);
            if (idx >= 0 && idx < TurnManager.Instance.battlers.Count)
            {
                TurnManager.Instance.battlers[idx].hp = Mathf.Max(0, TurnManager.Instance.battlers[idx].hp - dmg);
                Debug.Log($"[SwordWeapon] Reduced battler {{TurnManager.Instance.battlers[idx].name}} hp by {{dmg}}. New hp={{TurnManager.Instance.battlers[idx].hp}}");
                return;
            }
        }

        Debug.LogWarning($"[SwordWeapon] No known damage path found for {{target.name}});
    }

    // -------------------------------------------------------
    // NEW: ApplyWeaponData for data-driven equip system
    // -------------------------------------------------------
    /// <summary>
    /// Apply values from a WeaponItem (ScriptableObject) to this SwordWeapon.
    /// Call this after instantiating a prefab or when equipping an item.
    /// </summary>
    public void ApplyWeaponData(WeaponItem item)
    {
        if (item == null) return;
        appliedItem = item;

        baseDamage = item.baseDamage;
        normalBleedChance = item.normalBleedChance;
        skillBleedChance = item.skillBleedChance;
        bleedDurationTurns = item.bleedDuration;
        bleedDamagePerTurn = item.bleedDmgPerTurn;
    }
}