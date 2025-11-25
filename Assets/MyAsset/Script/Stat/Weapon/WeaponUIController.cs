using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simplified WeaponUIController:
/// - Normal attack: tries to use PlayerEquipment.DoNormalAttack(target) (sync) or DoNormalAttack(target, Action) (async)
/// - Skill: builds targets list preferring selected monster, then invokes UseSkill with common signatures.
/// - Fallback: pointer-based target detection.
/// This version is intentionally compact and avoids duplicated methods / complex blocks to keep compilation stable.
/// </summary>
public class WeaponUIController : MonoBehaviour
{
    public CharacterEquipment playerEquipment;
    public TurnBaseSystem turnManager;

    public Button normalButton;
    public Button skillButton;
    public Button swapButton;

    private bool _actionInProgress = false;
    private Coroutine _recoveryCoroutine;
    public float recoveryTimeout = 6f;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        if (normalButton != null) { normalButton.onClick.RemoveAllListeners(); normalButton.onClick.AddListener(OnNormalButtonClicked); }
        if (skillButton != null) { skillButton.onClick.RemoveAllListeners(); skillButton.onClick.AddListener(OnSkillButtonClicked); }
        if (swapButton != null) { swapButton.onClick.RemoveAllListeners(); swapButton.onClick.AddListener(OnSwapButtonClicked); }
    }

    void OnDestroy()
    {
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalButtonClicked);
        if (skillButton != null) skillButton.onClick.RemoveListener(OnSkillButtonClicked);
        if (swapButton != null) swapButton.onClick.RemoveListener(OnSwapButtonClicked);
    }

    // Normal attack: movement -> attack -> return -> end turn
    public void OnNormalButtonClicked()
    {
        Debug.Log("[WeaponUI] OnNormalButtonClicked");
        if (_actionInProgress) { Debug.Log("[WeaponUI] action in progress, ignoring"); return; }
        _actionInProgress = true;
        StartRecovery();

        var equip = ResolveEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUI] No CharacterEquipment found."); FinishAction(); return; }

        GameObject target = GetSelectedOrPointerTarget();
        if (target == null) { Debug.LogWarning("[WeaponUI] No target for normal attack"); FinishAction(); return; }

        var goAI = equip.gameObject.GetComponent<GoAttck>();
        SetButtons(false);

        if (goAI != null)
        {
            try
            {
                goAI.AttackMonster(target, () =>
                {
                    // try async DoNormalAttack(target, Action)
                    if (InvokeDoNormalAttackWithCallback(equip, target, () =>
                    {
                        goAI.ReturnToStart(() => { EndTurnSafely(); FinishAction(); });
                    }))
                    {
                        return;
                    }

                    // fallback sync
                    try { equip.DoNormalAttack(target); } catch (Exception ex) { Debug.LogWarning("[WeaponUI] DoNormalAttack threw: " + ex); }
                    goAI.ReturnToStart(() => { EndTurnSafely(); FinishAction(); });
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponUI] GoAttck.AttackMonster threw: " + ex);
                try { equip.DoNormalAttack(target); } catch { }
                EndTurnSafely();
                FinishAction();
            }
        }
        else
        {
            if (InvokeDoNormalAttackWithCallback(equip, target, () => { EndTurnSafely(); FinishAction(); })) return;
            try { equip.DoNormalAttack(target); } catch (Exception ex) { Debug.LogWarning("[WeaponUI] DoNormalAttack threw: " + ex); }
            EndTurnSafely();
            FinishAction();
        }
    }

    // Skill: prefer selected target as first element in targets list
    public void OnSkillButtonClicked()
    {
        Debug.Log("[WeaponUI] OnSkillButtonClicked");
        if (_actionInProgress) { Debug.Log("[WeaponUI] action in progress, ignoring"); return; }
        _actionInProgress = true;
        StartRecovery();

        var equip = ResolveEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUI] No CharacterEquipment for skill."); FinishAction(); return; }

        var tbs = turnManager ?? TurnBaseSystem.Instance;
        var targets = new List<GameObject>();

        // prefer selected
        GameObject sel = tbs?.selectedMonster ?? TurnManager.Instance?.selectedMonster;
        if (sel != null)
        {
            // best-effort alive check (if battlers available)
            bool alive = true;
            if (tbs != null)
            {
                alive = false;
                for (int i = 0; i < tbs.battlers.Count && i < tbs.battlerObjects.Count; i++)
                {
                    if (tbs.battlerObjects[i] == sel && tbs.battlers[i] != null && tbs.battlers[i].hp > 0) { alive = true; break; }
                }
            }
            if (alive) targets.Add(sel);
        }

        // add monsters from turn list
        if (tbs != null)
        {
            for (int i = 0; i < tbs.battlerObjects.Count && i < tbs.battlers.Count; i++)
            {
                var go = tbs.battlerObjects[i];
                var b = tbs.battlers[i];
                if (go == null || b == null) continue;
                if (!b.isMonster || b.hp <= 0) continue;
                if (!targets.Contains(go)) targets.Add(go);
            }
        }
        else
        {
            // fallback: try to find any monster colliders in scene (very basic)
            var all = FindObjectsOfType<GameObject>();
            foreach (var g in all)
            {
                if (g == null) continue;
                if (g.GetComponent<IMonsterStat>() != null && !targets.Contains(g)) targets.Add(g);
            }
        }

        if (targets.Count == 0)
        {
            Debug.LogWarning("[WeaponUI] No skill targets");
            FinishAction();
            return;
        }

        Debug.Log("[WeaponUI] Skill targets: " + string.Join(",", targets.Select(x => x != null ? x.name : "null")));

        SetButtons(false);

        var goAI = equip.gameObject.GetComponent<GoAttck>();
        if (goAI != null)
        {
            var mi = goAI.GetType().GetMethod("StrongAttackMonster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                try
                {
                    mi.Invoke(goAI, new object[] { targets[0], new Action(() =>
                    {
                        if (InvokeUseSkillWithBestMatch(equip, targets, () =>
                        {
                            goAI.ReturnToStart(() => { EndTurnSafely(); FinishAction(); });
                        })) return;

                        TryFallbackUseSkill(equip, targets);
                        goAI.ReturnToStart(() => { EndTurnSafely(); FinishAction(); });
                    })});
                    return;
                }
                catch (Exception ex) { Debug.LogWarning("[WeaponUI] StrongAttackMonster invoke failed: " + ex); }
            }
        }

        // no movement AI
        if (InvokeUseSkillWithBestMatch(equip, targets, () => { EndTurnSafely(); FinishAction(); })) return;
        TryFallbackUseSkill(equip, targets);
        EndTurnSafely();
        FinishAction();
    }

    public void OnSwapButtonClicked()
    {
        var equip = ResolveEquipment();
        if (equip == null) return;
        equip.SwapToNextWeapon();
        Debug.Log("[WeaponUI] Swap");
        FinishAction();
    }

    // --- Helpers ---

    CharacterEquipment ResolveEquipment()
    {
        GameObject current = turnManager != null ? turnManager.CurrentBattlerObject : TurnBaseSystem.Instance?.CurrentBattlerObject;
        if (current == null) return playerEquipment;
        if (playerEquipment == null) return current.GetComponent<CharacterEquipment>();
        if (playerEquipment.gameObject != current)
        {
            var fallback = current.GetComponent<CharacterEquipment>();
            if (fallback != null) return fallback;
        }
        return playerEquipment;
    }

    GameObject GetSelectedOrPointerTarget() => GetCurrentSelectedMonster() ?? TryFindMonsterUnderPointer();

    GameObject GetCurrentSelectedMonster()
    {
        if (turnManager != null && turnManager.selectedMonster != null) return turnManager.selectedMonster;
        if (TurnBaseSystem.Instance != null && TurnBaseSystem.Instance.selectedMonster != null) return TurnBaseSystem.Instance.selectedMonster;
        if (TurnManager.Instance != null && TurnManager.Instance.selectedMonster != null) return TurnManager.Instance.selectedMonster;
        return null;
    }

    GameObject TryFindMonsterUnderPointer()
    {
        var cam = Camera.main;
        if (cam == null) return null;
        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);

        try
        {
            var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            foreach (var h in hits) if (h != null)
                {
                    var g = h.gameObject;
                    if (g.GetComponent<IMonsterStat>() != null || g.CompareTag("Enemy") || g.CompareTag("Monster")) return g;
                }
        }
        catch { }

        try
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var g = hit.collider.gameObject;
                if (g.GetComponent<IMonsterStat>() != null || g.CompareTag("Enemy") || g.CompareTag("Monster")) return g;
            }
        }
        catch { }

        return null;
    }

    bool InvokeDoNormalAttackWithCallback(object equipObj, GameObject target, Action onComplete)
    {
        if (equipObj == null) return false;
        var t = equipObj.GetType();
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m.Name != "DoNormalAttack") continue;
            var ps = m.GetParameters();
            if (ps.Length == 2 && typeof(GameObject).IsAssignableFrom(ps[0].ParameterType) &&
                (ps[1].ParameterType == typeof(Action) || ps[1].ParameterType == typeof(System.Action)))
            {
                try { m.Invoke(equipObj, new object[] { target, onComplete ?? (Action)(() => { }) }); return true; }
                catch (Exception ex) { Debug.LogWarning("[WeaponUI] Async DoNormalAttack invoke failed: " + ex); return false; }
            }
        }
        return false;
    }

    bool InvokeUseSkillWithBestMatch(object equipObj, List<GameObject> targets, Action onComplete)
    {
        if (equipObj == null) return false;
        var t = equipObj.GetType();

        // List<GameObject>, Action
        var mListCb = t.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
        if (mListCb != null)
        {
            try { mListCb.Invoke(equipObj, new object[] { targets, onComplete ?? (Action)(() => { }) }); return true; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUI] UseSkill(List,Action) failed: " + ex); }
        }

        // GameObject, Action
        var mSingleCb = t.GetMethod("UseSkill", new Type[] { typeof(GameObject), typeof(Action) });
        if (mSingleCb != null)
        {
            try { mSingleCb.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null, onComplete ?? (Action)(() => { }) }); return true; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUI] UseSkill(GameObject,Action) failed: " + ex); }
        }

        // GameObject
        var mSingle = t.GetMethod("UseSkill", new Type[] { typeof(GameObject) });
        if (mSingle != null)
        {
            try { mSingle.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null }); onComplete?.Invoke(); return true; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUI] UseSkill(GameObject) failed: " + ex); }
        }

        // List<GameObject>
        var mList = t.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>) });
        if (mList != null)
        {
            try { mList.Invoke(equipObj, new object[] { targets }); onComplete?.Invoke(); return true; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUI] UseSkill(List) failed: " + ex); }
        }

        return false;
    }

    void TryFallbackUseSkill(object equipObj, List<GameObject> targets)
    {
        try
        {
            var t = equipObj.GetType();
            var m = t.GetMethod("UseSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null)
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && typeof(List<GameObject>).IsAssignableFrom(ps[0].ParameterType))
                {
                    m.Invoke(equipObj, new object[] { targets }); return;
                }
                if (ps.Length == 1 && typeof(GameObject).IsAssignableFrom(ps[0].ParameterType))
                {
                    m.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null }); return;
                }
            }
            Debug.LogWarning("[WeaponUI] No matching UseSkill signature found on CharacterEquipment.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WeaponUI] Fallback UseSkill attempt threw: " + ex);
        }
    }

    void EndTurnSafely()
    {
        try { turnManager?.EndTurn(); return; } catch { }
        try { TurnBaseSystem.Instance?.EndTurn(); return; } catch { }
        try { TurnManager.Instance?.EndTurn(); return; } catch { }
    }

    void StartRecovery()
    {
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryAfter(recoveryTimeout));
    }

    IEnumerator RecoveryAfter(float secs)
    {
        yield return new WaitForSeconds(secs);
        _actionInProgress = false;
        SetButtons(true);
        _recoveryCoroutine = null;
    }

    void FinishAction()
    {
        _actionInProgress = false;
        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
        SetButtons(true);
    }

    void SetButtons(bool v)
    {
        if (normalButton != null) normalButton.interactable = v;
        if (skillButton != null) skillButton.interactable = v;
        if (swapButton != null) swapButton.interactable = v;
    }
}