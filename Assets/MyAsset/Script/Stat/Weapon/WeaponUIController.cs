using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// เชื่อมปุ่ม UI (Normal / Skill / Swap) กับ CharacterEquipment ของผู้เล่น
/// - เปลี่ยนไอคอนและสถานะปุ่มตามอาวุธที่ equip
/// - เรียก DoNormalAttack / UseSkill / SwapToNextWeapon เมื่อกดปุ่ม
/// - ถ้ามี GoAttck component จะเรียก AttackMonster(...) เพื่อให้ตัวละครเดินไปตีก่อนค่อยทำดาเมจและ EndTurn
/// - เพิ่ม fallback: หาก panel.playerEquipment ไม่ตรงกับ TurnManager.CurrentBattlerObject
///   จะใช้ CharacterEquipment ของ CurrentBattlerObject แทน (ลดปัญหาการสั่ง actor ผิดคน)
/// - รองรับทั้ง TurnManager (turnManager) และ TurnBaseSystem (compatibility) ในการเช็ค state / EndTurn
/// - เพิ่ม recovery / safety path เพื่อหลีกเลี่ยง UI ถูกล็อกถ้า callback หายหรือเกิด exception
/// </summary>
public class WeaponUIController : MonoBehaviour
{
    [Header("References")]
    public CharacterEquipment playerEquipment; // ลาก Player GameObject ที่มี CharacterEquipment
    public TurnBaseSystem turnManager; // ลาก TurnManager (หรือ leave null แล้วใช้ TurnManager.Instance)

    [Header("Buttons / UI")]
    public Button normalButton;
    public Image normalIcon;
    public Text normalLabel;    // optional: ชื่อปุ่ม (เช่น "Attack")

    public Button skillButton;
    public Image skillIcon;
    public Text skillLabel;     // optional: ชื่อปุ่ม (เช่น "Skill")
    public Text skillCooldownText; // show remaining turns (optional)

    public Button swapButton;
    public Text swapLabel;

    // cache
    private WeaponItem lastEquipped = null;
    private WeaponController cachedWeaponController = null;

    // reflection helper to track subscription
    private EventInfo onEquippedEventInfo;
    private Delegate onEquippedDelegate;

    // --- safety / recovery ---
    private Coroutine _recoveryCoroutine = null;
    public float autoRecoverySeconds = 6f;
    private bool _actionInProgress = false;

    void Start()
    {
        // prefer explicit TurnManager set in inspector, otherwise try to get singleton instance
        if (turnManager == null) turnManager = TurnManager.Instance;

        if (normalButton != null) normalButton.onClick.AddListener(OnNormalButtonClicked);
        if (skillButton != null) skillButton.onClick.AddListener(OnSkillButtonClicked);
        if (swapButton != null) swapButton.onClick.AddListener(OnSwapButtonClicked);

        // subscribe if possible
        TrySubscribeToOnEquipped();

        RefreshUI();
    }

