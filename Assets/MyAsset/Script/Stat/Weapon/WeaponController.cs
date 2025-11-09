using System;
using System.Collections.Generic;
using UnityEngine;
using GameRaiwaa.Stat; // for BleedEffect / StunEffect if you keep them in that namespace

public class WeaponController : MonoBehaviour
{
    public WeaponItem appliedItem;

    // cooldown remaining in turns for skill (0 means ready)
    public int skillCooldownRemaining = 0;

    // public hooks for audio/animation
    public Action<GameObject> OnNormalAttackExecuted;
    public Action<List<GameObject>> OnSkillExecuted;

    public void ApplyWeaponData(WeaponItem item)
    {
        appliedItem = item;
        // optionally update visuals / UI icon here
    }

    public bool IsSkillReady()
    {
        return skillCooldownRemaining <= 0;
    }

    // called by CharacterEquipment or PlayerController when player presses attack button
    public void NormalAttack(GameObject target)
    {
        if (appliedItem == null) return;
        if (target == null) return;

        int dmg = appliedItem.baseDamage;
        // apply damage
        ApplyDamageToTarget(target, dmg);

        // Sword: chance to apply bleed
        if (appliedItem.category == WeaponCategory.Sword)
        {
            if (UnityEngine.Random.value <= appliedItem.normalBleedChance)
            {
                // use bleed settings from WeaponItem
                var bleed = new BleedEffect(appliedItem.bleedDuration, appliedItem.bleedDmgPerTurn);
                var sm = target.GetComponent<StatusManager>();
                if (sm != null) sm.ApplyStatus(bleed);
            }
        }

        OnNormalAttackExecuted?.Invoke(target);
    }

    // Skill behavior: different per WeaponCategory
    public void UseSkill(IEnumerable<GameObject> targets)
    {
        if (appliedItem == null) return;
        if (!IsSkillReady()) { Debug.Log("[WeaponController] Skill on cooldown"); return; }

        List<GameObject> affected = new List<GameObject>();

        if (appliedItem.category == WeaponCategory.Sword)
        {
            // AoE: hit every target, chance to apply bleed
            foreach (var t in targets)
            {
                if (t == null) continue;
                int dmg = appliedItem.skillDamage > 0 ? appliedItem.skillDamage : appliedItem.baseDamage;
                ApplyDamageToTarget(t, dmg);

                if (UnityEngine.Random.value <= appliedItem.skillBleedChance)
                {
                    var bleed = new BleedEffect(appliedItem.bleedDuration, appliedItem.bleedDmgPerTurn);
                    var sm = t.GetComponent<StatusManager>();
                    if (sm != null) sm.ApplyStatus(bleed);
                }
                affected.Add(t);
            }
            skillCooldownRemaining = appliedItem.swordSkillCooldownTurns;
        }
        else if (appliedItem.category == WeaponCategory.Hammer)
        {
            // Hammer: damage and try to stun one target (or hammerSkillTargetCount)
            int dmg = appliedItem.skillDamage > 0 ? appliedItem.skillDamage : appliedItem.baseDamage;
            foreach (var t in targets)
            {
                if (t == null) continue;
                ApplyDamageToTarget(t, dmg);
                affected.Add(t);
            }

            // attempt to stun up to hammerSkillTargetCount random alive targets from targets list
            var aliveTargets = new List<GameObject>();
            foreach (var t in targets) if (t != null) aliveTargets.Add(t);
            for (int i = 0; i < appliedItem.hammerSkillTargetCount && aliveTargets.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, aliveTargets.Count);
                var pick = aliveTargets[idx];
                aliveTargets.RemoveAt(idx);
                if (UnityEngine.Random.value <= appliedItem.hammerStunChance)
                {
                    // StunEffect now derives from StatusEffect, so this will match StatusManager.ApplyStatus(StatusEffect)
                    var stun = new StunEffect(2); // 2 turns stun (or make configurable field)
                    var sm = pick.GetComponent<StatusManager>();
                    if (sm != null) sm.ApplyStatus(stun);
                }
            }

            skillCooldownRemaining = appliedItem.hammerSkillCooldownTurns;
        }

        OnSkillExecuted?.Invoke(affected);
    }

    // apply damage via IDamageable / PlayerStat / EnemyStats / TurnManager fallback
    void ApplyDamageToTarget(GameObject target, int dmg)
    {
        if (target == null) return;
        if (dmg <= 0) return;

        var dmgComp = target.GetComponent<IDamageable>();
        if (dmgComp != null) { dmgComp.TakeDamage(dmg); return; }

        var ps = target.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var method = ps.GetType().GetMethod("TakeDamage", new System.Type[] { typeof(int) });
            if (method != null) { try { method.Invoke(ps, new object[] { dmg }); return; } catch { } }
            var hpField = ps.GetType().GetField("hp");
            if (hpField != null) { int hpVal = (int)hpField.GetValue(ps); hpVal = Mathf.Max(0, hpVal - dmg); hpField.SetValue(ps, hpVal); return; }
        }

        var ms = target.GetComponent<IMonsterStat>();
        if (ms != null)
        {
            var method = ms.GetType().GetMethod("TakeDamage", new System.Type[] { typeof(int) });
            if (method != null) { try { method.Invoke(ms, new object[] { dmg }); return; } catch { } }
        }

        // TurnManager fallback
        if (TurnManager.Instance != null)
        {
            int idx = TurnManager.Instance.battlerObjects.IndexOf(target);
            if (idx >= 0 && idx < TurnManager.Instance.battlers.Count)
            {
                TurnManager.Instance.battlers[idx].hp = Mathf.Max(0, TurnManager.Instance.battlers[idx].hp - dmg);
            }
        }
    }

    // Called at start of each turn (hook from TurnManager) to decrement cooldown
    public void OnTurnStart()
    {
        if (skillCooldownRemaining > 0) skillCooldownRemaining = Mathf.Max(0, skillCooldownRemaining - 1);
    }
}