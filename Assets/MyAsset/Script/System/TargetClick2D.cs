using UnityEngine;
using UnityEngine.InputSystem;

public class TargetMonsterRaycast : MonoBehaviour
{
    void Update()
    {
        // ใช้ Mouse.current.leftButton.wasPressedThisFrame จาก Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            if (hit.collider != null)
            {
                TargetMonster monster = hit.collider.GetComponent<TargetMonster>();
                if (monster != null)
                {
                    Debug.Log("Clicked: " + hit.collider.gameObject.name);
                    TurnManager.Instance.OnMonsterSelected(hit.collider.gameObject);
                }
            }
        }
    }
}