using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Small manager to show GameOver / Victory screens.
/// - Assign UI elements in Inspector (panel, templates, buttons)
/// - ShowVictory takes rewards + totalExp + list of alive player GameObjects
/// - Confirm will actually award EXP/items then load previous scene (fallback to main menu/start scene)
/// - Stores/reads previous scene to/from PlayerPrefs if needed (helper provided)
/// </summary>
public class BattleEndUIManager : MonoBehaviour
{
    public static BattleEndUIManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Panels")]
    public GameObject rootPanel; // panel that contains the whole end-screen UI (set inactive default)
    public TMP_Text titleText;
    public TMP_Text descriptionText; // optional short description

    [Header("Victory area")]
    public Transform rewardsContainer; // parent to instantiate reward lines
    public GameObject rewardEntryPrefab; // prefab with TMP_Text fields for reward display
    public Transform expDistributionContainer; // parent for listing which player gets how much
    public GameObject expEntryPrefab; // prefab to show "PlayerName - +X EXP"

    [Header("Buttons")]
    public Button confirmButton; // confirm & return (used for Victory)
    public Button restartButton; // reload current battle scene (used for Game Over)
    public Button mainMenuButton; // go to main menu (StartScene in your project)

    [Header("Behavior")]
    // Default main menu scene changed to the scene you said contains MainMenu UI.
    // Update this in Inspector if your main/start scene has a different name.
    public string mainMenuSceneName = "StartScene";

    // PlayerPrefs key used to temporarily store previous scene name before loading the battle scene
    const string PREF_PREV_SCENE = "BattleEndUIManager_PreviousScene";

    // If a caller (TurnManager or SceneLoader) calls SetPreviousScene prior to battle,
    // that value will be used. Otherwise we try to read from PlayerPrefs in Start().
    string previousSceneName;

    // Data to apply on confirm
    List<Reward> lastRewards;
    int lastTotalExp;
    List<GameObject> lastAlivePlayers;
    bool lastWasVictory = false;

