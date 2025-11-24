using UnityEngine;

public abstract class ItemBase : ScriptableObject
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    [TextArea(2, 4)] public string description;
    public bool stackable = true;
    public int maxStack = 99;

    public virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName)) displayName = this.name;
        if (string.IsNullOrEmpty(itemId)) itemId = this.name.Replace(" ", "_");
    }

    // เรียกเมื่อใช้ไอเท็มบน target (GameObject ที่เป็นตัวละคร)
    // คืน true ถ้าถูก consume (ต้องลบออกจาก inventory)
    public abstract bool Use(GameObject target);

    // ตัวช่วย: คำอธิบายสั้น ๆ สำหรับ UI
    public virtual string GetDescription() => description;
}