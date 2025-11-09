using UnityEngine;

// ติดที่ prefab ของ healthbar (RectTransform) เพื่อให้มันตามตำแหน่ง world target
public class HealthBarFollower : MonoBehaviour
{
    public Transform target;          // transform ของตัวละคร (world position)
    public Vector3 worldOffset = new Vector3(0, 2.0f, 0); // ปรับให้พอดีกับความสูงหัวตัวละคร
    public Camera uiCamera;           // camera สำหรับ Screen Space - Camera หรือ Camera.main สำหรับ Screen Space - Overlay ใช้ Camera.main
    public bool hideIfBehindCamera = true;

    RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (uiCamera == null) uiCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 worldPos = target.position + worldOffset;
        Vector3 screenPos = uiCamera.WorldToScreenPoint(worldPos);

        // ถ้าอยากซ่อนเมื่ออยู่ด้านหลังกล้อง
        if (hideIfBehindCamera && screenPos.z < 0)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // สำหรับ Screen Space - Overlay หรือ Screen Space - Camera ให้ใช้ anchored position
        rectTransform.position = screenPos;
    }
}