using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Adapted CharacterEquipment to use WeaponDefinition assets in inspector (instead of WeaponItem).
/// This version:
/// - Changes ownedWeapons/currentWeaponItem types to WeaponDefinition so you can drag WeaponDefinition assets in Inspector.
/// - Uses reflection to read common fields from the supplied ScriptableObject (weaponPrefab, displayName,
///   positionOffset, rotationOffset, icon, id) so it is tolerant if your WeaponDefinition doesn't define all fields.
/// - Calls WeaponController.ApplyWeaponData overload that accepts WeaponDefinition (or falls back).
/// </summary>
[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    [Tooltip("Where to mount weapon visuals (hand)")]
    public Transform weaponMount;

    // list of weapons this character owns; now accepts WeaponDefinition assets
    public List<WeaponDefinition> ownedWeapons = new List<WeaponDefinition>();

    // runtime instance of equipped weapon (as WeaponDefinition asset)
    public WeaponDefinition currentWeaponItem;
    private GameObject currentWeaponInstance;
    private WeaponController weaponController;

    // Events for UI / other systems to subscribe
    public event Action<WeaponDefinition> OnEquipped;
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
    }

    /// <summary>
    /// Equip a specific WeaponDefinition (instantiates visual prefab or creates empty holder).
    /// Uses reflection to read expected fields from the asset so this will still work if the asset differs.
    /// </summary>
    public void Equip(WeaponDefinition item)
    {
        if (item == null) { Unequip(); return; }
        if (currentWeaponItem == item) return;

        Unequip();

        currentWeaponItem = item;

        // Try to read prefab, offsets and displayName via reflection to support different asset shapes
        object prefabObj = GetValueFromAsset(item, new string[] { "weaponPrefab", "prefab", "weaponPrefabPrefab" });
        GameObject prefab = prefabObj as GameObject;

        Vector3 positionOffset = GetValueFromAsset(item, new string[] { "positionOffset", "posOffset", "position" }, Vector3.zero);
        Vector3 rotationOffset = GetValueFromAsset(item, new string[] { "rotationOffset", "rotOffset", "rotation" }, Vector3.zero);
        string displayName = GetValueFromAsset(item, new string[] { "displayName", "name", "display" }, item != null ? item.name : "Weapon");

        if (prefab != null && weaponMount != null)
        {
            currentWeaponInstance = Instantiate(prefab, weaponMount, false);
            currentWeaponInstance.transform.localPosition = positionOffset;
            currentWeaponInstance.transform.localEulerAngles = rotationOffset;
            currentWeaponInstance.transform.localScale = Vector3.one;

            weaponController = currentWeaponInstance.GetComponent<WeaponController>();
            if (weaponController == null) weaponController = currentWeaponInstance.AddComponent<WeaponController>();
        }
        else
        {
            // create placeholder object under mount
            var go = new GameObject($"Weapon_{displayName}");
            go.transform.SetParent(weaponMount, false);
            go.transform.localPosition = positionOffset;
            go.transform.localEulerAngles = rotationOffset;
            go.transform.localScale = Vector3.one;
            currentWeaponInstance = go;
            weaponController = go.AddComponent<WeaponController>();
        }

        // Give weaponController the asset data (try overload accepting WeaponDefinition)
        if (weaponController != null)
        {
            try
            {
                // prefer a strongly-typed overload if present
                var m = weaponController.GetType().GetMethod("ApplyWeaponData", new Type[] { typeof(WeaponDefinition) });
                if (m != null)
                {
                    m.Invoke(weaponController, new object[] { item });
                }
                else
                {
                    // fallback to object overload or existing WeaponItem overload via reflection
                    var m2 = weaponController.GetType().GetMethod("ApplyWeaponData", new Type[] { typeof(object) });
                    if (m2 != null) m2.Invoke(weaponController, new object[] { item });
                    else
                    {
                        // last resort: try to find ApplyWeaponData(WeaponItem) - won't match but attempt via dynamic invoke if possible
                        var m3 = weaponController.GetType().GetMethod("ApplyWeaponData");
                        if (m3 != null)
                        {
                            try { m3.Invoke(weaponController, new object[] { item }); }
                            catch { /* ignore - method signature mismatch possible */ }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CharacterEquipment] weaponController.ApplyWeaponData invoke failed: " + ex);
            }
        }

        Debug.Log($"[CharacterEquipment] Equipped {displayName} on {gameObject.name}");
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
    /// </summary>
    public void SwapToNextWeapon()
    {
        if (ownedWeapons == null || ownedWeapons.Count == 0) return;

        int idx = currentWeaponItem == null ? -1 : ownedWeapons.IndexOf(currentWeaponItem);

        int next;
        if (idx < 0)
        {
            next = 0;
        }
        else
        {
            next = (idx + 1) % ownedWeapons.Count;
        }

        Equip(ownedWeapons[next]);
    }

    // helper used by input/ability system to call normal attack
    public void DoNormalAttack(GameObject target)
    {
        float mult = 1f;
        var wh = GetComponent<WeaponHandler>();
        if (wh != null) mult = wh.CurrentDamageMultiplier;

        if (weaponController != null)
        {
            // Try NormalAttack(GameObject, float)
            var m = weaponController.GetType().GetMethod("NormalAttack", new System.Type[] { typeof(GameObject), typeof(float) });
            if (m != null)
            {
                try
                {
                    m.Invoke(weaponController, new object[] { target, mult });
                    Debug.Log($"[CharacterEquipment] Invoked NormalAttack with multiplier {mult} on {gameObject.name}");
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[CharacterEquipment] Invoke NormalAttack(GameObject,float) failed: " + ex);
                }
            }

            // fallback to NormalAttack(GameObject)
            try
            {
                weaponController.NormalAttack(target);
                Debug.Log($"[CharacterEquipment] Invoked NormalAttack without multiplier (applied mult={mult}) on {gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CharacterEquipment] weaponController.NormalAttack threw: " + ex);
            }
        }
        else
        {
            Debug.LogWarning("[CharacterEquipment] DoNormalAttack: no weaponController.");
        }
    }

    // Public helpers / API ------------------------------------------------

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

    public void EquipById(string itemId, WeaponDefinition[] lookupList = null)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        WeaponDefinition found = null;
        if (ownedWeapons != null)
        {
            foreach (var w in ownedWeapons)
            {
                if (w != null && GetValueFromAsset(w, new string[] { "id", "itemId", "name" }, "").ToString() == itemId) { found = w; break; }
            }
        }

        if (found == null && lookupList != null)
        {
            foreach (var w in lookupList)
            {
                if (w != null && GetValueFromAsset(w, new string[] { "id", "itemId", "name" }, "").ToString() == itemId) { found = w; break; }
            }
        }

        if (found != null) Equip(found);
    }

    public void AddOwnedWeapon(WeaponDefinition item)
    {
        if (item == null) return;
        if (ownedWeapons == null) ownedWeapons = new List<WeaponDefinition>();
        ownedWeapons.Add(item);
    }

    public void RemoveOwnedWeapon(WeaponDefinition item)
    {
        if (item == null || ownedWeapons == null) return;
        if (ownedWeapons.Contains(item)) ownedWeapons.Remove(item);
        if (currentWeaponItem == item) Unequip();
    }

    // UseSkill overloads
    public void UseSkill(IEnumerable<GameObject> targetsEnumerable)
    {
        if (targetsEnumerable == null) return;
        var targetsList = targetsEnumerable as List<GameObject> ?? targetsEnumerable.ToList();
        if (targetsList.Count == 0) return;

        try
        {
            if (weaponController != null)
            {
                var mList = weaponController.GetType().GetMethod("UseSkill", new Type[] { typeof(List<GameObject>) });
                if (mList != null)
                {
                    mList.Invoke(weaponController, new object[] { targetsList });
                    return;
                }

                var mEnum = weaponController.GetType().GetMethod("UseSkill", new Type[] { typeof(IEnumerable<GameObject>) });
                if (mEnum != null)
                {
                    mEnum.Invoke(weaponController, new object[] { targetsList });
                    return;
                }

                var mArr = weaponController.GetType().GetMethod("UseSkill", new Type[] { typeof(GameObject[]) });
                if (mArr != null)
                {
                    mArr.Invoke(weaponController, new object[] { targetsList.ToArray() });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CharacterEquipment] weaponController.UseSkill invoke failed: " + ex);
        }

        Debug.LogWarning("[CharacterEquipment] UseSkill: no suitable implementation on WeaponController.");
    }

    public void UseSkill(List<GameObject> targets)
    {
        UseSkill((IEnumerable<GameObject>)targets);
    }

    public void UseSkill(GameObject[] targets)
    {
        UseSkill((IEnumerable<GameObject>)targets);
    }

    public void OnTurnStart()
    {
        try
        {
            if (weaponController != null)
            {
                var m = weaponController.GetType().GetMethod("OnTurnStart");
                if (m != null) { m.Invoke(weaponController, null); return; }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CharacterEquipment] OnTurnStart failed: " + ex);
        }
    }

    // Reflection helpers to read common fields from WeaponDefinition (or other asset types)
    T GetValueFromAsset<T>(object asset, string[] names, T fallback)
    {
        if (asset == null) return fallback;
        var t = asset.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(T)) { return (T)f.GetValue(asset); }
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(T)) { return (T)p.GetValue(asset); }
        }
        return fallback;
    }

    object GetValueFromAsset(object asset, string[] names)
    {
        if (asset == null) return null;
        var t = asset.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(asset);
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(asset);
        }
        return null;
    }

    // Provide icon lookup for UI (works for WeaponDefinition or runtime applied item)
    public Sprite GetEquippedIcon()
    {
        try
        {
            // 1) try icon on currentWeaponItem (WeaponDefinition)
            if (currentWeaponItem != null)
            {
                var t = currentWeaponItem.GetType();
                string[] iconNames = new string[] { "icon", "sprite", "iconSprite", "image" };
                foreach (var n in iconNames)
                {
                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && typeof(Sprite).IsAssignableFrom(f.FieldType))
                    {
                        return (Sprite)f.GetValue(currentWeaponItem);
                    }
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && typeof(Sprite).IsAssignableFrom(p.PropertyType))
                    {
                        return (Sprite)p.GetValue(currentWeaponItem);
                    }
                }
            }

            // 2) try appliedItem on weaponController (WeaponItem)
            if (weaponController != null)
            {
                var appliedItemField = weaponController.GetType().GetField("appliedItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (appliedItemField != null)
                {
                    var applied = appliedItemField.GetValue(weaponController);
                    if (applied != null)
                    {
                        var t2 = applied.GetType();
                        var f2 = t2.GetField("icon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f2 != null && typeof(Sprite).IsAssignableFrom(f2.FieldType)) return (Sprite)f2.GetValue(applied);
                        var p2 = t2.GetProperty("icon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p2 != null && typeof(Sprite).IsAssignableFrom(p2.PropertyType)) return (Sprite)p2.GetValue(applied);
                    }
                }

                // 3) try appliedDefinition on weaponController
                var defField = weaponController.GetType().GetField("appliedDefinition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (defField != null)
                {
                    var def = defField.GetValue(weaponController);
                    if (def != null)
                    {
                        var t3 = def.GetType();
                        var iconNames = new string[] { "icon", "sprite", "image" };
                        foreach (var n in iconNames)
                        {
                            var f3 = t3.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (f3 != null && typeof(Sprite).IsAssignableFrom(f3.FieldType))
                            {
                                return (Sprite)f3.GetValue(def);
                            }
                            var p3 = t3.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (p3 != null && typeof(Sprite).IsAssignableFrom(p3.PropertyType))
                            {
                                return (Sprite)p3.GetValue(def);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }
}