    void OnDestroy()
    {
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalButtonClicked);
        if (skillButton != null) skillButton.onClick.RemoveListener(OnSkillButtonClicked);
        if (swapButton != null) swapButton.onClick.RemoveListener(OnSwapButtonClicked);
        TryUnsubscribeFromOnEquipped();
    }

    void Update()
    {
        if (playerEquipment != null)
        {
            if (playerEquipment.currentWeaponItem != lastEquipped) RefreshUI();

            var wc = playerEquipment.GetEquippedWeapon();
            if (wc != null) { cachedWeaponController = wc; UpdateSkillCooldown(); }
        }
    }

    void TrySubscribeToOnEquipped()
    {
        if (playerEquipment == null) return;
        try
        {
            var type = playerEquipment.GetType();
            onEquippedEventInfo = type.GetEvent("OnEquipped", BindingFlags.Public | BindingFlags.Instance);
            if (onEquippedEventInfo != null)
            {
                onEquippedDelegate = Delegate.CreateDelegate(onEquippedEventInfo.EventHandlerType, this, nameof(OnEquippedHandler));
                onEquippedEventInfo.AddEventHandler(playerEquipment, onEquippedDelegate);
            }
        }
        catch { onEquippedEventInfo = null; onEquippedDelegate = null; }
    }

    void TryUnsubscribeFromOnEquipped()
    {
        if (playerEquipment == null || onEquippedEventInfo == null || onEquippedDelegate == null) return;
        try { onEquippedEventInfo.RemoveEventHandler(playerEquipment, onEquippedDelegate); }
        catch { }
        onEquippedEventInfo = null; onEquippedDelegate = null;
    }

    public void OnEquippedHandler(WeaponItem item) => RefreshUI();

    void RefreshUI()
    {
        lastEquipped = playerEquipment != null ? playerEquipment.currentWeaponItem : null;
        cachedWeaponController = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;

        if (normalIcon != null)
        {
            Sprite s = lastEquipped != null && lastEquipped.icon != null ? lastEquipped.icon : null;
            normalIcon.sprite = s; normalIcon.enabled = s != null;
        }
        if (normalLabel != null) normalLabel.text = lastEquipped != null ? lastEquipped.displayName : "Attack";

        if (skillIcon != null)
        {
            Sprite s = lastEquipped != null && lastEquipped.icon != null ? lastEquipped.icon : null;
            skillIcon.sprite = s; skillIcon.enabled = s != null;
        }
        if (skillLabel != null)
        {
            if (lastEquipped != null)
                skillLabel.text = lastEquipped.category == WeaponCategory.Sword ? "Skill (AoE)" :
                                  lastEquipped.category == WeaponCategory.Hammer ? "Skill (Stun)" : "Skill";
            else skillLabel.text = "Skill";
        }

        UpdateSkillCooldown();
    }

    void UpdateSkillCooldown()
    {
        if (skillCooldownText != null) skillCooldownText.gameObject.SetActive(false);
        if (cachedWeaponController == null || playerEquipment == null || playerEquipment.currentWeaponItem == null)
        {
            if (skillButton != null) skillButton.interactable = false;
            return;
        }

        bool ready = cachedWeaponController.IsSkillReady();
        if (skillButton != null) skillButton.interactable = ready;

        if (!ready && skillCooldownText != null)
        {
            skillCooldownText.gameObject.SetActive(true);
            int rem = Mathf.Max(0, cachedWeaponController.skillCooldownRemaining);
            skillCooldownText.text = rem.ToString();
        }
    }

    // Helper: resolve which CharacterEquipment to use for action.
    // Prefer the TurnManager.CurrentBattlerObject's CharacterEquipment if it's available and different from this panel's playerEquipment.
    CharacterEquipment ResolveActiveCharacterEquipment()
    {
        // Try TurnManager first (explicit)
        GameObject current = null;
        if (turnManager != null)
            current = turnManager.CurrentBattlerObject;

        // If no TurnManager or no current from it, try TurnBaseSystem fallback
        if (current == null)
        {
            var tbs = TurnBaseSystem.Instance;
            if (tbs != null) current = tbs.CurrentBattlerObject;
        }

        // If still null, just return panel equipment
        if (current == null)
        {
            if (playerEquipment == null)
                Debug.Log("[WeaponUIController] No current battler and panel.playerEquipment is null.");
            return playerEquipment;
        }

        // If panel's equipment is not set, use current battler's equipment
        if (playerEquipment == null)
        {
            var fallback = current.GetComponent<CharacterEquipment>();
            if (fallback != null)
            {
                Debug.Log("[WeaponUIController] panel playerEquipment is null; using CurrentBattlerObject's CharacterEquipment: " + fallback.gameObject.name);
                return fallback;
            }
            return null;
        }

        // If panel is bound to a different GameObject than the current battler, prefer current battler's equipment
        if (playerEquipment.gameObject != current)
        {
            var fallback = current.GetComponent<CharacterEquipment>();
            if (fallback != null)
            {
                Debug.Log("[WeaponUIController] panel's CharacterEquipment (" + playerEquipment.gameObject.name + ") does not match CurrentBattlerObject (" + current.name + "). Using CurrentBattlerObject's CharacterEquipment (" + fallback.gameObject.name + ") for action.");
                return fallback;
            }
        }

        // otherwise return panel's equipment
        return playerEquipment;
    }

    // Helper: call EndTurn on whichever manager is present
    void EndTurnViaManager()
    {
        if (turnManager != null)
        {
            try { turnManager.EndTurn(); return; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUIController] turnManager.EndTurn threw: " + ex.Message); }
        }

        var tbs = TurnBaseSystem.Instance;
        if (tbs != null)
        {
            try { tbs.EndTurn(); return; }
            catch (Exception ex) { Debug.LogWarning("[WeaponUIController] TurnBaseSystem.EndTurn threw: " + ex.Message); }
        }

        Debug.LogWarning("[WeaponUIController] No turn manager available to EndTurn.");
    }

    // -------------------------
    // Safety / recovery helpers
    // -------------------------
    IEnumerator RecoveryEnableAfterTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Debug.LogWarning("[WeaponUIController] Recovery timeout reached - re-enabling buttons to avoid stuck state.");
        _actionInProgress = false;
        SetButtonsInteractable(true);
        _recoveryCoroutine = null;
    }

    void StartRecovery()
    {
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(autoRecoverySeconds));
    }

    void StopRecovery()
    {
        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
    }

    void FinishActionCleanup()
    {
        _actionInProgress = false;
        StopRecovery();
        SetButtonsInteractable(true);
    }

    // -------------------------
    // Button handlers (public for OnClick)
    // -------------------------
    public void OnNormalButtonClicked()
    {
        if (!CanAct()) return;
        if (_actionInProgress) { Debug.Log("[WeaponUIController] Action already in progress - ignoring Normal click."); return; }
        _actionInProgress = true;
        StartRecovery();

        var target = GetCurrentSelectedMonster();
        if (target == null) { Debug.LogWarning("[WeaponUIController] No target selected for Normal Attack."); FinishActionCleanup(); return; }

        var equip = ResolveActiveCharacterEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUIController] No CharacterEquipment found for actor to perform Normal Attack."); FinishActionCleanup(); return; }

        // Prefer using GoAttck to handle movement/animation before applying damage.
        var playerGO = equip.gameObject;
        var goAI = playerGO.GetComponent<GoAttck>();

        SetButtonsInteractable(false);

        if (goAI != null)
        {
            try
            {
                goAI.AttackMonster(target, () =>
                {
                    try
                    {
                        // Prefer async version with callback
                        var ceType = equip.GetType();
                        var doWithCb = ceType.GetMethod("DoNormalAttack", new Type[] { typeof(GameObject), typeof(Action) });
                        if (doWithCb != null)
                        {
                            doWithCb.Invoke(equip, new object[] { target, new Action(() =>
                            {
                                try
                                {
                                    goAI.ReturnToStart(() =>
                                    {
                                        try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                        FinishActionCleanup();
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning("[WeaponUIController] Exception during ReturnToStart cleanup: " + ex);
                                    FinishActionCleanup();
                                }
                            })});
                        }
                        else
                        {
                            // fallback: call DoNormalAttack(target) sync
                            equip.DoNormalAttack(target);
                            goAI.ReturnToStart(() =>
                            {
                                try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                FinishActionCleanup();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[WeaponUIController] Exception while applying normal attack: " + ex);
                        // Ensure we still return/cleanup
                        try { goAI.ReturnToStart(() => { FinishActionCleanup(); }); } catch { FinishActionCleanup(); }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponUIController] Exception while invoking GoAttck.AttackMonster: " + ex);
                // fallback: ensure cleanup
                try { equip.DoNormalAttack(target); } catch { }
                try { EndTurnViaManager(); } catch { }
                FinishActionCleanup();
            }
        }
        else
        {
            // fallback: instant attack then end turn (use async DoNormalAttack if available)
            try
            {
                var ceType = equip.GetType();
                var doWithCb = ceType.GetMethod("DoNormalAttack", new Type[] { typeof(GameObject), typeof(Action) });
                if (doWithCb != null)
                {
                    doWithCb.Invoke(equip, new object[] { target, new Action(() =>
                    {
                        try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                        FinishActionCleanup();
                    })});
                }
                else
                {
                    equip.DoNormalAttack(target);
                    try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                    FinishActionCleanup();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponUIController] Exception in Normal fallback: " + ex);
                FinishActionCleanup();
            }
        }
    }

    public void OnSkillButtonClicked()
    {
        if (!CanAct()) return;
        if (_actionInProgress) { Debug.Log("[WeaponUIController] Action already in progress - ignoring Skill click."); return; }
        _actionInProgress = true;
        StartRecovery();

        var equip = ResolveActiveCharacterEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUIController] playerEquipment not assigned and no fallback found."); FinishActionCleanup(); return; }

        // build target list (all alive monsters)
        var targets = new List<GameObject>();
        // prefer TurnManager list but fall back to TurnBaseSystem
        if (turnManager != null)
        {
            for (int i = 0; i < turnManager.battlerObjects.Count && i < turnManager.battlers.Count; i++)
            {
                var go = turnManager.battlerObjects[i];
                var b = turnManager.battlers[i];
                if (go != null && b != null && b.isMonster && b.hp > 0) targets.Add(go);
            }
        }
        else
        {
            var tbs = TurnBaseSystem.Instance;
            if (tbs != null)
            {
                for (int i = 0; i < tbs.battlerObjects.Count && i < tbs.battlers.Count; i++)
                {
                    var go = tbs.battlerObjects[i];
                    var b = tbs.battlers[i];
                    if (go != null && b != null && b.isMonster && b.hp > 0) targets.Add(go);
                }
            }
        }

        if (targets.Count == 0) { Debug.LogWarning("[WeaponUIController] No valid skill targets."); FinishActionCleanup(); return; }

        // If player has GoAttck and you want animation for skill, try to use StrongAttackMonster if available;
        var playerGO = equip.gameObject;
        var goAI = playerGO.GetComponent<GoAttck>();
        SetButtonsInteractable(false);

        if (goAI != null)
        {
            try
            {
                var mi = goAI.GetType().GetMethod("StrongAttackMonster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    mi.Invoke(goAI, new object[] { targets[0], new Action(() =>
                    {
                        // In the movement callback: call UseSkill and wait for its completion (prefer async overload)
                        try
                        {
                            var ceType = equip.GetType();
                            var useWithCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                            if (useWithCb != null)
                            {
                                useWithCb.Invoke(equip, new object[] { targets, new Action(() =>
                                {
                                    // return then end turn
                                    try
                                    {
                                        var retMethod = goAI.GetType().GetMethod("ReturnToStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        if (retMethod != null)
                                        {
                                            retMethod.Invoke(goAI, new object[] { new Action(() =>
                                            {
                                                try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                                FinishActionCleanup();
                                            })});
                                        }
                                        else
                                        {
                                            try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                            FinishActionCleanup();
                                        }
                                    }
                                    catch (Exception ex) { Debug.LogWarning("[WeaponUIController] Exception returning to start after skill: " + ex); FinishActionCleanup(); }
                                })});
                                return; // async path taken
                            }
                            else
                            {
                                // fallback synchronous UseSkill then return
                                equip.UseSkill(targets);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[WeaponUIController] Exception while calling UseSkill: " + ex);
                        }

                        // synchronous fallback: return and end turn
                        try
                        {
                            var retMethod = goAI.GetType().GetMethod("ReturnToStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (retMethod != null)
                            {
                                retMethod.Invoke(goAI, new object[] { new Action(() =>
                                {
                                    try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                    FinishActionCleanup();
                                })});
                            }
                            else
                            {
                                try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                                FinishActionCleanup();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[WeaponUIController] Exception during synchronous skill return: " + ex);
                            FinishActionCleanup();
                        }
                    })});
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponUIController] Reflection invoke StrongAttack failed: " + ex.Message);
            }
        }

        // Fallback: no GoAttck strong-move or reflection failed -> just play skill (prefer async)
        try
        {
            var ceType2 = equip.GetType();
            var useWithCb2 = ceType2.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
            if (useWithCb2 != null)
            {
                useWithCb2.Invoke(equip, new object[] { targets, new Action(() =>
                {
                    try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                    FinishActionCleanup();
                })});
                return;
            }
            else
            {
                equip.UseSkill(targets);
                try { EndTurnViaManager(); } catch (Exception ex) { Debug.LogWarning("[WeaponUIController] EndTurnViaManager threw: " + ex); }
                FinishActionCleanup();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WeaponUIController] UseSkill threw: " + ex);
            FinishActionCleanup();
        }
    }

    public void OnSwapButtonClicked()
    {
        var equip = ResolveActiveCharacterEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUIController] playerEquipment not assigned."); return; }
        equip.SwapToNextWeapon();
        RefreshUI();
        Debug.Log("[WeaponUIController] Swapped weapon via UI");
    }

    // Helpers
    bool CanAct()
    {
        // If explicit TurnManager available, prefer it
        if (turnManager != null)
            return turnManager.state == TurnManager.BattleState.WaitingForPlayerInput;

        // fallback to TurnBaseSystem if present
        var tbs = TurnBaseSystem.Instance;
        if (tbs != null)
            return tbs.state == TurnBaseSystem.BattleState.WaitingForPlayerInput;

        // unknown manager -> allow
        return true;
    }

    // Helper to get selected monster also compatible with TurnBaseSystem
    GameObject GetCurrentSelectedMonster()
    {
        if (turnManager != null) return turnManager.selectedMonster;
        var tbs = TurnBaseSystem.Instance;
        if (tbs != null) return tbs.selectedMonster;
        return null;
    }

    void SetButtonsInteractable(bool value)
    {
        if (normalButton != null) normalButton.interactable = value;
        if (skillButton != null) skillButton.interactable = value;
        if (swapButton != null) swapButton.interactable = value;
    }
}