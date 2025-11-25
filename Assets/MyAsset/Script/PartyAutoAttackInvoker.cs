using System;
using UnityEngine;

/// <summary>
/// Invoker ที่เติมช่องว่างให้ PartyAutoAttack ถูกเรียกเมื่อ TurnBaseSystem ข้ามผู้เล่นที่มี AutoAttackMarker.
/// - ให้ attach ไว้บน GameObject เดียวกับ TurnBaseSystem (หรือที่ใดก็ได้ใน Scene)
/// - ทำงานแบบ minimal: เรียก PartyAutoAttack.OnBattlerTurnStart(obj) เฉพาะเมื่อ TurnBaseSystem เรียกไม่ครบ (isPlayerControlled==true แต่มี AutoAttackMarker)
/// </summary>
[DefaultExecutionOrder(100)] // ให้ทำหลังส่วนอื่นๆ (ถ้ามี)
public class PartyAutoAttackInvoker : MonoBehaviour
{
    GameObject _lastHandledBattler = null;

    void Update()
    {
        var tbs = TurnBaseSystem.Instance;
        if (tbs == null) return;

        GameObject current = tbs.CurrentBattlerObject;
        if (current == _lastHandledBattler) return; // ไม่เรียกซ้ำ

        _lastHandledBattler = current;

        if (current == null) return;

        // ดูว่า TurnBaseSystem จะมองว่าเป็น player-controlled หรือไม่ (same heuristics used elsewhere)
        bool isPlayerControlled = (current.GetComponent<PlayerLevel>() != null)
                                  || (current.GetComponent<PlayerController>() != null)
                                  || current.CompareTag("Player");

        // ถ้า TurnBaseSystem ถือว่าเป็น player-controlled แต่ object มี marker AutoAttackMarker ให้เรียก PartyAutoAttack
        if (isPlayerControlled)
        {
            if (current.GetComponent<AutoAttackMarker>() != null)
            {
                try
                {
                    var pa = FindObjectOfType<PartyAutoAttack>();
                    if (pa != null)
                    {
                        pa.OnBattlerTurnStart(current);
                        Debug.Log($"[PartyAutoAttackInvoker] Invoked PartyAutoAttack for {current.name} (was player-controlled but has AutoAttackMarker).");
                    }
                    else
                    {
                        Debug.LogWarning("[PartyAutoAttackInvoker] No PartyAutoAttack instance found in scene.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PartyAutoAttackInvoker] Exception invoking PartyAutoAttack: " + ex);
                }
            }
        }
        // else: ถ้าไม่ได้เป็น player-controlled TurnBaseSystem ควรจะเรียก PartyAutoAttack เองแล้ว — ไม่ต้องทำอะไร
    }
}