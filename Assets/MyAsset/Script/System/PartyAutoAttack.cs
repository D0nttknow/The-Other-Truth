using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สั่งสมาชิกพรรค (non-player) ให้โจมตีอัตโนมัติเมื่อเป็นเทิร์นของพวกเขา
/// - ติดบน TurnBaseSystem หรือ GameObject ที่เป็น PartyManager และเรียก OnBattlerTurnStart(currentBattler) จาก TurnBaseSystem
/// - ถ้า autoAllocateOnLevel = true จะเรียก AutoAllocatePoints บน PlayerLevel เมื่อเลเวลอัพ (ถ้าติดอยู่บนคน ๆ นั้น)
/// </summary>
public class PartyAutoAttack : MonoBehaviour
{
    public bool autoAttackEnabled = true;
    public bool autoAllocateOnLevel = true;

    public TurnBaseSystem turnManager;

    void Awake()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
    }

    /// <summary>
    /// เรียกเมื่อเริ่มเทิร์นของ battler (TurnBaseSystem ควรเรียก)
    /// </summary>
    public void OnBattlerTurnStart(GameObject battler)
    {
        if (!autoAttackEnabled || battler == null) return;

        var ce = battler.GetComponent<CharacterEquipment>();
        if (ce == null) return;

        // ถ้ามี PlayerLevel แสดงว่าเป็นผู้เล่นหลัก ให้ข้าม (หรือเปลี่ยนตามต้องการ)
        var pl = battler.GetComponent<PlayerLevel>();
        if (pl != null) return;

        // หาเป้าศัตรูที่ยังมี hp > 0
        GameObject target = FindFirstMonster();
        if (target == null) return;

        Debug.LogFormat("[PartyAutoAttack] {0} auto-attacking {1}", battler.name, target.name);
        try
        {
            // เรียก DoNormalAttack ของ CharacterEquipment (assumed exist in project)
            ce.DoNormalAttack(target, () =>
            {
                Debug.LogFormat("[PartyAutoAttack] {0} attack complete", battler.name);
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[PartyAutoAttack] DoNormalAttack threw: " + ex);
        }
    }

    GameObject FindFirstMonster()
    {
        if (turnManager == null) return null;
        for (int i = 0; i < turnManager.battlerObjects.Count && i < turnManager.battlers.Count; i++)
        {
            var go = turnManager.battlerObjects[i];
            var b = turnManager.battlers[i];
            if (go != null && b != null && b.isMonster && b.hp > 0) return go;
        }
        return null;
    }

    /// <summary>
    /// หากต้องการให้ PartyAutoAttack จัดการ auto allocate สำหรับ PlayerLevel ของตัวละครใดๆ
    /// ให้เรียกเมทอดนี้ (เช่น subscribe กับ PlayerLevel.OnLevelUp)
    /// </summary>
    public void OnCharacterLeveled(PlayerLevel pl)
    {
        if (pl == null) return;
        if (!autoAllocateOnLevel) return;
        pl.AutoAllocatePoints();
        Debug.LogFormat("[PartyAutoAttack] AutoAllocatePoints applied for {0}", pl.gameObject.name);
    }
}