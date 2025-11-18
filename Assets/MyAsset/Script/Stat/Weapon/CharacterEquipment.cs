using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    public Transform weaponMount;
    public List<WeaponItem> ownedWeapons = new List<WeaponItem>();
    public WeaponItem currentWeaponItem;

    private GameObject currentWeaponInstance;
    private WeaponController weaponController;

    void Start()
    {
        Debug.Log($"[CharacterEquipment] Start on {gameObject.name} currentWeaponItem={(currentWeaponItem != null ? currentWeaponItem.displayName : "null")}, weaponMount={(weaponMount != null ? weaponMount.name : "null")}, enabled={enabled}");

        // หา mount อัตโนมัติถ้ายังไม่ตั้ง (หา child ชื่อ "Hand" หรือ "WeaponMount")
        if (weaponMount == null)
        {
            Transform hand = transform.Find("Hand") ?? transform.Find("WeaponMount");
            if (hand != null) weaponMount = hand;
            else
            {
                // สร้าง mount ถ้าไม่มี
                GameObject go = new GameObject("WeaponMount");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                weaponMount = go.transform;
                Debug.Log("[CharacterEquipment] Created WeaponMount at runtime");
            }
        }

        // auto-equip ถ้ามี item ตั้งไว้ใน Inspector
        if (currentWeaponItem != null)
        {
            Equip(currentWeaponItem);
        }
    }

    // ให้เรียกจาก Inspector ด้วยคลิกเมนู component (ContextMenu)
    [ContextMenu("Equip Now (Inspector)")]
    public void EquipNowFromInspector()
    {
        Equip(currentWeaponItem);
    }

    public void Equip(WeaponItem item)
    {
        if (item == null)
        {
            Debug.LogWarning($"[CharacterEquipment] Equip called with null on {gameObject.name}");
            return;
        }

        Debug.Log($"[CharacterEquipment] Equipping {item.displayName} on {gameObject.name} (prefab={(item.weaponPrefab != null ? item.weaponPrefab.name : "null")})");

        // ลบของเก่า
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
            weaponController = null;
        }

        if (item.weaponPrefab != null)
        {
            currentWeaponInstance = Instantiate(item.weaponPrefab, weaponMount != null ? weaponMount : transform, false);
            currentWeaponInstance.transform.localPosition = item.positionOffset;
            currentWeaponInstance.transform.localEulerAngles = item.rotationOffset;

            weaponController = currentWeaponInstance.GetComponent<WeaponController>();
            if (weaponController == null)
            {
                Debug.LogWarning("[CharacterEquipment] Prefab has no WeaponController; adding fallback component.");
                weaponController = currentWeaponInstance.AddComponent<WeaponController>();
            }
        }
        else
        {
            // fallback: สร้าง GameObject เปล่าและเพิ่ม WeaponController เพื่อให้ UI/logic ทำงาน
            currentWeaponInstance = new GameObject("Weapon_" + item.displayName);
            currentWeaponInstance.transform.SetParent(weaponMount != null ? weaponMount : transform, false);
            currentWeaponInstance.transform.localPosition = item.positionOffset;
            currentWeaponInstance.transform.localEulerAngles = item.rotationOffset;
            weaponController = currentWeaponInstance.AddComponent<WeaponController>();
            Debug.Log("[CharacterEquipment] Created runtime WeaponController fallback for " + item.displayName);
        }

        currentWeaponItem = item;
        Debug.Log($"[CharacterEquipment] Equipped {item.displayName} => weaponController={(weaponController != null ? weaponController.GetType().Name : "null")}");
    }

    public WeaponController GetEquippedWeapon()
    {
        return weaponController;
    }
}