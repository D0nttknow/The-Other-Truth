using System;
using UnityEngine;

namespace GameRaiwaa.Stat
{
    /// <summary>
    /// Base StatusEffect + BleedEffect.
    /// Uses the shared StatusType enum in GameRaiwaa.Stat.
    /// </summary>
    [Serializable]
    public class StatusEffect
    {
        // Legacy public fields (kept for compatibility)
        public StatusType type = StatusType.None;
        public int remainingTurns = 0;
        public bool refreshIfExists = true;

        // Modern properties mapped to legacy fields
        public StatusType Type { get => type; protected set => type = value; }
        public int RemainingTurns { get => remainingTurns; set => remainingTurns = value; }
        public bool RefreshIfExists { get => refreshIfExists; protected set => refreshIfExists = value; }

        public StatusEffect() { type = StatusType.None; remainingTurns = 0; refreshIfExists = true; }

        public StatusEffect(StatusType t, int turns, bool refresh = true)
        {
            Type = t;
            RemainingTurns = turns;
            RefreshIfExists = refresh;
        }

        /// <summary>
        /// Called at the start of owner's turn. Override in derived effects.
        /// Return damage amount applied (for logging / UI).
        /// </summary>
        public virtual int OnTurnStart(GameObject owner)
        {
            return 0;
        }
    }

    [Serializable]
    public class BleedEffect : StatusEffect
    {
        // Legacy field name used by existing code
        public int damagePerTick = 2;

        // New property
        public int DamagePerTick { get => damagePerTick; private set => damagePerTick = value; }

        public BleedEffect(int turns = 2, int dmg = 2) : base(StatusType.Bleed, turns, true)
        {
            damagePerTick = dmg;
        }

        public override int OnTurnStart(GameObject owner)
        {
            if (owner == null) return 0;

            int actualDamage = damagePerTick;

            // Prefer IDamageable if present
            var dmgComp = owner.GetComponent<IDamageable>();
            if (dmgComp != null)
            {
                dmgComp.TakeDamage(actualDamage);
                return actualDamage;
            }

            // Try PlayerStat method if it exists (reflection fallback)
            var ps = owner.GetComponent<PlayerStat>();
            if (ps != null)
            {
                var meth = ps.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
                if (meth != null)
                {
                    try { meth.Invoke(ps, new object[] { actualDamage }); return actualDamage; }
                    catch { Debug.LogWarning($"[BleedEffect] Failed to invoke PlayerStat.TakeDamage on {owner.name}"); }
                }
                var hpField = ps.GetType().GetField("hp");
                if (hpField != null)
                {
                    int hpVal = (int)hpField.GetValue(ps);
                    hpVal = Mathf.Max(0, hpVal - actualDamage);
                    hpField.SetValue(ps, hpVal);
                    Debug.Log($"[BleedEffect] (fallback) Subtracted {actualDamage} hp from {owner.name} (hp now {hpVal})");
                    var onDeath = ps.GetType().GetMethod("OnDeath", System.Type.EmptyTypes);
                    if (hpVal <= 0 && onDeath != null) onDeath.Invoke(ps, null);
                    return actualDamage;
                }
            }

            // Try EnemyStats fallback
            var es = owner.GetComponent<EnemyStats>();
            if (es != null)
            {
                try { es.TakeDamage(actualDamage); return actualDamage; }
                catch { Debug.LogWarning($"[BleedEffect] Failed to call EnemyStats.TakeDamage on {owner.name}"); }
            }

            // Last resort: modify TurnManager battler model if present
            var tm = TurnManager.Instance;
            if (tm != null)
            {
                int idx = tm.battlerObjects.IndexOf(owner);
                if (idx >= 0 && idx < tm.battlers.Count)
                {
                    tm.battlers[idx].hp = Mathf.Max(0, tm.battlers[idx].hp - actualDamage);
                    Debug.Log($"[BleedEffect] Applied {actualDamage} bleed damage to battler {tm.battlers[idx].name}. New hp={tm.battlers[idx].hp}");
                    return actualDamage;
                }
            }

            return 0;
        }
    }
}