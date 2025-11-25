using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวช่วยสุ่มดรอปไอเท็ม (คืน list ของ ItemBase จำนวน count)
/// - เมื่อมอนสเตอร์ตาย: var drops = LootGenerator.GenerateDrops(pool); foreach (var d in drops) InventoryManager.Instance.AddItem(d);
/// </summary>
public static class LootGenerator
{
    public static List<ItemBase> GenerateDrops(List<ItemBase> pool, int count = 3)
    {
        var drops = new List<ItemBase>();
        if (pool == null || pool.Count == 0) return drops;
        for (int i = 0; i < count; i++)
        {
            var item = pool[Random.Range(0, pool.Count)];
            if (item != null) drops.Add(item);
        }
        return drops;
    }
}