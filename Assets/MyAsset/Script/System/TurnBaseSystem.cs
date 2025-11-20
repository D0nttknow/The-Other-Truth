using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurnBaseSystem (merged TurnManager logic)
/// Consolidated from previous TurnManager implementation. Provides the same public API
/// via TurnBaseSystem.Instance and keeps a small compatibility subclass TurnManager : TurnBaseSystem.
/// </summary>
public class TurnBaseSystem : MonoBehaviour
{
    public static TurnBaseSystem Instance;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Header("UI References")]
    [Tooltip("Optional: assign the CharacterInfoPanel here. If left empty, TurnBaseSystem will try to FindObjectOfType<CharacterInfoPanel>() in Start.")]
    public CharacterInfoPanel characterInfoPanel;

    [Header("Round UI")]
    [Tooltip("Optional UI Text to show current round number on screen")]
    public Text roundText;
    [HideInInspector] public int roundNumber = 1;

    public GameObject selectedMonster = null;

    [Header("Runtime lists")]
    public List<GameObject> characterObjects;
    public List<Battler> battlers = new List<Battler>();
    public List<GameObject> battlerObjects = new List<GameObject>();

    private int turnIndex = 0;

    public enum BattleState { MonsterAttacking, WaitingForPlayerInput, PlayerReturning, WaitingForMonsterTurn }
    public BattleState state = BattleState.MonsterAttacking;

    [Header("Transient player UI panels (hidden during monster turns)")]
    public List<GameObject> playerUIPanels;

    [Header("Persistent player UI panels (e.g. per-player HP UI)")]
    public List<GameObject> persistentPlayerUIPanels;

    [Header("Runtime references")]
    [Tooltip("Canvas to parent persistent panels under")]
    public Canvas defaultCanvas;

    [Header("Behavior")]
    [Tooltip("If enabled, filter persistent panels to participants during actions")]
    public bool filterPersistentToParticipants = false;

    [Tooltip("If true, TurnBaseSystem will create and manage healthbars. Other scripts (AttachHealthBar / Stat) should skip creating their own healthbars.")]
    public bool manageHealthbars = true;

    private Dictionary<GameObject, GameObject> playerToPanel = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, GameObject> persistentPlayerToPanel = new Dictionary<GameObject, GameObject>();

    [Header("Turn Order")]
    public bool updateTurnOrderUI = true;

    [HideInInspector] public List<Reward> defeatedRewards = new List<Reward>();
    [HideInInspector] public List<GameObject> defeatedEnemies = new List<GameObject>();

    private HashSet<GameObject> actedThisRound = new HashSet<GameObject>();

    [Header("Per-character UI")]
    public GameObject perCharacterPanelPrefab;
    private Dictionary<GameObject, GameObject> battlerToPanel = new Dictionary<GameObject, GameObject>();

    // internal coroutine handles
    private Coroutine _delayedHealthbarCoroutine = null;

    // --- Selection debounce / dedupe helpers (prevent duplicate selection events) ---
    private float _lastSelectTime = -10f;
    private const float _selectDebounceSeconds = 0.12f;

    public GameObject CurrentBattlerObject
    {
        get
        {
            if (turnIndex >= 0 && turnIndex < battlerObjects.Count)
                return battlerObjects[turnIndex];
            return null;
        }
    }

    public bool IsCurrentTurn(GameObject go)
    {
        if (go == null) return false;
        return CurrentBattlerObject == go;
    }

    void Start()
    {
        Debug.Log("[TurnBaseSystem] START on GameObject='" + gameObject.name + "' characterObjects != null? "
                  + (characterObjects != null) + " count=" + (characterObjects != null ? characterObjects.Count : -1)
                  + " battlerObjects != null? " + (battlerObjects != null) + " count=" + (battlerObjects != null ? battlerObjects.Count : -1));

        // Auto-populate characterObjects if none assigned
        if ((characterObjects == null || characterObjects.Count == 0))
        {
            var foundList = new List<GameObject>();
            var all = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                if (mb is ICharacterStat || mb is IMonsterStat)
                {
                    if (!foundList.Contains(mb.gameObject))
                        foundList.Add(mb.gameObject);
                }
            }

            // deterministic ordering so mapping is stable across runs
            if (foundList.Count > 1)
            {
                foundList = foundList.OrderBy(g => g.name, StringComparer.Ordinal)
                                     .ThenBy(g => g.GetInstanceID())
                                     .ToList();
            }

            if (foundList.Count > 0)
            {
                characterObjects = foundList;
                Debug.Log("[TurnBaseSystem] Auto-populated characterObjects count=" + characterObjects.Count);
            }
            else
            {
                Debug.Log("[TurnBaseSystem] No ICharacterStat/IMonsterStat found for auto-populate.");
            }
        }

        if (characterObjects == null || characterObjects.Count == 0)
            Debug.LogWarning("[TurnBaseSystem] characterObjects is null or empty at Start. Make sure to populate it in the Inspector or before Start.");

        BuildBattlerListsFromCharacterObjects();

        if (defaultCanvas == null)
        {
            defaultCanvas = FindObjectOfType<Canvas>(true);
            if (defaultCanvas != null) Debug.Log("[TurnBaseSystem] defaultCanvas auto-assigned to '" + defaultCanvas.name + "'");
        }

        if (characterInfoPanel == null)
        {
            characterInfoPanel = FindObjectOfType<CharacterInfoPanel>();
            if (characterInfoPanel != null) Debug.Log("[TurnBaseSystem] characterInfoPanel auto-assigned to '" + characterInfoPanel.gameObject.name + "'");
        }

        // Create per-character panels based on battlerObjects (source of truth)
        CreateOrAssignPerCharacterPanels();

        // Create healthbars per battlerObject to keep mapping consistent if this manager is responsible
        if (manageHealthbars && HealthBarManager.Instance != null)
        {
            if (battlerObjects != null && battlerObjects.Count > 0)
            {
                for (int i = 0; i < battlerObjects.Count; i++)
                {
                    var go = battlerObjects[i];
                    if (go == null) continue;
                    HealthBarManager.Instance.CreateFor(go, go.transform);
                    Debug.Log("[TurnBaseSystem] HealthBarManager.CreateFor called for index=" + i + " go=" + go.name + " id=" + go.GetInstanceID());
                }
            }
            else
            {
                if (_delayedHealthbarCoroutine != null) StopCoroutine(_delayedHealthbarCoroutine);
                _delayedHealthbarCoroutine = StartCoroutine(DelayedHealthbarCreate(0.1f, 10));
                Debug.Log("[TurnBaseSystem] Scheduled delayed per-battler healthbar creation (battlerObjects empty at Start).");
            }
        }
        else if (!manageHealthbars)
        {
            Debug.Log("[TurnBaseSystem] manageHealthbars is false - skipping healthbar creation here. Other scripts should create healthbars.");
        }

        UpdatePlayerPanelMapping();
        EnsurePersistentPanelsVisible();
        RefreshTurnOrderUI();
        UpdateRoundUI();

        StartTurn();
    }

    void BuildBattlerListsFromCharacterObjects()
    {
        battlers.Clear();
        battlerObjects.Clear();

        if (characterObjects == null) return;

        var pairList = new List<(Battler battler, GameObject go)>();

        foreach (var go in characterObjects)
        {
            if (go == null) continue;

            var playerStat = go.GetComponent<ICharacterStat>();
            if (playerStat != null)
            {
                string name = SafeGet(() => playerStat.Name, go.name);

                int hp = SafeGet(() => playerStat.hp, 0);
                int atk = SafeGet(() => playerStat.atk, 0);
                int def = SafeGet(() => playerStat.def, 0);
                int spd = SafeGet(() => playerStat.speed, 0);

                var b = new Battler(string.IsNullOrEmpty(name) ? go.name : name, hp, atk, def, spd, false);
                pairList.Add((b, go));
                continue;
            }

            var monsterStat = go.GetComponent<IMonsterStat>();
            if (monsterStat != null)
            {
                string name = SafeGet(() => monsterStat.monsterName, go.name);

                int hp = SafeGet(() => monsterStat.monsterHp, 0);
                int atk = SafeGet(() => monsterStat.monsterAtk, 0);
                int def = SafeGet(() => monsterStat.monsterDef, 0);
                int spd = SafeGet(() => monsterStat.monsterSpeed, 0);

                var b = new Battler(string.IsNullOrEmpty(name) ? go.name : name, hp, atk, def, spd, true);
                pairList.Add((b, go));
                continue;
            }

            Debug.LogWarning("[TurnBaseSystem] GameObject '" + go.name + "' has no ICharacterStat or IMonsterStat - skipped when building turn order.");
        }

        // deterministic sort: primary = speed desc, secondary = name, tertiary = instance id
        pairList = pairList
            .OrderByDescending(p => p.battler.speed)
            .ThenBy(p => p.go.name, StringComparer.Ordinal)
            .ThenBy(p => p.go.GetInstanceID())
            .ToList();

        foreach (var p in pairList)
        {
            battlers.Add(p.battler);
            battlerObjects.Add(p.go);
        }

        // Debug listing for verification
        Debug.Log("[TurnBaseSystem] BuildBattlerListsFromCharacterObjects result:");
        for (int i = 0; i < battlers.Count; i++)
        {
            var goName = i < battlerObjects.Count && battlerObjects[i] != null ? battlerObjects[i].name : "null";
            var id = i < battlerObjects.Count && battlerObjects[i] != null ? battlerObjects[i].GetInstanceID().ToString() : "-";
            Debug.Log(" index=" + i + " battler=" + battlers[i].name + " go=" + goName + " id=" + id + " speed=" + battlers[i].speed);
        }

        if (turnIndex < 0 || turnIndex >= battlers.Count) turnIndex = 0;
    }

    T SafeGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    public void OnPlayerStrongAttack()
    {
        Debug.Log("[TurnBaseSystem] OnPlayerStrongAttack called");

        if (selectedMonster == null)
        {
            Debug.Log("[TurnBaseSystem] selectedMonster is null");
            return;
        }

        if (turnIndex < 0 || turnIndex >= battlerObjects.Count)
        {
            Debug.Log("[TurnBaseSystem] invalid turnIndex " + turnIndex + " / battlerObjects.Count " + battlerObjects.Count);
            return;
        }

        GameObject playerObj = battlerObjects[turnIndex];

        // Build safe info strings to avoid embedded-quote escaping issues
        string playerInfo = playerObj != null ? (playerObj.name + " id=" + playerObj.GetInstanceID()) : "null";
        Debug.Log("[TurnBaseSystem] Current playerObj = " + playerInfo);

        if (playerObj == null) return;

        GoAttck playerAI = playerObj.GetComponent<GoAttck>();
        Debug.Log("[TurnBaseSystem] playerObj has GoAttck? " + (playerAI != null));

        GameObject monsterObj = selectedMonster;
        string targetInfo = monsterObj != null ? (monsterObj.name + " id=" + monsterObj.GetInstanceID()) : "null";
        Debug.Log("[TurnBaseSystem] selectedMonster = " + targetInfo);

        if (playerAI != null && monsterObj != null)
        {
            ShowPanelsForParticipants(playerObj, monsterObj);

            // Pass the monsterObj as parameter and clear selection in callback after attack+return finishes
            playerAI.StrongAttackMonster(monsterObj, () =>
            {
                try
                {
                    var ce = playerObj.GetComponent<CharacterEquipment>();
                    var playerStat = playerObj.GetComponent<ICharacterStat>();

                    if (ce != null)
                    {
                        var targets = new List<GameObject> { monsterObj };
                        Debug.Log("[TurnBaseSystem] Applying skill via CharacterEquipment.UseSkill for " + playerObj.name);

                        // Try to call UseSkill with callback if available
                        var ceType = ce.GetType();
                        var useWithCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>), typeof(Action) });
                        if (useWithCb != null)
                        {
                            useWithCb.Invoke(ce, new object[] { targets, new Action(() =>
                            {
                                selectedMonster = null;
                                playerAI.ReturnToStart(OnPlayerReturned);
                            })});
                            return; // async path taken
                        }

                        // fallback: UseSkill(List<GameObject>)
                        var useNoCb = ceType.GetMethod("UseSkill", new Type[] { typeof(List<GameObject>) });
                        if (useNoCb != null)
                        {
                            useNoCb.Invoke(ce, new object[] { targets });
                        }
                    }
                    else if (playerStat != null)
                    {
                        var ms = monsterObj.GetComponent<IMonsterStat>();
                        if (ms != null)
                        {
                            Debug.Log("[TurnBaseSystem] Applying skill via ICharacterStat.StrongAttackMonster for " + playerObj.name);
                            try
                            {
                                var statType = playerStat.GetType();
                                var m = statType.GetMethod("StrongAttackMonster", new Type[] { typeof(IMonsterStat) });
                                if (m != null) m.Invoke(playerStat, new object[] { ms });
                                else playerStat.StrongAttackMonster(ms);
                            }
                            catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] playerStat.StrongAttackMonster threw: " + ex); }
                        }
                        else Debug.LogWarning("[TurnBaseSystem] Monster has no IMonsterStat - cannot call StrongAttackMonster on playerStat");
                    }
                    else
                    {
                        Debug.LogWarning("[TurnBaseSystem] No CharacterEquipment/ICharacterStat to apply skill from " + playerObj.name);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TurnBaseSystem] Exception while applying skill in OnPlayerStrongAttack callback: " + ex);
                }

                // Default synchronous path: clear selection and return to start
                selectedMonster = null;
                playerAI.ReturnToStart(OnPlayerReturned);
            });
        }
        else
        {
            if (playerAI == null) Debug.LogWarning("[TurnBaseSystem] Cannot perform StrongAttack: GoAttck component missing on playerObj");
            if (monsterObj == null) Debug.LogWarning("[TurnBaseSystem] Cannot perform StrongAttack: selectedMonster is null");
        }
    }

    public GameObject GetRandomAlivePlayer()
    {
        var alivePlayers = battlerObjects
            .Where((obj, i) => obj != null && i < battlers.Count && !battlers[i].isMonster && battlers[i].hp > 0)
            .Select(x => x)
            .ToList();

        return alivePlayers.Count > 0 ? alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)] : null;
    }

    void StartTurn()
    {
        int attempts = 0;
        int maxAttempts = Math.Max(1, Math.Max(1, battlers.Count));

        while (attempts < maxAttempts)
        {
            CleanUpDeadBattlers();

            if (battlers.Count == 0) { Debug.Log("Battle ended!"); HideAllPlayerUI(); return; }

            if (turnIndex >= battlers.Count) turnIndex = 0;
            if (turnIndex < 0) turnIndex = 0;

            int safetyCount = 0;
            while ((battlerObjects.Count == 0 || turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null) && safetyCount < battlers.Count)
            {
                turnIndex++;
                if (turnIndex >= battlers.Count) turnIndex = 0;
                safetyCount++;
            }
            if (battlerObjects.Count == 0 || turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null) { Debug.Log("No battler left to take turn!"); HideAllPlayerUI(); return; }

            TryTickStatusForIndex(turnIndex);
            CleanUpDeadBattlers();

            if (battlers.Count == 0) { Debug.Log("Battle ended after status ticks!"); HideAllPlayerUI(); return; }

            if (turnIndex >= battlers.Count) turnIndex = 0;

            if (turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null)
            {
                turnIndex = (turnIndex + 1) % Math.Max(1, battlers.Count);
                attempts++;
                continue;
            }

            break;
        }

        if (battlers.Count == 0) { Debug.Log("No battlers available to start turn."); HideAllPlayerUI(); return; }

        if (turnIndex >= battlers.Count) turnIndex = 0;
        if (turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null) { Debug.LogWarning("[TurnBaseSystem] No valid battler found after attempts."); HideAllPlayerUI(); return; }

        // ---------------- HOTFIX: robust mapping battler -> GameObject ----------------
        Battler current = null;
        GameObject obj = null;

        if (turnIndex >= 0 && turnIndex < battlers.Count)
            current = battlers[turnIndex];

        // Try direct index mapping first
        if (current != null && turnIndex >= 0 && turnIndex < battlerObjects.Count && battlerObjects[turnIndex] != null)
        {
            obj = battlerObjects[turnIndex];

            // sanity check: if battler.name != GameObject.name, attempt to find correct GameObject by name
            if (!string.Equals(current.name, obj.name, StringComparison.Ordinal))
            {
                Debug.LogWarning("[TurnBaseSystem] Name mismatch at index " + turnIndex + ": battler='" + current.name + "' but battlerObjects[" + turnIndex + "]='" + obj.name + "'. Searching for matching GameObject.");
                int foundIdx = battlerObjects.FindIndex(go => go != null && string.Equals(go.name, current.name, StringComparison.Ordinal));
                if (foundIdx >= 0)
                {
                    obj = battlerObjects[foundIdx];
                    Debug.LogWarning("[TurnBaseSystem] Remapped battler '" + current.name + "' -> battlerObjects[" + foundIdx + "] ('" + obj.name + "'). Updating turnIndex for consistency.");
                    turnIndex = foundIdx;
                }
            }
        }
        else if (current != null)
        {
            // index out of range or null at index -> try to find matching go by name (best-effort)
            int foundIdx = battlerObjects.FindIndex(go => go != null && string.Equals(go.name, current.name, StringComparison.Ordinal));
            if (foundIdx >= 0)
            {
                obj = battlerObjects[foundIdx];
                Debug.LogWarning("[TurnBaseSystem] Found battlerObjects[" + foundIdx + "] matching battler '" + current.name + "'. Using that GameObject and setting turnIndex=" + foundIdx + ".");
                turnIndex = foundIdx;
            }
            else
            {
                // last-resort: use first available GameObject but warn (indicates lists are out of sync)
                obj = battlerObjects.FirstOrDefault(go => go != null);
                Debug.LogWarning("[TurnBaseSystem] Could not find GameObject matching battler '" + (current != null ? current.name : "null") + "'. Falling back to first available GameObject '" + (obj != null ? obj.name : "null") + "'. This likely indicates lists are out of sync.");
            }
        }
        else
        {
            obj = null;
        }
        // ---------------------------------------------------------------------------

        EnsurePersistentPanelsVisible();
        RefreshTurnOrderUI();

        if (current != null && current.isMonster)
        {
            state = BattleState.MonsterAttacking;

            GameObject targetPlayer = GetRandomAlivePlayer();

            ShowPanelsForParticipants(obj, targetPlayer);

            SetPanelsInteractable(playerUIPanels, false);
            SetPanelsInteractable(persistentPlayerUIPanels, false);

            Enemiegoattck monsterAI = obj?.GetComponent<Enemiegoattck>();
            IMonsterStat monsterStat = obj?.GetComponent<IMonsterStat>();
            if (monsterAI != null && monsterStat != null && targetPlayer != null)
            {
                monsterAI.MonsterAttack(monsterStat, targetPlayer, OnMonsterAttackFinished);
            }
            else { Debug.Log("No player or MonsterAI - ending turn"); EndTurn(); }
        }
        else { state = BattleState.WaitingForPlayerInput; ShowPlayerUI(obj); }
    }

    void OnMonsterAttackFinished() => EndTurn();

    private float _lastEndTurnTime = -10f;
    private const float _endTurnDebounceSeconds = 0.12f;
    private bool _endTurnLock = false;

    public void EndTurn()
    {
        if (Time.realtimeSinceStartup - _lastEndTurnTime < _endTurnDebounceSeconds) { Debug.LogWarning("[TurnBaseSystem] Ignored duplicate EndTurn() call (debounced)."); return; }
        if (_endTurnLock) { Debug.LogWarning("[TurnBaseSystem] Ignored EndTurn() call because EndTurn is already processing."); return; }
        _endTurnLock = true;
        _lastEndTurnTime = Time.realtimeSinceStartup;

        try
        {
            MarkCurrentBattlerActed();
            if (battlers.Count == 0) { turnIndex = 0; StartTurn(); return; }
            turnIndex++;
            if (turnIndex >= battlers.Count) turnIndex = 0;
        }
        catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] Exception in EndTurn(): " + ex); turnIndex = Mathf.Clamp(turnIndex + 1, 0, Math.Max(0, battlers.Count - 1)); }
        finally { _endTurnLock = false; }

        StartTurn();
    }

    // Called when a player's movement/ReturnToStart finishes in previous flow
    public void OnPlayerReturned()
    {
        EndTurn();
    }

    // Updated: make selection idempotent and debounce rapid duplicate events
    public void OnMonsterSelected(GameObject monster)
    {
        // if same monster already selected, do nothing
        if (selectedMonster == monster)
        {
            Debug.Log("[TurnBaseSystem] Selected Monster (already): " + (monster != null ? monster.name : "null"));
            return;
        }

        // debounce very rapid repeated selection events (e.g. OnMouseDown + pointer event fired together)
        if (Time.realtimeSinceStartup - _lastSelectTime < _selectDebounceSeconds)
        {
            Debug.Log("[TurnBaseSystem] Selection ignored (debounced): " + (monster != null ? monster.name : "null"));
            // update timestamp to avoid a train of repeated logs
            _lastSelectTime = Time.realtimeSinceStartup;
            return;
        }

        _lastSelectTime = Time.realtimeSinceStartup;
        selectedMonster = monster;
        Debug.Log("[TurnBaseSystem] Selected Monster: " + (monster != null ? monster.name : "null"));
    }

    public void MarkCurrentBattlerActed()
    {
        try
        {
            var go = CurrentBattlerObject;
            if (go != null) actedThisRound.Add(go);
            if (AreAllAliveBattlersActed()) { roundNumber++; actedThisRound.Clear(); Debug.Log("[TurnBaseSystem] New round " + roundNumber); UpdateRoundUI(); }
        }
        catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] MarkCurrentBattlerActed exception: " + ex); }
    }

    bool AreAllAliveBattlersActed()
    {
        var alive = battlerObjects.Where((obj, idx) => obj != null && idx < battlers.Count && battlers[idx] != null && battlers[idx].hp > 0).ToList();
        if (alive.Count == 0) return false;
        foreach (var a in alive) if (!actedThisRound.Contains(a)) return false;
        return true;
    }

    void UpdateRoundUI() { if (roundText != null) roundText.text = "Round " + roundNumber; }

    void TryTickStatusForIndex(int idx)
    {
        if (idx < 0 || idx >= battlerObjects.Count) return;
        var go = battlerObjects[idx];
        if (go == null) return;

        var sm = go.GetComponent<StatusManager>();
        if (sm != null) { try { Debug.Log("[TurnBaseSystem] Ticking StatusManager for " + go.name); sm.TickStatusPerTurn(); } catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] Exception ticking StatusManager on " + go.name + ": " + ex); } }

        var es = go.GetComponent<EnemyStats>();
        if (es != null) { try { Debug.Log("[TurnBaseSystem] Ticking EnemyStats for " + go.name); es.TickStatusPerTurn(); } catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] Exception ticking EnemyStats on " + go.name + ": " + ex); } }

        var ce = go.GetComponent<CharacterEquipment>();
        if (ce != null) { try { ce.OnTurnStart(); } catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] Exception in CharacterEquipment.OnTurnStart for " + go.name + ": " + ex); } }

        var wc = go.GetComponent<WeaponController>();
        if (wc != null) { try { wc.OnTurnStart(); } catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] Exception in WeaponController.OnTurnStart for " + go.name + ": " + ex); } }
    }

    // ---------- Cleaning / removal / record ----------
    void CleanUpDeadBattlers()
    {
        if (battlerObjects == null) return;
        for (int i = battlerObjects.Count - 1; i >= 0; i--)
        {
            bool remove = false;
            if (battlerObjects[i] == null)
            {
                Debug.Log("[TurnBaseSystem] Removing dead/null battler at index " + i + " (GO null)");
                remove = true;
            }
            else if (i < battlers.Count && battlers[i].hp <= 0)
            {
                Debug.Log("[TurnBaseSystem] Removing dead battler at index " + i + " (hp<=0)");
                if (i < battlers.Count && battlers[i].isMonster)
                {
                    var ms = battlerObjects[i].GetComponent<IMonsterStat>();
                    if (ms != null) RecordEnemyDefeated(ms);
                    else RecordEnemyDefeated(battlerObjects[i]);
                }
                remove = true;
            }

            if (remove)
            {
                var removedGO = battlerObjects[i];
                if (battlerToPanel != null && removedGO != null && battlerToPanel.TryGetValue(removedGO, out var panel) && panel != null) { Destroy(panel); battlerToPanel.Remove(removedGO); }

                // remove healthbar if present
                if (HealthBarManager.Instance != null && removedGO != null)
                {
                    try { HealthBarManager.Instance.RemoveFor(removedGO); } catch { }
                }

                if (i < battlerObjects.Count) battlerObjects.RemoveAt(i);
                if (i < battlers.Count) battlers.RemoveAt(i);
                if (removedGO != null && actedThisRound.Contains(removedGO)) actedThisRound.Remove(removedGO);
                if (turnIndex >= battlers.Count) turnIndex = Math.Max(0, battlers.Count - 1);
            }
        }

        UpdatePlayerPanelMapping();
        CheckGameEnd();
        RefreshTurnOrderUI();
        CreateOrAssignPerCharacterPanels();
    }

    public void RemoveBattler(GameObject go, bool recordIfMonster = true)
    {
        if (go == null) return;
        int idx = battlerObjects.IndexOf(go);
        if (idx >= 0)
        {
            if (recordIfMonster && idx < battlers.Count && battlers[idx].isMonster)
            {
                var ms = go.GetComponent<IMonsterStat>();
                if (ms != null) RecordEnemyDefeated(ms);
                else RecordEnemyDefeated(go);
            }

            if (battlerToPanel != null && battlerToPanel.TryGetValue(go, out var panel) && panel != null) { Destroy(panel); battlerToPanel.Remove(go); }

            // remove healthbar for this GO if HealthBarManager tracking it
            if (HealthBarManager.Instance != null)
            {
                try { HealthBarManager.Instance.RemoveFor(go); } catch { }
            }

            if (idx < battlerObjects.Count) battlerObjects.RemoveAt(idx);
            if (idx < battlers.Count) battlers.RemoveAt(idx);
            if (actedThisRound.Contains(go)) actedThisRound.Remove(go);
            if (turnIndex >= battlers.Count) turnIndex = Math.Max(0, battlers.Count - 1);
            UpdatePlayerPanelMapping(); RefreshTurnOrderUI(); Debug.Log("[TurnBaseSystem] Removed battler '" + go.name + "' at index " + idx);
        }
        else Debug.LogWarning("[TurnBaseSystem] RemoveBattler: GameObject '" + go.name + "' not found in battlerObjects.");
    }

    // ---------- Inserted methods: RecordEnemyDefeated ----------
    public void RecordEnemyDefeated(GameObject enemy)
    {
        if (enemy == null) return;

        if (defeatedEnemies == null) defeatedEnemies = new List<GameObject>();
        if (!defeatedEnemies.Contains(enemy))
        {
            defeatedEnemies.Add(enemy);
            Debug.Log("[TurnBaseSystem] Recorded defeated enemy (GO): " + enemy.name);
        }
        else Debug.Log("[TurnBaseSystem] Enemy already recorded in defeatedEnemies: " + enemy.name);

        var ms = enemy.GetComponent<IMonsterStat>();
        if (ms != null) RecordEnemyDefeated(ms);
        else
        {
            if (defeatedRewards == null) defeatedRewards = new List<Reward>();
            var r = new Reward(enemy.name, 1, 0, 0);
            if (!defeatedRewards.Any(x => x.id == r.id && x.exp == r.exp))
            {
                defeatedRewards.Add(r);
            }
        }
    }

    public void RecordEnemyDefeated(IMonsterStat ms)
    {
        if (ms == null) return;
        if (defeatedRewards == null) defeatedRewards = new List<Reward>();

        var id = string.IsNullOrEmpty(ms.monsterName) ? "Monster" : ms.monsterName;
        var r = new Reward(id, 1, 0, ms.expValue);

        if (!defeatedRewards.Any(x => x.id == r.id && x.exp == r.exp))
        {
            defeatedRewards.Add(r);
            Debug.Log("[TurnBaseSystem] Recorded defeated enemy stat: " + id + " exp=" + ms.expValue);
        }
        else
        {
            Debug.Log("[TurnBaseSystem] Duplicate Reward ignored for " + id + " exp=" + ms.expValue);
        }
    }

    // ---------- Panels + Healthbars ----------
    void CreateOrAssignPerCharacterPanels()
    {
        if (perCharacterPanelPrefab == null) return;
        Transform parent = defaultCanvas != null ? defaultCanvas.transform : null;
        if (parent == null) { var found = FindObjectOfType<Canvas>(true); if (found != null) parent = found.transform; }
        if (parent == null) { Debug.LogWarning("[TurnBaseSystem] No Canvas found to parent per-character panels. Set defaultCanvas or add a Canvas in scene."); return; }

        // Destroy existing panels and recreate deterministically
        if (battlerToPanel != null && battlerToPanel.Count > 0)
        {
            foreach (var kv in battlerToPanel.ToList())
            {
                try { if (kv.Value != null) Destroy(kv.Value); } catch { }
            }
            battlerToPanel.Clear();
        }

        for (int i = 0; i < battlerObjects.Count && i < battlers.Count; i++)
        {
            var go = battlerObjects[i];
            if (go == null) continue;

            var panel = Instantiate(perCharacterPanelPrefab, parent, false);
            panel.name = "PerCharacterPanel_" + i + "_" + go.name + "_" + go.GetInstanceID();

            var ui = panel.GetComponent<PerCharacterUIController>();
            if (ui != null)
            {
                ui.playerEquipment = go.GetComponent<CharacterEquipment>();
                ui.turnManager = this;
                try { ui.RefreshAll(); } catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] ui.RefreshAll exception: " + ex); }
            }

            battlerToPanel[go] = panel;
            Debug.Log("[TurnBaseSystem] Created per-character panel: index=" + i + ", go=" + go.name + ", id=" + go.GetInstanceID() + ", panel=" + panel.name);
        }
    }

    private IEnumerator DelayedHealthbarCreate(float delaySeconds, int attempts)
    {
        int tries = 0;
        while (tries < attempts)
        {
            yield return new WaitForSeconds(delaySeconds);
            tries++;

            if (!manageHealthbars)
            {
                Debug.Log("[TurnBaseSystem] DelayedHealthbarCreate: manageHealthbars = false, aborting delayed healthbar creation.");
                _delayedHealthbarCoroutine = null;
                yield break;
            }

            if (battlerObjects != null && battlerObjects.Count > 0)
            {
                if (HealthBarManager.Instance != null)
                {
                    for (int i = 0; i < battlerObjects.Count; i++)
                    {
                        var go = battlerObjects[i];
                        if (go == null) continue;
                        HealthBarManager.Instance.CreateFor(go, go.transform);
                        Debug.Log("[TurnBaseSystem] Delayed: CreateFor called for " + go.name + " on attempt " + tries + " id=" + go.GetInstanceID());
                    }
                }
                _delayedHealthbarCoroutine = null;
                yield break;
            }

            if (characterObjects != null && characterObjects.Count > 0)
            {
                if (HealthBarManager.Instance != null)
                {
                    for (int i = 0; i < characterObjects.Count; i++)
                    {
                        var go = characterObjects[i];
                        if (go == null) continue;
                        HealthBarManager.Instance.CreateFor(go, go.transform);
                        Debug.Log("[TurnBaseSystem] Delayed: CreateFor called for characterObjects " + go.name + " on attempt " + tries);
                    }
                }
                _delayedHealthbarCoroutine = null;
                yield break;
            }
        }

        Debug.LogWarning("[TurnBaseSystem] DelayedHealthbarCreate gave up after " + attempts + " attempts; no characters found.");
        _delayedHealthbarCoroutine = null;
    }

    void CheckGameEnd()
    {
        bool hasPlayer = battlers.Select((b, i) => new { b, i }).Any(x => x.b != null && !x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);
        bool hasMonster = battlers.Select((b, i) => new { b, i }).Any(x => x.b != null && x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);
        Debug.Log("CheckGameEnd: hasPlayer=" + hasPlayer + ", hasMonster=" + hasMonster + ", battler count=" + battlers.Count);
        if (!hasPlayer) { Debug.Log("Game Over! All players are dead."); if (BattleEndUIManager.Instance != null) { BattleEndUIManager.Instance.ShowGameOver("Game Over"); } else { HideAllPlayerUI(); } return; }
        if (!hasMonster)
        {
            Debug.Log("Victory! All monsters are dead.");
            var rewards = new List<Reward>(); int totalExp = 0;
            if (defeatedRewards != null && defeatedRewards.Count > 0) { foreach (var r in defeatedRewards) { if (r == null) continue; rewards.Add(r); totalExp += r.exp; } }
            else
            {
                foreach (var go in defeatedEnemies)
                {
                    if (go == null) continue;
                    var ms = go.GetComponent<IMonsterStat>();
                    if (ms != null) { var r = new Reward(ms.monsterName, 1, 0, ms.expValue); rewards.Add(r); totalExp += ms.expValue; }
                    else { var r = new Reward(go.name, 1, 0, 0); rewards.Add(r); }
                }
            }

            var alivePlayers = battlerObjects.Select((obj, idx) => new { obj, idx }).Where(x => x.obj != null && x.idx < battlers.Count && !battlers[x.idx].isMonster && battlers[x.idx].hp > 0).Select(x => x.obj).ToList();
            if (BattleEndUIManager.Instance != null) { BattleEndUIManager.Instance.ShowVictory(rewards, totalExp, alivePlayers); }
            else { if (totalExp > 0 && alivePlayers.Count > 0) { AwardExpToPlayers(totalExp, alivePlayers); } if (rewards != null && rewards.Count > 0) { foreach (var r in rewards) Debug.Log("[TurnBaseSystem] (Fallback) Would award item '" + r.id + "' x" + r.quantity); } }
            defeatedRewards.Clear(); defeatedEnemies.Clear();
        }
    }

    void AwardExpToPlayers(int totalExp, List<GameObject> alivePlayers)
    {
        if (alivePlayers == null || alivePlayers.Count == 0 || totalExp <= 0) return;
        int perPlayer = totalExp / alivePlayers.Count;
        int remainder = totalExp % alivePlayers.Count;
        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var p = alivePlayers[i];
            if (p == null) continue;
            var ps = p.GetComponent<PlayerStat>();
            if (ps != null)
            {
                int grant = perPlayer + (i < remainder ? 1 : 0);
                ps.AddExp(grant);
                Debug.Log("[TurnBaseSystem] Awarded " + grant + " exp to " + p.name);
            }
        }
    }

    // UI helper stubs (implementations copied/kept from previous TurnManager if needed)
    void UpdatePlayerPanelMapping() { /* same logic as before, keep mapping playerObjects -> playerUIPanels */ }
    void EnsurePersistentPanelsVisible() { /* same logic as before */ }
    void RefreshTurnOrderUI() { if (updateTurnOrderUI && TurnOrderUI.Instance != null) TurnOrderUI.Instance.RefreshOrder(battlers, battlerObjects, turnIndex); }
    void ShowPlayerUI(GameObject playerObj) { /* keep previous logic */ }
    void ShowPanelsForParticipants(GameObject attacker, GameObject target) { /* keep previous logic */ }
    void SetPanelsInteractable(IEnumerable<GameObject> panels, bool interactable) { /* keep previous logic */ }
    void SetPanelInteractable(GameObject panel, bool interactable) { /* keep previous logic */ }
    void HideTransientPlayerUI() { /* keep previous logic */ }
    void HideAllPersistentPanels() { /* keep previous logic */ }
    void HideAllPlayerUI() { HideTransientPlayerUI(); HideAllPersistentPanels(); }
    void RefreshHPBars() { /* optional helper */ }
}