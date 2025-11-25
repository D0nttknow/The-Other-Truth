using UnityEngine;

/// <summary>
/// Button helper to set the Turn manager's selectedMonster.
/// Attach to a UI Button and set the 'target' in the Inspector (the monster GameObject).
/// Then set the Button OnClick to call OnButtonClick (Runtime Only).
/// This version is defensive: it will not throw if target isn't assigned.
/// </summary>
public class ClickOnTarget : MonoBehaviour
{
    [Tooltip("Assign the monster GameObject this button represents")]
    public GameObject target;

    void Start()
    {
        Debug.Log("[ClickOnTarget] Start for " + (target ? target.name : "(no target)"));
    }

    public void OnButtonClick()
    {
        if (target == null)
        {
            Debug.LogWarning("[ClickOnTarget] OnButtonClick called but target is not assigned on component attached to: " + gameObject.name);
            return;
        }

        // Prefer TurnBaseSystem (new) but fallback to TurnManager shim
        var tbs = TurnBaseSystem.Instance;
        if (tbs != null)
        {
            tbs.selectedMonster = target;
            Debug.Log($"[ClickOnTarget] selected (via TurnBaseSystem) = {target.name}");
            return;
        }

        var tm = TurnManager.Instance;
        if (tm != null)
        {
            tm.selectedMonster = target;
            Debug.Log($"[ClickOnTarget] selected (via TurnManager) = {target.name}");
            return;
        }

        Debug.LogWarning("[ClickOnTarget] No turn manager found to set selectedMonster.");
    }
}