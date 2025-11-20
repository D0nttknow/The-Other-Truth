using System;
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
/// </summary>
public class WeaponUIController : MonoBehaviour
{
    [Header("References")]
    public CharacterEquipment playerEquipment; // ลาก Player GameObject ที่มี CharacterEquipment
    public TurnManager turnManager;             // ลาก TurnManager (หรือ leave null แล้วใช้ TurnManager.Instance)

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

    void Start()
    {
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
        // If no turn manager, just return the panel's equipment
        if (turnManager == null)
        {
            return playerEquipment;
        }

        var current = turnManager.CurrentBattlerObject;
        if (current == null)
        {
            // no current battler, return panel's equipment
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

    // -------------------------
    // Button handlers (public for OnClick)
    // -------------------------
    public void OnNormalButtonClicked()
    {
        if (!CanAct()) return;

        var target = (turnManager != null) ? turnManager.selectedMonster : null;
        if (target == null) { Debug.LogWarning("[WeaponUIController] No target selected for Normal Attack."); return; }

        var equip = ResolveActiveCharacterEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUIController] No CharacterEquipment found for actor to perform Normal Attack."); return; }

        // Prefer using GoAttck to handle movement/animation before applying damage.
        var playerGO = equip.gameObject;
        var goAI = playerGO.GetComponent<GoAttck>();
        if (goAI != null)
        {
            // Disable UI interaction until action completes
            SetButtonsInteractable(false);

            // Call AttackMonster; when movement/attack animation reaches hit-frame, we apply damage and return to start,
            // then end turn. If your AttackMonster already applies damage, remove the DoNormalAttack call below.
            goAI.AttackMonster(target, () =>
            {
                // apply damage now (if AttackMonster doesn't already apply it)
                equip.DoNormalAttack(target);

                // return to start and when returned, end turn
                goAI.ReturnToStart(() =>
                {
                    if (turnManager != null) turnManager.EndTurn();
                    SetButtonsInteractable(true);
                });
            });
        }
        else
        {
            // fallback: instant attack then end turn
            equip.DoNormalAttack(target);
            if (turnManager != null) turnManager.EndTurn();
        }
    }

    public void OnSkillButtonClicked()
    {
        if (!CanAct()) return;

        var equip = ResolveActiveCharacterEquipment();
        if (equip == null) { Debug.LogWarning("[WeaponUIController] playerEquipment not assigned and no fallback found."); return; }

        // build target list (all alive monsters)
        var targets = new List<GameObject>();
        if (turnManager != null)
        {
            for (int i = 0; i < turnManager.battlerObjects.Count && i < turnManager.battlers.Count; i++)
            {
                var go = turnManager.battlerObjects[i];
                var b = turnManager.battlers[i];
                if (go != null && b != null && b.isMonster && b.hp > 0) targets.Add(go);
            }
        }

        if (targets.Count == 0) { Debug.LogWarning("[WeaponUIController] No valid skill targets."); return; }

        // If player has GoAttck and you want animation for skill, try to use StrongAttackMonster if available;
        var playerGO = equip.gameObject;
        var goAI = playerGO.GetComponent<GoAttck>();
        if (goAI != null)
        {
            SetButtonsInteractable(false);

            // Try to call a strong-attack movement method if present (method name may differ in your project)
            var mi = goAI.GetType().GetMethod("StrongAttackMonster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                // call StrongAttackMonster(targets[0], callback) if signature matches; using reflection to be safe
                try
                {
                    mi.Invoke(goAI, new object[] { targets[0], new Action(() =>
                    {
                        // apply skill logic (area damage / status) via resolved equipment
                        equip.UseSkill(targets);

                        // return to start then end turn (if method exists)
                        var retMethod = goAI.GetType().GetMethod("ReturnToStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (retMethod != null)
                        {
                            retMethod.Invoke(goAI, new object[] { new Action(() =>
                            {
                                if (turnManager != null) turnManager.EndTurn();
                                SetButtonsInteractable(true);
                            })});
                        }
                        else
                        {
                            if (turnManager != null) turnManager.EndTurn();
                            SetButtonsInteractable(true);
                        }
                    })});
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WeaponUIController] Reflection invoke StrongAttack failed: " + ex.Message);
                }
            }

            // Fallback: no StrongAttack method or reflection failed -> just play skill without movement
            equip.UseSkill(targets);
            if (turnManager != null) turnManager.EndTurn();
            SetButtonsInteractable(true);
        }
        else
        {
            equip.UseSkill(targets);
            if (turnManager != null) turnManager.EndTurn();
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
        if (turnManager == null) return true;
        return turnManager.state == TurnManager.BattleState.WaitingForPlayerInput;
    }

    void SetButtonsInteractable(bool value)
    {
        if (normalButton != null) normalButton.interactable = value;
        if (skillButton != null) skillButton.interactable = value;
        if (swapButton != null) swapButton.interactable = value;
    }
}