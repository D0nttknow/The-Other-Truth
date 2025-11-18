using System;
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
        Debug.Log($"[CharacterEquipment] Start on {gameObject.name} currentWeaponItem={{(currentWeaponItem!=null?currentWeaponItem.displayName:"null")}}, weaponMount={{(weaponMount!=null?weaponMount.name:"null")}}, enabled={{enabled}}");

        // auto-find/create mount if not set (look for child named "Hand" or "WeaponMount")
        if (weaponMount == null)
        {
            Transform hand = transform.Find("Hand") ?? transform.Find("WeaponMount");
            if (hand != null) weaponMount = hand;
            else
            {
                // create mount at runtime
                GameObject go = new GameObject("WeaponMount");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                weaponMount = go.transform;
                Debug.Log("[CharacterEquipment] Created WeaponMount at runtime");
            }
        }

        // auto-equip if inspector has item assigned
        if (currentWeaponItem != null)
        {
            Equip(currentWeaponItem);
        }
    }

    // Make it callable from inspector for quick testing
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

        Debug.Log($"[CharacterEquipment] Equipping {item.displayName} on {gameObject.name} (prefab={{(item.weaponPrefab!=null?item.weaponPrefab.name:"null")}})");

        // destroy old
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
            else
            {
                // if controller supports ApplyWeaponData, call it
                try { weaponController.ApplyWeaponData(item); } catch { }
            }
        }
        else
        {
            // fallback: create minimal GameObject with WeaponController so UI logic works
            currentWeaponInstance = new GameObject("Weapon_" + item.displayName);
            currentWeaponInstance.transform.SetParent(weaponMount != null ? weaponMount : transform, false);
            currentWeaponInstance.transform.localPosition = item.positionOffset;
            currentWeaponInstance.transform.localEulerAngles = item.rotationOffset;
            weaponController = currentWeaponInstance.AddComponent<WeaponController>();
            try { weaponController.ApplyWeaponData(item); } catch { }
            Debug.Log("[CharacterEquipment] Created runtime WeaponController fallback for " + item.displayName);
        }

        currentWeaponItem = item;
        Debug.Log($"[CharacterEquipment] Equipped {item.displayName} => weaponController={{(weaponController!=null?weaponController.GetType().Name:"null")}});
    }

    // Existing accessor
    public WeaponController GetEquippedWeapon()
    {
        return weaponController;
    }

    // Called by UI: do a normal attack and call onComplete when done
    public void DoNormalAttack(GameObject target, Action onComplete)
    {
        Debug.Log($"[CharacterEquipment] DoNormalAttack on {gameObject.name} target={{(target!=null?target.name:"null")}});
        // If you have a WeaponController-specific attack implementation, call it here.
        // For now, just simulate immediate completion.
        try
        {
            // if weaponController provides a method, attempt to call it
            // e.g., weaponController.PerformNormalAttack(target, onComplete);
        }
        catch { }
        onComplete?.Invoke();
    }

    // Called by UI: use skill on targets and call onComplete when done
    public void UseSkill(List<GameObject> targets, Action onComplete)
    {
        Debug.Log($"[CharacterEquipment] UseSkill on {gameObject.name} targetsCount={{ (targets!=null?targets.Count:0) }});
        try
        {
            // if weaponController provides a method, attempt to call it
            // e.g., weaponController.PerformSkill(targets, onComplete);
        }
        catch { }
        onComplete?.Invoke();
    }

    // Swap to next weapon in ownedWeapons
    public void SwapToNextWeapon()
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;
        int idx = ownedWeapons.IndexOf(currentWeaponItem);
        int next = (idx + 1) % ownedWeapons.Count;
        Equip(ownedWeapons[next]);
    }
}