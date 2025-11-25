using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบเลเวลสำหรับตัวละคร (ติดบน Main Character ที่มี PlayerStat/CharacterEquipment)
/// - Level, CurrentExp, ExpToNext, AvailableStatPoints
/// - MaxLevel default = 20
/// - เมื่อ AllocatePoint("hp"/"atk"/"def") จะลด AvailableStatPoints ลง 1 และพยายามเพิ่มค่าสเตตัสจริงใน PlayerStat ของ GameObject เดียวกัน
/// - มี event OnLevelUp เพื่อให้ระบบอื่น subscribe
/// - สามารถตั้ง autoAllocate และ Priorities เพื่อแจกแต้มอัตโนมัติเมื่อเลเวลอัพ
/// </summary>
public class PlayerLevel : MonoBehaviour
{
    [Header("Leveling Settings")]
    public int Level = 1;
    public int MaxLevel = 20;
    public int CurrentExp = 0;
    public int BaseExpToNext = 100;
    public float ExpGrowth = 1.15f;
    public int PointsPerLevel = 1;

    [Header("Auto allocation settings")]
    public bool autoAllocate = false;
    [Tooltip("ลำดับความสำคัญในการแจกแต้ม เช่น hp, atk, def")]
    public List<string> Priorities = new List<string>() { "hp", "atk", "def" };

    public int AvailableStatPoints = 0;

    // event เมื่อเลเวลอัพ (ส่งเลเวลใหม่)
    public event Action<int> OnLevelUp;

    public int ExpToNext
    {
        get
        {
            return Mathf.FloorToInt(BaseExpToNext * Mathf.Pow(ExpGrowth, Mathf.Max(0, Level - 1)));
        }
    }

    /// <summary>
    /// เพิ่ม exp แล้วจัดการการเลเวลอัพ (จะเรียก AutoAllocatePoints ถ้าตั้ง autoAllocate)
    /// </summary>
    public void AddExp(int amount)
    {
        if (Level >= MaxLevel) return;
        CurrentExp += Mathf.Max(0, amount);
        Debug.LogFormat("[PlayerLevel] AddExp +{0} -> CurrentExp={1}/{2}", amount, CurrentExp, ExpToNext);

        while (Level < MaxLevel && CurrentExp >= ExpToNext)
        {
            CurrentExp -= ExpToNext;
            Level++;
            AvailableStatPoints += PointsPerLevel;
            Debug.LogFormat("[PlayerLevel] LevelUp -> Level={0}, AvailableStatPoints={1}", Level, AvailableStatPoints);
            OnLevelUp?.Invoke(Level);

            if (autoAllocate) AutoAllocatePoints();
        }

        if (Level >= MaxLevel)
        {
            CurrentExp = Mathf.Min(CurrentExp, ExpToNext);
            Debug.Log("[PlayerLevel] Reached Max Level");
        }
    }

    /// <summary>
    /// Allocate 1 point to statName ("hp","atk","def").
    /// ถ้ามี PlayerStat component บน GameObject เดียวกัน จะเพิ่มค่าสเตตัสจริงให้ด้วย
    /// คืนค่า true ถ้า allocate สำเร็จ
    /// </summary>
    public bool AllocatePoint(string statName)
    {
        if (AvailableStatPoints <= 0) return false;
        if (string.IsNullOrEmpty(statName)) return false;

        statName = statName.ToLowerInvariant();
        if (statName != "hp" && statName != "atk" && statName != "def") return false;

        // ลดแต้ม
        AvailableStatPoints--;

        // พยายามเพิ่มค่าสเตตัสบน PlayerStat ถ้ามี
        var ps = GetComponent<PlayerStat>();
        if (ps != null)
        {
            ApplyStatIncrementToPlayerStat(ps, statName, 1);
            Debug.LogFormat("[PlayerLevel] Allocated 1 point to {0} and applied to PlayerStat on {1}", statName, gameObject.name);
        }
        else
        {
            Debug.LogFormat("[PlayerLevel] Allocated 1 point to {0} but no PlayerStat component found on {1}", statName, gameObject.name);
        }

        return true;
    }

    /// <summary>
    /// กระจายแต้มทั้งหมดตาม Priorities (เรียก AllocatePoint ที่จะพยายามอัปเดต PlayerStat ด้วย)
    /// </summary>
    public void AutoAllocatePoints()
    {
        if (AvailableStatPoints <= 0 || Priorities == null || Priorities.Count == 0) return;
        int safety = 0;
        while (AvailableStatPoints > 0 && safety < 1000)
        {
            foreach (var p in Priorities)
            {
                if (AvailableStatPoints <= 0) break;
                AllocatePoint(p);
            }
            safety++;
        }
    }

    // helper: เพิ่มค่าใน PlayerStat โดยพยายามค้น field/property หลายชื่อ (รองรับการตั้งชื่อต่างกัน)
    void ApplyStatIncrementToPlayerStat(PlayerStat ps, string statName, int amount)
    {
        if (ps == null) return;
        string[] hpKeys = { "maxHp", "MaxHp", "maxHP", "MaxHP" }; // เราให้แต้มเพิ่ม Max HP
        string[] atkKeys = { "atk", "Atk", "ATK", "attack", "Attack" };
        string[] defKeys = { "def", "Def", "DEF", "defence", "Defense", "defense" };

        if (statName == "hp")
        {
            if (!TryAddIntFieldOrProperty(ps, hpKeys, amount))
            {
                // fallback: เพิ่ม current hp ถ้าไม่เจอ maxHp
                TryAddIntFieldOrProperty(ps, new string[] { "hp", "Hp", "HP" }, amount);
            }
        }
        else if (statName == "atk")
        {
            TryAddIntFieldOrProperty(ps, atkKeys, amount);
        }
        else if (statName == "def")
        {
            TryAddIntFieldOrProperty(ps, defKeys, amount);
        }
    }

    bool TryAddIntFieldOrProperty(object obj, string[] names, int add)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(int))
            {
                int cur = (int)f.GetValue(obj);
                f.SetValue(obj, cur + add);
                return true;
            }
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead && p.CanWrite)
            {
                int cur = (int)p.GetValue(obj);
                p.SetValue(obj, cur + add);
                return true;
            }
        }
        return false;
    }
}