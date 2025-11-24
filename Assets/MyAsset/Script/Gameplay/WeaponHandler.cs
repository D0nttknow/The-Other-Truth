using System.Collections;
using UnityEngine;

/// <summary>
/// Component ที่ติดกับ CharacterEquipment เพื่อจัดการอาวุธและเอฟเฟกต์ที่เกิดเมื่อใช้
/// - SetWeapon() เพื่อเปลี่ยนอาวุธ
/// - OnUse() เรียกเมื่อเริ่มโจมตี/ใช้สกิล (ก่อนคำนวณดาเมจ) เพื่อเซ็ต multiplier และ speed mod
/// - OnTurnEnd() เรียกเมื่อเทิร์นของผู้ใช้จบ (TurnBaseSystem ควรเรียก) เพื่อลด duration และ reset เมื่อครบ
/// - ตัวคูณ CurrentDamageMultiplier และ CurrentSpeedModPercent ให้ระบบคำนวณดาเมจ/ลำดับเทิร์นอ่านค่าต่อ
/// </summary>
[DisallowMultipleComponent]
public class WeaponHandler : MonoBehaviour
{
    public WeaponDefinition currentWeapon;

    [System.NonSerialized] public float CurrentDamageMultiplier = 1f;
    [System.NonSerialized] public float CurrentSpeedModPercent = 0f;
    int _remainingTurns = 0;

    public void SetWeapon(WeaponDefinition def)
    {
        currentWeapon = def;
        Debug.LogFormat("[WeaponHandler] Set weapon {0}", def != null ? def.name : "null");
    }

    /// <summary>
    /// เรียกเมื่อใช้การโจมตีหรือสกิล — จะตั้ง multiplier และตั้ง remainingTurns = durationTurns
    /// </summary>
    public void OnUse()
    {
        if (currentWeapon == null)
        {
            CurrentDamageMultiplier = 1f;
            CurrentSpeedModPercent = 0f;
            _remainingTurns = 0;
            return;
        }

        CurrentDamageMultiplier = currentWeapon.damageMultiplier;
        CurrentSpeedModPercent = currentWeapon.speedModPercent;
        _remainingTurns = currentWeapon.durationTurns;

        Debug.LogFormat("[WeaponHandler] OnUse applied dmgMult={0} speedMod%={1} for {2} turns", CurrentDamageMultiplier, CurrentSpeedModPercent, _remainingTurns);
    }

    /// <summary>
    /// เรียกเมื่อเทิร์นของผู้ใช้จบ - จะลด counter และ reset ถ้าหมด
    /// TurnBaseSystem หรือ Turn handler ควรเรียกเมทอดนี้สำหรับ battler ที่เทิร์นจบ
    /// </summary>
    public void OnTurnEnd()
    {
        if (_remainingTurns <= 0) return;
        _remainingTurns--;
        if (_remainingTurns <= 0)
        {
            ResetBuffs();
        }
        else
        {
            Debug.LogFormat("[WeaponHandler] OnTurnEnd reduced remainingTurns -> {0}", _remainingTurns);
        }
    }

    public void ResetBuffs()
    {
        CurrentDamageMultiplier = 1f;
        CurrentSpeedModPercent = 0f;
        _remainingTurns = 0;
        Debug.Log("[WeaponHandler] ResetBuffs");
    }
}