using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Per-character action panel controller (cleaned & robust)
/// - Removes persistent listeners and binds instance listeners in Start()
/// - Uses TryBeginPlayerActionOnTurnManager to mark action-in-progress (public API or reflection fallback)
/// - Avoids pointer fallback when pointer is over UI
/// - Local and global locks to reduce duplicate activations
/// - Defensive try/catch and cleanup to avoid leaving UI in locked state
/// </summary>
[DisallowMultipleComponent]
public class PerCharacterUIController : MonoBehaviour
{
    [Header("References (set by TurnManager or in Inspector)")]
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

    // cached previous state for less log spam
    private bool _prevIsTurn = false;
    private string _prevWeaponInfo = null;
    private TurnBaseSystem.BattleState _prevTmState = (TurnBaseSystem.BattleState)(-1);

    // Recovery coroutine to avoid permanently stuck UI when callbacks fail
    private Coroutine _recoveryCoroutine = null;
    public float recoveryTimeoutSeconds = 5f;

    // cached components
    private PlayerStat _playerStat = null;

    // local action lock to prevent double activation from this panel
    private bool _localActionInProgress = false;

    // global action lock to prevent multiple panels firing on same click
    private static bool s_globalActionInProgress = false;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;

        // Defensive: remove persistent listeners and bind instance listeners
        if (normalButton != null)
        {
#if UNITY_EDITOR
            try
            {
                int cnt = normalButton.onClick.GetPersistentEventCount();
                if (cnt > 0)
                {
                    Debug.LogWarning("[PerCharacterUI DEBUG] normalButton persistent listeners count=" + cnt + " on panel=" + gameObject.name);
                    for (int i = 0; i < cnt; i++)
                    {
                        var tgt = normalButton.onClick.GetPersistentTarget(i);
                        var m = normalButton.onClick.GetPersistentMethodName(i);
                        Debug.LogWarning("[PerCharacterUI DEBUG] normalButton persistent[" + i + "] target=" + (tgt != null ? tgt.ToString() : "null") + " method=" + (m != null ? m : "null"));
                    }
                }
            }
            catch { }
#endif
            normalButton.onClick.RemoveAllListeners();
            normalButton.onClick.AddListener(OnNormalClicked);
        }

        if (skillButton != null)
        {
#if UNITY_EDITOR
            try
            {
                int cnt = skillButton.onClick.GetPersistentEventCount();
                if (cnt > 0)
                {
                    Debug.LogWarning("[PerCharacterUI DEBUG] skillButton persistent listeners count=" + cnt + " on panel=" + gameObject.name);
                    for (int i = 0; i < cnt; i++)
                    {
                        var tgt = skillButton.onClick.GetPersistentTarget(i);
                        var m = skillButton.onClick.GetPersistentMethodName(i);
                        Debug.LogWarning("[PerCharacterUI DEBUG] skillButton persistent[" + i + "] target=" + (tgt != null ? tgt.ToString() : "null") + " method=" + (m != null ? m : "null"));
                    }
                }
            }
            catch { }
#endif
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OnSkillClicked);
        }

        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(OnSwapClicked);
        }

#if UNITY_EDITOR
        // Debug: list instances (helpful to detect duplicate panels)
        try
        {
            var all = FindObjectsOfType<PerCharacterUIController>();
            Debug.Log("[PerCharacterUI DEBUG] Found " + all.Length + " PerCharacterUIController instances in scene (this=" + gameObject.name + ")");
            foreach (var pc in all)
            {
                var pe = pc.playerEquipment != null ? pc.playerEquipment.gameObject.name : "null";
                Debug.Log("[PerCharacterUI DEBUG] panel=" + pc.gameObject.name + " playerEquipment=" + pe + " active=" + pc.gameObject.activeInHierarchy);
            }
        }
        catch { }
