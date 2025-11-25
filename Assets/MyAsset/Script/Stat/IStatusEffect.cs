using UnityEngine;

namespace GameRaiwaa.Stat
{
    /// <summary>
    /// Minimal interface for status effect instances.
    /// Placed in the same namespace as StatusType so the enum is found.
    /// </summary>
    public interface IStatusEffect
    {
        StatusType Type { get; }
        int RemainingTurns { get; set; }
        bool RefreshIfExists { get; }

        /// <summary>
        /// Called at start of owner's turn. Return damage dealt (or 0).
        /// </summary>
        int OnTurnStart(GameObject owner);
    }
}