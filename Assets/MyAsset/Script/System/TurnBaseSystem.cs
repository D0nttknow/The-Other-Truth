using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurnBaseSystem - full, self-contained implementation.
/// - Includes resilient BuildBattlerListsFromCharacterObjects that can read project-specific stat components via reflection.
/// - Exposes public methods other scripts call (RemoveBattler, RecordEnemyDefeated, OnMonsterSelected, OnPlayerReturned, EndTurn, etc.).
/// - Uses reflection to call WeaponUIController.RefreshUI where needed.
/// </summary>
public class TurnBaseSystem : MonoBehaviour
{
    public static TurnBaseSystem Instance;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    [Header("Optional: assign global Weapon UI Controller (single shared UI)")]
    [Tooltip("If assigned, TurnBaseSystem will set globalWeaponUI.playerEquipment when a player battler's turn begins.")]
    public WeaponUIController globalWeaponUI;

    [Header("Loot / Consumables (assign in Inspector)")]
    public List<ItemBase> poolOfConsumables = new List<ItemBase>();

    [Header("UI References")]
    public CharacterInfoPanel characterInfoPanel;

    [Header("Round UI")]
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
    public Canvas defaultCanvas;

    [Header("Behavior")]
    public bool filterPersistentToParticipants = false;

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
        Instance = this;

        BuildBattlerListsFromCharacterObjects();

        if (defaultCanvas == null)
        {
            defaultCanvas = FindObjectOfType<Canvas>(true);
            if (defaultCanvas != null) Debug.Log($"[TurnBaseSystem] defaultCanvas auto-assigned to '{defaultCanvas.name}'");
        }

        if (characterInfoPanel == null)
        {
            characterInfoPanel = FindObjectOfType<CharacterInfoPanel>();
            if (characterInfoPanel != null) Debug.Log($"[TurnBaseSystem] characterInfoPanel auto-assigned to '{characterInfoPanel.gameObject.name}'");
        }

        if (HealthBarManager.Instance != null)
        {
            HealthBarManager.Instance.CreateForAllFromTurnManager();
            Debug.Log("[TurnBaseSystem] Requested HealthBarManager to CreateForAllFromTurnManager()");
        }

        UpdatePlayerPanelMapping();
        EnsurePersistentPanelsVisible();
        RefreshTurnOrderUI();
        UpdateRoundUI();

        CreateOrAssignPerCharacterPanels();

