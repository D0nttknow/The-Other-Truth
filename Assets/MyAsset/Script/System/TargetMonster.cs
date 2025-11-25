using UnityEngine;

public class TargetMonster : MonoBehaviour
{
    public IMonsterStat monsterStat;

    void Start()
    {
        // เซ็ตอัตโนมัติถ้าใน Inspector เป็น None
        if (monsterStat == null)
            monsterStat = GetComponent<IMonsterStat>();
    }

    void OnMouseDown()
    {
        Debug.Log("Clicked: " + gameObject.name);
        // แจ้ง TurnManager ว่า Monster ตัวนี้ถูกเลือก
        TurnManager.Instance.OnMonsterSelected(gameObject);
    }
}