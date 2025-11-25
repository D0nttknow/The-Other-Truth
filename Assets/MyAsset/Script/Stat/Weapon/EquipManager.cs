using UnityEngine;

/// <summary>
/// Very small example manager that bridges inventory -> equipment.
/// In a real project Inventory would hold WeaponItem references; here we assume an array for demo.
/// Updated to use WeaponDefinition assets (matches CharacterEquipment).
/// </summary>
public class EquipManager : MonoBehaviour
{
    public CharacterEquipment playerEquipment;
    public WeaponDefinition[] sampleItems; // assign WeaponDefinition assets in inspector for testing

    // Example: equip by index (UI button calls)
    public void EquipIndex(int idx)
    {
        if (playerEquipment == null) return;
        if (sampleItems == null) return;
        if (idx < 0 || idx >= sampleItems.Length) return;
        playerEquipment.Equip(sampleItems[idx]);
    }

    public void Unequip()
    {
        if (playerEquipment != null) playerEquipment.Unequip();
    }
}