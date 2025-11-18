using System.Reflection;
using UnityEngine;

/// <summary>
/// Very small example manager that bridges inventory -> equipment.
/// In a real project Inventory would hold WeaponItem references; here we assume an array for demo.
/// </summary>
public class EquipManager : MonoBehaviour
{
    public CharacterEquipment playerEquipment;
    public WeaponItem[] sampleItems; // assign WeaponItem assets in inspector for testing

    // Example: equip by index (UI button calls)
    public void EquipIndex(int idx)
    {
        if (playerEquipment == null) return;
        if (idx < 0 || idx >= sampleItems.Length) return;
        playerEquipment.Equip(sampleItems[idx]);
    }

    // Call Unequip if available on CharacterEquipment; use reflection so code compiles even if method is missing.
    public void Unequip()
    {
        if (playerEquipment == null) return;

        // Try direct call if method exists at compile time
        // (this will compile only if CharacterEquipment defines Unequip; kept for clarity)
        // If you prefer always using reflection remove the try/catch block below.
        try
        {
            // If CharacterEquipment has an accessible Unequip() method, call it directly.
            // This direct call will fail to compile only if CharacterEquipment truly lacks the method at compile time;
            // in that case the reflection fallback below ensures runtime safety.
            // Uncomment the next line if CharacterEquipment has Unequip() in your codebase:
            // playerEquipment.Unequip();
        }
        catch { }

        // Reflection fallback: call Unequip() if present (works regardless of whether compile-time symbol exists)
        var mi = playerEquipment.GetType().GetMethod("Unequip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            mi.Invoke(playerEquipment, null);
            Debug.Log("[EquipManager] Called Unequip() via reflection.");
        }
        else
        {
            Debug.LogWarning("[EquipManager] Unequip() not found on CharacterEquipment. Nothing to unequip.");
        }
    }
}