#endif

        CacheComponents();
        RefreshAll();
    }

    void OnDestroy()
    {
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalClicked);
        if (skillButton != null) skillButton.onClick.RemoveListener(OnSkillClicked);
        if (swapButton != null) swapButton.onClick.RemoveListener(OnSwapClicked);
    }

    void Update()
    {
        UpdateInteractableState();
        RefreshCooldown();
        RefreshHP();
    }

    void CacheComponents()
    {
        _playerStat = playerEquipment != null ? playerEquipment.GetComponent<PlayerStat>() : null;
    }

    public void RefreshAll()
    {
        CacheComponents();
        RefreshIcon();
        RefreshHP();
        RefreshCooldown();
        if (nameText != null && playerEquipment != null) nameText.text = playerEquipment.gameObject.name;
    }

    void RefreshIcon()
    {
        if (iconImage == null || playerEquipment == null) return;
        var wi = playerEquipment.currentWeaponItem;
        if (wi != null && wi.icon != null)
        {
            iconImage.sprite = wi.icon;
            iconImage.enabled = true;
        }
        else iconImage.enabled = false;
    }

    void RefreshHP()
    {
        if (hpText == null || playerEquipment == null) return;
        var ps = _playerStat ?? playerEquipment.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var maxHpProp = ps.GetType().GetProperty("maxHp");
            var maxHp = maxHpProp != null ? maxHpProp.GetValue(ps) : "?";
            hpText.text = string.Format("{0}/{1}", GetFieldInt(ps, "hp"), maxHp);
            _playerStat = ps;
        }
    }

    void RefreshCooldown()
    {
        if (skillCooldownText == null || playerEquipment == null) return;
        var wc = playerEquipment.GetEquippedWeapon();
        if (wc == null) { skillCooldownText.text = ""; return; }
        int rem = wc.skillCooldownRemaining;
        skillCooldownText.text = rem > 0 ? rem.ToString() : "";
    }

    void UpdateInteractableState()
    {
        var tm = turnManager ?? TurnBaseSystem.Instance;

        var playerName = playerEquipment != null ? playerEquipment.gameObject.name : "null";
        var isTurn = (tm != null && playerEquipment != null) ? tm.IsCurrentTurn(playerEquipment.gameObject) : false;
        var weapon = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
        var wcInfo = weapon != null ? ("wc present cooldown=" + weapon.skillCooldownRemaining) : "wc=null";
        var tmState = tm != null ? tm.state : (TurnBaseSystem.BattleState)(-1);

        bool changed = false;
        if (isTurn != _prevIsTurn) changed = true;
        if (_prevWeaponInfo == null || wcInfo != _prevWeaponInfo) changed = true;
        if (tmState != _prevTmState) changed = true;

        if (changed)
        {
            Debug.Log("[UI DEBUG] Panel=" + gameObject.name + " player=" + playerName + " isTurn=" + isTurn
                      + " tmState=" + (tm != null ? tm.state.ToString() : "null") + " weapon=" + wcInfo);

            _prevIsTurn = isTurn;
            _prevWeaponInfo = wcInfo;
            _prevTmState = tmState;
        }

        bool isActiveTurn = (tm != null && playerEquipment != null && tm.IsCurrentTurn(playerEquipment.gameObject)
                             && tm.state == TurnBaseSystem.BattleState.WaitingForPlayerInput);

        if (normalButton != null) normalButton.interactable = isActiveTurn && !_localActionInProgress && !s_globalActionInProgress;
        if (skillButton != null)
        {
            var wc = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
            skillButton.interactable = isActiveTurn && wc != null && wc.IsSkillReady() && !_localActionInProgress && !s_globalActionInProgress;
        }
        if (swapButton != null) swapButton.interactable = isActiveTurn && !_localActionInProgress && !s_globalActionInProgress;
    }

    // Try to get selected monster; fallback to pointer raycast unless pointer is over UI
    GameObject GetSelectedOrPointerMonster(TurnBaseSystem tm)
    {
        // prefer tm.selectedMonster if present
        if (tm != null && tm.selectedMonster != null) return tm.selectedMonster;

        // fallback: try TurnManager if assigned and different
        try
        {
            if (turnManager != null && turnManager != tm)
            {
                var field = turnManager.GetType().GetField("selectedMonster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(turnManager) as GameObject;
                    if (val != null) return val;
                }
            }
        }
        catch { }

        // If pointer is over UI, avoid pointer fallback (prevents catching world objects under UI)
        try
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
        }
        catch { }

        var cam = Camera.main;
        if (cam == null) return null;

        Vector3 screenPos = Input.mousePosition;
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        try
        {
            var hits2d = Physics2D.OverlapPointAll(new Vector2(worldPos.x, worldPos.y));
            foreach (var c in hits2d)
            {
                if (c == null) continue;
                var go = c.gameObject;
                if (go.GetComponent<IMonsterStat>() != null || go.CompareTag("Enemy") || go.CompareTag("Monster")) return go;
            }
        }
        catch { }

        try
        {
            var ray2 = Physics2D.RaycastAll(new Vector2(worldPos.x, worldPos.y), Vector2.zero);
            foreach (var r in ray2)
            {
                if (r.collider == null) continue;
                var go = r.collider.gameObject;
                if (go.GetComponent<IMonsterStat>() != null || go.CompareTag("Enemy") || go.CompareTag("Monster")) return go;
            }
        }
        catch { }

        try
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var go = hit.collider.gameObject;
                if (go.GetComponent<IMonsterStat>() != null || go.CompareTag("Enemy") || go.CompareTag("Monster")) return go;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Try to mark player action as begun on TurnBaseSystem.
    /// Returns true if succeeded (public API called or private flag set).
    /// </summary>
    bool TryBeginPlayerActionOnTurnManager(TurnBaseSystem tm)
    {
        if (tm == null) return false;

        // Prefer public API BeginPlayerAction if present
        try
        {
            var pub = tm.GetType().GetMethod("BeginPlayerAction", BindingFlags.Instance | BindingFlags.Public);
            if (pub != null)
            {
                pub.Invoke(tm, null);
                Debug.Log("[PerCharacterUI] Invoked TurnManager.BeginPlayerAction() (public)");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] BeginPlayerAction public invocation failed: " + ex);
        }

        // fallback: try set private field _playerActionInProgress = true
        try
        {
            var f = tm.GetType().GetField("_playerActionInProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null)
            {
                f.SetValue(tm, true);
                Debug.Log("[PerCharacterUI] Set TurnManager._playerActionInProgress = true (via reflection)");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] Reflection fallback to set _playerActionInProgress failed: " + ex);
        }

        return false;
    }

    public void OnNormalClicked()
    {
        Debug.Log("[PerCharacterUI] OnNormalClicked invoked panel=" + gameObject.name + " player=" + (playerEquipment != null ? playerEquipment.gameObject.name : "null") + " globalLock=" + s_globalActionInProgress);

        if (!CanAct()) return;
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return;

        if (tm.CurrentBattlerObject != playerEquipment.gameObject)
        {
            Debug.LogWarning("[PerCharacterUI] OnNormalClicked but this panel is not CurrentBattlerObject. Ignoring. panel=" + (playerEquipment != null ? playerEquipment.gameObject.name : "null") + " current=" + (tm.CurrentBattlerObject != null ? tm.CurrentBattlerObject.name : "null"));
            return;
        }

        // Early global lock: prevent other panels from starting concurrently
        if (s_globalActionInProgress)
        {
            Debug.LogWarning("[PerCharacterUI] Ignored OnNormalClicked because global action lock is set. panel=" + gameObject.name);
            return;
        }
        // set both locks early so duplicate calls are ignored immediately
        s_globalActionInProgress = true;
        _localActionInProgress = true;

        // Prefer explicit selection on the TurnBaseSystem (tm.selectedMonster or singleton instance) before pointer fallback
        GameObject target = null;
        try
        {
            if (tm != null && tm.selectedMonster != null) target = tm.selectedMonster;
            else
            {
                var inst = TurnBaseSystem.Instance;
                if (inst != null && inst.selectedMonster != null) target = inst.selectedMonster;
            }
        }
        catch { }

        if (target == null) target = GetSelectedOrPointerMonster(tm);

        if (target == null)
        {
            Debug.LogWarning("[PerCharacterUI] No target selected or found under pointer");
            // cleanup locks before returning
            _localActionInProgress = false;
            s_globalActionInProgress = false;
            return;
        }

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();

        bool beganAction = false;

        // Try to inform TurnBaseSystem that a player action is starting (blocks EndTurn).
        try
        {
            beganAction = TryBeginPlayerActionOnTurnManager(tm);
            if (beganAction) Debug.Log("[PerCharacterUI] Began player action on TurnManager");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] TryBeginPlayerActionOnTurnManager threw: " + ex);
            beganAction = false;
        }

        // disable buttons immediately and start recovery timer
        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        if (goAI != null)
        {
            Debug.Log("[PerCharacterUI] OnNormalClicked start player=" + playerEquipment.gameObject.name + " target=" + target.name);

            try
            {
                // AttackMonster should call this callback at the hit-frame
                goAI.AttackMonster(target, () =>
                {
                    try
                    {
                        Debug.Log("[PerCharacterUI] Attack hit callback - applying damage via CharacterEquipment");

                        // Apply damage; when damage application completes it should call onComplete
                        playerEquipment.DoNormalAttack(target, () =>
                        {
                            try
                            {
                                Debug.Log("[PerCharacterUI] Damage applied callback - returning to start");

                                // Return to start, then notify TurnBaseSystem by calling OnPlayerReturned (so TurnBaseSystem manages EndTurn)
                                goAI.ReturnToStart(() =>
                                {
                                    try
                                    {
                                        Debug.Log("[PerCharacterUI] ReturnToStart complete - notifying TurnBaseSystem.OnPlayerReturned");
                                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                                        try { var tbs = TurnBaseSystem.Instance; if (tbs != null) tbs.selectedMonster = null; } catch { }

                                        if (beganAction)
                                        {
                                            try { tm.OnPlayerReturned(); }
                                            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                                        }
                                        else
                                        {
                                            try { tm.OnPlayerReturned(); } catch { }
                                        }
                                    }
                                    catch (Exception exInner)
                                    {
                                        Debug.LogWarning("[PerCharacterUI] Exception in ReturnToStart callback: " + exInner);
                                    }
                                    finally
                                    {
                                        SetAllButtonsInteractable(true);
                                        _localActionInProgress = false;
                                        s_globalActionInProgress = false;
                                    }
                                });
                            }
                            catch (Exception exDo)
                            {
                                Debug.LogWarning("[PerCharacterUI] Exception during DoNormalAttack callback: " + exDo);
                                // ensure cleanup
                                _localActionInProgress = false;
                                s_globalActionInProgress = false;
                                SetAllButtonsInteractable(true);
                            }
                        });
                    }
                    catch (Exception exHit)
                    {
                        Debug.LogWarning("[PerCharacterUI] Exception in attack-hit callback: " + exHit);
                        _localActionInProgress = false;
                        s_globalActionInProgress = false;
                        SetAllButtonsInteractable(true);
                    }
                });
            }
            catch (Exception exAttack)
            {
                Debug.LogWarning("[PerCharacterUI] Exception when calling goAI.AttackMonster: " + exAttack);
                _localActionInProgress = false;
                s_globalActionInProgress = false;
                SetAllButtonsInteractable(true);
            }
        }
        else
        {
            // fallback: no movement AI — call damage then notify TurnBaseSystem
            try
            {
                playerEquipment.DoNormalAttack(target, () =>
                {
                    try
                    {
                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                        try { var tbs = TurnBaseSystem.Instance; if (tbs != null) tbs.selectedMonster = null; } catch { }

                        if (beganAction)
                        {
                            try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                        }
                        else
                        {
                            try { tm.OnPlayerReturned(); } catch { }
                        }

                        SetAllButtonsInteractable(true);
                    }
                    catch (Exception exCB)
                    {
                        Debug.LogWarning("[PerCharacterUI] Exception in DoNormalAttack fallback callback: " + exCB);
                        // ensure cleanup
                        _localActionInProgress = false;
                        s_globalActionInProgress = false;
                        SetAllButtonsInteractable(true);
                    }
                    finally
                    {
                        _localActionInProgress = false;
                        s_globalActionInProgress = false;
                    }
                });
            }
            catch (Exception exDo)
            {
                Debug.LogWarning("[PerCharacterUI] Exception when calling DoNormalAttack fallback: " + exDo);
                _localActionInProgress = false;
                s_globalActionInProgress = false;
                SetAllButtonsInteractable(true);
            }
        }
    }

    public void OnSkillClicked()
    {
        // Same pattern applied: local + global locks and TryBeginPlayerActionOnTurnManager usage
        if (!CanAct()) return;
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return;

        if (tm.CurrentBattlerObject != playerEquipment.gameObject)
        {
            Debug.LogWarning("[PerCharacterUI] OnSkillClicked but this panel is not CurrentBattlerObject. Ignoring. panel=" + (playerEquipment != null ? playerEquipment.gameObject.name : "null") + " current=" + (tm.CurrentBattlerObject != null ? tm.CurrentBattlerObject.name : "null"));
            return;
        }

        if (s_globalActionInProgress)
        {
            Debug.LogWarning("[PerCharacterUI] Ignored OnSkillClicked because global action lock is set. panel=" + gameObject.name);
            return;
        }

        // set locks early to avoid duplicates
        s_globalActionInProgress = true;
        _localActionInProgress = true;

        // prefer tm.selectedMonster first then fallback
        GameObject selected = null;
        try
        {
            if (tm != null && tm.selectedMonster != null) selected = tm.selectedMonster;
            else
            {
                var inst = TurnBaseSystem.Instance;
                if (inst != null && inst.selectedMonster != null) selected = inst.selectedMonster;
            }
        }
        catch { }

        if (selected == null) selected = GetSelectedOrPointerMonster(tm);

        if (selected == null)
        {
            Debug.LogWarning("[PerCharacterUI] No target selected or found under pointer for skill");
            _localActionInProgress = false;
            s_globalActionInProgress = false;
            return;
        }

        bool beganAction = TryBeginPlayerActionOnTurnManager(tm);

        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();

        if (selected != null && goAI != null)
        {
            Debug.Log("[PerCharacterUI] SkillClicked: will StrongAttackMonster to selected target: " + selected.name);
            try
            {
                goAI.StrongAttackMonster(selected, () =>
                {
                    try
                    {
                        var ceType = playerEquipment.GetType();
                        var useWithCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                        var targets = new List<GameObject> { selected };

                        if (useWithCb != null)
                        {
                            useWithCb.Invoke(playerEquipment, new object[] { targets, new Action(() =>
                            {
                                try
                                {
                                    goAI.ReturnToStart(() =>
                                    {
                                        try { if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; } } catch { }
                                        try { var tbs = TurnBaseSystem.Instance; if (tbs != null) tbs.selectedMonster = null; } catch { }

                                        if (beganAction) { try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); } }
                                        else { try { tm.OnPlayerReturned(); } catch { } }

                                        SetAllButtonsInteractable(true);
                                    });
                                }
                                catch (Exception exInner) { Debug.LogWarning("[PerCharacterUI] Exception returning after skill callback: " + exInner); SetAllButtonsInteractable(true); }
                                finally { _localActionInProgress = false; s_globalActionInProgress = false; }
                            })});
                        }
                        else
                        {
                            playerEquipment.UseSkill(targets);
                            goAI.ReturnToStart(() =>
                            {
                                try { if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; } } catch { }
                                try { var tbs = TurnBaseSystem.Instance; if (tbs != null) tbs.selectedMonster = null; } catch { }
                                if (beganAction) { try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); } }
                                else { try { tm.OnPlayerReturned(); } catch { } }

                                SetAllButtonsInteractable(true);
                                _localActionInProgress = false;
                                s_globalActionInProgress = false;
                            });
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] Exception while invoking UseSkill after StrongAttack: " + ex); SetAllButtonsInteractable(true); _localActionInProgress = false; s_globalActionInProgress = false; }
                });
            }
            catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] Exception while calling StrongAttackMonster: " + ex); SetAllButtonsInteractable(true); _localActionInProgress = false; s_globalActionInProgress = false; }
        }
        else
        {
            // fallback to area-use (existing behaviour)
            var targetsAll = new List<GameObject>();
            for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
            {
                var go = tm.battlerObjects[i];
                var b = tm.battlers[i];
                if (go != null && b != null && b.isMonster && b.hp > 0) targetsAll.Add(go);
            }
            if (targetsAll.Count == 0) { Debug.LogWarning("[PerCharacterUI] No valid skill targets (fallback)."); SetAllButtonsInteractable(true); _localActionInProgress = false; s_globalActionInProgress = false; return; }

            try
            {
                var ceType2 = playerEquipment.GetType();
                var useWithCb2 = ceType2.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                if (useWithCb2 != null)
                {
                    useWithCb2.Invoke(playerEquipment, new object[] { targetsAll, new Action(() =>
                    {
                        try { if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; } } catch { }
                        try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                        SetAllButtonsInteractable(true);
                    })});
                }
                else
                {
                    playerEquipment.UseSkill(targetsAll);
                    if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                    try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                    SetAllButtonsInteractable(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerCharacterUI] UseSkill threw (fallback all-targets): " + ex);
                if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); }
                SetAllButtonsInteractable(true);
            }
            finally
            {
                _localActionInProgress = false;
                s_globalActionInProgress = false;
            }
        }
    }

    public void OnSwapClicked()
    {
        if (!CanAct()) return;
        playerEquipment.SwapToNextWeapon();
        RefreshIcon();
    }

    bool CanAct()
    {
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return false;
        return tm.state == TurnBaseSystem.BattleState.WaitingForPlayerInput && tm.IsCurrentTurn(playerEquipment.gameObject);
    }

    void SetAllButtonsInteractable(bool v)
    {
        if (normalButton != null) normalButton.interactable = v;
        if (skillButton != null) skillButton.interactable = v;
        if (swapButton != null) swapButton.interactable = v;
    }

    IEnumerator RecoveryEnableAfterTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Debug.LogWarning("[PerCharacterUIController] Recovery timeout reached (" + seconds + "s) — re-enabling buttons to avoid stuck state.");
        SetAllButtonsInteractable(true);
        _recoveryCoroutine = null;
    }

    int GetFieldInt(object obj, string name)
    {
        if (obj == null) return 0;
        var t = obj.GetType();
        var f = t.GetField(name);
        if (f != null) { var val = f.GetValue(obj); return val is int ? (int)val : 0; }
        var p = t.GetProperty(name);
        if (p != null) { var val = p.GetValue(obj); return val is int ? (int)val : 0; }
        return 0;
    }
}