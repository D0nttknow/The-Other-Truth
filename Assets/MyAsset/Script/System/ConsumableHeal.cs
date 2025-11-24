// Assets/MyAsset/Script/System/ConsumableHeal.cs
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/ConsumableHeal")]
public class ConsumableHeal : ScriptableObject /* or : ItemBase if you use ItemBase */
{
    public int minRestore = 1;
    public int maxRestore = 3;

    public bool Use(GameObject target)
    {
        if (target == null) return false;
        try
        {
            var ps = target.GetComponent<PlayerStat>();
            if (ps == null)
            {
                Debug.LogWarning("[ConsumableHeal] Target has no PlayerStat component");
                return false;
            }

            int add = UnityEngine.Random.Range(minRestore, maxRestore + 1);

            // เพิ่ม maxHp หากมี field/property ชื่อที่เป็นไปได้
            AddToIntFieldOrProperty(ps, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" }, add);

            // เพิ่ม current hp ด้วย (ไม่เกิน max)
            int maxHp = GetIntFieldOrProp(ps, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" });
            int curHp = GetIntFieldOrProp(ps, new string[] { "hp", "Hp", "HP" });
            int newHp = Mathf.Min(maxHp, curHp + add);
            SetIntFieldOrProp(ps, new string[] { "hp", "Hp", "HP" }, newHp);

            Debug.LogFormat("[ConsumableHeal] Used on {0}: +{1} MaxHP -> newMax={2}, newHP={3}", target.name, add, maxHp, newHp);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ConsumableHeal] Exception: " + ex);
            return false;
        }
    }

    int GetIntFieldOrProp(object obj, string[] names)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n);
            if (f != null) { var v = f.GetValue(obj); return v is int ? (int)v : 0; }
            var p = t.GetProperty(n);
            if (p != null) { var v = p.GetValue(obj); return v is int ? (int)v : 0; }
        }
        return 0;
    }

    void SetIntFieldOrProp(object obj, string[] names, int val)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, val); return; }
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(int) && p.CanWrite) { p.SetValue(obj, val); return; }
        }
    }

    void AddToIntFieldOrProperty(object obj, string[] names, int add)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(int))
            {
                int cur = (int)f.GetValue(obj);
                f.SetValue(obj, cur + add);
                return;
            }
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead && p.CanWrite)
            {
                int cur = (int)p.GetValue(obj);
                p.SetValue(obj, cur + add);
                return;
            }
        }
    }
}