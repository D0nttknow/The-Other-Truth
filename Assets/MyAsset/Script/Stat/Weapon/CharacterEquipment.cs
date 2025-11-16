using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages equipping weapons and swapping them. Holds a list of WeaponItem references (inventory for weapons).
/// - Equip at index / Equip item by id
/// - Swap to next weapon in list (during battle by input)
/// - OnTurnStart() should be called by TurnManager when it's this player's turn start (to tick cooldown)
/// 
/// Adjusted: DoNormalAttack / UseSkill now accept an optional Action onComplete callback and forward it to WeaponController.
/// </summary>
[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    [Tooltip("Where to mount weapon visuals (hand)")]
    public Transform weaponMount;

    public List<WeaponItem> ownedWeapons = new List<WeaponItem>();

    public WeaponItem currentWeaponItem;
    private GameObject currentWeaponInstance;
    private WeaponController weaponController;

    public event Action<WeaponItem> OnEquipped;
    public event Action OnUnequipped;

    void Start()
    {
        if (weaponMount == null)
        {
            var anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var right = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (right != null) weaponMount = right;
            }
        }
    }

    public void Equip(WeaponItem item)
    {
        if (item == null) { Unequip(); return; }
        if (currentWeaponItem == item) return;

        Unequip();

        currentWeaponItem = item;

        if (item.weaponPrefab != null && weaponMount != null)
        {
            currentWeaponInstance = Instantiate(item.weaponPrefab, weaponMount, false);
            currentWeaponInstance.transform.localPosition = item.positionOffset;
            currentWeaponInstance.transform.localEulerAngles = item.rotationOffset;
            currentWeaponInstance.transform.localScale = Vector3.one;

            weaponController = currentWeaponInstance.GetComponent<WeaponController>();
            if (weaponController == null) weaponController = currentWeaponInstance.AddComponent<WeaponController>();
        }
        else
        {
            var go = new GameObject($"Weapon_{item.displayName}");
            go.transform.SetParent(weaponMount, false);
            go.transform.localPosition = item.positionOffset;
            go.transform.localEulerAngles = item.rotationOffset;
            go.transform.localScale = Vector3.one;
            currentWeaponInstance = go;
            weaponController = go.AddComponent<WeaponController>();
        }

        if (weaponController != null) weaponController.ApplyWeaponData(item);

        Debug.Log($"[CharacterEquipment] Equipped {item.displayName} on {gameObject.name}");
        OnEquipped?.Invoke(item);
    }

    public void Unequip()
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }
        weaponController = null;
        if (currentWeaponItem != null)
        {
            currentWeaponItem = null;
            OnUnequipped?.Invoke();
        }
    }

    public void SwapToNextWeapon()
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;

        int idx = currentWeaponItem == null ? -1 : ownedWeapons.IndexOf(currentWeaponItem);

        int next;
        if (idx < 0) next = 0;
        else next = (idx + 1) % ownedWeapons.Count;

        Equip(ownedWeapons[next]);
    }

    // Updated: accept optional onComplete callback so caller can wait for animation & then EndTurn.
    public void DoNormalAttack(GameObject target, Action onComplete = null)
    {
        if (weaponController != null)
        {
            weaponController.NormalAttack(target, onComplete);
        }
        else
        {
            Debug.LogWarning("[CharacterEquipment] DoNormalAttack: no weapon equipped.");
            onComplete?.Invoke();
        }
    }

    // Updated: UseSkill now accepts an optional onComplete callback and forwards targets to WeaponController.
    public void UseSkill(IEnumerable<GameObject> targets, Action onComplete = null)
    {
        if (weaponController != null)
        {
            weaponController.UseSkill(targets, onComplete);
        }
        else
        {
            Debug.LogWarning("[CharacterEquipment] UseSkill: no weapon equipped.");
            onComplete?.Invoke();
        }
    }

    public void OnTurnStart()
    {
        if (weaponController != null) weaponController.OnTurnStart();
    }

    public WeaponController GetEquippedWeapon()
    {
        return weaponController;
    }

    public void EquipAtIndex(int index)
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;
        if (index < 0 || index >= ownedWeapons.Count) return;
        Equip(ownedWeapons[index]);
    }

    public void EquipById(string itemId, WeaponItem[] lookupList = null)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        WeaponItem found = null;
        if (ownedWeapons != null)
        {
            foreach (var w in ownedWeapons)
            {
                if (w != null && w.id == itemId) { found = w; break; }
            }
        }

        if (found == null && lookupList != null)
        {
            foreach (var w in lookupList)
            {
                if (w != null && w.id == itemId) { found = w; break; }
            }
        }

        if (found != null) Equip(found);
    }

    public void AddOwnedWeapon(WeaponItem item)
    {
        if (item == null) return;
        if (ownedWeapons == null) ownedWeapons = new List<WeaponItem>();
        ownedWeapons.Add(item);
    }

    public void RemoveOwnedWeapon(WeaponItem item)
    {
        if (item == null || ownedWeapons == null) return;
        if (ownedWeapons.Contains(item)) ownedWeapons.Remove(item);
        if (currentWeaponItem == item) Unequip();
    }
}
