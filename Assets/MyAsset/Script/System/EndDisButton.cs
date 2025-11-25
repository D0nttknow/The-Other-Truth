using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End/Disable button controller (เดิมมีชื่อชนกับ TurnUIController)
/// เปลี่ยนชื่อคลาสเป็น EndDisButton เพื่อหลีกเลี่ยงการชนกันกับ TurnUIController ในไฟล์อื่น
/// ออกแบบมาเพื่อเปิด/ปิดปุ่มเมื่อเป็นเทิร์นของผู้เล่นที่ UI ควบคุม (global UI)
/// </summary>
public class EndDisButton : MonoBehaviour
{
    public Button playerActionButton;            // Attack button (Inspector)
    public TurnBaseSystem turnManager;           // optional - auto-find if null
    public WeaponUIController weaponUIController;// optional - global UI controller that holds playerEquipment

    GameObject lastControlledPlayer;

    void Start()
    {
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        if (weaponUIController != null && weaponUIController.playerEquipment != null)
            lastControlledPlayer = weaponUIController.playerEquipment.gameObject;
    }

    void Update()
    {
        if (playerActionButton == null) return;
        if (turnManager == null) turnManager = TurnBaseSystem.Instance;
        if (turnManager == null) { playerActionButton.interactable = false; return; }

        // Base enable: it's player input phase
        bool enable = (turnManager.state == TurnBaseSystem.BattleState.WaitingForPlayerInput);

        // If we have a global WeaponUIController, enable only when current battler is UI-controlled one
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