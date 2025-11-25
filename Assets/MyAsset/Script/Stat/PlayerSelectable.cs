using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to player character GameObjects.
/// - Supports both 2D (Collider2D) and 3D (Collider) setups.
/// - Uses OnMouseDown (physics) or IPointerClickHandler (EventSystem + Raycaster).
/// - Calls CharacterInfoPanel via TurnManager.characterInfoPanel (if assigned) or falls back to FindObjectOfType.
/// - Includes Debug.Log and a ContextMenu method for manual testing.
/// 
/// Changes:
/// - No longer calls CharacterInfoPanel.BringToFront() (fixes CS1061 if your CharacterInfoPanel doesn't define that method).
///   Instead it calls panel.transform.SetAsLastSibling() to bring the panel GameObject to front in the hierarchy.
/// - Uses FindObjectOfType<CharacterInfoPanel>(true) to avoid the deprecation warning for the parameterless overload.
/// </summary>
public class PlayerSelectable : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("If true, OnMouseDown (physics click) will send selection. If false, rely on pointer events.")]
    public bool useMouseClick = true;

    [Tooltip("If true, selection only allowed when this object has ICharacterStat.")]
    public bool onlyIfPlayer = true;

    void Awake()
    {
        // optional check to help debugging if no collider present
        bool has2D = GetComponent<Collider2D>() != null;
        bool has3D = GetComponent<Collider>() != null;
        if (!has2D && !has3D)
        {
            Debug.LogWarning($"[PlayerSelectable] '{gameObject.name}' has no Collider2D or Collider. Selection won't work with physics clicks.", this);
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[PlayerSelectable] No EventSystem found in scene. Pointer events will not work. (Add GameObject->UI->EventSystem)", this);
        }
    }

    void OnMouseDown()
    {
        if (!useMouseClick) return;
        TrySelect();
    }

    // UI pointer clicks (requires EventSystem + appropriate Raycaster on Camera/Canvas)
    public void OnPointerClick(PointerEventData eventData)
    {
        TrySelect();
    }

    public void TrySelect()
    {
        Debug.Log($"[PlayerSelectable] TrySelect called on '{gameObject.name}' (useMouseClick={useMouseClick}, onlyIfPlayer={onlyIfPlayer})", this);

        if (onlyIfPlayer)
        {
            var cs = GetComponent<ICharacterStat>();
            if (cs == null)
            {
                Debug.Log("[PlayerSelectable] Not a player (no ICharacterStat) - ignoring.", this);
                return;
            }
        }

        // Preferred: central ShowCharacterInfo via TurnManager if available
        if (TurnManager.Instance != null)
        {
            var tmPanel = TurnManager.Instance.characterInfoPanel;
            if (tmPanel != null)
            {
                Debug.Log("[PlayerSelectable] Using TurnManager.characterInfoPanel to show info.", tmPanel);

                // Set the target and open the panel
                tmPanel.SetTarget(gameObject);

                // Bring panel GameObject to front in hierarchy (avoid calling methods that may not exist)
                // Use transform.SetAsLastSibling() instead of a non-existent BringToFront() method.
                try
                {
                    tmPanel.transform.SetAsLastSibling();
                }
                catch
                {
                    // ignore if transform missing for any reason
                }

                tmPanel.Open();
                return;
            }
        }

        // Fallback: find any CharacterInfoPanel in the scene (use the overload with includeInactive to avoid deprecation warning)
        var foundPanel = Object.FindObjectOfType<CharacterInfoPanel>(true);
        if (foundPanel != null)
        {
            Debug.Log("[PlayerSelectable] Found CharacterInfoPanel in scene - using it.", foundPanel);
            foundPanel.SetTarget(gameObject);
            try
            {
                foundPanel.transform.SetAsLastSibling();
            }
            catch { }
            foundPanel.Open();
            return;
        }

        Debug.LogWarning("[PlayerSelectable] No CharacterInfoPanel found to handle selection.");
    }

    // Manual test from Inspector (right-click component -> DebugSelect)
    [ContextMenu("DebugSelect")]
    public void DebugSelect()
    {
        TrySelect();
    }
}