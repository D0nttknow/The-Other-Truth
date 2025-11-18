using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurnManager (ปรับปรุง)
/// - Tick status effects (เช่น Bleed) ก่อน battler แต่ละตัวทำแอคชัน
/// - ถ้า status ทำให้ตาย จะ CleanUp และข้ามตัวนั้น
/// - เก็บ set ของ battlers ที่ทำแอคชันในรอบปัจจุบัน; เมื่อทั้งหมดทำครบ => เพิ่ม round และแสดงบน UI
/// - ปรับปรุงการป้องกัน null / index-out-of-range และ logging
/// - เพิ่ม support สร้าง per-character UI panels อัตโนมัติและ expose CurrentBattlerObject / IsCurrentTurn
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Header("UI References")]
    [Tooltip("Optional: assign the CharacterInfoPanel here. If left empty, TurnManager will try to FindObjectOfType<CharacterInfoPanel>() in Start.")]
    public CharacterInfoPanel characterInfoPanel;

    [Header("Round UI")]
    [Tooltip("Optional UI Text to show current round number on screen")]
    public Text roundText;
    [HideInInspector] public int roundNumber = 1;

    // เลือก Monster เป้าหมาย
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
    [Tooltip("ลาก Canvas หลักของ UI (Canvas) ที่ต้องการให้ persistent panels เป็นลูกของมัน เพื่อป้องกันการถูกปิดโดยพาเรนท์ชั่วคราว")]
    public Canvas defaultCanvas;

    [Header("Behavior")]
    [Tooltip("ถ้าเปิด จะซ่อน persistentPlayerUIPanels ของผู้เล่นที่ไม่ได้เกี่ยวข้อง (แสดงเฉพาะ attacker/target) ระหว่างเหตุการณ์โจมตี")]
    public bool filterPersistentToParticipants = false;

    private Dictionary<GameObject, GameObject> playerToPanel = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, GameObject> persistentPlayerToPanel = new Dictionary<GameObject, GameObject>();

    [Header("Turn Order")]
    [Tooltip("ถ้าเปิด จะให้ TurnManager เรียก TurnOrderUI เพื่ออัพเดตลำดับเทิร์นเมื่อจำเป็น")]
    public bool updateTurnOrderUI = true;

    // NEW: เก็บ Reward info (ไม่พึ่ง GameObject ที่อาจถูก Destroy แล้ว)
    [HideInInspector] public List<Reward> defeatedRewards = new List<Reward>();
    [HideInInspector] public List<GameObject> defeatedEnemies = new List<GameObject>(); // backward compat

    // Track which battlers have acted in the current round (use GameObject identity)
    private HashSet<GameObject> actedThisRound = new HashSet<GameObject>();

    // PER-CHARACTER UI support
    [Header("Per-character UI")]
    [Tooltip("Optional prefab for per-character action panel. If set, TurnManager will instantiate one per battler and assign it.")]
    public GameObject perCharacterPanelPrefab;
    private Dictionary<GameObject, GameObject> battlerToPanel = new Dictionary<GameObject, GameObject>();

    // Public helpers ---------------------------------------------------------
    /// <summary>
    /// Returns the GameObject whose turn it currently is (may be null)
    /// </summary>
    public GameObject CurrentBattlerObject
    {
        get
        {
            if (turnIndex >= 0 && turnIndex < battlerObjects.Count)
                return battlerObjects[turnIndex];
            return null;
        }
    }

    /// <summary>
    /// Returns true if the given GameObject is the one whose turn it currently is.
    /// Safe to call from UI/input code.
    /// </summary>
    public bool IsCurrentTurn(GameObject go)
    {
        if (go == null) return false;
        return CurrentBattlerObject == go;
    }

    // -----------------------------------------------------------------------

    void Start()
    {
        if (characterObjects == null || characterObjects.Count == 0)
            Debug.LogWarning("[TurnManager] characterObjects is null or empty at Start. Make sure to populate it in the Inspector or before Start.");

        BuildBattlerListsFromCharacterObjects();

        if (defaultCanvas == null)
        {
            defaultCanvas = FindObjectOfType<Canvas>(true);
            if (defaultCanvas != null) Debug.Log($"[TurnManager] defaultCanvas auto-assigned to '{defaultCanvas.name}'");
        }

        if (characterInfoPanel == null)
        {
            characterInfoPanel = FindObjectOfType<CharacterInfoPanel>();
            if (characterInfoPanel != null) Debug.Log($"[TurnManager] characterInfoPanel auto-assigned to '{characterInfoPanel.gameObject.name}'");
        }

        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.CreateForAllFromTurnManager();
            Debug.Log("[TurnManager] Requested HealthBarManager to CreateForAllFromTurnManager()");
        }

        UpdatePlayerPanelMapping();
        EnsurePersistentPanelsVisible();
        RefreshTurnOrderUI();
        UpdateRoundUI();

        // create per-character panels if prefab provided
        CreateOrAssignPerCharacterPanels();

        StartTurn();
    }

    /// <summary>
    /// Build battlers & battlerObjects from characterObjects in a safe way.
    /// </summary>
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

            Debug.LogWarning($"[TurnManager] GameObject '{go.name}' has no ICharacterStat or IMonsterStat - skipped when building turn order.");
        }

        // order by speed descending
        pairList = pairList.OrderByDescending(p => p.battler.speed).ToList();

        foreach (var p in pairList)
        {
            battlers.Add(p.battler);
            battlerObjects.Add(p.go);
        }

        if (turnIndex < 0 || turnIndex >= battlers.Count) turnIndex = 0;
    }

    // generic safe accessor helper
    T SafeGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch
        {
            return fallback;
        }
    }

    public void OnPlayerStrongAttack()
    {
        if (selectedMonster == null) return;
        if (turnIndex < 0 || turnIndex >= battlerObjects.Count) return;

        GameObject playerObj = battlerObjects[turnIndex];
        if (playerObj == null) return;

        GoAttck playerAI = playerObj.GetComponent<GoAttck>();
        GameObject monsterObj = selectedMonster;
        if (playerAI != null && monsterObj != null)
        {
            ShowPanelsForParticipants(playerObj, monsterObj);
            playerAI.StrongAttackMonster(monsterObj, () => playerAI.ReturnToStart(OnPlayerReturned));
            selectedMonster = null;
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
        // Use loop to find next valid battler and process ticks; avoid recursion
        int attempts = 0;
        int maxAttempts = Math.Max(1, Math.Max(1, battlers.Count));

        while (attempts < maxAttempts)
        {
            // cleanup before starting; this may remove dead battlers and adjust lists
            CleanUpDeadBattlers();

            if (battlers.Count == 0)
            {
                Debug.Log("Battle ended!");
                HideAllPlayerUI();
                return;
            }

            if (turnIndex >= battlers.Count) turnIndex = 0;
            if (turnIndex < 0) turnIndex = 0;

            // find next valid battler index
            int safetyCount = 0;
            while ((battlerObjects.Count == 0 || turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null) && safetyCount < battlers.Count)
            {
                turnIndex++;
                if (turnIndex >= battlers.Count) turnIndex = 0;
                safetyCount++;
            }
            if (battlerObjects.Count == 0 || turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null)
            {
                Debug.Log("No battler left to take turn!");
                HideAllPlayerUI();
                return;
            }

            // Tick status effects for the battler who is about to act
            TryTickStatusForIndex(turnIndex);

            // After ticking, a battler might die => clean up and try next
            CleanUpDeadBattlers();

            // Re-check validity after possible removals
            if (battlers.Count == 0)
            {
                Debug.Log("Battle ended after status ticks!");
                HideAllPlayerUI();
                return;
            }

            if (turnIndex >= battlers.Count) turnIndex = 0;

            // if current battler is invalid (null GO) skip to next
            if (turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null)
            {
                turnIndex = (turnIndex + 1) % Math.Max(1, battlers.Count);
                attempts++;
                continue;
            }

            // Found valid battler to act — break and process below
            break;
        }

        // Final validation
        if (battlers.Count == 0)
        {
            Debug.Log("No battlers available to start turn.");
            HideAllPlayerUI();
            return;
        }

        if (turnIndex >= battlers.Count) turnIndex = 0;
        if (turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null)
        {
            Debug.LogWarning("[TurnManager] No valid battler found after attempts.");
            HideAllPlayerUI();
            return;
        }

        Battler current = (turnIndex < battlers.Count) ? battlers[turnIndex] : null;
        GameObject obj = (turnIndex < battlerObjects.Count) ? battlerObjects[turnIndex] : null;

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
            else
            {
                Debug.Log("ไม่มีผู้เล่นให้โจมตี หรือไม่มี MonsterAI");
                EndTurn();
            }
        }
        else
        {
            state = BattleState.WaitingForPlayerInput;
            ShowPlayerUI(obj);
        }
    }

    void OnMonsterAttackFinished() => EndTurn();

    // RUNTIME guards for EndTurn to avoid double-calls that skip battlers
    private float _lastEndTurnTime = -10f;
    private const float _endTurnDebounceSeconds = 0.12f;
    private bool _endTurnLock = false;

    public void EndTurn()
    {
        // Debounce / guard against rapid duplicate calls that cause skipping of turns
        if (Time.realtimeSinceStartup - _lastEndTurnTime < _endTurnDebounceSeconds)
        {
            Debug.LogWarning("[TurnManager] Ignored duplicate EndTurn() call (debounced).");
            return;
        }

        if (_endTurnLock)
        {
            Debug.LogWarning("[TurnManager] Ignored EndTurn() call because EndTurn is already processing.");
            return;
        }

        _endTurnLock = true;
        _lastEndTurnTime = Time.realtimeSinceStartup;

        try
        {
            // Mark current battler as having acted in this round
            MarkCurrentBattlerActed();

            // advance index safely
            if (battlers.Count == 0) { turnIndex = 0; StartTurn(); return; }
            turnIndex++;
            if (turnIndex >= battlers.Count) turnIndex = 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TurnManager] Exception in EndTurn(): {ex}");
            // try to move to next turn safely even if something bad happened
            turnIndex = Mathf.Clamp(turnIndex + 1, 0, Math.Max(0, battlers.Count - 1));
        }
        finally
        {
            _endTurnLock = false;
        }

        // Start the next turn
        StartTurn();
    }

    /// <summary>
    /// Mark the battler at current turnIndex as having acted in this round.
    /// If all alive battlers have acted, increment roundNumber, clear set and update UI.
    /// </summary>
    void MarkCurrentBattlerActed()
    {
        try
        {
            var go = CurrentBattlerObject;
            if (go != null)
            {
                actedThisRound.Add(go);
            }

            if (AreAllAliveBattlersActed())
            {
                roundNumber++;
                actedThisRound.Clear();
                Debug.Log($"[TurnManager] New round {roundNumber}");
                UpdateRoundUI();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TurnManager] MarkCurrentBattlerActed exception: {ex}");
        }
    }

    bool AreAllAliveBattlersActed()
    {
        var alive = battlerObjects
            .Where((obj, idx) => obj != null && idx < battlers.Count && battlers[idx] != null && battlers[idx].hp > 0)
            .ToList();

        if (alive.Count == 0) return false;

        foreach (var a in alive)
        {
            if (!actedThisRound.Contains(a)) return false;
        }
        return true;
    }

    void UpdateRoundUI()
    {
        if (roundText != null)
        {
            roundText.text = $"Round {roundNumber}";
        }
    }

    /// <summary>
    /// Try to tick status for the battler at index (if it has EnemyStats component or StatusManager).
    /// Also call per-turn hooks on CharacterEquipment/WeaponController to decrement cooldowns.
    /// </summary>
    void TryTickStatusForIndex(int idx)
    {
        if (idx < 0 || idx >= battlerObjects.Count) return;
        var go = battlerObjects[idx];
        if (go == null) return;

        // Prefer StatusManager if present
        var sm = go.GetComponent<StatusManager>();
        if (sm != null)
        {
            try
            {
                Debug.Log($"[TurnManager] Ticking StatusManager for {go.name}");
                sm.TickStatusPerTurn();

                // If status manager exposes per-effect logs, it should log Bleed like:
                // Debug.Log($"[StatusManager] Bleed tick on {go.name}: -{damage} HP (remaining {currentHp})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TurnManager] Exception ticking StatusManager on {go.name}: {ex}");
            }
        }

        // Backwards-compatible: EnemyStats may have TickStatusPerTurn
        var es = go.GetComponent<EnemyStats>();
        if (es != null)
        {
            try
            {
                Debug.Log($"[TurnManager] Ticking EnemyStats for {go.name}");
                es.TickStatusPerTurn();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TurnManager] Exception ticking EnemyStats on {go.name}: {ex}");
            }
        }

        // CharacterEquipment may want to tick weapon cooldowns
        var ce = go.GetComponent<CharacterEquipment>();
        if (ce != null)
        {
            try
            {
                ce.OnTurnStart();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TurnManager] Exception in CharacterEquipment.OnTurnStart for {go.name}: {ex}");
            }
        }

        // If there's a WeaponController directly on this GameObject, tick it too (rare)
        var wc = go.GetComponent<WeaponController>();
        if (wc != null)
        {
            try
            {
                wc.OnTurnStart();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TurnManager] Exception in WeaponController.OnTurnStart for {go.name}: {ex}");
            }
        }
    }

    /// <summary>
    /// Centralized removal helper.
    /// If the GameObject is part of battlerObjects, remove the entry and optionally record reward if monster.
    /// Also removes per-character UI panel if present.
    /// </summary>
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

            // remove associated per-character panel if present
            if (battlerToPanel != null && battlerToPanel.TryGetValue(go, out var panel) && panel != null)
            {
                Destroy(panel);
                battlerToPanel.Remove(go);
            }

            // remove safely
            if (idx < battlerObjects.Count) battlerObjects.RemoveAt(idx);
            if (idx < battlers.Count) battlers.RemoveAt(idx);

            // ensure acted set doesn't keep destroyed entries
            if (actedThisRound.Contains(go)) actedThisRound.Remove(go);

            if (turnIndex >= battlers.Count) turnIndex = Mathf.Max(0, battlers.Count - 1);
            UpdatePlayerPanelMapping();
            RefreshTurnOrderUI();
            Debug.Log($"[TurnManager] Removed battler '{go.name}' at index {idx}");
        }
        else
        {
            Debug.LogWarning($"[TurnManager] RemoveBattler: GameObject '{go.name}' not found in battlerObjects.");
        }
    }

    void CleanUpDeadBattlers()
    {
        if (battlerObjects == null) return;

        for (int i = battlerObjects.Count - 1; i >= 0; i--)
        {
            bool remove = false;
            if (battlerObjects[i] == null)
            {
                Debug.Log($"[TurnManager] Removing dead/null battler at index {i} (GO null)");
                remove = true;
            }
            else if (i < battlers.Count && battlers[i].hp <= 0)
            {
                Debug.Log($"[TurnManager] Removing dead battler at index {i} (hp<=0)");
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

                // destroy per-character panel if exists
                if (battlerToPanel != null && removedGO != null && battlerToPanel.TryGetValue(removedGO, out var panel) && panel != null)
                {
                    Destroy(panel);
                    battlerToPanel.Remove(removedGO);
                }

                if (i < battlerObjects.Count) battlerObjects.RemoveAt(i);
                if (i < battlers.Count) battlers.RemoveAt(i);

                // remove from acted set if present
                if (removedGO != null && actedThisRound.Contains(removedGO)) actedThisRound.Remove(removedGO);

                if (turnIndex >= battlers.Count) turnIndex = Mathf.Max(0, battlers.Count - 1);
            }
        }

        UpdatePlayerPanelMapping();
        CheckGameEnd();
        RefreshTurnOrderUI();

        // ensure panels mapping is in sync (recreate if necessary)
        CreateOrAssignPerCharacterPanels();
    }

    // --- Per-character panel creation / mapping ---
    void CreateOrAssignPerCharacterPanels()
    {
        // If no prefab set, skip
        if (perCharacterPanelPrefab == null) return;

        // Ensure we have a parent canvas to place panels under
        Transform parent = defaultCanvas != null ? defaultCanvas.transform : null;
        if (parent == null)
        {
            var found = FindObjectOfType<Canvas>(true);
            if (found != null) parent = found.transform;
        }
        if (parent == null)
        {
            Debug.LogWarning("[TurnManager] No Canvas found to parent per-character panels. Set defaultCanvas or add a Canvas in scene.");
            return;
        }

        // Remove any panels for battlers no longer present
        var existingKeys = battlerToPanel.Keys.ToList();
        foreach (var key in existingKeys)
        {
            if (!battlerObjects.Contains(key))
            {
                if (battlerToPanel.TryGetValue(key, out var oldP) && oldP != null) Destroy(oldP);
                battlerToPanel.Remove(key);
            }
        }

        // Create panels for battlers missing panels
        for (int i = 0; i < battlerObjects.Count && i < battlers.Count; i++)
        {
            var go = battlerObjects[i];
            if (go == null) continue;
            if (battlerToPanel.ContainsKey(go)) continue;

            var panel = Instantiate(perCharacterPanelPrefab, parent, false);
            // try to find PerCharacterUIController and assign
            var ui = panel.GetComponent<PerCharacterUIController>();
            if (ui != null)
            {
                ui.playerEquipment = go.GetComponent<CharacterEquipment>();
                ui.turnManager = this;
                try { ui.RefreshAll(); } catch { }
            }
            battlerToPanel[go] = panel;
        }
    }

    // --- Game end handling ---
    void CheckGameEnd()
    {
        bool hasPlayer = battlers.Select((b, i) => new { b, i })
            .Any(x => x.b != null && !x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);

        bool hasMonster = battlers.Select((b, i) => new { b, i })
            .Any(x => x.b != null && x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);

        Debug.Log($"CheckGameEnd: hasPlayer={hasPlayer}, hasMonster={hasMonster}, battler count={battlers.Count}");

        if (!hasPlayer)
        {
            Debug.Log("Game Over! All players are dead.");
            if (BattleEndUIManager.Instance != null)
            {
                BattleEndUIManager.Instance.ShowGameOver("Game Over");
            }
            else HideAllPlayerUI();

            return;
        }

        if (!hasMonster)
        {
            Debug.Log("Victory! All monsters are dead.");

            var rewards = new List<Reward>();
            int totalExp = 0;

            if (defeatedRewards != null && defeatedRewards.Count > 0)
            {
                foreach (var r in defeatedRewards)
                {
                    if (r == null) continue;
                    rewards.Add(r);
                    totalExp += r.exp;
                }
            }
            else
            {
                foreach (var go in defeatedEnemies)
                {
                    if (go == null) continue;
                    var ms = go.GetComponent<IMonsterStat>();
                    if (ms != null)
                    {
                        var r = new Reward(ms.monsterName, 1, 0, ms.expValue);
                        rewards.Add(r);
                        totalExp += ms.expValue;
                    }
                    else
                    {
                        var r = new Reward(go.name, 1, 0, 0);
                        rewards.Add(r);
                    }
                }
            }

            var alivePlayers = battlerObjects
                .Select((obj, idx) => new { obj, idx })
                .Where(x => x.obj != null && x.idx < battlers.Count && !battlers[x.idx].isMonster && battlers[x.idx].hp > 0)
                .Select(x => x.obj)
                .ToList();

            if (BattleEndUIManager.Instance != null)
            {
                BattleEndUIManager.Instance.ShowVictory(rewards, totalExp, alivePlayers);
            }
            else
            {
                if (totalExp > 0 && alivePlayers.Count > 0)
                {
                    AwardExpToPlayers(totalExp, alivePlayers);
                }

                if (rewards != null && rewards.Count > 0)
                {
                    foreach (var r in rewards) Debug.Log($"[TurnManager] (Fallback) Would award item '{r.id}' x{r.quantity}");
                }
            }

            // clear recorded defeated data after awarding
            defeatedRewards.Clear();
            defeatedEnemies.Clear();
        }
    }

    /// <summary>
    /// Distribute totalExp fairly among players, preserving the total (distribute remainder).
    /// </summary>
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
                Debug.Log($"[TurnManager] Awarded {grant} exp to {p.name}");
            }
        }
    }
}