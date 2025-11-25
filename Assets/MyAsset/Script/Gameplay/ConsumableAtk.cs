using System;
using UnityEngine;

/// <summary>
/// ใช้แล้วเพิ่ม ATK ของ target (1-3)
/// </summary>
[CreateAssetMenu(menuName = "Gameplay/ConsumableAtk")]
public class ConsumableAtk : ItemBase
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
                Debug.LogWarning("[ConsumableAtk] Target has no PlayerStat component");
                return false;
            }

            int add = UnityEngine.Random.Range(minAdd, maxAdd + 1);
            AddToIntFieldOrProperty(ps, new string[] { "atk", "Atk", "ATK" }, add);
            Debug.LogFormat("[ConsumableAtk] Used on {0}: +{1} ATK", target.name, add);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ConsumableAtk] Exception: " + ex);
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