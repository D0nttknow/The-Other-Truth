using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turn UI controller for a single global UI:
/// - Enables the action button only when it's the UI-controlled player's turn.
/// </summary>
public class TurnUIController : MonoBehaviour
{
    public Button playerActionButton;            // Attack button (Inspector)
    public TurnBaseSystem turnManager;           // TurnBaseSystem (Inspector) - optional, auto-find in Start
    public WeaponUIController weaponUIController;// Optional: global UI controller that holds playerEquipment

    GameObject lastControlledPlayer;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        // cache controlled player if weaponUIController already assigned
        if (weaponUIController != null && weaponUIController.playerEquipment != null)
            lastControlledPlayer = weaponUIController.playerEquipment.gameObject;
    }

    void Update()
    {
        if (playerActionButton == null)
            return;

        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        if (turnManager == null) { playerActionButton.interactable = false; return; }

        // base enable: it's player input phase
        bool enable = (turnManager.state == TurnBaseSystem.BattleState.WaitingForPlayerInput);

        // If we have a global WeaponUIController, ensure the current battler is the one that UI controls
        if (weaponUIController != null && weaponUIController.playerEquipment != null)
        {
            var controlled = weaponUIController.playerEquipment.gameObject;
            enable = enable && turnManager.IsCurrentTurn(controlled);
            lastControlledPlayer = controlled;
        }
        else if (lastControlledPlayer != null)
        {
            enable = enable && turnManager.IsCurrentTurn(lastControlledPlayer);
        }

        playerActionButton.interactable = enable;
    }
}