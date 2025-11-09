using System.Collections.Generic;
using UnityEngine;
using GameRaiwaa.Stat; // <<< จำเป็น: ให้ EnemyStats เห็น IStatusEffect / StatusType

/// <summary>
/// Example EnemyStats component that stores status effects and exposes TickStatusPerTurn().
/// This is defensive/safe: if you already have a StatusManager in the project, you can
/// remove this class and let EnemyStats call StatusManager.OnTurnStart instead.
/// This implementation uses IStatusEffect (see IStatusEffect.cs).
/// </summary>
[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour
{
    [Header("HP / Stats (example)")]
    public int maxHp = 10;
    public int hp = 10;

    // store status effects directly (uses the IStatusEffect interface)
    [SerializeField] List<IStatusEffect> effects = new List<IStatusEffect>();

    /// <summary>
    /// Adds an effect instance. If an effect of same type exists and RefreshIfExists==true,
    /// refresh its remainingTurns instead of stacking.
    /// </summary>
    public void ApplyStatus(IStatusEffect newEffect)
    {
        if (newEffect == null || newEffect.Type == StatusType.None) return;

        for (int i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            if (e != null && e.Type == newEffect.Type)
            {
                if (newEffect.RefreshIfExists)
                {
                    e.RemainingTurns = newEffect.RemainingTurns;
                    Debug.Log($"[EnemyStats] Refreshed status {e.Type} on {gameObject.name} to {e.RemainingTurns} turns.");
                }
                else
                {
                    Debug.Log($"[EnemyStats] Status {e.Type} already present on {gameObject.name}, not stacking.");
                }
                return;
            }
        }

        // add new effect (note: IStatusEffect is likely a concrete class instance in your code)
        effects.Add(newEffect);
        Debug.Log($"[EnemyStats] Applied status {newEffect.Type} to {gameObject.name} for {newEffect.RemainingTurns} turns.");
    }

    /// <summary>
    /// Called by TurnManager at the start of this object's turn.
    /// Processes each effect's OnTurnStart, applies damage if returned, decrements durations and removes expired.
    /// </summary>
    public void TickStatusPerTurn()
    {
        if (effects == null || effects.Count == 0) return;

        // copy to allow safe modification during iteration
        var snapshot = new List<IStatusEffect>(effects);

        foreach (var eff in snapshot)
        {
            if (eff == null) continue;
            if (eff.RemainingTurns <= 0) continue;

            int dmg = 0;
            try
            {
                dmg = eff.OnTurnStart(gameObject);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EnemyStats] Exception while ticking {eff.Type} on {gameObject.name}: {ex.Message}");
            }

            if (dmg > 0)
            {
                // apply damage to this enemy
                TakeDamage(dmg);
                Debug.Log($"[EnemyStats] {gameObject.name} took {dmg} damage from {eff.Type} (status). HP now {hp}/{maxHp}");
            }

            // decrease duration
            eff.RemainingTurns = Mathf.Max(0, eff.RemainingTurns - 1);

            // remove expired
            if (eff.RemainingTurns <= 0)
            {
                effects.Remove(eff);
                Debug.Log($"[EnemyStats] Status {eff.Type} expired on {gameObject.name}");
            }

            // early exit if dead
            if (hp <= 0)
            {
                Debug.Log($"[EnemyStats] {gameObject.name} died from status effects.");
                break;
            }
        }
    }

    public void TakeDamage(int amount)
    {
        hp -= amount;
        if (hp < 0) hp = 0;
        // you may want to invoke death logic here (animation, Drop loot, notify TurnManager)
        if (hp == 0)
        {
            OnDeath();
        }
    }

    void OnDeath()
    {
        Debug.Log($"[EnemyStats] {gameObject.name} died. Notifying TurnManager/cleanup.");
        // If you want TurnManager to remove this battler immediately, call:
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RemoveBattler(gameObject, true);
        }
        else
        {
            // fallback: destroy
            Destroy(gameObject);
        }
    }

    // Optional: expose current effects for UI/inspection
    public IReadOnlyList<IStatusEffect> GetStatusEffects() => effects.AsReadOnly();
}