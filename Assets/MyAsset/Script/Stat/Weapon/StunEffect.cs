using System;
using UnityEngine;
using GameRaiwaa.Stat; // หาก StatusEffect อยู่ใน namespace นี้ ให้แน่ใจว่า path ถูกต้อง

namespace GameRaiwaa.Stat
{
    /// <summary>
    /// StunEffect now derives from StatusEffect so it can be passed to ApplyStatus(StatusEffect).
    /// Presence of this effect indicates the target is stunned; TurnManager/AI must check for Stun and skip actions.
    /// </summary>
    [Serializable]
    public class StunEffect : StatusEffect
    {
        // Legacy field (kept for compatibility)
        public int stunTurns = 2;

        public StunEffect() : base(StatusType.Stun, 2, true)
        {
            stunTurns = 2;
        }

        public StunEffect(int turns = 2) : base(StatusType.Stun, turns, true)
        {
            stunTurns = turns;
        }

        // Stun doesn't deal damage; override if needed for logging.
        public override int OnTurnStart(GameObject owner)
        {
            // No damage. Presence is checked elsewhere to skip actions.
            return 0;
        }
    }
}