using System;
using System.Collections.Generic;
using UnityEngine;
using GameRaiwaa.Stat; // ใช้ StatusEffect / StatusType จาก namespace เดียวกัน

/// <summary>
/// StatusManager: เก็บและจัดการสถานะบน GameObject (player / enemy)
/// - เข้ากันได้กับ StatusEffect (มี legacy fields และ modern properties)
/// - ให้ API: ApplyStatus, HasStatus, RemoveStatus, TickStatusPerTurn / OnTurnStart
/// </summary>
[DisallowMultipleComponent]
public class StatusManager : MonoBehaviour
{
    // เก็บ effect instances (ใช้ StatusEffect ซึ่งมีทั้ง legacy fields และ properties)
    [SerializeField] List<StatusEffect> effects = new List<StatusEffect>();

    public IReadOnlyList<StatusEffect> Effects => effects.AsReadOnly();

    /// <summary>
    /// Apply a new effect instance. If the same type exists and RefreshIfExists==true, refresh duration.
    /// Non-stacking by default for same-type effects.
    /// </summary>
    public void ApplyStatus(StatusEffect newEffect)
    {
        if (newEffect == null || newEffect.Type == StatusType.None) return;

        for (int i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            if (e != null && e.Type == newEffect.Type)
            {
                if (newEffect.RefreshIfExists)
                {
                    // refresh remainingTurns using both modern property and legacy field
                    e.RemainingTurns = newEffect.RemainingTurns;
                    try { e.remainingTurns = newEffect.remainingTurns; } catch { }
                    Debug.Log($"[StatusManager] Refreshed {newEffect.Type} on {gameObject.name} to {newEffect.RemainingTurns} turns.");
                }
                else
                {
                    Debug.Log($"[StatusManager] {newEffect.Type} already present on {gameObject.name} - not stacking.");
                }
                return;
            }
        }

        // Add a shallow copy (avoid external mutation). If needed, create a new instance of correct derived type.
        StatusEffect toAdd;
        if (newEffect is BleedEffect b)
        {
            var copy = new BleedEffect(b.RemainingTurns, b.DamagePerTick);
            // keep legacy field too
            copy.damagePerTick = b.damagePerTick;
            toAdd = copy;
        }
        else
        {
            toAdd = new StatusEffect(newEffect.Type, newEffect.RemainingTurns, newEffect.RefreshIfExists);
            // copy legacy fields if present on incoming instance
            try { toAdd.remainingTurns = newEffect.remainingTurns; } catch { }
        }

        effects.Add(toAdd);
        Debug.Log($"[StatusManager] Applied {toAdd.Type} to {gameObject.name} for {toAdd.RemainingTurns} turns.");
    }

    public bool HasStatus(StatusType type)
    {
        return effects.Exists(e => e != null && e.Type == type && e.RemainingTurns > 0);
    }

    public void RemoveStatus(StatusType type)
    {
        effects.RemoveAll(e => e != null && e.Type == type);
    }

    /// <summary>
    /// Called at start of owner's turn to process effects.
    /// Calls OnTurnStart on each effect, applies damage if returned,
    /// decrements durations and removes expired effects.
    /// </summary>
    public void TickStatusPerTurn()
    {
        if (effects == null || effects.Count == 0) return;

        // snapshot to allow safe modification during iteration
        var snapshot = new List<StatusEffect>(effects);

        foreach (var eff in snapshot)
        {
            if (eff == null) continue;
            if (eff.RemainingTurns <= 0) continue;

            int dmg = 0;
            try
            {
                dmg = eff.OnTurnStart(gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StatusManager] Exception while ticking {eff.Type} on {gameObject.name}: {ex.Message}");
            }

            if (dmg > 0)
            {
                // Try to apply damage via IDamageable if present
                var dmgComp = gameObject.GetComponent<IDamageable>();
                if (dmgComp != null)
                {
                    dmgComp.TakeDamage(dmg);
                }
                else
                {
                    // fallback to EnemyStats/PlayerStat if available
                    var es = gameObject.GetComponent<EnemyStats>();
                    if (es != null) es.TakeDamage(dmg);
                    else
                    {
                        var ps = gameObject.GetComponent<PlayerStat>();
                        if (ps != null)
                        {
                            // try method if exists
                            var method = ps.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
                            if (method != null)
                            {
                                try { method.Invoke(ps, new object[] { dmg }); }
                                catch { Debug.LogWarning($"[StatusManager] Failed to call PlayerStat.TakeDamage on {gameObject.name}"); }
                            }
                            else
                            {
                                // try reduce hp field
                                var hpField = ps.GetType().GetField("hp");
                                if (hpField != null)
                                {
                                    int val = (int)hpField.GetValue(ps);
                                    val = Mathf.Max(0, val - dmg);
                                    hpField.SetValue(ps, val);
                                }
                            }
                        }
                    }
                }

                Debug.Log($"[StatusManager] {gameObject.name} took {dmg} damage from {eff.Type}");
            }

            // decrease duration
            eff.RemainingTurns = Math.Max(0, eff.RemainingTurns - 1);
            try { eff.remainingTurns = eff.RemainingTurns; } catch { }

            if (eff.RemainingTurns <= 0)
            {
                effects.Remove(eff);
                Debug.Log($"[StatusManager] {eff.Type} expired on {gameObject.name}");
            }

            // if died, optionally notify; actual removal handled by TurnManager/CleanUp
        }
    }
}