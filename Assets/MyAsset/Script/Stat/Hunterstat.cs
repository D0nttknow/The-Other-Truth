using System;
using System.Collections;
using UnityEngine;

public class Hunterstat : MonoBehaviour, ICharacterStat, IHpProvider
{
    [Header("Hunter Stats")]
    public string hunterName = "Hunter";

    [Tooltip("Current HP (will be clamped to maxHp on Start)")]
    public int hunterhp = 30;

    [Tooltip("Maximum HP (capacity). Keep this > 0.")]
    public int hunterMaxHp = 30;

    public int hunteratk = 10;
    public int hunterdef = 5;
    public int hunterspeed = 8;
    public int hunterlevel = 1;

    // Optional event for UI subscription (if you use event-driven health UI)
    public event Action<int, int> OnHpChanged; // (current, max)

    // IHpProvider properties (for HealthBarManager)
    public int CurrentHp => hunterhp;
    public int MaxHp => hunterMaxHp;

    // ICharacterStat implementation (include maxHp)
    public string Name => hunterName;
    public int hp => hunterhp;
    public int maxHp => hunterMaxHp;
    public int atk => hunteratk;
    public int def => hunterdef;
    public int speed => hunterspeed;
    public int level => hunterlevel;

    [Header("HealthBar (screen-space via HealthBarManager)")]
    [Tooltip("If true, this hunter will request a healthbar from HealthBarManager at Start.")]
    public bool createManagerUI = true;
    [Tooltip("Optional transform to follow (e.g. head). If null, uses this.transform.")]
    public Transform headTransform;
    [Tooltip("World offset applied to the follower when creating manager UI.")]
    public Vector3 hpUiOffset = new Vector3(0f, 1.6f, 0f);

    void Start()
    {
        // Ensure maxHp is valid and clamp current hp to max
        if (hunterMaxHp <= 0) hunterMaxHp = Mathf.Max(1, hunterhp);
        hunterhp = Mathf.Clamp(hunterhp, 0, hunterMaxHp);

        // Notify UI initial values if any
        OnHpChanged?.Invoke(hunterhp, hunterMaxHp);

        // Optionally register healthbar with manager (safe-wait to avoid ordering/race)
        if (createManagerUI)
            StartCoroutine(RegisterHealthBarSafe());

        Debug.Log($"Hunter Level: {hunterlevel} HP: {hunterhp}/{hunterMaxHp}, ATK: {hunteratk}, Def {hunterdef}, Speed {hunterspeed}");
    }

    IEnumerator RegisterHealthBarSafe()
    {
        float t = 0f;
        float timeout = 2f;
        while (HealthBarManager.Instance == null && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (HealthBarManager.Instance == null)
        {
            Debug.LogWarning($"{name}: HealthBarManager not found. Manager UI not created for Hunter.");
            yield break;
        }

        Transform target = headTransform != null ? headTransform : transform;
        var hb = HealthBarManager.Instance.CreateFor(gameObject, target);
        if (hb != null)
        {
            var follower = hb.GetComponent<HealthBarFollower>();
            if (follower != null)
            {
                follower.worldOffset = hpUiOffset;
                if (follower.uiCamera == null && Camera.main != null) follower.uiCamera = Camera.main;
            }
            Debug.Log($"{name}: Registered manager healthbar.");
        }
    }

    // รับ Damage
    public void TakeDamage(int damage)
    {
        Debug.Log($"TakeDamage called on {gameObject.name} dmg={damage} time={Time.time}");
        Debug.Log(System.Environment.StackTrace);
        int dmg = Mathf.Max(damage - hunterdef, 1);
        hunterhp -= dmg;
        hunterhp = Mathf.Max(0, hunterhp);

        Debug.Log($"Hunter got hit {dmg} HP left: {hunterhp}/{hunterMaxHp}");

        // notify UI
        OnHpChanged?.Invoke(hunterhp, hunterMaxHp);

        if (hunterhp <= 0)
        {
            hunterhp = 0;
            Die();
        }
    }

    // Heal (utility)
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        hunterhp = Mathf.Min(hunterhp + amount, hunterMaxHp);
        OnHpChanged?.Invoke(hunterhp, hunterMaxHp);
    }

    // Change max HP safely. keepPercent=true preserves current hp percentage.
    public void SetMaxHp(int newMaxHp, bool keepPercent = true)
    {
        if (newMaxHp <= 0) newMaxHp = 1;
        if (keepPercent)
        {
            float pct = (hunterMaxHp > 0) ? (float)hunterhp / hunterMaxHp : 1f;
            hunterMaxHp = newMaxHp;
            hunterhp = Mathf.Clamp(Mathf.RoundToInt(pct * hunterMaxHp), 0, hunterMaxHp);
        }
        else
        {
            hunterMaxHp = newMaxHp;
            hunterhp = Mathf.Clamp(hunterhp, 0, hunterMaxHp);
        }
        OnHpChanged?.Invoke(hunterhp, hunterMaxHp);
    }

    // Set current HP as percentage (0..1)
    public void SetHpPercent(float pct)
    {
        pct = Mathf.Clamp01(pct);
        hunterhp = Mathf.RoundToInt(pct * hunterMaxHp);
        OnHpChanged?.Invoke(hunterhp, hunterMaxHp);
    }

    // ตาย
    void Die()
    {
        Debug.Log($"{Name} died");
        // remove manager UI if any (HealthBarManager.RemoveFor is safe no-op if not registered)
        if (HealthBarManager.Instance != null)
            HealthBarManager.Instance.RemoveFor(gameObject);
        Destroy(gameObject);
    }

    // โจมตี Monster
    public void AttackMonster(IMonsterStat monster)
    {
        if (monster == null) return;
        monster.TakeDamage(hunteratk);
        // Animation / Sound เพิ่มตรงนี้
    }

    public void StrongAttackMonster(IMonsterStat monster)
    {
        if (monster == null) return;
        int strongAtk = atk * 2; // แรงขึ้น 2 เท่า
        monster.TakeDamage(strongAtk);
        // Animation / Sound เพิ่มตรงนี้
    }

    void OnDestroy()
    {
        // ensure manager cleanup
        if (HealthBarManager.Instance != null)
            HealthBarManager.Instance.RemoveFor(gameObject);
    }
}