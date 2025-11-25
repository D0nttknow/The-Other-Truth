using System;
using UnityEngine;

/// <summary>
/// Simple serializable Reward representation used by TurnManager / BattleEndUIManager.
/// - id: machine id (e.g. item id, monster id)
/// - name: human readable name (optional)
/// - quantity: item count (if applicable)
/// - gold: (optional) gold reward
/// - exp: experience reward
/// </summary>
[Serializable]
public class Reward
{
    // machine id (item id / monster id)
    public string id;

    // readable name (optional)
    public string name;

    // quantity for item rewards
    public int quantity;

    // optional gold reward (if you use currency)
    public int gold;

    // experience points granted by this reward (used by your TurnManager)
    public int exp;

    public Reward()
    {
        id = string.Empty;
        name = string.Empty;
        quantity = 0;
        gold = 0;
        exp = 0;
    }

    public Reward(string id, int quantity = 1, int gold = 0, int exp = 0)
    {
        this.id = id ?? string.Empty;
        this.name = id;
        this.quantity = quantity;
        this.gold = gold;
        this.exp = exp;
    }

    public Reward(string id, string name, int quantity = 1, int gold = 0, int exp = 0)
    {
        this.id = id ?? string.Empty;
        this.name = name ?? id ?? string.Empty;
        this.quantity = quantity;
        this.gold = gold;
        this.exp = exp;
    }

    public override string ToString()
    {
        return $"Reward(id='{id}', name='{name}', qty={quantity}, gold={gold}, exp={exp})";
    }
}