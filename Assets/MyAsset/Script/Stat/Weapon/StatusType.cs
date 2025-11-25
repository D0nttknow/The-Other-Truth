using System;

namespace GameRaiwaa.Stat
{
    /// <summary>
    /// Central definition for status types.
    /// Keep a single copy of this enum in the project to avoid duplicate-definition errors.
    /// Add more statuses here as needed.
    /// </summary>
    public enum StatusType
    {
        None = 0,
        Bleed = 10,
        Stun = 20,
        // add more status IDs here (example):
        // Poison = 30,
    }
}