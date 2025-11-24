using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �����Ҫԡ��ä (non-player) ��������ѵ��ѵ�����������칢ͧ�ǡ��
/// - �Դ�� TurnManager ���� GameObject ����� PartyManager ������¡ OnBattlerTurnStart(currentBattler) �ҡ TurnManager
/// - ��� autoAllocateOnLevel = true �����¡ AutoAllocatePoints �� PlayerLevel �����������Ѿ (��ҵԴ���躹�� � ���)
///
/// WIRING NOTES:
/// - Attach this component to the TurnManager GameObject (or a dedicated manager object).
/// - TurnManager should call OnBattlerTurnStart(CurrentBattlerObject) from its StartTurn() method for AI-controlled characters.
/// - Ensure turnManager reference is set (will auto-assign from TurnManager.Instance if null).
/// </summary>
public class PartyAutoAttack : MonoBehaviour
{
    public bool autoAttackEnabled = true;
    public bool autoAllocateOnLevel = true;

    public TurnManager turnManager;

    void Awake()
    {
        if (turnManager == null) 
        {
            turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                Debug.Log("[PartyAutoAttack] turnManager auto-assigned to TurnManager.Instance");
            }
        }
    }

    /// <summary>
    /// ���¡�������������칢ͧ battler (TurnManager ������¡)
    /// </summary>
    public void OnBattlerTurnStart(GameObject battler)
    {
        if (!autoAttackEnabled || battler == null) return;

        // Safety check: ensure turnManager is available
        if (turnManager == null)
        {
            turnManager = TurnManager.Instance;
            if (turnManager == null)
            {
                Debug.LogWarning("[PartyAutoAttack] turnManager is null and TurnManager.Instance not found.");
                return;
            }
        }

        var ce = battler.GetComponent<CharacterEquipment>();
        if (ce == null) return;

        // ����� PlayerLevel �ʴ�����繼�������ѡ ������ (��������¹�����ͧ���)
        var pl = battler.GetComponent<PlayerLevel>();
        if (pl != null) return;

        // ������ѵ�ٷ���ѧ�� hp > 0
        GameObject target = FindFirstMonster();
        if (target == null) return;

        Debug.LogFormat("[PartyAutoAttack] {0} auto-attacking {1}", battler.name, target.name);
        try
        {
            // ���¡ DoNormalAttack �ͧ CharacterEquipment (assumed exist in project)
            ce.DoNormalAttack(target);
            Debug.LogFormat("[PartyAutoAttack] {0} attack complete", battler.name);
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
    /// �ҡ��ͧ������ PartyAutoAttack �Ѵ��� auto allocate ����Ѻ PlayerLevel �ͧ����Ф���
    /// ������¡���ʹ��� (�� subscribe �Ѻ PlayerLevel.OnLevelUp)
    /// </summary>
    public void OnCharacterLeveled(PlayerLevel pl)
    {
        if (pl == null) return;
        if (!autoAllocateOnLevel) return;
        pl.AutoAllocatePoints();
        Debug.LogFormat("[PartyAutoAttack] AutoAllocatePoints applied for {0}", pl.gameObject.name);
    }
}