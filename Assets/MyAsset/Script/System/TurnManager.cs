using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility shim: exposes the fields/methods other scripts expect on 'TurnManager'
/// by forwarding to TurnBaseSystem.Instance. Place this on the same GameObject as TurnBaseSystem
/// or on any active GameObject in the scene.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ----- Forwarded properties (read/write where appropriate) -----
    public CharacterInfoPanel characterInfoPanel
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.characterInfoPanel : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.characterInfoPanel = value; }
    }

    public List<GameObject> characterObjects
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.characterObjects : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.characterObjects = value; }
    }

    public List<Battler> battlers => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.battlers : null;

    public List<GameObject> battlerObjects => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.battlerObjects : null;

    public GameObject selectedMonster
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.selectedMonster : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.selectedMonster = value; }
    }

    public List<GameObject> playerUIPanels
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.playerUIPanels : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.playerUIPanels = value; }
    }

    public List<GameObject> persistentPlayerUIPanels
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.persistentPlayerUIPanels : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.persistentPlayerUIPanels = value; }
    }

    public Canvas defaultCanvas
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.defaultCanvas : null;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.defaultCanvas = value; }
    }

    public int roundNumber
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.roundNumber : 0;
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.roundNumber = value; }
    }

    public TurnBaseSystem.BattleState state
    {
        get => TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.state : default(TurnBaseSystem.BattleState);
        set { if (TurnBaseSystem.Instance != null) TurnBaseSystem.Instance.state = value; }
    }

    // ----- Forwarded methods -----
    public void RemoveBattler(GameObject go, bool recordIfMonster = true)
    {
        TurnBaseSystem.Instance?.RemoveBattler(go, recordIfMonster);
    }

    public void RecordEnemyDefeated(GameObject enemy)
    {
        TurnBaseSystem.Instance?.RecordEnemyDefeated(enemy);
    }

    public void RecordEnemyDefeated(IMonsterStat ms)
    {
        TurnBaseSystem.Instance?.RecordEnemyDefeated(ms);
    }

    public void OnMonsterSelected(GameObject monster)
    {
        TurnBaseSystem.Instance?.OnMonsterSelected(monster);
    }

    public void EndTurn()
    {
        TurnBaseSystem.Instance?.EndTurn();
    }

    public void OnPlayerReturned()
    {
        TurnBaseSystem.Instance?.OnPlayerReturned();
    }

    public GameObject GetRandomAlivePlayer()
    {
        return TurnBaseSystem.Instance != null ? TurnBaseSystem.Instance.GetRandomAlivePlayer() : null;
    }

    public void StartTurn()
    {
        TurnBaseSystem.Instance?.StartTurn();
    }

    // Add other forwards if a specific script still complains about missing members
}