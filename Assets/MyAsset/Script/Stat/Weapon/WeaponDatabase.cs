using UnityEngine;

/// <summary>
/// Simple registry to look up WeaponItem by id at runtime (used for save/load).
/// Put this on a GameObject in the Scene and assign all WeaponItem assets in inspector.
/// </summary>
public class WeaponDatabase : MonoBehaviour
{
    public static WeaponDatabase Instance { get; private set; }
    public WeaponItem[] allWeapons;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public WeaponItem FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < allWeapons.Length; i++)
        {
            if (allWeapons[i] != null && allWeapons[i].id == id) return allWeapons[i];
        }
        return null;
    }
}