    void Start()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmVictory);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);

        // Try to read saved previous scene from PlayerPrefs if not set by caller.
        EnsurePreviousSceneLoaded();

        Debug.Log($"[BattleEndUIManager] Start() previousSceneName='{previousSceneName}'");
    }

    /// <summary>
    /// Ensure previousSceneName is loaded from PlayerPrefs if not already set.
    /// Call this whenever we need to be sure we have the previous scene.
    /// </summary>
    void EnsurePreviousSceneLoaded()
    {
        if (string.IsNullOrEmpty(previousSceneName) && PlayerPrefs.HasKey(PREF_PREV_SCENE))
        {
            previousSceneName = PlayerPrefs.GetString(PREF_PREV_SCENE);
            PlayerPrefs.DeleteKey(PREF_PREV_SCENE); // consume it
            Debug.Log($"[BattleEndUIManager] previousSceneName loaded from PlayerPrefs: {previousSceneName}");
        }
    }

    /// <summary>
    /// Allow external code (TurnManager or scene loader) to set which scene to return to after battle.
    /// Should be called with the scene name that was active before loading the battle scene.
    /// </summary>
    public void SetPreviousScene(string name)
    {
        previousSceneName = name;
        Debug.Log($"[BattleEndUIManager] previousSceneName set to: {previousSceneName}");
    }

    /// <summary>
    /// Helper: save current active scene name into PlayerPrefs so the battle loader can pick it up.
    /// Call this BEFORE loading the FightingScene (from Sub1, call this, then SceneManager.LoadScene("FightingScene")).
    /// </summary>
    public static void SavePreviousSceneToPrefs()
    {
        try
        {
            string current = SceneManager.GetActiveScene().name;
            PlayerPrefs.SetString(PREF_PREV_SCENE, current);
            PlayerPrefs.Save();
            Debug.Log($"[BattleEndUIManager] Saved previous scene '{current}' to PlayerPrefs ({PREF_PREV_SCENE}).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BattleEndUIManager] Failed to save previous scene to PlayerPrefs: {ex.Message}");
        }
    }

    /// <summary>
    /// Optional: clear the saved previous scene from PlayerPrefs
    /// </summary>
    public static void ClearSavedPreviousScene()
    {
        if (PlayerPrefs.HasKey(PREF_PREV_SCENE)) PlayerPrefs.DeleteKey(PREF_PREV_SCENE);
    }

    public void ShowGameOver(string message = "Game Over")
    {
        if (rootPanel == null) return;
        ClearUI();

        lastWasVictory = false;

        titleText.text = message;
        descriptionText.text = "You have been defeated.";

        // Only show restart / main menu
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(true);
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(true);

        rootPanel.SetActive(true);
    }

    /// <summary>
    /// Show victory UI.
    /// rewards: list of Reward (your existing struct/class)
    /// totalExp: total exp to distribute among alivePlayers
    /// alivePlayers: list of player GameObjects (to show who will get exp)
    /// </summary>
    public void ShowVictory(List<Reward> rewards, int totalExp, List<GameObject> alivePlayers)
    {
        if (rootPanel == null) return;
        ClearUI();

        // Ensure we have previousSceneName loaded (in case prefs were written after Start)
        EnsurePreviousSceneLoaded();
        Debug.Log($"[BattleEndUIManager] ShowVictory() previousSceneName='{previousSceneName}'");

        lastWasVictory = true;

        titleText.text = "Victory!";
        descriptionText.text = $"You defeated all enemies!";

        lastRewards = new List<Reward>(rewards ?? new List<Reward>());
        lastTotalExp = totalExp;
        lastAlivePlayers = new List<GameObject>(alivePlayers ?? new List<GameObject>());

        // Populate rewards list
        if (rewardsContainer != null && rewardEntryPrefab != null)
        {
            foreach (var r in lastRewards)
            {
                var go = Instantiate(rewardEntryPrefab, rewardsContainer, false);
                var texts = go.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 1) texts[0].text = !string.IsNullOrEmpty(r.name) ? r.name : (r.id ?? "Reward");
                if (texts.Length >= 2)
                {
                    string extra = "";
                    if (r.exp > 0) extra += $"EXP: {r.exp} ";
                    if (r.quantity > 0) extra += $"x{r.quantity}";
                    texts[1].text = extra.Trim();
                }
            }
        }

        // Distribute totalExp visually (not awarding yet)
        if (expDistributionContainer != null && expEntryPrefab != null)
        {
            int players = Mathf.Max(1, lastAlivePlayers.Count);
            int perPlayer = players > 0 ? totalExp / players : 0;
            int remainder = players > 0 ? totalExp % players : 0;
            for (int i = 0; i < lastAlivePlayers.Count; i++)
            {
                var p = lastAlivePlayers[i];
                string pname = p != null ? p.name : $"Player {i + 1}";
                int award = perPlayer + (i < remainder ? 1 : 0);
                var go = Instantiate(expEntryPrefab, expDistributionContainer, false);
                var txt = go.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = $"{pname}  →  +{award} EXP";
            }
        }

        // Buttons: show Confirm, hide Restart & MainMenu
        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);

        // Show panel
        rootPanel.SetActive(true);
    }

    void ClearUI()
    {
        if (rewardsContainer != null) { foreach (Transform t in rewardsContainer) Destroy(t.gameObject); }
        if (expDistributionContainer != null) { foreach (Transform t in expDistributionContainer) Destroy(t.gameObject); }
        if (!lastWasVictory)
        {
            lastRewards = null;
            lastAlivePlayers = null;
            lastTotalExp = 0;
        }
    }

    // Called when user confirms victory (award exp/items and go back)
    void OnConfirmVictory()
    {
        // Ensure we try to load previous scene from prefs if not yet present
        EnsurePreviousSceneLoaded();
        Debug.Log($"[BattleEndUIManager] OnConfirmVictory: previousSceneName='{previousSceneName}', current='{SceneManager.GetActiveScene().name}'");

        if (!lastWasVictory)
        {
            Debug.LogWarning("[BattleEndUIManager] Confirm pressed but last state wasn't Victory.");
            return;
        }

        // award EXP/items
        if (lastAlivePlayers != null && lastAlivePlayers.Count > 0 && lastTotalExp > 0)
        {
            int players = Mathf.Max(1, lastAlivePlayers.Count);
            int perPlayer = lastTotalExp / players;
            int remainder = lastTotalExp % players;
            for (int i = 0; i < lastAlivePlayers.Count; i++)
            {
                var pgo = lastAlivePlayers[i];
                if (pgo == null) continue;
                var ps = pgo.GetComponent<PlayerStat>();
                if (ps != null)
                {
                    int grant = perPlayer + (i < remainder ? 1 : 0);
                    ps.AddExp(grant);
                }
                else
                {
                    Debug.LogWarning($"[BattleEndUIManager] Player object '{pgo.name}' missing PlayerStat - cannot award EXP.");
                }
            }
        }

        // award items if any (placeholder)
        if (lastRewards != null && lastRewards.Count > 0)
        {
            foreach (var r in lastRewards)
            {
                try
                {
                    if (!string.IsNullOrEmpty(r.id) && r.quantity > 0)
                    {
                        Debug.Log($"[BattleEndUIManager] Would award item '{r.id}' x{r.quantity} (implement inventory hook)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BattleEndUIManager] Failed to award item: " + ex.Message);
                }
            }
        }

        rootPanel.SetActive(false);

        // scene navigation logic with safety checks:
        string current = SceneManager.GetActiveScene().name;

        // If previousSceneName is set and different AND exists in build, load it.
        if (!string.IsNullOrEmpty(previousSceneName) && previousSceneName != current && Application.CanStreamedLevelBeLoaded(previousSceneName))
        {
            SceneManager.LoadScene(previousSceneName);
            return;
        }

        // Otherwise try the project's main menu/start scene (default mainMenuSceneName)
        if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        // Last resort: cannot find any scene to load
        Debug.LogError($"[BattleEndUIManager] Cannot load previous scene ('{previousSceneName}') or main menu/start scene ('{mainMenuSceneName}'). Please add the scene(s) to Build Settings.");
    }

    void OnRestart()
    {
        // Restart reloads the current battle scene by default.
        var current = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(current);
    }

    void OnMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogError($"[BattleEndUIManager] Main menu/start scene '{mainMenuSceneName}' is not in Build Settings. Add it or set the correct name in the inspector.");
    }
}