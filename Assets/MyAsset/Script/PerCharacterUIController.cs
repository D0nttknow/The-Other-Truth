using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-character UI controller — uses TurnBaseSystem and fallback pointer selection.
/// Provides OnNormalClicked / OnSkillClicked / OnSwapClicked and robustly invokes CharacterEquipment attack/skill.
/// </summary>
[DisallowMultipleComponent]
public class PerCharacterUIController : MonoBehaviour
{
    [Header("References")]
    public CharacterEquipment playerEquipment;
    public TurnBaseSystem turnManager;

    [Header("UI Elements")]
    public Button normalButton;
    public Button skillButton;
    public Button swapButton;
    public Image iconImage;
    public Text nameText;
    public Text hpText;
    public Text skillCooldownText;

    private bool _localActionInProgress = false;
    private Coroutine _recoveryCoroutine = null;
    public float recoveryTimeoutSeconds = 5f;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        if (normalButton != null) { normalButton.onClick.RemoveAllListeners(); normalButton.onClick.AddListener(OnNormalClicked); }
        if (skillButton != null) { skillButton.onClick.RemoveAllListeners(); skillButton.onClick.AddListener(OnSkillClicked); }
        if (swapButton != null) { swapButton.onClick.RemoveAllListeners(); swapButton.onClick.AddListener(OnSwapClicked); }
        RefreshAll();
    }

    void OnDestroy()
    {
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalClicked);
        if (skillButton != null) skillButton.onClick.RemoveListener(OnSkillClicked);
        if (swapButton != null) swapButton.onClick.RemoveListener(OnSwapClicked);
    }

    public void RefreshAll()
    {
        if (nameText != null && playerEquipment != null) nameText.text = playerEquipment.gameObject.name;
        RefreshIcon();
        RefreshCooldown();
        RefreshHP();
    }

    void RefreshIcon()
    {
        if (iconImage == null || playerEquipment == null) return;
        var wi = playerEquipment.currentWeaponItem;
        Sprite s = null;
        if (wi != null)
        {
            var t = wi.GetType();
            var pi = t.GetProperty("icon", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) s = pi.GetValue(wi) as Sprite;
            else
            {
                var fi = t.GetField("icon", BindingFlags.Public | BindingFlags.Instance);
                if (fi != null) s = fi.GetValue(wi) as Sprite;
            }
        }
        if (s != null) { iconImage.sprite = s; iconImage.enabled = true; } else iconImage.enabled = false;
    }

    void RefreshCooldown()
    {
        if (skillCooldownText == null || playerEquipment == null) return;
        var wc = playerEquipment.GetEquippedWeapon();
        if (wc == null) { skillCooldownText.text = ""; return; }
        skillCooldownText.text = (wc.skillCooldownRemaining > 0) ? wc.skillCooldownRemaining.ToString() : "";
    }

    void RefreshHP()
    {
        if (hpText == null || playerEquipment == null) return;
        var ps = playerEquipment.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var hpField = ps.GetType().GetField("hp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var maxField = ps.GetType().GetField("maxHp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? ps.GetType().GetField("maxHp", BindingFlags.Public | BindingFlags.Instance);
            int hp = 0; object max = "?";
            try { if (hpField != null) { var v = hpField.GetValue(ps); if (v is int) hp = (int)v; } } catch { }
            try { if (maxField != null) max = maxField.GetValue(ps); } catch { }
            hpText.text = string.Format("{0}/{1}", hp, max);
        }
    }

    bool CanAct()
    {
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return false;
        return tm.state == TurnBaseSystem.BattleState.WaitingForPlayerInput && tm.IsCurrentTurn(playerEquipment.gameObject);
    }

    // -------------------------
    // Button handlers
    // -------------------------
    public void OnNormalClicked()
    {
        Debug.Log("[PerCharacterUI] OnNormalClicked start for " + (playerEquipment != null ? playerEquipment.gameObject.name : "null"));
        if (!CanAct()) return;
        if (_localActionInProgress) { Debug.Log("[PerCharacterUI] local action in progress - ignore"); return; }
        _localActionInProgress = true;

        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) { _localActionInProgress = false; return; }

        GameObject target = tm.selectedMonster ?? TryFindMonsterUnderPointer();
        if (target == null)
        {
            Debug.LogWarning("[PerCharacterUI] No target selected or under pointer.");
            _localActionInProgress = false;
            return;
        }

        Debug.Log("[PerCharacterUI] Normal target = " + target.name);

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();
        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        if (goAI != null)
        {
            goAI.AttackMonster(target, () =>
            {
                if (InvokeDoNormalAttackWithCallback(playerEquipment, target, () =>
                {
                    goAI.ReturnToStart(() =>
                    {
                        TryFinishAfterAction(tm);
                    });
                })) return;

                try { playerEquipment.DoNormalAttack(target); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] DoNormalAttack threw: " + ex); }
                goAI.ReturnToStart(() =>
                {
                    TryFinishAfterAction(tm);
                });
            });
        }
        else
        {
            if (InvokeDoNormalAttackWithCallback(playerEquipment, target, () =>
            {
                TryFinishAfterAction(tm);
            })) return;
            try { playerEquipment.DoNormalAttack(target); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] DoNormalAttack threw: " + ex); }
            TryFinishAfterAction(tm);
        }
    }

    public void OnSkillClicked()
    {
        Debug.Log("[PerCharacterUI] OnSkillClicked start for " + (playerEquipment != null ? playerEquipment.gameObject.name : "null") + " selected=" + (TurnBaseSystem.Instance?.selectedMonster?.name ?? "null"));

        if (!CanAct()) return;
        if (_localActionInProgress) { Debug.Log("[PerCharacterUI] local action in progress - ignore"); return; }
        _localActionInProgress = true;
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) { _localActionInProgress = false; return; }

        // Build targets, prefer selected
        var targets = new List<GameObject>();
        GameObject selected = tm.selectedMonster ?? TurnManager.Instance?.selectedMonster;
        if (selected != null)
        {
            // ensure alive
            bool alive = false;
            for (int i = 0; i < tm.battlers.Count && i < tm.battlerObjects.Count; i++)
            {
                if (tm.battlerObjects[i] == selected && tm.battlers[i] != null && tm.battlers[i].hp > 0) { alive = true; break; }
            }
            if (alive) targets.Add(selected);
        }

        for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
        {
            var go = tm.battlerObjects[i];
            var b = tm.battlers[i];
            if (go == null || b == null) continue;
            if (!b.isMonster || b.hp <= 0) continue;
            if (targets.Contains(go)) continue;
            targets.Add(go);
        }

        if (targets.Count == 0) { Debug.LogWarning("[PerCharacterUI] No skill targets."); _localActionInProgress = false; return; }

        Debug.Log("[PerCharacterUI] skill targets = [" + string.Join(",", targets.Select(x => x ? x.name : "null")) + "]");

        SetAllButtonsInteractable(false);
        if (InvokeUseSkillWithPossibleSignatures(playerEquipment, targets, () =>
        {
            TryFinishAfterAction(tm);
        })) return;

        TryFallbackUseSkill(playerEquipment, targets);
        TryFinishAfterAction(tm);
    }

    public void OnSwapClicked()
    {
        if (playerEquipment == null) return;
        playerEquipment.SwapToNextWeapon();
        RefreshAll();
        Debug.Log("[PerCharacterUI] Swap clicked for " + playerEquipment.gameObject.name);
    }

    // -------------------------
    // Finish helper used after action completes
    // -------------------------
    void TryFinishAfterAction(TurnBaseSystem tm)
    {
        try
        {
            if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
        }
        catch { }

        try
        {
            if (tm != null) tm.selectedMonster = null;
            else TurnBaseSystem.Instance.selectedMonster = null;
        }
        catch { }

        try
        {
            if (tm != null) tm.OnPlayerReturned();
            else TurnBaseSystem.Instance?.OnPlayerReturned();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] OnPlayerReturned threw: " + ex);
            try { TurnBaseSystem.Instance?.EndTurn(); } catch { }
        }

        SetAllButtonsInteractable(true);
        _localActionInProgress = false;
    }

    // -------------------------
    // Reflection helpers
    // -------------------------
    bool InvokeDoNormalAttackWithCallback(object equipObj, GameObject target, Action onComplete)
    {
        if (equipObj == null) return false;
        var t = equipObj.GetType();
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            if (m.Name != "DoNormalAttack") continue;
            var ps = m.GetParameters();
            if (ps.Length == 2 && typeof(GameObject).IsAssignableFrom(ps[0].ParameterType) && (ps[1].ParameterType == typeof(Action) || ps[1].ParameterType == typeof(System.Action)))
            {
                try { m.Invoke(equipObj, new object[] { target, onComplete ?? (Action)(() => { }) }); return true; }
                catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] Async DoNormalAttack invoke failed: " + ex); return false; }
            }
        }
        return false;
    }

    bool InvokeUseSkillWithCallback(object equipObj, List<GameObject> targets, Action onComplete)
    {
        if (equipObj == null) return false;
        var t = equipObj.GetType();

        // List<GameObject>, Action
        var mListCb = t.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
        if (mListCb != null)
        {
            try { mListCb.Invoke(equipObj, new object[] { targets, onComplete ?? (Action)(() => { }) }); return true; }
            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] UseSkill(List,Action) failed: " + ex); }
        }

        // GameObject, Action
        var mSingleCb = t.GetMethod("UseSkill", new Type[] { typeof(GameObject), typeof(Action) });
        if (mSingleCb != null)
        {
            try { mSingleCb.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null, onComplete ?? (Action)(() => { }) }); return true; }
            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] UseSkill(GameObject,Action) failed: " + ex); }
        }

        // GameObject
        var mSingle = t.GetMethod("UseSkill", new Type[] { typeof(GameObject) });
        if (mSingle != null)
        {
            try { mSingle.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null }); onComplete?.Invoke(); return true; }
            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] UseSkill(GameObject) failed: " + ex); }
        }

        // List<GameObject>
        var mList = t.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>) });
        if (mList != null)
        {
            try { mList.Invoke(equipObj, new object[] { targets }); onComplete?.Invoke(); return true; }
            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] UseSkill(List) failed: " + ex); }
        }

        return false;
    }

    bool InvokeUseSkillWithPossibleSignatures(object equipObj, List<GameObject> targets, Action onComplete)
    {
        // kept for symmetry with WeaponUIController naming
        return InvokeUseSkillWithCallback(equipObj, targets, onComplete);
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
                    m.Invoke(equipObj, new object[] { targets });
                    return;
                }
                if (ps.Length == 1 && typeof(GameObject).IsAssignableFrom(ps[0].ParameterType))
                {
                    m.Invoke(equipObj, new object[] { targets.Count > 0 ? targets[0] : null });
                    return;
                }
            }
            Debug.LogWarning("[PerCharacterUI] No matching UseSkill signature found on CharacterEquipment.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] Fallback UseSkill threw: " + ex);
        }
    }

    // -------------------------
    // Utilities
    // -------------------------
    IEnumerator RecoveryEnableAfterTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetAllButtonsInteractable(true); _recoveryCoroutine = null;
    }

    void SetAllButtonsInteractable(bool v)
    {
        if (normalButton != null) normalButton.interactable = v;
        if (skillButton != null) skillButton.interactable = v;
        if (swapButton != null) swapButton.interactable = v;
    }

    GameObject TryFindMonsterUnderPointer()
    {
        var tbs = turnManager ?? TurnBaseSystem.Instance;
        if (tbs != null && tbs.selectedMonster != null) return tbs.selectedMonster;
        var tm = TurnManager.Instance;
        if (tm != null && tm.selectedMonster != null) return tm.selectedMonster;

        var cam = Camera.main;
        if (cam == null) return null;
        Vector3 screenPos = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(screenPos);

        try
        {
            var hits2d = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            foreach (var c in hits2d) if (c != null)
                {
                    var g = c.gameObject;
                    if (g.GetComponent<IMonsterStat>() != null || g.CompareTag("Enemy") || g.CompareTag("Monster")) return g;
                }
        }
        catch { }

        try
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var g = hit.collider.gameObject;
                if (g.GetComponent<IMonsterStat>() != null || g.CompareTag("Enemy") || g.CompareTag("Monster")) return g;
            }
        }
        catch { }

        return null;
    }
}