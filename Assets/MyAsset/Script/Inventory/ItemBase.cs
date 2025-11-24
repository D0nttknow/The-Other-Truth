using UnityEngine;

/// <summary>
/// พื้นฐานของไอเท็ม ScriptableObject
/// สร้าง asset ผ่าน Create -> Gameplay -> (ConsumableHeal / ConsumableAtk / ConsumableDef)
/// </summary>
public abstract class ItemBase : ScriptableObject
{
    public string itemId;
    public string displayName;
    public Sprite icon;

    // เรียกเมื่อใช้ไอเท็มบน target (GameObject ที่เป็นตัวละคร)
    // คืน true ถ้าถูก consume (ต้องลบออกจาก inventory)
    public abstract bool Use(GameObject target);
}