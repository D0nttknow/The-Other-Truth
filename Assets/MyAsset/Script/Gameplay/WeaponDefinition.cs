using UnityEngine;

/// <summary>
/// ScriptableObject สำหรับนิยามอาวุธ
/// - weaponType: Sword / Cudgel (Hammer)
/// - damageMultiplier: คูณดาเมจเมื่อใช้งาน
/// - speedModPercent: เปอร์เซ็นต์ปรับ speed ของผู้ใช้ (+25 หรือ -25)
/// - durationTurns: จำนวนเทิร์นที่เอฟเฟกต์คงอยู่ (เช่น 1)
/// </summary>
[CreateAssetMenu(menuName = "Gameplay/WeaponDefinition")]
public class WeaponDefinition : ScriptableObject
{
    public enum WeaponType { Sword, Cudgel }
    public WeaponType weaponType;
    public float damageMultiplier = 1f;
    public float speedModPercent = 0f;
    public int durationTurns = 1;
}