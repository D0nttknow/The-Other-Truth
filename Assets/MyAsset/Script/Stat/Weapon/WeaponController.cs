using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using GameRaiwaa.Stat; // for BleedEffect / StunEffect if you keep them in that namespace

public class WeaponController : MonoBehaviour
{
    // Legacy ScriptableObject-based item (if you have WeaponItem assets)
    public WeaponItem appliedItem;

    // New: keep a reference to a WeaponDefinition asset if used
    public ScriptableObject appliedDefinition;

    // cooldown remaining in turns for skill (0 means ready)
    public int skillCooldownRemaining = 0;

    // public hooks for audio/animation
    public Action<GameObject> OnNormalAttackExecuted;
    public Action<List<GameObject>> OnSkillExecuted;

    public void ApplyWeaponData(WeaponItem item)
    {
        appliedItem = item;
        appliedDefinition = null;
    }

    // New overload accepting WeaponDefinition (or any ScriptableObject)
    public void ApplyWeaponData(WeaponDefinition def)
    {
        appliedDefinition = def;
        appliedItem = null;
    }

    // Fallback generic object overload (tolerant)
    public void ApplyWeaponData(object any)
    {
        if (any is WeaponItem wi) ApplyWeaponData(wi);
        else if (any is WeaponDefinition wd) ApplyWeaponData(wd);
        else if (any is ScriptableObject so) appliedDefinition = so;
        else appliedDefinition = null;
    }

    public bool IsSkillReady()
    {
        return skillCooldownRemaining <= 0;
    }

