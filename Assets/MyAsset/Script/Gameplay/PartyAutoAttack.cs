using UnityEngine;

/// <summary>
/// PartyAutoAttack: สั่งสมาชิกพรรค (non-player) โจมตีอัตโนมัติเมื่อเป็นเทิร์นของพวกเขา.
/// TurnManager ควรเรียก partyAutoAttack.OnBattlerTurnStart(CurrentBattlerObject) เมื่อสลับเทิร์น
/// </summary>
public class PartyAutoAttack : MonoBehaviour
{
    public bool autoAttackEnabled = true;
    public bool autoAllocateOnLevel = true;

    public TurnManager turnManager;

    void Awake()
    {
        if (turnManager == null) turnManager = TurnManager.Instance;
    }

    public void OnBattlerTurnStart(GameObject battler)
    {
        if (!autoAttackEnabled || battler == null) return;

        var ce = battler.GetComponent<CharacterEquipment>();
        if (ce == null) return;

        var pl = battler.GetComponent<PlayerLevel>();
        if (pl != null) return; // skip player-controlled

        GameObject target = FindFirstMonster();
        if (target == null) return;

        Debug.LogFormat("[PartyAutoAttack] {0} auto-attacking {1}", battler.name, target.name);

        try
        {
            ce.DoNormalAttack(target);
            Debug.LogFormat("[PartyAutoAttack] {0} attack invoked", battler.name);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[PartyAutoAttack] DoNormalAttack failed: " + ex);
        }
    }

    GameObject FindFirstMonster()
    {
        if (turnManager == null) turnManager = TurnManager.Instance;
        if (turnManager == null) return null;

        var objs = turnManager.battlerObjects;
        var listBattlers = turnManager.battlers;
        if (objs == null || listBattlers == null) return null;

        for (int i = 0; i < objs.Count && i < listBattlers.Count; i++)
        {
            var go = objs[i];
            var b = listBattlers[i];
            if (go == null || b == null) continue;
            if (b.isMonster && b.hp > 0) return go;
            if (go.CompareTag("Enemy") || go.CompareTag("Monster")) return go;
        }
        return null;
    }

    public void OnCharacterLeveled(PlayerLevel pl)
    {
        if (pl == null) return;
        if (!autoAllocateOnLevel) return;
        pl.AutoAllocatePoints();
        Debug.LogFormat("[PartyAutoAttack] AutoAllocatePoints applied for {0}", pl.gameObject.name);
    }
}