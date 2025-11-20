using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Per-character action panel controller (updated):
/// - Use callbacks from CharacterEquipment so EndTurn is called only after animation/processing finishes.
/// - Skill now waits for UseSkill onComplete callback before calling EndTurn.
/// - Adds a small recovery timeout to avoid UI stuck if callbacks never arrive (debug only).
///
/// Minor improvements added:
/// - Cache PlayerStat reference when possible to avoid repeated GetComponent calls every frame.
/// - Provide a small CacheComponents() helper and call it from RefreshAll so the component is robust when
///   TurnBaseSystem assigns playerEquipment after this component's Start.
/// - Slightly reduce per-frame allocation by avoiding repeated reflection calls when not needed.
/// - Improved skill flow: prefer selectedMonster + GoAttck movement path to avoid "random target" behavior.
/// - Prevent re-entry by local action lock to avoid double-activations.
/// - Fallback: if TurnBaseSystem.selectedMonster==null (e.g. UI listener cleared it), try to find the clicked monster under the mouse pointer.
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

    // --- Added cached prev-state fields to avoid per-frame log spam ---
    private bool _prevIsTurn = false;
    private string _prevWeaponInfo = null;
    private TurnBaseSystem.BattleState _prevTmState = (TurnBaseSystem.BattleState)(-1);

    // Recovery coroutine to avoid permanently stuck UI when callbacks fail
    private Coroutine _recoveryCoroutine = null;
    public float recoveryTimeoutSeconds = 5f;

    // cached components to avoid repeated GetComponent calls
    private PlayerStat _playerStat = null;

    // NEW: local action lock to prevent double activation from this panel
    private bool _localActionInProgress = false;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;

        // Defensive: remove any persistent listeners (from Prefab/Inspector) and bind instance listeners only.
        // This ensures the button will call this panel's handlers rather than an inspector-bound MainCharacter handler.
        if (normalButton != null)
        {
            normalButton.onClick.RemoveAllListeners();
            normalButton.onClick.AddListener(OnNormalClicked);
        }
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OnSkillClicked);
        }
        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(OnSwapClicked);
        }

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

    // Cache references that are safe to reuse between frames.
    // Call whenever playerEquipment might be (re)assigned.
    void CacheComponents()
    {
        _playerStat = playerEquipment != null ? playerEquipment.GetComponent<PlayerStat>() : null;
    }

    public void RefreshAll()
    {
        // Re-cache components in case TurnBaseSystem assigned playerEquipment programmatically after this component's Start
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

        // use cached PlayerStat if available
        var ps = _playerStat ?? playerEquipment.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var maxHpProp = ps.GetType().GetProperty("maxHp");
            var maxHp = maxHpProp != null ? maxHpProp.GetValue(ps) : "?";
            hpText.text = string.Format("{0}/{1}", GetFieldInt(ps, "hp"), maxHp);
            // cache for subsequent frames
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

        // compute current values
        var playerName = playerEquipment != null ? playerEquipment.gameObject.name : "null";
        var isTurn = (tm != null && playerEquipment != null) ? tm.IsCurrentTurn(playerEquipment.gameObject) : false;
        var weapon = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
        var wcInfo = weapon != null ? ("wc present cooldown=" + weapon.skillCooldownRemaining) : "wc=null";
        var tmState = tm != null ? tm.state : (TurnBaseSystem.BattleState)(-1);

        // Log only when something meaningful changed (avoids per-frame spam)
        bool changed = false;
        if (isTurn != _prevIsTurn) changed = true;
        if (_prevWeaponInfo == null || wcInfo != _prevWeaponInfo) changed = true;
        if (tmState != _prevTmState) changed = true;

        if (changed)
        {
            Debug.Log("[UI DEBUG] Panel=" + gameObject.name + " player=" + playerName + " isTurn=" + isTurn
                      + " tmState=" + (tm != null ? tm.state.ToString() : "null") + " weapon=" + wcInfo);

            // update cached prev values
            _prevIsTurn = isTurn;
            _prevWeaponInfo = wcInfo;
            _prevTmState = tmState;
        }

        bool isActiveTurn = (tm != null && playerEquipment != null && tm.IsCurrentTurn(playerEquipment.gameObject)
                             && tm.state == TurnBaseSystem.BattleState.WaitingForPlayerInput);

        if (normalButton != null) normalButton.interactable = isActiveTurn && !_localActionInProgress;
        if (skillButton != null)
        {
            var wc = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
            skillButton.interactable = isActiveTurn && wc != null && wc.IsSkillReady() && !_localActionInProgress;
        }
        if (swapButton != null) swapButton.interactable = isActiveTurn && !_localActionInProgress;
    }

    // Helper: try to get selected monster, but if null attempt to find a monster under the current pointer
    GameObject GetSelectedOrPointerMonster(TurnBaseSystem tm)
    {
        if (tm != null && tm.selectedMonster != null) return tm.selectedMonster;

        // Fallback: try TurnManager if assigned and different
        try
        {
            if (turnManager != null && turnManager != tm)
            {
                // use reflection in case TurnManager type differs
                var field = turnManager.GetType().GetField("selectedMonster");
                if (field != null)
                {
                    var val = field.GetValue(turnManager) as GameObject;
                    if (val != null) return val;
                }
            }
        }
        catch { }

        // If pointer is over UI, avoid using pointer fallback (prevents catching world objects under UI)
        try
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return null;
            }
        }
        catch { }

        // Raycast from pointer position to find a monster under cursor
        var cam = Camera.main;
        if (cam == null) return null;

        Vector3 screenPos = Input.mousePosition;
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        // Try 2D overlap first
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

        // Try 2D ray
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

        // Try 3D raycast
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

    // Try to set player's action-in-progress on the Turn manager so EndTurn is blocked until OnPlayerReturned
    void TryBeginPlayerActionOnTurnManager(TurnBaseSystem tm)
    {
        if (tm == null) return;
        try
        {
            // try method BeginPlayerAction()
            var m = tm.GetType().GetMethod("BeginPlayerAction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null)
            {
                m.Invoke(tm, null);
                Debug.Log("[PerCharacterUI] Invoked TurnManager.BeginPlayerAction()");
                return;
            }

            // fallback: try set private field _playerActionInProgress = true
            var f = tm.GetType().GetField("_playerActionInProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null)
            {
                f.SetValue(tm, true);
                Debug.Log("[PerCharacterUI] Set TurnManager._playerActionInProgress = true (via reflection)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] Failed to set playerActionInProgress on TurnManager via reflection: " + ex);
        }
    }

    public void OnNormalClicked()
    {
        if (!CanAct()) return;
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return;

        // Defensive: ensure this panel belongs to current battler (should already be ensured by CanAct)
        if (tm.CurrentBattlerObject != playerEquipment.gameObject)
        {
            Debug.LogWarning("[PerCharacterUI] OnNormalClicked but this panel is not CurrentBattlerObject. Ignoring. panel=" + playerEquipment.gameObject.name + " current=" + (tm.CurrentBattlerObject != null ? tm.CurrentBattlerObject.name : "null"));
            return;
        }

        // Prevent re-entry
        if (_localActionInProgress) { Debug.Log("[PerCharacterUI] OnNormalClicked ignored because local action in progress"); return; }
        _localActionInProgress = true;

        // Try to get selected monster; fallback to pointer raycast to recover from selection-clearing listeners.
        var target = GetSelectedOrPointerMonster(tm);
        if (target == null) { Debug.LogWarning("[PerCharacterUI] No target selected or found under pointer"); _localActionInProgress = false; return; }

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();

        // Tell TurnManager that player action begins (block EndTurn while action in progress)
        TryBeginPlayerActionOnTurnManager(tm);

        // disable buttons immediately and start recovery timer
        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        if (goAI != null)
        {
            Debug.Log("[PerCharacterUI] OnNormalClicked start player=" + playerEquipment.gameObject.name + " target=" + target.name);

            // AttackMonster should call this callback at the hit-frame
            goAI.AttackMonster(target, () =>
            {
                Debug.Log("[PerCharacterUI] Attack hit callback - applying damage via CharacterEquipment");

                // Apply damage; when damage application completes it should call onComplete
                playerEquipment.DoNormalAttack(target, () =>
                {
                    Debug.Log("[PerCharacterUI] Damage applied callback - returning to start");

                    // Return to start, then notify TurnBaseSystem by calling OnPlayerReturned (so TurnBaseSystem manages EndTurn)
                    goAI.ReturnToStart(() =>
                    {
                        Debug.Log("[PerCharacterUI] ReturnToStart complete - notifying TurnBaseSystem.OnPlayerReturned");
                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                        // Ensure we clear the selection in TurnBaseSystem (so future panels don't reuse it accidentally)
                        try
                        {
                            var tbs = TurnBaseSystem.Instance;
                            if (tbs != null) tbs.selectedMonster = null;
                        }
                        catch { }

                        // Use OnPlayerReturned so TurnBaseSystem can clear any in-progress flags and call EndTurn.
                        try { tm.OnPlayerReturned(); }
                        catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }

                        SetAllButtonsInteractable(true);
                        _localActionInProgress = false;
                    });
                });
            });
        }
        else
        {
            // fallback: no movement AI — call damage then notify TurnBaseSystem
            playerEquipment.DoNormalAttack(target, () =>
            {
                if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                try
                {
                    var tbs = TurnBaseSystem.Instance;
                    if (tbs != null) tbs.selectedMonster = null;
                }
                catch { }

                try { tm.OnPlayerReturned(); }
                catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }

                SetAllButtonsInteractable(true);
                _localActionInProgress = false;
            });
        }
    }

    public void OnSkillClicked()
    {
        if (!CanAct()) return;
        var tm = turnManager ?? TurnBaseSystem.Instance;
        if (tm == null || playerEquipment == null) return;

        // Defensive: ensure this panel belongs to current battler (avoid acting on wrong panel)
        if (tm.CurrentBattlerObject != playerEquipment.gameObject)
        {
            Debug.LogWarning("[PerCharacterUI] OnSkillClicked but this panel is not CurrentBattlerObject. Ignoring. panel=" + playerEquipment.gameObject.name + " current=" + (tm.CurrentBattlerObject != null ? tm.CurrentBattlerObject.name : "null"));
            return;
        }

        // Prevent re-entry
        if (_localActionInProgress) { Debug.Log("[PerCharacterUI] OnSkillClicked ignored because local action in progress"); return; }
        _localActionInProgress = true;

        // prefer selectedMonster if player explicitly selected a target (fallback to pointer)
        var selected = GetSelectedOrPointerMonster(tm);

        // Tell TurnManager that player action begins (block EndTurn while action in progress)
        TryBeginPlayerActionOnTurnManager(tm);

        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();

        // If player clicked on a specific monster and we have movement AI, move to that selected target first
        if (selected != null && goAI != null)
        {
            Debug.Log("[PerCharacterUI] SkillClicked: will StrongAttackMonster to selected target: " + selected.name);

            try
            {
                goAI.StrongAttackMonster(selected, () =>
                {
                    // After movement/attack animation, call UseSkill and wait for completion via callback if available
                    try
                    {
                        var ceType = playerEquipment.GetType();
                        var useWithCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                        var targets = new List<GameObject> { selected };

                        if (useWithCb != null)
                        {
                            useWithCb.Invoke(playerEquipment, new object[] { targets, new Action(() =>
                            {
                                // when UseSkill finished, return then notify TurnBaseSystem
                                try
                                {
                                    goAI.ReturnToStart(() =>
                                    {
                                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                                        try
                                        {
                                            var tbs = TurnBaseSystem.Instance;
                                            if (tbs != null) tbs.selectedMonster = null;
                                        }
                                        catch { }

                                        try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                                        SetAllButtonsInteractable(true);
                                        _localActionInProgress = false;
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning("[PerCharacterUI] Exception returning after skill callback: " + ex);
                                    if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                                    try { tm.OnPlayerReturned(); } catch (Exception ex2) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex2); tm.EndTurn(); }
                                    SetAllButtonsInteractable(true);
                                    _localActionInProgress = false;
                                }
                            })});
                        }
                        else
                        {
                            // fallback: synchronous UseSkill then return+notify
                            playerEquipment.UseSkill(targets);
                            try
                            {
                                goAI.ReturnToStart(() =>
                                {
                                    if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }

                                    try
                                    {
                                        var tbs = TurnBaseSystem.Instance;
                                        if (tbs != null) tbs.selectedMonster = null;
                                    }
                                    catch { }

                                    try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                                    SetAllButtonsInteractable(true);
                                    _localActionInProgress = false;
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[PerCharacterUI] Exception during fallback skill return: " + ex);
                                if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                                try { tm.OnPlayerReturned(); } catch (Exception ex2) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex2); tm.EndTurn(); }
                                SetAllButtonsInteractable(true);
                                _localActionInProgress = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[PerCharacterUI] Exception while invoking UseSkill after StrongAttack: " + ex);
                        // ensure cleanup
                        try { goAI.ReturnToStart(() => { try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); } SetAllButtonsInteractable(true); _localActionInProgress = false; }); }
                        catch { try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); } SetAllButtonsInteractable(true); _localActionInProgress = false; }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerCharacterUI] Exception while calling StrongAttackMonster: " + ex);
                // fallback to direct UseSkill
                try
                {
                    var ceType = playerEquipment.GetType();
                    var useWithCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                    var targets = new List<GameObject> { selected };
                    if (useWithCb != null)
                    {
                        useWithCb.Invoke(playerEquipment, new object[] { targets, new Action(() =>
                        {
                            if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                            try { tm.OnPlayerReturned(); } catch (Exception ex2) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex2); tm.EndTurn(); }
                            SetAllButtonsInteractable(true);
                            _localActionInProgress = false;
                        })});
                    }
                    else
                    {
                        playerEquipment.UseSkill(new List<GameObject> { selected });
                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                        try { tm.OnPlayerReturned(); } catch (Exception ex2) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex2); tm.EndTurn(); }
                        SetAllButtonsInteractable(true);
                        _localActionInProgress = false;
                    }
                }
                catch (Exception ex2)
                {
                    Debug.LogWarning("[PerCharacterUI] Fallback UseSkill failed: " + ex2);
                    if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                    try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); }
                    SetAllButtonsInteractable(true);
                    _localActionInProgress = false;
                }
            }

            return;
        }

        // If no selected target or no movement AI, fallback to area-use on all monsters (original behaviour),
        // but prefer UseSkill overload with callback so we end turn only after completion.
        var targetsAll = new List<GameObject>();
        for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
        {
            var go = tm.battlerObjects[i];
            var b = tm.battlers[i];
            if (go != null && b != null && b.isMonster && b.hp > 0) targetsAll.Add(go);
        }
        if (targetsAll.Count == 0)
        {
            Debug.LogWarning("[PerCharacterUI] No valid skill targets (fallback).");
            if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
            SetAllButtonsInteractable(true);
            _localActionInProgress = false;
            return;
        }

        try
        {
            var ceType2 = playerEquipment.GetType();
            var useWithCb2 = ceType2.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
            if (useWithCb2 != null)
            {
                useWithCb2.Invoke(playerEquipment, new object[] { targetsAll, new Action(() =>
                {
                    if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                    try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                    SetAllButtonsInteractable(true);
                    _localActionInProgress = false;
                })});
            }
            else
            {
                playerEquipment.UseSkill(targetsAll);
                if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                try { tm.OnPlayerReturned(); } catch (Exception ex) { Debug.LogWarning("[PerCharacterUI] tm.OnPlayerReturned threw: " + ex); tm.EndTurn(); }
                SetAllButtonsInteractable(true);
                _localActionInProgress = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PerCharacterUI] UseSkill threw (fallback all-targets): " + ex);
            if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
            try { tm.OnPlayerReturned(); } catch { tm.EndTurn(); }
            SetAllButtonsInteractable(true);
            _localActionInProgress = false;
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