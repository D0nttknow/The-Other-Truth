using System;
using UnityEngine;
using GameRaiwaa.Stat; // optional, if you use Status types elsewhere

/// <summary>
/// Adapter to make a GameObject with PlayerStat implement IDamageable.
/// Attach to player prefabs (or add at runtime) so Bleed and other systems can call TakeDamage.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStatDamageAdapter : MonoBehaviour, IDamageable
{
    PlayerStat ps;

    void Awake()
    {
        ps = GetComponent<PlayerStat>();
    }

    public bool TakeDamage(int amount)
    {
        if (ps == null) return false;

        // 1) Try direct method TakeDamage(int)
        var method = ps.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
        if (method != null)
        {
            try
            {
                var result = method.Invoke(ps, new object[] { amount });
                if (result is bool) return (bool)result;
                // otherwise try to determine death by hp field/property
            }
            catch { Debug.LogWarning($"[PlayerStatDamageAdapter] Invoking PlayerStat.TakeDamage failed on {gameObject.name}"); }
        }

        // 2) Try hp field
        var hpField = ps.GetType().GetField("hp");
        if (hpField != null)
        {
            try
            {
                int hpVal = (int)hpField.GetValue(ps);
                hpVal = Mathf.Max(0, hpVal - amount);
                hpField.SetValue(ps, hpVal);
                Debug.Log($"[PlayerStatDamageAdapter] {gameObject.name} took {amount}. hp now {hpVal}");
                if (hpVal <= 0) HandleDeath();
                return hpVal <= 0;
            }
            catch { }
        }

        // 3) Try hp property
        var hpProp = ps.GetType().GetProperty("hp") ?? ps.GetType().GetProperty("HP") ?? ps.GetType().GetProperty("Hp");
        if (hpProp != null && hpProp.CanRead && hpProp.CanWrite)
        {
            try
            {
                int hpVal = (int)hpProp.GetValue(ps);
                hpVal = Mathf.Max(0, hpVal - amount);
                hpProp.SetValue(ps, hpVal);
                Debug.Log($"[PlayerStatDamageAdapter] {gameObject.name} took {amount}. hp now {hpVal}");
                if (hpVal <= 0) HandleDeath();
                return hpVal <= 0;
            }
            catch { }
        }

        Debug.LogWarning($"[PlayerStatDamageAdapter] Cannot apply damage to {gameObject.name}: no TakeDamage or hp field/property found.");
        return false;
    }

    void HandleDeath()
    {
        // Try to call OnDeath on PlayerStat if it exists
        var onDeath = ps.GetType().GetMethod("OnDeath", Type.EmptyTypes);
        if (onDeath != null)
        {
            try { onDeath.Invoke(ps, null); return; }
            catch { }
        }

        // fallback: notify TurnManager to remove this battler
        if (TurnManager.Instance != null) TurnManager.Instance.RemoveBattler(gameObject, true);
        else Destroy(gameObject);
    }
}