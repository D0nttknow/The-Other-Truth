// name: Assets/MyAsset/Script/Stat/Weapon/WeaponController.cs
using GameRaiwaa.Stat;
using System;
using System.Collections.Generic;
using UnityEngine;
// ถ้าคลาส BleedEffect / StatusEffect อยู่ใน namespace ใดให้ uncomment ตัวอย่างด้านล่าง
// using GameRaiwaa.Stat;

public class WeaponController : MonoBehaviour
{
    public int skillCooldownRemaining = 0;
    public int skillCooldown = 0;
    public float normalBleedChance = 0f;
    public float skillBleedChance = 0f;
    private WeaponItem data;

    public void ApplyWeaponData(WeaponItem item)
    {
        data = item;
        if (item != null)
        {
            normalBleedChance = item.normalBleedChance;
            skillBleedChance = item.skillBleedChance;

            // set skillCooldown based on weapon category / item fields that exist
            if (item.category == WeaponCategory.Sword) skillCooldown = item.swordSkillCooldownTurns;
            else if (item.category == WeaponCategory.Hammer) skillCooldown = item.hammerSkillCooldownTurns;
            else skillCooldown = 0;
        }
    }

    public bool IsSkillReady() => skillCooldownRemaining <= 0;

    public void OnTurnStart()
    {
        if (skillCooldownRemaining > 0) skillCooldownRemaining--;
    }

    public void NormalAttack(GameObject target, Action onComplete = null)
    {
        if (target != null)
        {
            int dmg = CalculateNormalDamage();

            // Apply damage: prefer IDamageable if present
            var dmgComp = target.GetComponent<IDamageable>();
            if (dmgComp != null) dmgComp.TakeDamage(dmg);
            else target.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);

            // Apply bleed according to WeaponItem fields
            if (normalBleedChance > 0f && UnityEngine.Random.value <= normalBleedChance)
            {
                var sm = target.GetComponent<StatusManager>();
                if (sm != null)
                {
                    // Use the correct Bleed type/name from your project
                    // If your bleed class is BleedEffect and is accessible, use that:
                    try
                    {
                        sm.ApplyStatus(new BleedEffect(data != null ? data.bleedDuration : 1, data != null ? data.bleedDmgPerTurn : 1));
                    }
                    catch (Exception)
                    {
                        // Fallback: if class name differs (BleedStatusEffect etc.), log to help debug
                        Debug.LogWarning("[WeaponController] Failed to create BleedEffect - check its class name/namespace.");
                    }
                }
            }
        }

        onComplete?.Invoke();
    }

    public void UseSkill(IEnumerable<GameObject> targets, Action onComplete = null)
    {
        if (targets == null) { onComplete?.Invoke(); return; }

        int dmg = CalculateSkillDamage();

        foreach (var t in targets)
        {
            if (t == null) continue;

            var dmgComp = t.GetComponent<IDamageable>();
            if (dmgComp != null) dmgComp.TakeDamage(dmg);
            else t.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);

            if (skillBleedChance > 0f && UnityEngine.Random.value <= skillBleedChance)
            {
                var sm = t.GetComponent<StatusManager>();
                if (sm != null)
                {
                    try
                    {
                        sm.ApplyStatus(new BleedEffect(data != null ? data.bleedDuration : 1, data != null ? data.bleedDmgPerTurn : 1));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("[WeaponController] Failed to create BleedEffect - check its class name/namespace.");
                    }
                }
            }
        }

        // start cooldown (we set skillCooldown earlier in ApplyWeaponData)
        skillCooldownRemaining = skillCooldown;

        onComplete?.Invoke();
    }

    int CalculateNormalDamage() => data != null ? data.baseDamage : 1;
    int CalculateSkillDamage() => (data != null && data.skillDamage > 0) ? data.skillDamage : (data != null ? data.baseDamage : 1);
}