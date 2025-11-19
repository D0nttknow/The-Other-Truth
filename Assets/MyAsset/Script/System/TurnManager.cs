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
    // Backward-compatible Instance property returning a TurnManager if available,
    // otherwise attaching a TurnManager to the TurnBaseSystem GameObject.
    public new static TurnManager Instance
    {
        get
        {
            // If TurnBaseSystem.Instance is actually a TurnManager (e.g. you attached TurnManager),
            // return it.
            if (TurnBaseSystem.Instance is TurnManager tm) return tm;

            // Try find existing TurnManager component in scene
            var existing = Object.FindObjectOfType<TurnManager>();
            if (existing != null) return existing;

            // If TurnBaseSystem exists, attach a TurnManager component to the same GameObject (keeps serialized fields accessible)
            if (TurnBaseSystem.Instance != null)
            {
                var go = TurnBaseSystem.Instance.gameObject;
                var added = go.GetComponent<TurnManager>();
                if (added != null) return added;
                return go.AddComponent<TurnManager>();
            }

            // Nothing available
            return null;
        }
    }

    // No extra state here — implementation lives in TurnBaseSystem (inherited).
}