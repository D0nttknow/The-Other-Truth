using UnityEngine;

/// <summary>
/// Backwards-compatible TurnManager shim.
/// Delegates implementation to TurnBaseSystem (so existing code calling TurnManager.Instance,
/// TurnManager.battlers, TurnManager.EndTurn(), etc. will work).
/// Keep exactly one TurnManager class in the project to avoid duplicate-type CS0101.
/// Marked Obsolete so you can migrate callers later to TurnBaseSystem.Instance.
/// </summary>
[System.Obsolete("TurnManager moved to TurnBaseSystem. Use TurnBaseSystem.Instance instead.")]
public class TurnManager : TurnBaseSystem
{
    public new static TurnManager Instance
    {
        get
        {
            if (TurnBaseSystem.Instance is TurnManager tm) return tm;

            var existing = Object.FindObjectOfType<TurnManager>();
            if (existing != null) return existing;

            if (TurnBaseSystem.Instance != null)
            {
                var go = TurnBaseSystem.Instance.gameObject;
                var added = go.GetComponent<TurnManager>();
                if (added != null) return added;
                return go.AddComponent<TurnManager>();
            }

            return null;
        }
    }

    // No extra state here — implementation lives in TurnBaseSystem (inherited).
}