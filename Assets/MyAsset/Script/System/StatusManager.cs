using System;
using System.Collections.Generic;
using UnityEngine;
using GameRaiwaa.Stat; // ใช้ StatusEffect / StatusType จาก namespace เดียวกัน

/// <summary>
/// StatusManager: เก็บและจัดการสถานะบน GameObject (player / enemy)
/// - เข้ากันได้กับ StatusEffect (มี legacy fields และ modern properties)
/// - ให้ API: ApplyStatus, HasStatus, RemoveStatus, TickStatusPerTurn / OnTurnStart
/// - เพิ่ม overload ApplyStatus(object) เพื่อรองรับการเรียกด้วย reflection จากโค้ดเก่า
/// - เพิ่ม helper AddBleed(...) เพื่อเรียกใช้ง่ายจากสคริปต์อื่น ๆ
/// - เมื่อ Bleed ถูกเพิ่ม/ลบ จะพยายามเรียก BleedingIntegrationExample.OnBleedingApplied/OnBleedingRemoved
/// </summary>
[DisallowMultipleComponent]
public class StatusManager : MonoBehaviour
{
    // เก็บ effect instances (ใช้ StatusEffect ซึ่งมีทั้ง legacy fields และ properties)
    [SerializeField] List<StatusEffect> effects = new List<StatusEffect>();

    public IReadOnlyList<StatusEffect> Effects => effects.AsReadOnly();

    void Awake()
    {
        if (effects == null) effects = new List<StatusEffect>();
    }

    /// <summary>
    /// Apply a new effect instance. If the same type exists and RefreshIfExists==true, refresh duration.
    /// Non-stacking by default for same-type effects.
    /// </summary>
    public void ApplyStatus(StatusEffect newEffect)
    {
        if (newEffect == null || newEffect.Type == StatusType.None) return;

        // Try refresh existing same-type effect
        for (int i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            if (e != null && e.Type == newEffect.Type)
            {
                if (newEffect.RefreshIfExists)
                {
                    e.RemainingTurns = newEffect.RemainingTurns;
                    try { e.remainingTurns = newEffect.remainingTurns; } catch { }
                    Debug.Log($"[StatusManager] Refreshed {newEffect.Type} on {gameObject.name} to {newEffect.RemainingTurns} turns.");
                    // If it's bleed, re-notify UI to refresh icon (best-effort)
                    if (e is BleedEffect)
                    {
                        TryNotifyBleedingApplied();
                    }
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
            // keep legacy field too if present
            try { copy.damagePerTick = b.damagePerTick; } catch { }
            toAdd = copy;
        }
        else
        {
            toAdd = new StatusEffect(newEffect.Type, newEffect.RemainingTurns, newEffect.RefreshIfExists);
            try { toAdd.remainingTurns = newEffect.remainingTurns; } catch { }
        }

        effects.Add(toAdd);
        Debug.Log($"[StatusManager] Applied {toAdd.Type} to {gameObject.name} for {toAdd.RemainingTurns} turns.");

        // Notify UI if this is Bleed
        if (toAdd is BleedEffect)
        {
            TryNotifyBleedingApplied();
        }
    }

    /// <summary>
    /// Backwards-compatible overload to support reflection calls that pass object.
    /// It will try to cast to StatusEffect or handle simple legacy bleed tuples if needed.
    /// </summary>
    public void ApplyStatus(object newEffectObj)
    {
        if (newEffectObj == null) return;

        // common case: already a StatusEffect
        if (newEffectObj is StatusEffect se)
        {
            ApplyStatus(se);
            return;
        }

        // if someone passed (int dmg, int turns) or similar, try to interpret it as bleed
        try
        {
            // attempt dynamic-like handling (best-effort)
            var type = newEffectObj.GetType();
            var dmgProp = type.GetProperty("DamagePerTurn") ?? type.GetProperty("damagePerTurn") ?? type.GetProperty("Dmg");
            var turnsProp = type.GetProperty("RemainingTurns") ?? type.GetProperty("remainingTurns") ?? type.GetProperty("Turns");

            if (dmgProp != null && turnsProp != null)
            {
                int dmg = Convert.ToInt32(dmgProp.GetValue(newEffectObj));
                int turns = Convert.ToInt32(turnsProp.GetValue(newEffectObj));
                ApplyStatus(new BleedEffect(turns, dmg));
                return;
            }
        }
        catch { /* swallow best-effort conversion errors */ }

        Debug.LogWarning("[StatusManager] ApplyStatus(object) received an unsupported object type: " + newEffectObj.GetType().FullName);
    }

    /// <summary>
    /// Convenience helper to directly add a bleed without creating BleedEffect externally.
    /// </summary>
    public void AddBleed(int damagePerTurn, int duration)
    {
        ApplyStatus(new BleedEffect(duration, damagePerTurn));
    }

    public bool HasStatus(StatusType type)
    {
        return effects.Exists(e => e != null && e.Type == type && e.RemainingTurns > 0);
    }

    public void RemoveStatus(StatusType type)
    {
        // If removing bleed, notify UI about removal for each removed instance.
        if (type == StatusType.Bleed)
        {
            bool had = effects.Exists(e => e != null && e.Type == StatusType.Bleed);
            effects.RemoveAll(e => e != null && e.Type == type);
            if (had)
            {
                TryNotifyBleedingRemoved();
            }
        }
        else
        {
            effects.RemoveAll(e => e != null && e.Type == type);
        }
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
        var toRemove = new List<StatusEffect>();

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
                                    try
                                    {
                                        int val = (int)hpField.GetValue(ps);
                                        val = Mathf.Max(0, val - dmg);
                                        hpField.SetValue(ps, val);
                                    }
                                    catch { }
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
                toRemove.Add(eff);
            }
        }

        // remove expired effects and notify if bleed expired
        if (toRemove.Count > 0)
        {
            bool bleedExpired = false;
            foreach (var r in toRemove)
            {
                if (r != null && r.Type == StatusType.Bleed) bleedExpired = true;
                effects.Remove(r);
                Debug.Log($"[StatusManager] {r?.Type} expired on {gameObject.name}");
            }

            if (bleedExpired)
            {
                TryNotifyBleedingRemoved();
            }
        }
    }

    // Best-effort UI integration helpers (wrap calls in try/catch to avoid breaking game logic)
    void TryNotifyBleedingApplied()
    {
        try
        {
            var mi = typeof(BleedingIntegrationExample).GetMethod("OnBleedingApplied", new Type[] { typeof(GameObject), typeof(int) });
            if (mi != null)
            {
                mi.Invoke(null, new object[] { gameObject, 1 });
                return;
            }

            // fallback: direct healthbar call
            var sprite = BleedingIntegrationExample.bleedingSprite ?? Resources.Load<Sprite>("StatusIcons/bleeding");
            HealthBarManager.Instance?.AddStatusIconFor(gameObject, "bleeding", sprite, "Bleeding");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StatusManager] TryNotifyBleedingApplied failed: {ex.Message}");
        }
    }

    void TryNotifyBleedingRemoved()
    {
        try
        {
            var mi = typeof(BleedingIntegrationExample).GetMethod("OnBleedingRemoved", new Type[] { typeof(GameObject) });
            if (mi != null)
            {
                mi.Invoke(null, new object[] { gameObject });
                return;
            }

            HealthBarManager.Instance?.RemoveStatusIconFor(gameObject, "bleeding");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StatusManager] TryNotifyBleedingRemoved failed: {ex.Message}");
        }
    }
}