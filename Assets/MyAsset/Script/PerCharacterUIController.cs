using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-character action panel controller (updated):
/// - Use callbacks from CharacterEquipment so EndTurn is called only after animation/processing finishes.
/// - Skill now waits for UseSkill onComplete callback before calling EndTurn.
/// - Adds a small recovery timeout to avoid UI stuck if callbacks never arrive (debug only).
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

    // --- Added cached prev-state fields to avoid per-frame log spam ---
    private bool _prevIsTurn = false;
    private string _prevWeaponInfo = null;
    private TurnManager.BattleState _prevTmState = (TurnManager.BattleState)(-1);

    // Recovery coroutine to avoid permanently stuck UI when callbacks fail
    private Coroutine _recoveryCoroutine = null;
    public float recoveryTimeoutSeconds = 5f;

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
            hpText.text = string.Format("{0}/{1}", GetFieldInt(ps, "hp"), maxHp);
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

        // compute current values
        var playerName = playerEquipment != null ? playerEquipment.gameObject.name : "null";
        var isTurn = (tm != null && playerEquipment != null) ? tm.IsCurrentTurn(playerEquipment.gameObject) : false;
        var weapon = playerEquipment != null ? playerEquipment.GetEquippedWeapon() : null;
        var wcInfo = weapon != null ? ("wc present cooldown=" + weapon.skillCooldownRemaining) : "wc=null";
        var tmState = tm != null ? tm.state : (TurnManager.BattleState)(-1);

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

        // disable buttons immediately and start recovery timer
        SetAllButtonsInteractable(false);
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        if (goAI != null)
        {
            Debug.Log($"[PerCharacterUI] OnNormalClicked start player={playerEquipment.gameObject.name}");

            // AttackMonster should call this callback at the hit-frame
            goAI.AttackMonster(target, () =>
            {
                Debug.Log("[PerCharacterUI] Attack hit callback - applying damage via CharacterEquipment");

                // Apply damage; when damage application completes it should call onComplete
                playerEquipment.DoNormalAttack(target, () =>
                {
                    Debug.Log("[PerCharacterUI] Damage applied callback - returning to start");

                    // Return to start, then end turn once
                    goAI.ReturnToStart(() =>
                    {
                        Debug.Log("[PerCharacterUI] ReturnToStart complete - ending turn");
                        if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                        if (tm != null) tm.EndTurn();
                        SetAllButtonsInteractable(true);
                    });
                });
            });
        }
        else
        {
            // fallback: no movement AI — call damage then EndTurn
            playerEquipment.DoNormalAttack(target, () =>
            {
                if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
                if (tm != null) tm.EndTurn();
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
        if (_recoveryCoroutine != null) StopCoroutine(_recoveryCoroutine);
        _recoveryCoroutine = StartCoroutine(RecoveryEnableAfterTimeout(recoveryTimeoutSeconds));

        // UseSkill will invoke onComplete when done (our CharacterEquipment.UseSkill supports callback)
        playerEquipment.UseSkill(targets, () =>
        {
            if (_recoveryCoroutine != null) { StopCoroutine(_recoveryCoroutine); _recoveryCoroutine = null; }
            if (tm != null) tm.EndTurn();
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

    IEnumerator RecoveryEnableAfterTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Debug.LogWarning($"[PerCharacterUIController] Recovery timeout reached ({seconds}s) — re-enabling buttons to avoid stuck state.");
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