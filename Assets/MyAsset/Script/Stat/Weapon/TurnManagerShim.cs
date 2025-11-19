using UnityEngine;

/// <summary>
/// Compatibility shim: keep TurnManager type available for older scripts.
/// TurnManager functionality has moved to TurnBaseSystem; this shim ensures
/// existing references to TurnManager still compile and at runtime returns
/// an instance attached to the same GameObject as TurnBaseSystem.Instance
/// (or creates one if needed).
/// Marked Obsolete to encourage migration to TurnBaseSystem.
/// </summary>
[System.Obsolete("TurnManager moved to TurnBaseSystem. Use TurnBaseSystem.Instance instead.")]
public class TurnManager : TurnBaseSystem
{
    public new static TurnManager Instance
    {
        get
        {
            // Prefer an existing TurnManager in the scene
            var existing = Object.FindObjectOfType<TurnManager>();
            if (existing != null) return existing;

            // If the TurnBaseSystem instance is actually a TurnManager, return it
            if (TurnBaseSystem.Instance is TurnManager tm) return tm;

            // If TurnBaseSystem exists, attach/return a TurnManager component on the same GameObject
            if (TurnBaseSystem.Instance != null)
            {
                var go = TurnBaseSystem.Instance.gameObject;
                var added = go.GetComponent<TurnManager>();
                if (added != null) return added;
                return go.AddComponent<TurnManager>();
            }

            // No manager available
            return null;
        }
    }
}