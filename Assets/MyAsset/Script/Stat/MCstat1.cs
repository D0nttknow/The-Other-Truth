using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Main Knight stats + HP UI hookup.
/// - Implements ICharacterStat and IHpProvider (so HealthBarManager can subscribe).
/// - Registers a healthbar with HealthBarManager on Start (safe-wait to avoid ordering/race).
/// - Exposes optional headTransform + offset so the healthbar can track the head position.
/// </summary>
public class MCstat1 : MonoBehaviour, ICharacterStat, IHpProvider
{
    [Header("Player Base Stats")]
    public string _name = "Main Knight";

    [Tooltip("Current HP (will be clamped to maxHp on Start)")]
    public int _hp = 20;

    [Tooltip("Maximum HP (capacity). Keep this > 0.")]
    public int _maxHp = 20;

    public int _atk = 15;
    public int _def = 6;
    public int _speed = 10;
    public int _level = 1;

    [Header("HP UI (optional)")]
    [Tooltip("If set, the healthbar will follow this transform (e.g. head). If null, the character transform is used.")]
    public Transform headTransform;
    [Tooltip("Offset from the head/world position for the UI (in world units).")]
    public Vector3 hpUiOffset = new Vector3(0f, 1.6f, 0f);

    // Event for UI (current, max)
    public event Action<int, int> OnHpChanged;

    // IHpProvider properties (used by HealthBarManager/healthbar UI)
    public int CurrentHp => _hp;
    public int MaxHp => _maxHp;

    // ICharacterStat implementation
    public string Name => _name;
    public int hp => _hp;
    public int maxHp => _maxHp;
    public int atk => _atk;
    public int def => _def;
    public int speed => _speed;
    public int level => _level;

    void Start()
    {
        // Ensure maxHp is valid and clamp current hp to max
        if (_maxHp <= 0) _maxHp = Mathf.Max(1, _hp);
        _hp = Mathf.Clamp(_hp, 0, _maxHp);

        // Notify UI initial values
        OnHpChanged?.Invoke(_hp, _maxHp);

        // Register healthbar with manager (safe-wait to avoid race where manager Awake hasn't run yet)
        StartCoroutine(RegisterHealthBarSafe());

        Debug.Log($"Player HP: {hp}/{maxHp}, ATK: {atk}, Def {def}, Speed {speed}");
    }

    IEnumerator RegisterHealthBarSafe()
    {
        // wait one frame (or a bit) so HealthBarManager.Instance is likely ready
        yield return null;
        yield return new WaitForSeconds(0.02f);

        if (HealthBarManager.Instance == null)
        {
            Debug.LogWarning("MCstat1: HealthBarManager.Instance is null. Healthbar not created. Make sure HealthBarManager exists in the scene and is active before characters start.");
            yield break;
        }

        Transform target = headTransform != null ? headTransform : transform;
        var hb = HealthBarManager.Instance.CreateFor(gameObject, target);
        if (hb != null)
        {
            // set follower offset if available
            var follower = hb.GetComponent<HealthBarFollower>();
            if (follower != null)
            {
                follower.worldOffset = hpUiOffset;
                // make sure follower has a camera reference if needed
                if (follower.uiCamera == null && Camera.main != null) follower.uiCamera = Camera.main;
            }

            // HealthBarManager already subscribes to IHpProvider in CreateFor; nothing else to do here
        }
    }

    public void TakeDamage(int damage)
    {
        Debug.Log($"TakeDamage called on {gameObject.name} dmg={damage} time={Time.time}");
        Debug.Log(System.Environment.StackTrace);
        int dmg = Mathf.Max(damage - def, 1);
        _hp -= dmg;
        _hp = Mathf.Max(0, _hp);

        Debug.Log($"Player got hit {dmg} HP left: {_hp}/{_maxHp}");

        // notify UI
        OnHpChanged?.Invoke(_hp, _maxHp);

        if (_hp <= 0)
        {
            _hp = 0;
            Die();
        }
    }

    // Heal method
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        _hp = Mathf.Min(_hp + amount, _maxHp);
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    // Change max HP safely. If keepPercent true, keep same HP percentage after change.
    public void SetMaxHp(int newMaxHp, bool keepPercent = true)
    {
        if (newMaxHp <= 0) newMaxHp = 1;
        if (keepPercent)
        {
            float percent = (_maxHp > 0) ? (float)_hp / _maxHp : 1f;
            _maxHp = newMaxHp;
            _hp = Mathf.Clamp(Mathf.RoundToInt(percent * _maxHp), 0, _maxHp);
        }
        else
        {
            _maxHp = newMaxHp;
            _hp = Mathf.Clamp(_hp, 0, _maxHp);
        }
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    // Set current HP as percentage of max (0..1)
    public void SetHpPercent(float pct)
    {
        pct = Mathf.Clamp01(pct);
        _hp = Mathf.RoundToInt(pct * _maxHp);
        OnHpChanged?.Invoke(_hp, _maxHp);
    }

    void Die()
    {
        Debug.Log($"{Name} died");
        // optionally remove healthbar explicitly
        if (HealthBarManager.Instance != null) HealthBarManager.Instance.RemoveFor(gameObject);
        Destroy(gameObject);
    }

    public void AttackMonster(IMonsterStat monster)
    {
        if (monster == null) return;
        monster.TakeDamage(atk);
    }

    public void StrongAttackMonster(IMonsterStat monster)
    {
        if (monster == null) return;
        int strongAtk = atk * 2;
        monster.TakeDamage(strongAtk);
    }

    void OnDestroy()
    {
        // clean up healthbar subscription/instance if any
        if (HealthBarManager.Instance != null) HealthBarManager.Instance.RemoveFor(gameObject);
    }
}