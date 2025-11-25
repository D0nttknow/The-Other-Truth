using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple test helper: add an item to the inventory on Start or when pressing a key.
/// Works with both the old Input (Input.GetKeyDown) and the new Input System (Keyboard.current).
/// </summary>
public class InventoryTestAdd : MonoBehaviour
{
    public string itemId = "potion_small";
    public int amount = 3;
    public bool addOnStart = true;

    // For old Input (Input.GetKeyDown)
#if ENABLE_LEGACY_INPUT_MANAGER
    public KeyCode addKey = KeyCode.K;
#endif

    // For new Input System (Input System package). Set this in the Inspector if using the new system.
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Key to press when using the new Input System (Keyboard.current).")]
    public UnityEngine.InputSystem.Key addKeyNew = UnityEngine.InputSystem.Key.K; // <-- use uppercase K
#endif

    void Start()
    {
        if (addOnStart)
            AddTestItem();
    }

    void Update()
    {
        // Try new Input System first if available
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var control = Keyboard.current[addKeyNew];
            if (control != null && control.wasPressedThisFrame)
            {
                AddTestItem();
                return;
            }
        }
#endif

        // Fallback to old Input API only if legacy input is enabled in Player Settings (Active Input Handling includes "Input Manager")
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(addKey))
        {
            AddTestItem();
        }
#else
        // If legacy input isn't available, do nothing here (we already attempted new system above).
        // Optionally you can log once to help debugging in editor:
        // Debug.Log("[InventoryTestAdd] Legacy Input not enabled; skipping Input.GetKeyDown fallback.");
#endif
    }

    public void AddTestItem()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(itemId, amount);
            Debug.Log($"[InventoryTestAdd] Added {itemId} x{amount}");
        }
        else
        {
            Debug.LogWarning("[InventoryTestAdd] InventoryManager.Instance is null.");
        }
    }
}