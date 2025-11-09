using System;
using UnityEngine;

/// <summary>
/// Simple PlayerStat: handles EXP, Level and LevelUp.
/// - baseExp: base requirement for level 1 -> level 2
/// - expGrowth: multiplier applied per level (exponential growth)
/// - AddExp(amount) will add exp, check level up repeatedly and invoke OnLevelUp when level increases
/// - Optional: Save/Load using PlayerPrefs (by unique id)
/// </summary>
public class PlayerStat : MonoBehaviour
{
    [Header("Level / EXP")]
    public int level = 1;
    public int currentExp = 0;

    [Tooltip("Base EXP required for level 2 (level 1->2). Formula used: required = Round(baseExp * Mathf.Pow(expGrowth, level-1))")]
    public int baseExp = 100;
    [Tooltip("Growth per level (1.2 = 20% more EXP required each level)")]
    public float expGrowth = 1.2f;
    [Tooltip("Maximum level allowed (0 = unlimited)")]
    public int maxLevel = 0;

    // Event: (oldLevel, newLevel)
    public event Action<int, int> OnLevelUp;
    // Event: when exp changes (currentExp, requiredExp)
    public event Action<int, int> OnExpChanged;

    // computes required EXP for next level from current level
    public int ExpToNext
    {
        get
        {
            int lvl = Mathf.Max(1, level);
            double val = baseExp * Math.Pow((double)expGrowth, lvl - 1);
            int req = Mathf.Max(1, (int)Math.Round(val));
            return req;
        }
    }

    // Add exp and handle leveling (returns number of levels gained)
    public int AddExp(int amount)
    {
        if (amount <= 0) return 0;
        if (maxLevel > 0 && level >= maxLevel)
        {
            // optionally clamp at max level
            return 0;
        }

        currentExp += amount;
        int levelsGained = 0;

        while ((maxLevel == 0 || level < maxLevel) && currentExp >= ExpToNext)
        {
            currentExp -= ExpToNext;
            int old = level;
            level++;
            levelsGained++;
            OnLevelUp?.Invoke(old, level);
        }

        // if reached max level, optionally clamp currentExp
        if (maxLevel > 0 && level >= maxLevel)
        {
            currentExp = Mathf.Min(currentExp, ExpToNext - 1);
        }

        OnExpChanged?.Invoke(currentExp, ExpToNext);
        Debug.Log($"{gameObject.name} gained {amount} EXP. Level now {level} (gained {levelsGained}) CurrentExp={currentExp}/{ExpToNext}");
        return levelsGained;
    }

    // Optional: helper to set exp directly (useful for debug/save)
    public void SetExp(int newExp)
    {
        currentExp = Mathf.Max(0, newExp);
        OnExpChanged?.Invoke(currentExp, ExpToNext);
    }

    // Optional basic persistence (PlayerPrefs) using GameObject.name as key.
    // For real game use a proper save system and unique player IDs.
    public void Save()
    {
        string key = $"PlayerStat_{gameObject.name}";
        PlayerPrefs.SetString(key, JsonUtility.ToJson(new SaveData { level = level, currentExp = currentExp }));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        string key = $"PlayerStat_{gameObject.name}";
        if (!PlayerPrefs.HasKey(key)) return;
        var sd = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key));
        level = sd.level;
        currentExp = sd.currentExp;
        OnExpChanged?.Invoke(currentExp, ExpToNext);
    }

    [Serializable]
    class SaveData { public int level; public int currentExp; }
}