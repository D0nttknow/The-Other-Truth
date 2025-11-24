using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Consumable that heals the whole party when used.
/// - Heals every alive player in TurnManager.battlerObjects (where battlers[i].isMonster == false)
/// - Heal amount: random between minHeal..maxHeal (same amount applied to all by default)
/// - Does not increase max HP; only increases current HP and clamps to the target's max HP via reflection
/// - Inherits ItemBase (if your project uses ItemBase). If you don't have ItemBase, change base to ScriptableObject and adjust InventoryManager.
/// </summary>
[CreateAssetMenu(menuName = "Gameplay/ConsumablePartyHeal")]
public class ConsumablePartyHeal : ItemBase
{
    [Tooltip("Minimum heal amount applied to each party member")]
    public int minHeal = 10;

    [Tooltip("Maximum heal amount applied to each party member")]
    public int maxHeal = 20;

    [Tooltip("If true: pick a separate random heal for each member. If false: pick one amount and apply to all.")]
    public bool randomPerMember = false;

    public override bool Use(GameObject user)
    {
        try
        {
            if (TurnManager.Instance == null)
            {
                Debug.LogWarning("[ConsumablePartyHeal] No TurnManager instance found. Cannot heal party.");
                return false;
            }

            // determine list of player GameObjects (alive)
            var tm = TurnManager.Instance;
            var players = new List<GameObject>();
            for (int i = 0; i < tm.battlerObjects.Count && i < tm.battlers.Count; i++)
            {
                var go = tm.battlerObjects[i];
                var b = tm.battlers[i];
                if (go == null || b == null) continue;
                if (!b.isMonster && b.hp > 0)
                {
                    players.Add(go);
                }
            }

            if (players.Count == 0)
            {
                Debug.Log("[ConsumablePartyHeal] No alive party members to heal.");
                return false;
            }

            // single amount or random per member
            int amountForAll = UnityEngine.Random.Range(minHeal, maxHeal + 1);

            foreach (var p in players)
            {
                int healAmount = randomPerMember ? UnityEngine.Random.Range(minHeal, maxHeal + 1) : amountForAll;

                var ps = p.GetComponent<PlayerStat>();
                if (ps == null)
                {
                    // still attempt generic reflection if PlayerStat not present
                    HealViaReflection(p, healAmount);
                }
                else
                {
                    // Get max hp (via common names) and current hp, then clamp
                    int maxHp = GetIntFieldOrProp(ps, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" });
                    int curHp = GetIntFieldOrProp(ps, new string[] { "hp", "Hp", "HP", "currentHp", "currentHP" });

                    int newHp = Mathf.Min(maxHp, curHp + healAmount);
                    SetIntFieldOrProp(ps, new string[] { "hp", "Hp", "HP", "currentHp", "currentHP" }, newHp);

                    Debug.LogFormat("[ConsumablePartyHeal] Healed {0} by {1} -> {2}/{3}", p.name, healAmount, newHp, maxHp);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ConsumablePartyHeal] Exception: " + ex);
            return false;
        }
    }

    // Fallback healing by reflection on arbitrary GameObject (if PlayerStat missing)
    void HealViaReflection(GameObject go, int healAmount)
    {
        if (go == null) return;
        var t = go.GetType(); // not useful for GameObject; need components
        // Try common component names that might store HP if PlayerStat isn't used
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            int maxHp = GetIntFieldOrProp(c, new string[] { "maxHp", "MaxHp", "maxHP", "MaxHP" });
            int curHp = GetIntFieldOrProp(c, new string[] { "hp", "Hp", "HP", "currentHp", "currentHP" });
            if (maxHp > 0)
            {
                int newHp = Mathf.Min(maxHp, curHp + healAmount);
                SetIntFieldOrProp(c, new string[] { "hp", "Hp", "HP", "currentHp", "currentHP" }, newHp);
                Debug.LogFormat("[ConsumablePartyHeal] (fallback) Healed {0}.{1} by {2} -> {3}/{4}", go.name, c.GetType().Name, healAmount, newHp, maxHp);
                return;
            }
        }

        // nothing to do
        Debug.LogWarning($"[ConsumablePartyHeal] Could not find HP field/property on {go.name} to heal.");
    }

    // reflection helpers (same approach used elsewhere)
    int GetIntFieldOrProp(object obj, string[] names)
    {
        if (obj == null) return 0;
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
        if (obj == null) return;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, val); return; }
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(int) && p.CanWrite) { p.SetValue(obj, val); return; }
        }
    }
}