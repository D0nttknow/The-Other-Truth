using System;
using System.Collections.Generic;
using System.Reflection;
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

        Debug.Log($"[CharacterEquipment] Equipping {item.displayName} on {gameObject.name} (prefab={(item.weaponPrefab != null ? item.weaponPrefab.name : "null")})");

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
        Debug.Log($"[CharacterEquipment] Equipped {item.displayName} => weaponController={(weaponController != null ? weaponController.GetType().Name : "null")}");
    }

    // New: called by TurnManager at start of this battler's turn
    // - decrements weapon cooldowns and forwards OnTurnStart to WeaponController if available
    public void OnTurnStart()
    {
        try
        {
            if (weaponController != null)
            {
                // decrement cooldown if field exists (most WeaponController implementations use this)
                try
                {
                    weaponController.skillCooldownRemaining = Mathf.Max(0, weaponController.skillCooldownRemaining - 1);
                }
                catch { /* ignore if field not present */ }

                // call WeaponController.OnTurnStart() if it exists (use reflection to avoid hard dependency)
                try
                {
                    var mi = weaponController.GetType().GetMethod("OnTurnStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null) mi.Invoke(weaponController, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CharacterEquipment] Failed to invoke WeaponController.OnTurnStart: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CharacterEquipment] OnTurnStart exception: {ex}");
        }
    }

    // New: Unequip helper (called from EquipManager/UI)
    public void Unequip()
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }
        weaponController = null;
        currentWeaponItem = null;
        Debug.Log($"[CharacterEquipment] Unequipped on {gameObject.name}");
    }

    // Existing accessor
    public WeaponController GetEquippedWeapon()
    {
        return weaponController;
    }

    // Called by UI: do a normal attack and call onComplete when done
    public void DoNormalAttack(GameObject target, Action onComplete)
    {
        Debug.Log($"[CharacterEquipment] DoNormalAttack on {gameObject.name} target={(target != null ? target.name : "null")}");
        try
        {
            // Optionally delegate to WeaponController if it provides a method
            var mi = weaponController != null ? weaponController.GetType().GetMethod("PerformNormalAttack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
            if (mi != null)
            {
                mi.Invoke(weaponController, new object[] { target, onComplete });
                return;
            }
        }
        catch { }

        // fallback immediate completion
        onComplete?.Invoke();
    }

    // Compatibility overload: allow callers to call DoNormalAttack(target) without callback.
    public void DoNormalAttack(GameObject target)
    {
        DoNormalAttack(target, null);
    }

    // Called by UI: use skill on targets and call onComplete when done
    public void UseSkill(List<GameObject> targets, Action onComplete)
    {
        Debug.Log($"[CharacterEquipment] UseSkill on {gameObject.name} targetsCount={(targets != null ? targets.Count : 0)}");
        try
        {
            var mi = weaponController != null ? weaponController.GetType().GetMethod("PerformSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
            if (mi != null)
            {
                mi.Invoke(weaponController, new object[] { targets, onComplete });
                return;
            }
        }
        catch { }

        onComplete?.Invoke();
    }

    // Compatibility overload: allow callers to call UseSkill(targets) without callback.
    public void UseSkill(List<GameObject> targets)
    {
        UseSkill(targets, null);
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