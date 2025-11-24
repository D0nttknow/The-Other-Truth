using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurnManager (ปรับปรุง)
/// - Complete implementation, public API methods available for other scripts.
/// - Includes: loot drops, awarding EXP (prefer PlayerLevel), weapon OnTurnEnd tick, per-character UI panel handling.
/// - Make sure to assign poolOfConsumables and InventoryManager in the scene.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Header("Loot / Consumables (assign in Inspector)")]
    [Tooltip("Assign consumable ItemBase assets (ConsumableHeal/Atk/Def). TurnManager will drop 3 random items from this pool when a monster dies.")]
    public List<ItemBase> poolOfConsumables = new List<ItemBase>();

    [Header("UI References")]
    [Tooltip("Optional: assign the CharacterInfoPanel here. If left empty, TurnManager will try to FindObjectOfType<CharacterInfoPanel>() in Start.")]
    public CharacterInfoPanel characterInfoPanel;

    [Header("Round UI")]
    [Tooltip("Optional UI Text to show current round number on screen")]
    public Text roundText;
    [HideInInspector] public int roundNumber = 1;

    // selected target
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
    [Tooltip("Main Canvas to parent persistent player panels under")]
    public Canvas defaultCanvas;

    [Header("Behavior")]
    [Tooltip("If enabled, show persistent panels only for participants during attacks")]
    public bool filterPersistentToParticipants = false;

    private Dictionary<GameObject, GameObject> playerToPanel = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, GameObject> persistentPlayerToPanel = new Dictionary<GameObject, GameObject>();

    [Header("Turn Order")]
    [Tooltip("If true, TurnManager will call TurnOrderUI to refresh")]
    public bool updateTurnOrderUI = true;

    [HideInInspector] public List<Reward> defeatedRewards = new List<Reward>();
    [HideInInspector] public List<GameObject> defeatedEnemies = new List<GameObject>();

    private HashSet<GameObject> actedThisRound = new HashSet<GameObject>();

    [Header("Per-character UI")]
    [Tooltip("Optional prefab for per-character action panel.")]
    public GameObject perCharacterPanelPrefab;
    private Dictionary<GameObject, GameObject> battlerToPanel = new Dictionary<GameObject, GameObject>();

    // Public helper properties
    public GameObject CurrentBattlerObject
    {
        get
        {
            if (turnIndex >= 0 && turnIndex < battlerObjects.Count) return battlerObjects[turnIndex];
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

        CreateOrAssignPerCharacterPanels();

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

                var wh = go.GetComponent<WeaponHandler>();
                if (wh != null && wh.CurrentSpeedModPercent != 0f)
                    spd = Mathf.RoundToInt(spd * (1f + wh.CurrentSpeedModPercent / 100f));

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

        pairList = pairList.OrderByDescending(p => p.battler.speed).ToList();
        foreach (var p in pairList)
        {
            battlers.Add(p.battler);
            battlerObjects.Add(p.go);
        }

        if (turnIndex < 0 || turnIndex >= battlers.Count) turnIndex = 0;
    }

    T SafeGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    public void StartTurn()
    {
        // loop to find next valid battler
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

            if (battlerObjects.Count == 0 || turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null)
            {
                Debug.Log("No battler left to take turn!");
                HideAllPlayerUI();
                return;
            }

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
        if (turnIndex >= battlerObjects.Count || battlerObjects[turnIndex] == null) { Debug.LogWarning("[TurnManager] No valid battler found after attempts."); HideAllPlayerUI(); return; }

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
                Debug.Log("No player to attack or no MonsterAI");
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

    public void OnPlayerAction()
    {
        if (state != BattleState.WaitingForPlayerInput) return;
        CleanUpDeadBattlers();
        if (turnIndex < 0 || turnIndex >= battlers.Count) { Debug.LogError("turnIndex out of range!"); return; }

        Battler current = battlers[turnIndex];
        if (current != null && current.isMonster) { Debug.LogError("OnPlayerAction called during monster turn!"); return; }

        GameObject playerObj = (turnIndex < battlerObjects.Count) ? battlerObjects[turnIndex] : null;
        if (playerObj == null) { Debug.LogError("playerObj is null!"); EndTurn(); return; }

        GoAttck playerAI = playerObj.GetComponent<GoAttck>();
        if (playerAI == null) { Debug.LogError("GameObject has no GoAttck!"); EndTurn(); return; }

        if (selectedMonster == null) { Debug.LogWarning("Please select a monster before attacking!"); return; }

        GameObject monsterObj = selectedMonster;
        if (playerAI != null && monsterObj != null)
        {
            ShowPanelsForParticipants(playerObj, monsterObj);
            SetPanelsInteractable(playerUIPanels, false);
            if (playerToPanel.ContainsKey(playerObj)) SetPanelInteractable(playerToPanel[playerObj], true);
            if (playerToPanel.ContainsKey(monsterObj)) SetPanelInteractable(playerToPanel[monsterObj], true);

            playerAI.AttackMonster(monsterObj, () => playerAI.ReturnToStart(OnPlayerReturned));
            selectedMonster = null;
        }
    }

    public void OnMonsterSelected(GameObject monsterObj) { selectedMonster = monsterObj; Debug.Log("Selected Monster: " + (monsterObj ? monsterObj.name : "null")); }

    public void OnPlayerAttackSelectedMonster()
    {
        if (selectedMonster == null) return;
        if (turnIndex < 0 || turnIndex >= battlerObjects.Count) return;
        GameObject playerObj = battlerObjects[turnIndex];
        GoAttck playerAI = playerObj?.GetComponent<GoAttck>();
        GameObject monsterObj = selectedMonster;
        if (playerAI != null && monsterObj != null)
        {
            ShowPanelsForParticipants(playerObj, monsterObj);
            SetPanelsInteractable(playerUIPanels, false);
            if (playerToPanel.ContainsKey(playerObj)) SetPanelInteractable(playerToPanel[playerObj], true);
            if (playerToPanel.ContainsKey(monsterObj)) SetPanelInteractable(playerToPanel[monsterObj], true);

            playerAI.AttackMonster(monsterObj, () => playerAI.ReturnToStart(OnPlayerReturned));
            selectedMonster = null;
        }
    }

    public void OnPlayerEndTurn()
    {
        state = BattleState.PlayerReturning;
        if (turnIndex < 0 || turnIndex >= battlerObjects.Count) { EndTurn(); return; }
        GameObject playerObj = battlerObjects[turnIndex];
        if (playerObj == null) { EndTurn(); return; }
        GoAttck playerAI = playerObj.GetComponent<GoAttck>();
        if (playerAI != null) playerAI.ReturnToStart(OnPlayerReturned);
        else EndTurn();
    }

    void OnPlayerReturned() { state = BattleState.WaitingForMonsterTurn; EndTurn(); }

    public void EndTurn()
    {
        // Notify weapon handler to tick down duration for the battler that just acted
        try
        {
            var currentGo = CurrentBattlerObject;
            if (currentGo != null)
            {
                var wh = currentGo.GetComponent<WeaponHandler>();
                if (wh != null)
                {
                    wh.OnTurnEnd();
                    Debug.Log($"[TurnManager] WeaponHandler.OnTurnEnd called for {currentGo.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurnManager] WeaponHandler.OnTurnEnd threw: " + ex);
        }

        // Mark current battler as having acted in this round
        MarkCurrentBattlerActed();

        // advance index safely
        if (battlers.Count == 0) { turnIndex = 0; StartTurn(); return; }
        turnIndex++;
        if (turnIndex >= battlers.Count) turnIndex = 0;
        StartTurn();
    }

    void MarkCurrentBattlerActed()
    {
        try
        {
            var go = CurrentBattlerObject;
            if (go != null) actedThisRound.Add(go);

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
        var alive = battlerObjects.Where((obj, idx) => obj != null && idx < battlers.Count && battlers[idx] != null && battlers[idx].hp > 0).ToList();
        if (alive.Count == 0) return false;
        foreach (var a in alive) if (!actedThisRound.Contains(a)) return false;
        return true;
    }

    void UpdateRoundUI()
    {
        if (roundText != null) roundText.text = $"Round {roundNumber}";
    }

    // ----------------------------
    // Public API used by other systems
    // ----------------------------

    // Expose EnsurePersistentPanelsVisible publicly (other scripts may call)
    public void EnsurePersistentPanelsVisiblePublic() => EnsurePersistentPanelsVisible();

    public void EnsurePersistentPanelsVisible()
    {
        if (persistentPlayerUIPanels == null) return;
        if (defaultCanvas == null)
        {
            var found = FindObjectOfType<Canvas>(true);
            if (found != null) defaultCanvas = found;
        }

        foreach (var p in persistentPlayerUIPanels)
        {
            if (p == null) continue;

            bool parentIsTransient = false;
            if (p.transform.parent != null && playerUIPanels != null)
            {
                foreach (var tp in playerUIPanels)
                {
                    if (tp != null && p.transform.IsChildOf(tp.transform)) { parentIsTransient = true; break; }
                }
            }

            if (defaultCanvas != null && (p.transform.parent != defaultCanvas.transform || parentIsTransient))
            {
                p.transform.SetParent(defaultCanvas.transform, false);
                Debug.Log($"[TurnManager] Reparented persistent panel '{p.name}' under canvas '{defaultCanvas.name}' to keep it visible.");
            }

            if (!p.activeSelf) { p.SetActive(true); Debug.Log($"[TurnManager] Activated persistent panel '{p.name}'."); }

            var cg = GetOrAddCanvasGroup(p);
            if (cg != null && cg.alpha == 0f)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
    }

    public void RefreshTurnOrderUI()
    {
        if (!updateTurnOrderUI) return;
        if (TurnOrderUI.Instance != null) TurnOrderUI.Instance.RefreshOrder(battlers, battlerObjects, turnIndex);
    }

    // Public wrapper for TryTickStatusForIndex
    public void TryTickStatusForIndexPublic(int idx) => TryTickStatusForIndex(idx);

    void TryTickStatusForIndex(int idx)
    {
        if (idx < 0 || idx >= battlerObjects.Count) return;
        var go = battlerObjects[idx];
        if (go == null) return;

        var sm = go.GetComponent<StatusManager>();
        if (sm != null)
        {
            try { Debug.Log($"[TurnManager] Ticking StatusManager for {go.name}"); sm.TickStatusPerTurn(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnManager] Exception ticking StatusManager on {go.name}: {ex}"); }
        }

        var es = go.GetComponent<EnemyStats>();
        if (es != null)
        {
            try { Debug.Log($"[TurnManager] Ticking EnemyStats for {go.name}"); es.TickStatusPerTurn(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnManager] Exception ticking EnemyStats on {go.name}: {ex}"); }
        }

        var ce = go.GetComponent<CharacterEquipment>();
        if (ce != null)
        {
            try { ce.OnTurnStart(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnManager] Exception in CharacterEquipment.OnTurnStart for {go.name}: {ex}"); }
        }

        var wc = go.GetComponent<WeaponController>();
        if (wc != null)
        {
            try { wc.OnTurnStart(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnManager] Exception in WeaponController.OnTurnStart for {go.name}: {ex}"); }
        }
    }

    // Centralized removal helper
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

                // Drop loot into inventory when a monster dies (immediate)
                try
                {
                    if (poolOfConsumables != null && poolOfConsumables.Count > 0 && InventoryManager.Instance != null)
                    {
                        var drops = LootGenerator.GenerateDrops(poolOfConsumables, 3);
                        foreach (var item in drops)
                        {
                            if (item != null)
                            {
                                InventoryManager.Instance.AddItem(item);
                                Debug.Log($"[TurnManager] Loot drop: added '{item.displayName}' to Inventory");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("[TurnManager] No poolOfConsumables assigned or InventoryManager missing - skipping loot drops.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[TurnManager] Exception while generating/adding loot: " + ex);
                }
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

    // Clean up dead battlers and remove them from lists
    public void CleanUpDeadBattlers()
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

                if (battlerToPanel != null && removedGO != null && battlerToPanel.TryGetValue(removedGO, out var panel) && panel != null)
                {
                    Destroy(panel);
                    battlerToPanel.Remove(removedGO);
                }

                if (i < battlerObjects.Count) battlerObjects.RemoveAt(i);
                if (i < battlers.Count) battlers.RemoveAt(i);

                if (removedGO != null && actedThisRound.Contains(removedGO)) actedThisRound.Remove(removedGO);

                if (turnIndex >= battlers.Count) turnIndex = Mathf.Max(0, battlers.Count - 1);
            }
        }

        UpdatePlayerPanelMapping();
        CheckGameEnd();
        RefreshTurnOrderUI();
        CreateOrAssignPerCharacterPanels();
    }

    // Per-character panel creation / mapping
    public void CreateOrAssignPerCharacterPanels()
    {
        if (perCharacterPanelPrefab == null) return;

        Transform parent = defaultCanvas != null ? defaultCanvas.transform : null;
        if (parent == null)
        {
            var found = FindObjectOfType<Canvas>(true);
            if (found != null) parent = found.transform;
        }
        if (parent == null) { Debug.LogWarning("[TurnManager] No Canvas found to parent per-character panels."); return; }

        var existingKeys = battlerToPanel.Keys.ToList();
        foreach (var key in existingKeys)
        {
            if (!battlerObjects.Contains(key))
            {
                if (battlerToPanel.TryGetValue(key, out var oldP) && oldP != null) Destroy(oldP);
                battlerToPanel.Remove(key);
            }
        }

        for (int i = 0; i < battlerObjects.Count && i < battlers.Count; i++)
        {
            var go = battlerObjects[i];
            if (go == null) continue;
            if (battlerToPanel.ContainsKey(go)) continue;

            var panel = Instantiate(perCharacterPanelPrefab, parent, false);
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

    // Check game end and award exp / items
    public void CheckGameEnd()
    {
        bool hasPlayer = battlers.Select((b, i) => new { b, i })
            .Any(x => x.b != null && !x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);

        bool hasMonster = battlers.Select((b, i) => new { b, i })
            .Any(x => x.b != null && x.b.isMonster && x.b.hp > 0 && x.i < battlerObjects.Count && battlerObjects[x.i] != null);

        Debug.Log($"CheckGameEnd: hasPlayer={hasPlayer}, hasMonster={hasMonster}, battler count={battlers.Count}");

        if (!hasPlayer)
        {
            Debug.Log("Game Over! All players are dead.");
            if (BattleEndUIManager.Instance != null) BattleEndUIManager.Instance.ShowGameOver("Game Over");
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
                foreach (var r in defeatedRewards) { if (r == null) continue; rewards.Add(r); totalExp += r.exp; }
            }
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

            var alivePlayers = battlerObjects.Select((obj, idx) => new { obj, idx })
                .Where(x => x.obj != null && x.idx < battlers.Count && !battlers[x.idx].isMonster && battlers[x.idx].hp > 0)
                .Select(x => x.obj).ToList();

            if (BattleEndUIManager.Instance != null) BattleEndUIManager.Instance.ShowVictory(rewards, totalExp, alivePlayers);
            else
            {
                if (totalExp > 0 && alivePlayers.Count > 0) AwardExpToPlayers(totalExp, alivePlayers);
                if (rewards != null && rewards.Count > 0) foreach (var r in rewards) Debug.Log($"[TurnManager] (Fallback) Would award item '{r.id}' x{r.quantity}");
            }

            defeatedRewards.Clear();
            defeatedEnemies.Clear();
        }
    }

    public void AwardExpToPlayers(int totalExp, List<GameObject> alivePlayers)
    {
        if (alivePlayers == null || alivePlayers.Count == 0 || totalExp <= 0) return;

        int perPlayer = totalExp / alivePlayers.Count;
        int remainder = totalExp % alivePlayers.Count;

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var p = alivePlayers[i];
            if (p == null) continue;
            int grant = perPlayer + (i < remainder ? 1 : 0);

            var pl = p.GetComponent<PlayerLevel>();
            if (pl != null)
            {
                pl.AddExp(grant);
                Debug.Log($"[TurnManager] Awarded {grant} EXP to {p.name} via PlayerLevel");
                continue;
            }

            var ps = p.GetComponent<PlayerStat>();
            if (ps != null)
            {
                try
                {
                    var m = ps.GetType().GetMethod("AddExp", new Type[] { typeof(int) });
                    if (m != null) { m.Invoke(ps, new object[] { grant }); Debug.Log($"[TurnManager] Awarded {grant} EXP to {p.name} via PlayerStat.AddExp"); continue; }
                }
                catch (Exception ex) { Debug.LogWarning($"[TurnManager] Failed to call PlayerStat.AddExp on {p.name}: {ex}"); }
            }

            Debug.LogWarning($"[TurnManager] Could not award EXP to {p.name} - no PlayerLevel or PlayerStat.AddExp found");
        }
    }

    public void UpdatePlayerPanelMapping()
    {
        playerToPanel.Clear();
        persistentPlayerToPanel.Clear();
        if ((playerUIPanels == null || playerUIPanels.Count == 0) && (persistentPlayerUIPanels == null || persistentPlayerUIPanels.Count == 0)) return;

        var playerObjects = characterObjects?.Where(go => go != null && go.GetComponent<ICharacterStat>() != null).ToList() ?? new List<GameObject>();
        for (int i = 0; i < playerObjects.Count; i++)
        {
            if (i < playerUIPanels.Count && playerObjects[i] != null && playerUIPanels[i] != null) playerToPanel[playerObjects[i]] = playerUIPanels[i];
            if (i < persistentPlayerUIPanels.Count && playerObjects[i] != null && persistentPlayerUIPanels[i] != null) persistentPlayerToPanel[playerObjects[i]] = persistentPlayerUIPanels[i];
        }
    }

    // Interaction helpers
    public CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    public void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (panel == null) return;
        var cg = GetOrAddCanvasGroup(panel);
        if (cg == null) return;
        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;
        cg.alpha = interactable ? 1f : 0.6f;
        var buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons) if (b != null) b.interactable = interactable;
    }

    public void SetPanelsInteractable(IEnumerable<GameObject> panels, bool interactable)
    {
        if (panels == null) return;
        foreach (var p in panels) SetPanelInteractable(p, interactable);
    }

    public void ShowPlayerUI(GameObject playerObj)
    {
        HideTransientPlayerUI();
        SetPanelsInteractable(playerUIPanels, false);

        if (playerObj != null && playerToPanel.ContainsKey(playerObj))
        {
            var panel = playerToPanel[playerObj];
            if (panel != null) { panel.SetActive(true); SetPanelInteractable(panel, true); }
        }

        if (filterPersistentToParticipants)
        {
            SetPanelsInteractable(persistentPlayerUIPanels, false);
            if (playerObj != null && persistentPlayerToPanel.ContainsKey(playerObj))
            {
                var p = persistentPlayerToPanel[playerObj];
                if (p != null) { p.SetActive(true); SetPanelInteractable(p, true); }
            }
        }
        else
        {
            SetPanelsInteractable(persistentPlayerUIPanels, true);
            if (persistentPlayerUIPanels != null) foreach (var p in persistentPlayerUIPanels) if (p != null) p.SetActive(true);
        }
    }

    public void ShowPanelsForParticipants(GameObject attacker, GameObject target)
    {
        HideTransientPlayerUI();
        SetPanelsInteractable(playerUIPanels, false);

        if (attacker != null && playerToPanel.ContainsKey(attacker))
        {
            var p = playerToPanel[attacker];
            if (p != null) { p.SetActive(true); SetPanelInteractable(p, state == BattleState.WaitingForPlayerInput); }
        }

        if (target != null && playerToPanel.ContainsKey(target))
        {
            var p = playerToPanel[target];
            if (p != null) { p.SetActive(true); SetPanelInteractable(p, state == BattleState.WaitingForPlayerInput); }
        }

        if (filterPersistentToParticipants)
        {
            SetPanelsInteractable(persistentPlayerUIPanels, false);

            if (attacker != null && persistentPlayerToPanel.ContainsKey(attacker))
            {
                var p = persistentPlayerToPanel[attacker];
                if (p != null) { p.SetActive(true); SetPanelInteractable(p, state == BattleState.WaitingForPlayerInput); }
            }
            if (target != null && persistentPlayerToPanel.ContainsKey(target))
            {
                var p = persistentPlayerToPanel[target];
                if (p != null) { p.SetActive(true); SetPanelInteractable(p, state == BattleState.WaitingForPlayerInput); }
            }
        }
        else
        {
            bool interact = (state == BattleState.WaitingForPlayerInput);
            SetPanelsInteractable(persistentPlayerUIPanels, interact);
            if (persistentPlayerUIPanels != null) foreach (var p in persistentPlayerUIPanels) if (p != null) p.SetActive(true);
        }
    }

    void HideTransientPlayerUI()
    {
        if (playerUIPanels == null) return;
        foreach (var panel in playerUIPanels)
        {
            if (panel == null) continue;
            var cg = GetOrAddCanvasGroup(panel);
            if (cg == null) continue;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0.0f;
        }
    }

    public void HideAllPersistentPanels()
    {
        if (persistentPlayerUIPanels == null) return;
        foreach (var panel in persistentPlayerUIPanels)
        {
            if (panel != null)
            {
                var cg = GetOrAddCanvasGroup(panel);
                if (cg == null) continue;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                cg.alpha = 0f;
            }
        }
    }

    public void HideAllPlayerUI()
    {
        HideTransientPlayerUI();
        HideAllPersistentPanels();
    }

    // Recording defeated enemy (public)
    public void RecordEnemyDefeated(GameObject enemy)
    {
        if (enemy == null) return;
        if (defeatedEnemies == null) defeatedEnemies = new List<GameObject>();
        if (!defeatedEnemies.Contains(enemy)) { defeatedEnemies.Add(enemy); Debug.Log($"[TurnManager] Recorded defeated enemy (GO): {enemy.name}"); }
        else Debug.Log($"[TurnManager] Enemy already recorded in defeatedEnemies: {enemy.name}");

        var ms = enemy.GetComponent<IMonsterStat>();
        if (ms != null) RecordEnemyDefeated(ms);
        else
        {
            if (defeatedRewards == null) defeatedRewards = new List<Reward>();
            var r = new Reward(enemy.name, 1, 0, 0);
            if (!defeatedRewards.Any(x => x.id == r.id && x.exp == r.exp)) defeatedRewards.Add(r);
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
            Debug.Log($"[TurnManager] Recorded defeated enemy stat: {id} exp={ms.expValue}");
        }
        else Debug.Log($"[TurnManager] Duplicate Reward ignored for {id} exp={ms.expValue}");
    }
    public GameObject GetRandomAlivePlayer()
    {
        if (battlerObjects == null || battlers == null) return null;
        var alivePlayers = battlerObjects
            .Select((obj, i) => new { obj, i })
            .Where(x => x.obj != null && x.i < battlers.Count && battlers[x.i] != null && !battlers[x.i].isMonster && battlers[x.i].hp > 0)
            .Select(x => x.obj)
            .ToList();

        return alivePlayers.Count > 0 ? alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)] : null;
    }
}