    // Helper accessors that read from appliedItem if present, otherwise try appliedDefinition via reflection
    int GetInt(string[] names, int fallback = 0)
    {
        if (appliedItem != null)
        {
            var t = appliedItem.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(appliedItem);
                var p = t.GetProperty(n);
                if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(appliedItem);
            }
        }
        if (appliedDefinition != null)
        {
            var t = appliedDefinition.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(appliedDefinition);
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(appliedDefinition);
            }
        }
        return fallback;
    }

    float GetFloat(string[] names, float fallback = 0f)
    {
        if (appliedItem != null)
        {
            var t = appliedItem.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(appliedItem);
                var p = t.GetProperty(n);
                if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(appliedItem);
            }
        }
        if (appliedDefinition != null)
        {
            var t = appliedDefinition.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(appliedDefinition);
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(appliedDefinition);
            }
        }
        return fallback;
    }

    string GetString(string[] names, string fallback = "")
    {
        if (appliedItem != null)
        {
            var t = appliedItem.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(appliedItem);
                var p = t.GetProperty(n);
                if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(appliedItem);
            }
        }
        if (appliedDefinition != null)
        {
            var t = appliedDefinition.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(appliedDefinition);
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(appliedDefinition);
            }
        }
        return fallback;
    }

    WeaponCategory GetCategory(WeaponCategory fallback = WeaponCategory.Sword)
    {
        if (appliedItem != null) return appliedItem.category;
        if (appliedDefinition != null)
        {
            // try fields that may represent category/type
            var t = appliedDefinition.GetType();
            var f = t.GetField("weaponType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try
                {
                    var val = f.GetValue(appliedDefinition);
                    if (val is WeaponCategory wc) return wc;
                    if (val is Enum) return (WeaponCategory)val;
                }
                catch { }
            }
            var p = t.GetProperty("weaponType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(appliedDefinition);
                    if (val is WeaponCategory wc2) return wc2;
                    if (val is Enum) return (WeaponCategory)val;
                }
                catch { }
            }
        }
        return fallback;
    }

    // called by CharacterEquipment or PlayerController when player presses attack button
    public void NormalAttack(GameObject target)
    {
        if (target == null) return;

        int dmg = GetInt(new string[] { "baseDamage", "baseDmg", "damage" }, 0);
        if (dmg <= 0) return;
        ApplyDamageToTarget(target, dmg);

        if (GetCategory() == WeaponCategory.Sword)
        {
            float bleedChance = GetFloat(new string[] { "normalBleedChance", "bleedChance" }, 0f);
            if (UnityEngine.Random.value <= bleedChance)
            {
                int dur = GetInt(new string[] { "bleedDuration" }, 0);
                int per = GetInt(new string[] { "bleedDmgPerTurn" }, 0);
                var bleed = new BleedEffect(dur, per);
                var sm = target.GetComponent<StatusManager>();
                if (sm != null) sm.ApplyStatus(bleed);
            }
        }

        OnNormalAttackExecuted?.Invoke(target);
    }

    public void NormalAttack(GameObject target, float multiplier)
    {
        if (target == null) return;
        int baseDmg = GetInt(new string[] { "baseDamage", "baseDmg", "damage" }, 0);
        int dmg = Mathf.RoundToInt(baseDmg * multiplier);
        if (dmg <= 0) return;
        ApplyDamageToTarget(target, dmg);

        if (GetCategory() == WeaponCategory.Sword)
        {
            float bleedChance = GetFloat(new string[] { "normalBleedChance", "bleedChance" }, 0f);
            if (UnityEngine.Random.value <= bleedChance)
            {
                int dur = GetInt(new string[] { "bleedDuration" }, 0);
                int per = GetInt(new string[] { "bleedDmgPerTurn" }, 0);
                var bleed = new BleedEffect(dur, per);
                var sm = target.GetComponent<StatusManager>();
                if (sm != null) sm.ApplyStatus(bleed);
            }
        }

        OnNormalAttackExecuted?.Invoke(target);
    }

    public void UseSkill(IEnumerable<GameObject> targets)
    {
        if (targets == null) return;
        if (!IsSkillReady()) { Debug.Log("[WeaponController] Skill on cooldown"); return; }

        List<GameObject> affected = new List<GameObject>();
        var cat = GetCategory();

        if (cat == WeaponCategory.Sword)
        {
            int dmg = GetInt(new string[] { "skillDamage", "skillDmg" }, GetInt(new string[] { "baseDamage" }, 0));
            foreach (var t in targets)
            {
                if (t == null) continue;
                ApplyDamageToTarget(t, dmg);

                float chance = GetFloat(new string[] { "skillBleedChance", "skillBleed" }, 0f);
                if (UnityEngine.Random.value <= chance)
                {
                    int dur = GetInt(new string[] { "bleedDuration" }, 0);
                    int per = GetInt(new string[] { "bleedDmgPerTurn" }, 0);
                    var bleed = new BleedEffect(dur, per);
                    var sm = t.GetComponent<StatusManager>();
                    if (sm != null) sm.ApplyStatus(bleed);
                }
                affected.Add(t);
            }

            skillCooldownRemaining = GetInt(new string[] { "swordSkillCooldownTurns", "skillCooldown" }, 1);
        }
        else if (cat == WeaponCategory.Hammer)
        {
            int dmg = GetInt(new string[] { "skillDamage", "skillDmg" }, GetInt(new string[] { "baseDamage" }, 0));
            foreach (var t in targets)
            {
                if (t == null) continue;
                ApplyDamageToTarget(t, dmg);
                affected.Add(t);
            }

            var aliveTargets = new List<GameObject>();
            foreach (var t in targets) if (t != null) aliveTargets.Add(t);
            int targetCount = GetInt(new string[] { "hammerSkillTargetCount", "skillTargetCount" }, 1);
            float stunChance = GetFloat(new string[] { "hammerStunChance", "stunChance" }, 0f);
            for (int i = 0; i < targetCount && aliveTargets.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, aliveTargets.Count);
                var pick = aliveTargets[idx];
                aliveTargets.RemoveAt(idx);
                if (UnityEngine.Random.value <= stunChance)
                {
                    var stun = new StunEffect(2);
                    var sm = pick.GetComponent<StatusManager>();
                    if (sm != null) sm.ApplyStatus(stun);
                }
            }

            skillCooldownRemaining = GetInt(new string[] { "hammerSkillCooldownTurns", "skillCooldown" }, 1);
        }

        OnSkillExecuted?.Invoke(affected);
    }

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

        if (TurnManager.Instance != null)
        {
            int idx = TurnManager.Instance.battlerObjects.IndexOf(target);
            if (idx >= 0 && idx < TurnManager.Instance.battlers.Count)
            {
                TurnManager.Instance.battlers[idx].hp = Mathf.Max(0, TurnManager.Instance.battlers[idx].hp - dmg);
            }
        }
    }

    public void OnTurnStart()
    {
        if (skillCooldownRemaining > 0) skillCooldownRemaining = Mathf.Max(0, skillCooldownRemaining - 1);
    }
}