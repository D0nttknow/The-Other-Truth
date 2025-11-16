using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Example WeaponController changes:
/// - NormalAttack(target, onComplete)
/// - UseSkill(targets, onComplete) -> applies damage to each, applies bleed chance, runs animation, then calls onComplete
/// - Exposes skillCooldownRemaining and IsSkillReady()
/// Implement the animation hooks in your real WeaponController (this is sample logic).
/// </summary>
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
            skillCooldown = item.skillCooldown;
        }
    }

    public bool IsSkillReady() => skillCooldownRemaining <= 0;

    public void OnTurnStart()
    {
        if (skillCooldownRemaining > 0) skillCooldownRemaining--;
    }

    // Normal attack with callback
    public void NormalAttack(GameObject target, Action onComplete = null)
    {
        if (target != null)
        {
            target.SendMessage("TakeDamage", CalculateNormalDamage(), SendMessageOptions.DontRequireReceiver);

            if (normalBleedChance > 0f && UnityEngine.Random.value <= normalBleedChance)
            {
                var sm = target.GetComponent<StatusManager>();
                if (sm != null) sm.ApplyStatus(new BleedStatusEffect(3, 2));
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
            t.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);

            if (skillBleedChance > 0f && UnityEngine.Random.value <= skillBleedChance)
            {
                var sm = t.GetComponent<StatusManager>();
                if (sm != null) sm.ApplyStatus(new BleedStatusEffect(3, 2));
            }
        }

        skillCooldownRemaining = skillCooldown;

        onComplete?.Invoke();
    }

    int CalculateNormalDamage() => data != null ? data.normalDamage : 1;
    int CalculateSkillDamage() => data != null ? data.skillDamage : 1;
}