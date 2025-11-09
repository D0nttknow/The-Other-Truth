using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages equipping weapons and swapping them. Holds a list of WeaponItem references (inventory for weapons).
/// - Equip at index / Equip item by id
/// - Swap to next weapon in list (during battle by input)
/// - OnTurnStart() should be called by TurnManager when it's this player's turn start (to tick cooldown)
///
/// Changes in this version:
/// - Removed direct Input.GetKeyDown usage from Update (avoid Input System conflicts).
/// - Added OnEquipped / OnUnequipped events so UI can subscribe and update icons instantly.
/// - Added helper methods: GetEquippedWeapon(), AddOwnedWeapon(), RemoveOwnedWeapon(), EquipById(), EquipAtIndex().
/// - Improved SwapToNextWeapon() safety when current item is null or not found in ownedWeapons.
/// - Ensured instantiated weapon instance has localScale=Vector3.one to avoid scale issues.
/// </summary>
[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    [Tooltip("Where to mount weapon visuals (hand)")]
    public Transform weaponMount;

    // list of weapons this character owns; fill from Inventory (you can store itemId instead if needed)
    public List<WeaponItem> ownedWeapons = new List<WeaponItem>();

    // runtime instance of equipped weapon
    public WeaponItem currentWeaponItem;
    private GameObject currentWeaponInstance;
    private WeaponController weaponController;

    // Events for UI / other systems to subscribe
    public event Action<WeaponItem> OnEquipped;
    public event Action OnUnequipped;

    void Start()
    {
        // auto-assign weaponMount from animator right hand if not set (optional)
        if (weaponMount == null)
        {
            var anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var right = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (right != null) weaponMount = right;
            }
        }

        // If there's at least one owned weapon and nothing equipped, optionally equip the first one
        // Uncomment the next line if you want auto-equip on Start when ownedWeapons has items:
        // if (currentWeaponItem == null && ownedWeapons != null && ownedWeapons.Count > 0) Equip(ownedWeapons[0]);
    }

    // NOTE: Removed Update() input polling to avoid conflicts with Unity's new Input System.
    // Input should be handled by a centralized input handler (PlayerInputHandler or UI buttons)
    // which then calls SwapToNextWeapon(), DoNormalAttack(), UseSkill(), etc.

    /// <summary>
    /// Equip a specific WeaponItem (instantiates visual prefab or creates empty holder).
    /// </summary>
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
            // create placeholder object under mount
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

    /// <summary>
    /// Swap to next owned weapon (wrap-around). Use this for in-battle quick-swap.
    /// Public: input/UI should call this (do NOT rely on Update polling here).
    /// </summary>
    public void SwapToNextWeapon()
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;

        int idx = currentWeaponItem == null ? -1 : ownedWeapons.IndexOf(currentWeaponItem);

        int next;
        if (idx < 0)
        {
            // if current not found, equip first
            next = 0;
        }
        else
        {
            next = (idx + 1) % ownedWeapons.Count;
        }

        // if only one weapon, this will equip the same one (safe)
        Equip(ownedWeapons[next]);
    }

    // helper used by input/ability system to call normal attack
    public void DoNormalAttack(GameObject target)
    {
        if (weaponController != null) weaponController.NormalAttack(target);
        else Debug.LogWarning("[CharacterEquipment] DoNormalAttack: no weapon equipped.");
    }

    // helper to call skill; targets provided by targeting system / TurnManager
    public void UseSkill(IEnumerable<GameObject> targets)
    {
        if (weaponController != null) weaponController.UseSkill(targets);
        else Debug.LogWarning("[CharacterEquipment] UseSkill: no weapon equipped.");
    }

    // Called by TurnManager to tick per-turn state (cooldowns)
    public void OnTurnStart()
    {
        if (weaponController != null) weaponController.OnTurnStart();
    }

    // Public helpers / API ------------------------------------------------

    /// <summary>
    /// Returns the runtime WeaponController for the equipped weapon (may be null).
    /// </summary>
    public WeaponController GetEquippedWeapon()
    {
        return weaponController;
    }

    /// <summary>
    /// Equip by index in ownedWeapons. Safe checks included.
    /// </summary>
    public void EquipAtIndex(int index)
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;
        if (index < 0 || index >= ownedWeapons.Count) return;
        Equip(ownedWeapons[index]);
    }

    /// <summary>
    /// Equip by item id using a WeaponDatabase lookup (if you keep one).
    /// </summary>
    public void EquipById(string itemId, WeaponItem[] lookupList = null)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        // If a lookup list is provided, search it; otherwise search ownedWeapons first
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

    /// <summary>
    /// Add a weapon to ownedWeapons (does not auto-equip).
    /// </summary>
    public void AddOwnedWeapon(WeaponItem item)
    {
        if (item == null) return;
        if (ownedWeapons == null) ownedWeapons = new List<WeaponItem>();
        ownedWeapons.Add(item);
    }

    /// <summary>
    /// Remove a weapon from ownedWeapons. If it was equipped, unequip.
    /// </summary>
    public void RemoveOwnedWeapon(WeaponItem item)
    {
        if (item == null || ownedWeapons == null) return;
        if (ownedWeapons.Contains(item)) ownedWeapons.Remove(item);
        if (currentWeaponItem == item) Unequip();
    }
}