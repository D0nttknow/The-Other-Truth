using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-character action panel controller (updated):
/// - Use callbacks from CharacterEquipment so EndTurn is called only after animation/processing finishes.
/// - Skill now waits for UseSkill onComplete callback before calling EndTurn.
/// </summary>
[DisallowMultipleComponent]
public class PerCharacterUIController : MonoBehaviour
{
    [Header("References (set by TurnManager or in Inspector)")]
    public CharacterEquipment playerEquipment;
    public TurnManager turnManager;

    [Header("UI Elements")]
    public Button normalButton;
    public Button skillButton;
    public Button swapButton;
    public Image iconImage;
    public Text nameText;
    public Text hpText;
    public Text skillCooldownText;

    void Start()
    {
        if (turnManager == null) turnManager = TurnManager.Instance;

        if (normalButton != null) normalButton.onClick.AddListener(OnNormalClicked);
        if (skillButton != null) skillButton.onClick.AddListener(OnSkillClicked);
        if (swapButton != null) swapButton.onClick.AddListener(OnSwapClicked);

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

    public void RefreshAll()
    {
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
        var ps = playerEquipment.GetComponent<PlayerStat>();
        if (ps != null)
        {
            var maxHpProp = ps.GetType().GetProperty("maxHp");
            var maxHp = maxHpProp != null ? maxHpProp.GetValue(ps) : "?";
            hpText.text = $"{GetFieldInt(ps, "hp")}/{maxHp}";
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
        var tm = turnManager ?? TurnManager.Instance;

        // Debug info to help track why buttons are not interactable
        var playerName = playerEquipment != null ? playerEquipment.gameObject.name : "null";
        var isTurn = (tm != null && playerEquipment != null) ? tm.IsCurrentTurn(playerEquipment.gameObject) : false;
        var weapon = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
        var wcInfo = weapon != null ? ("wc present cooldown=" + weapon.skillCooldownRemaining) : "wc=null";
        Debug.Log("[UI DEBUG] Panel=" + gameObject.name + " player=" + playerName + " isTurn=" + isTurn
                  + " tmState=" + (tm != null ? tm.state.ToString() : "null") + " weapon=" + wcInfo);

        bool isActiveTurn = (tm != null && playerEquipment != null && tm.IsCurrentTurn(playerEquipment.gameObject)
                             && tm.state == TurnManager.BattleState.WaitingForPlayerInput);

        if (normalButton != null) normalButton.interactable = isActiveTurn;
        if (skillButton != null)
        {
            var wc = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
            skillButton.interactable = isActiveTurn && wc != null && wc.IsSkillReady();
        }
        if (swapButton != null) swapButton.interactable = isActiveTurn;
    }

    public void OnNormalClicked()
    {
        if (!CanAct()) return;
        var tm = turnManager ?? TurnManager.Instance;
        if (tm == null || playerEquipment == null) return;

        var target = tm.selectedMonster;
        if (target == null) { Debug.LogWarning("[PerCharacterUI] No target selected"); return; }

        var goAI = playerEquipment.gameObject.GetComponent<GoAttck>();
        if (goAI != null)
        {
            SetAllButtonsInteractable(false);
            goAI.AttackMonster(target, () =>
            {
                try
                {
                    // Use callback-based DoNormalAttack so we wait for weapon effects too
                    playerEquipment.DoNormalAttack(target, () =>
                    {
                        goAI.ReturnToStart(() =>
                        {
                            tm.EndTurn();
                            SetAllButtonsInteractable(true);
                        });
                    });
                }
                catch
                {
                    goAI.ReturnToStart(() =>
                    {
                        tm.EndTurn();
                        SetAllButtonsInteractable(true);
                    });
                }
            });
        }
        else
        {
            // fallback: call with callback then EndTurn
            SetAllButtonsInteractable(false);
            playerEquipment.DoNormalAttack(target, () =>
            {
                tm.EndTurn();
                SetAllButtonsInteractable(true);
            });
        }
    }

    public void OnSkillClicked()
    {
        if (!CanAct()) return;
        var tm = turnManager ?? TurnManager.Instance;
        if (tm == null || playerEquipment == null) return;

        var targets = new List<GameObject>();
        for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
        {
            var go = tm.battlerObjects[i];
            var b = tm.battlers[i];
            if (go != null && b != null && b.isMonster && b.hp > 0) targets.Add(go);
        }
        if (targets.Count == 0) { Debug.LogWarning("[PerCharacterUI] No valid skill targets"); return; }

        SetAllButtonsInteractable(false);

        // Use callback form so EndTurn happens only after skill has finished (animation/effects)
        playerEquipment.UseSkill(targets, () =>
        {
            tm.EndTurn();
            SetAllButtonsInteractable(true);
        });
    }

    public void OnSwapClicked()
    {
        if (!CanAct()) return;
        playerEquipment.SwapToNextWeapon();
        RefreshIcon();
    }

    bool CanAct()
    {
        var tm = turnManager ?? TurnManager.Instance;
        if (tm == null || playerEquipment == null) return false;
        return tm.state == TurnManager.BattleState.WaitingForPlayerInput && tm.IsCurrentTurn(playerEquipment.gameObject);
    }

    void SetAllButtonsInteractable(bool v)
    {
        if (normalButton != null) normalButton.interactable = v;
        if (skillButton != null) skillButton.interactable = v;
        if (swapButton != null) swapButton.interactable = v;
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