using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/ConsumableHeal")]
public class ConsumableHeal : ItemBase
{
    public int minRestore = 1;
    public int maxRestore = 3;

    // ถ้า ItemBase มีเมทอด abstract/virtual Use ให้ override
    public override bool Use(GameObject target)
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

            // เพิ่ม max HP เท่านั้น (เหมือน pot_atk/pot_def เพิ่มค่าสเตตัส)
            AddToIntFieldOrProperty(ps, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" }, add);

            int newMax = GetIntFieldOrProp(ps, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" });
            Debug.LogFormat("[ConsumableHeal] Used on {0}: +{1} MaxHP -> newMax={2}", target.name, add, newMax);
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