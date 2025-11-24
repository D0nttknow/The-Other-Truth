using System;
using UnityEngine;

/// <summary>
/// ใช้แล้วเพิ่ม DEF ของ target (1-3)
/// </summary>
[CreateAssetMenu(menuName = "Gameplay/ConsumableDef")]
public class ConsumableDef : ItemBase
{
    public int minAdd = 1;
    public int maxAdd = 3;

    public override bool Use(GameObject target)
    {
        if (target == null) return false;
        try
        {
            var ps = target.GetComponent<PlayerStat>();
            if (ps == null)
            {
                Debug.LogWarning("[ConsumableDef] Target has no PlayerStat component");
                return false;
            }

            int add = UnityEngine.Random.Range(minAdd, maxAdd + 1);
            AddToIntFieldOrProperty(ps, new string[] { "def", "Def", "DEF" }, add);
            Debug.LogFormat("[ConsumableDef] Used on {0}: +{1} DEF", target.name, add);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ConsumableDef] Exception: " + ex);
            return false;
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