using UnityEngine;

/// <summary>
/// AttachHealthBar
/// - Creates a healthbar via HealthBarManager for this GameObject (if it implements IHpProvider).
/// - Will skip creating a healthbar if TurnBaseSystem.Instance.manageHealthbars == true so that
///   TurnBaseSystem can be the single authoritative creator of healthbars and panels.
/// - If you want this component to always create the healthbar regardless of TurnBaseSystem, set
///   forceCreateEvenIfManagerExists = true.
/// </summary>
public class AttachHealthBar : MonoBehaviour
{
    [Tooltip("If left empty this will use the GameObject.transform as follower target")]
    public Transform headTransform;

    [Tooltip("World offset applied to the HealthBarFollower.worldOffset (if follower exists)")]
    public Vector3 offset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("If true this component will create a healthbar even if TurnBaseSystem.manageHealthbars is true")]
    public bool forceCreateEvenIfManagerExists = false;

    GameObject created;

    void Start()
    {
        // If a central TurnBaseSystem exists and is configured to manage healthbars, skip local creation
        if (!forceCreateEvenIfManagerExists && TurnBaseSystem.Instance != null && TurnBaseSystem.Instance.manageHealthbars)
        {
            Debug.Log($"[AttachHealthBar] Skipping local healthbar creation for '{gameObject.name}' because TurnBaseSystem.manageHealthbars == true");
            return;
        }

        // find IHpProvider on this object or in children
        var hpProv = GetComponent(typeof(IHpProvider)) as IHpProvider;
        if (hpProv == null) hpProv = GetComponentInChildren(typeof(IHpProvider)) as IHpProvider;

        if (hpProv == null)
        {
            Debug.LogWarning($"[AttachHealthBar] No IHpProvider found on '{gameObject.name}' - skipping healthbar creation.");
            return;
        }

        if (HealthBarManager.Instance == null)
        {
            Debug.LogWarning($"[AttachHealthBar] HealthBarManager.Instance is null - cannot create healthbar for '{gameObject.name}'.");
            return;
        }

        if (headTransform == null) headTransform = transform; // fallback

        created = HealthBarManager.Instance.CreateFor(gameObject, headTransform);

        if (created != null)
        {
            var follower = created.GetComponent<HealthBarFollower>();
            if (follower != null)
            {
                follower.worldOffset = offset;
            }
            Debug.Log($"[AttachHealthBar] Created healthbar '{created.name}' for '{gameObject.name}' (id={gameObject.GetInstanceID()})");
        }
    }

    void OnDestroy()
    {
        // Only remove if THIS component created it (created != null)
        if (created != null && HealthBarManager.Instance != null)
        {
            try { HealthBarManager.Instance.RemoveFor(gameObject); }
            catch { }
        }
    }
}