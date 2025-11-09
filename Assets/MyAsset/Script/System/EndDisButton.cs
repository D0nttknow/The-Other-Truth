using UnityEngine;
using UnityEngine.UI;

public class TurnUIController : MonoBehaviour
{
    public Button playerActionButton;      // ลากปุ่มจาก Inspector
    public TurnManager turnManager;        // ลาก TurnManager จาก Inspector

    void Update()
    {
        // เปิดปุ่มเฉพาะตอนเทิร์นผู้เล่น
        if (turnManager != null && playerActionButton != null)
        {
            playerActionButton.interactable = (turnManager.state == TurnManager.BattleState.WaitingForPlayerInput);
        }
    }
}