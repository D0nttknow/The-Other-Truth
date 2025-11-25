using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StatEnemie1Monster (updated)
/// - Same behavior as StatEnemieMokou: gameplay max HP is set to randomized HP on spawn
/// - Separates gameplay max vs UI max (uiMaxHp)
/// - Raises OnHpChanged(current, uiMax) for HealthBarManager/UI consumers
/// - Optional world-space uiPrefab OR registration with HealthBarManager (screen-space)
/// - EXP calculation added: configurable baseExp and expMultiplier, scales by monsterLevel
/// - Calls TurnManager.RecordEnemyDefeated(this.gameObject) in Die() so TurnManager can collect rewards/exp
/// </summary>
public class StatEnemie1Monster : MonoBehaviour, IMonsterStat, IHpProvider
{
    [Header("Monster Stat")]
    public string _monsterName = "Chess";
    public int _monsterLevel = 1;

    // current / base stats
    public int _monsterHp;
    public int _monsterAtk;
    public int _monsterDef;
    public int _monsterSpeed;

    // gameplay max hp (used by game logic)
    [Header("Runtime (gameplay)")]
    [Tooltip("Gameplay max HP used for clamps and combat calculations.")]
    public int _monsterMaxHpGame = 30;

    // UI-only max HP (optional). If > 0, UI will use this value as the max for the health bar.
    [Header("UI-only (optional)")]
    [Tooltip("If > 0, UI will use this value as its max. If 0, UI falls back to gameplay max.")]
    public int uiMaxHp = 0;

    [Header("EXP (designer configurable)")]
    [Tooltip("Base EXP for level 1. Final exp = Round(baseExp * expMultiplier^(level-1))")]
    public int baseExp = 10;
    [Tooltip("Multiplier per level (e.g. 1.2 => each level gives 20% more EXP)")]
    public float expMultiplier = 1.2f;

    // Implement IMonsterStat properties (names must match interface)
    public string monsterName => _monsterName;
    public int monsterLevel => _monsterLevel;
    public int monsterHp => _monsterHp;
    public int monsterMaxHp => _monsterMaxHpGame; // expose gameplay max
    public int monsterAtk => _monsterAtk;
    public int monsterDef => _monsterDef;
    public int monsterSpeed => _monsterSpeed;

    // IMPLEMENTATION REQUIRED BY IMonsterStat
    // EXP value awarded when this monster is defeated — scales with level
    public int expValue
    {
        get
        {
            int lvl = Math.Max(1, _monsterLevel);
            double val = baseExp * Math.Pow((double)expMultiplier, lvl - 1);
            int outVal = Math.Max(1, (int)Math.Round(val));
            return outVal;
        }
    }

    // IHpProvider implementation (event + props) for HealthBarManager subscribe
    public event Action<int, int> OnHpChanged; // (current, maxForUI)
    public int CurrentHp => _monsterHp;
    public int MaxHp => _monsterMaxHpGame; // other systems see gameplay max

    [Header("World-space UI (optional)")]
    [Tooltip("Assign a small World-Space Canvas prefab that contains an Image for HP fill and a Text for name.")]
    public GameObject uiPrefab; // prefab structure described below
    GameObject uiInstance;
    Image hpFillImage;
    Text nameText;
    CanvasGroup uiCanvasGroup;

    [Header("UI Options")]
    public Vector3 uiWorldOffset = new Vector3(0, 2.0f, 0);
    public bool faceCamera = true;
    public float uiSmoothing = 8f; // lerp smoothing for hp bar

    // If true, also create a screen-space UI via HealthBarManager (screen-space overlay canvas)
    [Header("HealthBarManager (screen-space UI)")]
    [Tooltip("If enabled, this monster will request a healthbar from HealthBarManager at runtime.")]
    public bool createManagerUI = true;

    // internal displayed fill for smoothing
    float displayedFill = 1f;

    void Start()
    {
        SetStatsByLevel();

        // use the randomized _monsterHp as the gameplay max so the bar is full on spawn
        _monsterMaxHpGame = Mathf.Max(1, _monsterHp);
        _monsterHp = Mathf.Clamp(_monsterHp, 0, _monsterMaxHpGame);

        // initial displayed fill uses uiMax if set, otherwise gameplay max
        displayedFill = GetFillFraction();

        SetupUI();
        UpdateUIImmediate();

        // fire initial event with UI max (so manager/UI draws correctly)
        OnHpChanged?.Invoke(_monsterHp, GetMaxForUI());

        // optionally register with HealthBarManager (safe wait)
        if (createManagerUI)
            StartCoroutine(RegisterHealthBarSafe());

        Debug.Log($"{_monsterName} Level {monsterLevel} HP: {monsterHp}/{_monsterMaxHpGame}, ATK: {monsterAtk}, Def: {monsterDef}, Speed: {monsterSpeed}, EXP: {expValue}");
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
            Debug.LogWarning($"{name}: HealthBarManager not found, manager UI not created.");
            yield break;
        }

