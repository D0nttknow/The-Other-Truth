using UnityEngine;

/// <summary>
/// Attach to a world pickup object (has a collider with isTrigger).
/// Configure itemId (matching ItemDefinition.id) and amount.
/// On trigger (or OnInteract) it adds to InventoryManager and destroys pickup.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public string itemId;
    public int amount = 1;
    public bool pickOnTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (!pickOnTrigger) return;
        // optionally check tag for player
        if (other.CompareTag("Player"))
        {
            TryPickup();
        }
    }

    // 2D physics version
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!pickOnTrigger) return;
        if (other.CompareTag("Player"))
            TryPickup();
    }

    public void TryPickup()
    {
        if (InventoryManager.Instance != null && !string.IsNullOrEmpty(itemId))
        {
            InventoryManager.Instance.AddItem(itemId, amount);
            Destroy(gameObject);
        }
    }
}