using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple, safe PartyAutoAttack implementation:
/// - Only auto-acts for GameObjects that are explicitly marked (AllyAuto component or tag "AllyAuto").
/// - Skips player-controlled characters (PlayerLevel / PlayerController / tag "Player").
/// - If attack is instant (CharacterEquipment.DoNormalAttack without animation), it calls EndTurn immediately.
/// - If the ally has movement/animation (GoAttck), it uses callbacks and calls EndTurn via TurnBaseSystem when the action completes.
/// </summary>
public class PartyAutoAttack : MonoBehaviour
{
    [Tooltip("If true, this system will auto-attack for allies marked with AllyAuto or tag 'AllyAuto'")]
    public bool autoAttackEnabled = true;

    public void OnBattlerTurnStart(GameObject battler)
    {
        if (!autoAttackEnabled || battler == null) return;

        // Skip real players
        if (battler.GetComponent<PlayerLevel>() != null || battler.GetComponent<PlayerController>() != null || battler.CompareTag("Player"))
        {
            Debug.Log($"[PartyAutoAttack] Skipping battler (player-controlled): {battler.name}");
            return;
        }

        // Require explicit marker for auto ally behavior (avoid accidentally treating players as auto)
        bool hasMarker = battler.GetComponent<AllyAuto>() != null || battler.CompareTag("AllyAuto");
        if (!hasMarker)
        {
            Debug.Log($"[PartyAutoAttack] Skipping {battler.name} because it is not marked AllyAuto.");
            return;
        }

        var ce = battler.GetComponent<CharacterEquipment>();
        if (ce == null)
        {
            Debug.Log($"[PartyAutoAttack] No CharacterEquipment on {battler.name} - skipping auto-attack.");
            return;
        }

        // pick a target (basic: first alive monster)
        GameObject target = FindFirstMonster();
        if (target == null)
        {
            Debug.Log("[PartyAutoAttack] No monster target found for auto-ally: " + battler.name);
            return;
        }

        Debug.Log($"[PartyAutoAttack] {battler.name} auto-attacking {target.name}");

        // If battler has GoAttck, use its animation path and call EndTurn when completed
        var goAI = battler.GetComponent<GoAttck>();
        if (goAI != null)
        {
            try
            {
                goAI.AttackMonster(target, () =>
                {
                    try
                    {
                        // Try async DoNormalAttack with callback if available
                        var ceType = ce.GetType();
                        var doWithCb = ceType.GetMethod("DoNormalAttack", new Type[] { typeof(GameObject), typeof(Action) });
                        if (doWithCb != null)
                        {
                            doWithCb.Invoke(ce, new object[] { target, new Action(() =>
                            {
                                // return to start then end turn
                                goAI.ReturnToStart(() =>
                                {
                                    EndTurnViaManager();
                                });
                            })});
                        }
                        else
                        {
                            // fallback: synchronous
                            ce.DoNormalAttack(target);
                            goAI.ReturnToStart(() => { EndTurnViaManager(); });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[PartyAutoAttack] Exception during animated auto-attack: " + ex);
                        try { EndTurnViaManager(); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PartyAutoAttack] GoAttck.AttackMonster invoke failed: " + ex);
                // fallback to instant attack below
            }
            return;
        }

        // Fallback: instant attack and immediately end turn
        try
        {
            ce.DoNormalAttack(target);
            EndTurnViaManager();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PartyAutoAttack] Instant auto-attack failed: " + ex);
            try { EndTurnViaManager(); } catch { }
        }
    }

    GameObject FindFirstMonster()
    {
        var tbs = TurnBaseSystem.Instance;
        if (tbs != null)
        {
            for (int i = 0; i < tbs.battlerObjects.Count && i < tbs.battlers.Count; i++)
            {
                var go = tbs.battlerObjects[i];
                var b = tbs.battlers[i];
                if (go != null && b != null && b.isMonster && b.hp > 0) return go;
            }
        }
        else
        {
            var tm = TurnManager.Instance;
            if (tm != null)
            {
                for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
                {
                    var go = tm.battlerObjects[i];
                    var b = tm.battlers[i];
                    if (go != null && b != null && b.isMonster && b.hp > 0) return go;
                }
            }
        }
        return null;
    }

    void EndTurnViaManager()
    {
        var tbs = TurnBaseSystem.Instance;
        if (tbs != null) { try { tbs.EndTurn(); } catch (Exception ex) { Debug.LogWarning("[PartyAutoAttack] tbs.EndTurn threw: " + ex); } return; }

        var tm = TurnManager.Instance;
        if (tm != null) { try { tm.EndTurn(); } catch (Exception ex) { Debug.LogWarning("[PartyAutoAttack] tm.EndTurn threw: " + ex); } return; }

        Debug.LogWarning("[PartyAutoAttack] No turn manager available to EndTurn.");
    }
}

/// <summary>
/// Simple marker component. Add to ally NPCs that should auto-act via PartyAutoAttack.
/// (Alternatively, set GameObject tag to 'AllyAuto'.)
/// </summary>
public class AllyAuto : MonoBehaviour { }