        var hb = HealthBarManager.Instance.CreateFor(gameObject, transform);
        if (hb != null)
        {
            var follower = hb.GetComponent<HealthBarFollower>();
            if (follower != null)
            {
                follower.worldOffset = uiWorldOffset;
                if (follower.uiCamera == null && Camera.main != null)
                    follower.uiCamera = Camera.main;
            }
            Debug.Log($"{name}: Registered manager healthbar.");
        }
    }

    void Update()
    {
        // rotate/face camera if required
        if (uiInstance != null)
        {
            uiInstance.transform.position = transform.position + uiWorldOffset;

            if (faceCamera && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                uiInstance.transform.LookAt(uiInstance.transform.position + (uiInstance.transform.position - camPos));
            }

            // smooth HP fill
            if (hpFillImage != null)
            {
                float targetFill = GetFillFraction();
                displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * uiSmoothing);
                hpFillImage.fillAmount = displayedFill;
            }
        }
    }

    // กำหนดค่าสถานะตามเลเวล (ยังคง Random เหมือนเดิม)
    void SetStatsByLevel()
    {
        if (_monsterLevel == 1)
        {
            _monsterHp = UnityEngine.Random.Range(10, 31);
            _monsterAtk = UnityEngine.Random.Range(5, 16);
            _monsterDef = UnityEngine.Random.Range(5, 21);
            _monsterSpeed = UnityEngine.Random.Range(5, 11);
        }
        else if (_monsterLevel == 2)
        {
            _monsterHp = UnityEngine.Random.Range(20, 51);
            _monsterAtk = UnityEngine.Random.Range(8, 22);
            _monsterDef = UnityEngine.Random.Range(9, 27);
            _monsterSpeed = UnityEngine.Random.Range(8, 15);
        }
    }

    // รับ Damage
    public void TakeDamage(int damage)
    {
        Debug.Log($"TakeDamage called on {gameObject.name} dmg={damage} time={Time.time}");
        Debug.Log(System.Environment.StackTrace);
        int dmg = Mathf.Max(damage - _monsterDef, 1);
        _monsterHp -= dmg;
        _monsterHp = Mathf.Max(0, _monsterHp);

        Debug.Log($"{monsterName} got hit {dmg} HP left: {_monsterHp}/{_monsterMaxHpGame}");

        // update UI and event (send UI max)
        UpdateUIImmediate();
        OnHpChanged?.Invoke(_monsterHp, GetMaxForUI());

        if (_monsterHp <= 0)
        {
            Die();
        }
    }

    // ตาย
    void Die()
    {
        // record defeat so TurnManager can assemble rewards/exp
        if (TurnManager.Instance != null)
            TurnManager.Instance.RecordEnemyDefeated(gameObject);

        // hide UI gracefully if exists
        if (uiCanvasGroup != null)
        {
            uiCanvasGroup.alpha = 0f;
            uiCanvasGroup.interactable = false;
            uiCanvasGroup.blocksRaycasts = false;
        }
        else if (uiInstance != null)
        {
            uiInstance.SetActive(false);
        }

        // if registered with manager, manager will remove when we call RemoveFor in OnDestroy
        Destroy(gameObject);
    }

    // โจมตี Player ผ่าน interface
    public void AttackPlayer(ICharacterStat player)
    {
        if (player != null)
        {
            player.TakeDamage(_monsterAtk);
            Debug.Log($"{monsterName} attacks {player.Name} with {_monsterAtk} damage");
        }
    }

    // --- UI helper methods ---
    void SetupUI()
    {
        if (uiPrefab == null)
        {
            Debug.LogWarning($"StatEnemie1Monster ({name}): uiPrefab not assigned. To show HP above monster, assign a world-space UI prefab.");
            return;
        }

        uiInstance = Instantiate(uiPrefab, transform.position + uiWorldOffset, Quaternion.identity, null);
        uiInstance.transform.position = transform.position + uiWorldOffset;

        Transform nameTf = uiInstance.transform.Find("NameText");
        if (nameTf != null) nameText = nameTf.GetComponent<Text>();
        Transform fillTf = uiInstance.transform.Find("HPBackground/HPFill");
        if (fillTf != null) hpFillImage = fillTf.GetComponent<Image>();

        if (nameText == null) nameText = uiInstance.GetComponentInChildren<Text>();
        if (hpFillImage == null)
        {
            var imgs = uiInstance.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                string iname = img.gameObject.name.ToLower();
                if (iname.Contains("fill") || iname.Contains("hp"))
                {
                    hpFillImage = img;
                    break;
                }
            }
        }

        uiCanvasGroup = uiInstance.GetComponent<CanvasGroup>();
        if (uiCanvasGroup == null) uiCanvasGroup = uiInstance.AddComponent<CanvasGroup>();

        if (nameText != null) nameText.text = _monsterName;
    }

    void UpdateUIImmediate()
    {
        if (hpFillImage != null)
        {
            float fill = GetFillFraction();
            hpFillImage.fillAmount = fill;
            displayedFill = fill;
        }
        if (nameText != null)
        {
            nameText.text = _monsterName;
        }
        if (uiInstance != null)
        {
            uiInstance.transform.position = transform.position + uiWorldOffset;
        }
    }

    void OnDestroy()
    {
        if (uiInstance != null)
        {
            Destroy(uiInstance);
        }

        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.RemoveFor(gameObject);
        }
    }

    // Optional debuff APIs (stubs)
    public void ApplyBleed(int damagePerTurn, int duration)
    {
        Debug.Log($"{monsterName} would receive Bleed {damagePerTurn} for {duration} turns (Not implemented here).");
    }

    public void ApplyStun(int duration)
    {
        Debug.Log($"{monsterName} would be Stunned for {duration} turns (Not implemented here).");
    }

    // --- Helpers for UI vs gameplay max ---
    int GetMaxForUI()
    {
        return (uiMaxHp > 0) ? uiMaxHp : _monsterMaxHpGame;
    }

    float GetFillFraction()
    {
        int maxForUI = GetMaxForUI();
        return (maxForUI > 0) ? (float)_monsterHp / maxForUI : 0f;
    }
}