        StartTurn();
    }

    // ----------------------
    // Battler discovery
    // ----------------------
    void BuildBattlerListsFromCharacterObjects()
    {
        battlers.Clear();
        battlerObjects.Clear();
        if (characterObjects == null) return;

        var pairList = new List<(Battler battler, GameObject go)>();

        foreach (var go in characterObjects)
        {
            if (go == null) continue;

            // 1) Preferred: objects implementing ICharacterStat
            var iChar = go.GetComponent<ICharacterStat>();
            if (iChar != null)
            {
                string name = SafeGet(() => iChar.Name, go.name);
                int hp = SafeGet(() => iChar.hp, 0);
                int atk = SafeGet(() => iChar.atk, 0);
                int def = SafeGet(() => iChar.def, 0);
                int spd = SafeGet(() => iChar.speed, 0);

                var wh = go.GetComponent<WeaponHandler>();
                if (wh != null && wh.CurrentSpeedModPercent != 0f)
                    spd = Mathf.RoundToInt(spd * (1f + wh.CurrentSpeedModPercent / 100f));

                var b = new Battler(string.IsNullOrEmpty(name) ? go.name : name, hp, atk, def, spd, false);
                pairList.Add((b, go));
                continue;
            }

            // 2) Preferred monster: IMonsterStat
            var iMon = go.GetComponent<IMonsterStat>();
            if (iMon != null)
            {
                string name = SafeGet(() => iMon.monsterName, go.name);
                int hp = SafeGet(() => iMon.monsterHp, 0);
                int atk = SafeGet(() => iMon.monsterAtk, 0);
                int def = SafeGet(() => iMon.monsterDef, 0);
                int spd = SafeGet(() => iMon.monsterSpeed, 0);
                var b = new Battler(string.IsNullOrEmpty(name) ? go.name : name, hp, atk, def, spd, true);
                pairList.Add((b, go));
                continue;
            }

            // 3) Fallback: inspect other components on the GameObject with reflection heuristics
            bool added = false;
            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                var t = comp.GetType();

                // skip Unity engine built-in components to avoid false positives
                if (t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) continue;

                if (TryExtractStatsFromComponent(comp, out string extractedName, out int hp, out int atk, out int def, out int spd))
                {
                    string tn = t.Name.ToLowerInvariant();
                    bool isMonster = (tn.Contains("enemy") || tn.Contains("monster") || tn.Contains("enemie") || tn.Contains("mob"));

                    var b = new Battler(string.IsNullOrEmpty(extractedName) ? go.name : extractedName, hp, atk, def, spd, isMonster);
                    pairList.Add((b, go));
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                Debug.LogWarning($"[TurnBaseSystem] GameObject '{go.name}' has no ICharacterStat/IMonsterStat and no readable stat component - skipped when building turn order.");
            }
        }

        // sort by speed descending and populate lists
        pairList = pairList.OrderByDescending(p => p.battler.speed).ToList();
        foreach (var p in pairList)
        {
            battlers.Add(p.battler);
            battlerObjects.Add(p.go);
        }

        if (turnIndex < 0 || turnIndex >= battlers.Count) turnIndex = 0;
    }

    // Try to extract stats using reflection heuristics. Returns true if at least one meaningful stat found (hp/atk/def/speed).
    bool TryExtractStatsFromComponent(Component comp, out string name, out int hp, out int atk, out int def, out int speed)
    {
        name = comp?.gameObject?.name ?? "";
        hp = atk = def = speed = 0;
        if (comp == null) return false;

        var t = comp.GetType();

        // Try to read name-like property
        var nameProp = t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? t.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? t.GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (nameProp != null)
        {
            try { var v = nameProp.GetValue(comp); if (v != null) name = v.ToString(); } catch { }
        }

        // Helper: search by keywords, tries fields then properties, then substring match
        Func<string[], int> findIntByKeywords = (keywords) =>
        {
            foreach (var kw in keywords)
            {
                // fields
                var f = t.GetField(kw, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    try
                    {
                        var val = f.GetValue(comp);
                        if (val is int) return (int)val;
                        if (val is float) return Mathf.RoundToInt((float)val);
                        if (val is double) return Mathf.RoundToInt((float)(double)val);
                    }
                    catch { }
                }
                // properties
                var p = t.GetProperty(kw, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    try
                    {
                        var val = p.GetValue(comp);
                        if (val is int) return (int)val;
                        if (val is float) return Mathf.RoundToInt((float)val);
                        if (val is double) return Mathf.RoundToInt((float)(double)val);
                    }
                    catch { }
                }
            }

            // broader substring search on fields
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string n = f.Name.ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (n.Contains(kw.ToLowerInvariant()))
                    {
                        try
                        {
                            var val = f.GetValue(comp);
                            if (val is int) return (int)val;
                            if (val is float) return Mathf.RoundToInt((float)val);
                            if (val is double) return Mathf.RoundToInt((float)(double)val);
                        }
                        catch { }
                    }
                }
            }

            // substring search on properties
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string n = p.Name.ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (n.Contains(kw.ToLowerInvariant()))
                    {
                        try
                        {
                            var val = p.GetValue(comp);
                            if (val is int) return (int)val;
                            if (val is float) return Mathf.RoundToInt((float)val);
                            if (val is double) return Mathf.RoundToInt((float)(double)val);
                        }
                        catch { }
                    }
                }
            }

            return 0;
        };

        // Try several variants including project-specific names like Hunterhp etc.
        hp = findIntByKeywords(new string[] { "hp", "health", "hunterhp", "currenthp", "hitpoints" });
        atk = findIntByKeywords(new string[] { "atk", "attack", "damage", "hunteratk" });
        def = findIntByKeywords(new string[] { "def", "defense", "armour", "hunterdef" });
        speed = findIntByKeywords(new string[] { "speed", "spd", "hunterspeed" });

        // Last resort: try to find any plausible hp-like field
        if (hp == 0)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var n = f.Name.ToLowerInvariant();
                if (n.Contains("hp") || n.Contains("health") || n.Contains("hit"))
                {
                    try
                    {
                        var v = f.GetValue(comp);
                        if (v is int) { hp = (int)v; break; }
                        if (v is float) { hp = Mathf.RoundToInt((float)v); break; }
                    }
                    catch { }
                }
            }
        }

        return (hp > 0) || (atk > 0) || (def > 0) || (speed > 0);
    }

    // ----------------------------
    // Core turn logic (StartTurn, EndTurn etc.)
    // ----------------------------
    T SafeGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    public void StartTurn()
    {
        // DEBUG: log battler lists for diagnosis
        Debug.Log($"[TMDebug] StartTurn called. turnIndex={turnIndex} round={roundNumber} battlers={battlers.Count} battlerObjects={battlerObjects.Count}");
        for (int i = 0; i < Math.Max(battlers.Count, battlerObjects.Count); i++)
        {
            string goName = (i < battlerObjects.Count && battlerObjects[i] != null) ? battlerObjects[i].name : "NULL";
            string binfo = (i < battlers.Count && battlers[i] != null) ? $"hp={battlers[i].hp} isMonster={battlers[i].isMonster}" : "noBattler";
            Debug.Log($"[TMDebug] idx={i} GO={goName} | {binfo}");
        }

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

            Debug.Log($"[TMDebug] Ticking status for index {turnIndex} (GO: {(turnIndex < battlerObjects.Count && battlerObjects[turnIndex] != null ? battlerObjects[turnIndex].name : "null")})");
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

        Battler current = (turnIndex < battlers.Count) ? battlers[turnIndex] : null;
        GameObject obj = (turnIndex < battlerObjects.Count) ? battlerObjects[turnIndex] : null;

        EnsurePersistentPanelsVisible();
        RefreshTurnOrderUI();

        // If this is a player (not a monster), assign the global WeaponUIController so the single UI controls the current player
        try
        {
            if (current != null && !current.isMonster)
            {
                if (globalWeaponUI == null)
                {
                    // try to auto-find if not assigned in inspector
                    globalWeaponUI = FindObjectOfType<WeaponUIController>();
                    if (globalWeaponUI != null) Debug.Log($"[TurnBaseSystem] Auto-assigned globalWeaponUI to '{globalWeaponUI.gameObject.name}'");
                }

                if (globalWeaponUI != null)
                {
                    var ce = obj != null ? obj.GetComponent<CharacterEquipment>() : null;
                    globalWeaponUI.playerEquipment = ce;
                    globalWeaponUI.turnManager = this;
                    try
                    {
                        var mi = typeof(WeaponUIController).GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (mi != null) mi.Invoke(globalWeaponUI, null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TurnBaseSystem] invoking globalWeaponUI.RefreshUI threw: " + ex);
                    }

                    Debug.Log($"[TurnBaseSystem] Assigned global WeaponUIController to {ce?.gameObject.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurnBaseSystem] globalWeaponUI assign threw: " + ex);
        }

        // Safer PartyAutoAttack invocation: only trigger for non-player NPCs (unless PartyAutoAttack wants a different policy)
        try
        {
            var pa = FindObjectOfType<PartyAutoAttack>();
            if (pa != null && obj != null)
            {
                bool isPlayerControlled = (obj.GetComponent<PlayerLevel>() != null) || (obj.GetComponent<PlayerController>() != null) || obj.CompareTag("Player");
                if (!isPlayerControlled)
                {
                    try
                    {
                        pa.OnBattlerTurnStart(obj);
                        Debug.Log($"[TurnBaseSystem] PartyAutoAttack triggered for {obj.name}");
                    }
                    catch (Exception ex) { Debug.LogWarning("[TurnBaseSystem] PartyAutoAttack.OnBattlerTurnStart threw: " + ex); }
                }
                else
                {
                    Debug.Log($"[TurnBaseSystem] Skipping PartyAutoAttack for player-controlled {obj.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurnBaseSystem] PartyAutoAttack lookup threw: " + ex);
        }

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

    // Make public so controllers can call when player returns
    public void OnPlayerReturned() { state = BattleState.WaitingForMonsterTurn; EndTurn(); }

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

    // ----------------------------
    // Methods used elsewhere in project (exposed publicly)
    // ----------------------------
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
                    Debug.Log($"[TurnBaseSystem] WeaponHandler.OnTurnEnd called for {currentGo.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurnBaseSystem] WeaponHandler.OnTurnEnd threw: " + ex);
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
            Debug.Log($"[TMDebug] MarkCurrentBattlerActed for {(go != null ? go.name : "null")}");
            if (go != null) actedThisRound.Add(go);

            if (AreAllAliveBattlersActed())
            {
                roundNumber++;
                actedThisRound.Clear();
                Debug.Log($"[TurnBaseSystem] New round {roundNumber}");
                UpdateRoundUI();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TurnBaseSystem] MarkCurrentBattlerActed exception: {ex}");
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
                Debug.Log($"[TurnBaseSystem] Reparented persistent panel '{p.name}' under canvas '{defaultCanvas.name}' to keep it visible.");
            }

            if (!p.activeSelf) { p.SetActive(true); Debug.Log($"[TurnBaseSystem] Activated persistent panel '{p.name}'."); }

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

    public void TryTickStatusForIndexPublic(int idx) => TryTickStatusForIndex(idx);

    void TryTickStatusForIndex(int idx)
    {
        if (idx < 0 || idx >= battlerObjects.Count) return;
        var go = battlerObjects[idx];
        if (go == null) return;

        var sm = go.GetComponent<StatusManager>();
        if (sm != null)
        {
            try { Debug.Log($"[TurnBaseSystem] Ticking StatusManager for {go.name}"); sm.TickStatusPerTurn(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnBaseSystem] Exception ticking StatusManager on {go.name}: {ex}"); }
        }

        var es = go.GetComponent<EnemyStats>();
        if (es != null)
        {
            try { Debug.Log($"[TurnBaseSystem] Ticking EnemyStats for {go.name}"); es.TickStatusPerTurn(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnBaseSystem] Exception ticking EnemyStats on {go.name}: {ex}"); }
        }

        var ce = go.GetComponent<CharacterEquipment>();
        if (ce != null)
        {
            try { ce.OnTurnStart(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnBaseSystem] Exception in CharacterEquipment.OnTurnStart for {go.name}: {ex}"); }
        }

        var wc = go.GetComponent<WeaponController>();
        if (wc != null)
        {
            try { wc.OnTurnStart(); }
            catch (Exception ex) { Debug.LogWarning($"[TurnBaseSystem] Exception in WeaponController.OnTurnStart for {go.name}: {ex}"); }
        }
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
                                Debug.Log($"[TurnBaseSystem] Loot drop: added '{item.displayName}' to Inventory");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("[TurnBaseSystem] No poolOfConsumables assigned or InventoryManager missing - skipping loot drops.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[TurnBaseSystem] Exception while generating/adding loot: " + ex);
                }
            }

            if (battlerToPanel != null && battlerToPanel.TryGetValue(go, out var panel) && panel != null)
            {
                Destroy(panel);
                battlerToPanel.Remove(go);
            }

            if (idx < battlerObjects.Count) battlerObjects.RemoveAt(idx);
            if (idx < battlers.Count) battlers.RemoveAt(idx);

            if (actedThisRound.Contains(go)) actedThisRound.Remove(go);

            if (turnIndex >= battlers.Count) turnIndex = Mathf.Max(0, battlers.Count - 1);
            UpdatePlayerPanelMapping();
            RefreshTurnOrderUI();
            Debug.Log($"[TurnBaseSystem] Removed battler '{go.name}' at index {idx}");
        }
        else
        {
            Debug.LogWarning($"[TurnBaseSystem] RemoveBattler: GameObject '{go.name}' not found in battlerObjects.");
        }
    }

    public void CleanUpDeadBattlers()
    {
        if (battlerObjects == null) return;

        for (int i = battlerObjects.Count - 1; i >= 0; i--)
        {
            bool remove = false;
            if (battlerObjects[i] == null)
            {
                Debug.Log($"[TurnBaseSystem] Removing dead/null battler at index {i} (GO null)");
                remove = true;
            }
            else if (i < battlers.Count && battlers[i].hp <= 0)
            {
                Debug.Log($"[TurnBaseSystem] Removing dead battler at index {i} (hp<=0)");
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

    public void CreateOrAssignPerCharacterPanels()
    {
        if (perCharacterPanelPrefab == null) return;

        Transform parent = defaultCanvas != null ? defaultCanvas.transform : null;
        if (parent == null)
        {
            var found = FindObjectOfType<Canvas>(true);
            if (found != null) parent = found.transform;
        }
        if (parent == null) { Debug.LogWarning("[TurnBaseSystem] No Canvas found to parent per-character panels."); return; }

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
                if (rewards != null && rewards.Count > 0) foreach (var r in rewards) Debug.Log($"[TurnBaseSystem] (Fallback) Would award item '{r.id}' x{r.quantity}");
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
                Debug.Log($"[TurnBaseSystem] Awarded {grant} EXP to {p.name} via PlayerLevel");
                continue;
            }

            var ps = p.GetComponent<PlayerStat>();
            if (ps != null)
            {
                try
                {
                    var m = ps.GetType().GetMethod("AddExp", new Type[] { typeof(int) });
                    if (m != null) { m.Invoke(ps, new object[] { grant }); Debug.Log($"[TurnBaseSystem] Awarded {grant} EXP to {p.name} via PlayerStat.AddExp"); continue; }
                }
                catch (Exception ex) { Debug.LogWarning($"[TurnBaseSystem] Failed to call PlayerStat.AddExp on {p.name}: {ex}"); }
            }

            Debug.LogWarning($"[TurnBaseSystem] Could not award EXP to {p.name} - no PlayerLevel or PlayerStat.AddExp found");
        }
    }

    public void UpdatePlayerPanelMapping()
    {
        playerToPanel.Clear();
        persistentPlayerToPanel.Clear();
        if ((playerUIPanels == null || playerUIPanels.Count == 0) && (persistentPlayerUIPanels == null || persistentPlayerUIPanels.Count == 0)) return;

        // Use ICharacterStat to consider a GameObject a "player"; fallback not used here intentionally.
        var playerObjects = characterObjects?.Where(go => go != null && go.GetComponent<ICharacterStat>() != null).ToList() ?? new List<GameObject>();
        for (int i = 0; i < playerObjects.Count; i++)
        {
            if (i < playerUIPanels.Count && playerObjects[i] != null && playerUIPanels[i] != null) playerToPanel[playerObjects[i]] = playerUIPanels[i];
            if (i < persistentPlayerUIPanels.Count && playerObjects[i] != null && persistentPlayerUIPanels[i] != null) persistentPlayerToPanel[playerObjects[i]] = persistentPlayerUIPanels[i];
        }
    }

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

    public void RecordEnemyDefeated(GameObject enemy)
    {
        if (enemy == null) return;
        if (defeatedEnemies == null) defeatedEnemies = new List<GameObject>();
        if (!defeatedEnemies.Contains(enemy)) { defeatedEnemies.Add(enemy); Debug.Log($"[TurnBaseSystem] Recorded defeated enemy (GO): {enemy.name}"); }
        else Debug.Log($"[TurnBaseSystem] Enemy already recorded in defeatedEnemies: {enemy.name}");

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
            Debug.Log($"[TurnBaseSystem] Recorded defeated enemy stat: {id} exp={ms.expValue}");
        }
        else Debug.Log($"[TurnBaseSystem] Duplicate Reward ignored for {id} exp={ms.expValue}");
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