/// <summary>
/// Generic damage receiver interface.
/// Add to any GameObject that can be damaged by status effects / weapons.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Apply integer damage. Return true if the object died from this hit (optional).
    /// </summary>
    bool TakeDamage(int amount);
}