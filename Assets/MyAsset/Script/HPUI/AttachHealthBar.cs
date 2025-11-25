using UnityEngine;

[RequireComponent(typeof(MonoBehaviour))]
public class AttachHealthBar : MonoBehaviour
{
    public Transform headTransform; // ถ้าไม่มี ให้วางเป็น transform ของตัวละคร
    public Vector3 offset = new Vector3(0f, 1.6f, 0f);
    GameObject created;

    void Start()
    {
        // find IHpProvider on this object or in children
        var hpProv = GetComponent(typeof(IHpProvider)) as IHpProvider;
        if (hpProv == null) hpProv = GetComponentInChildren(typeof(IHpProvider)) as IHpProvider;

        if (hpProv != null && HealthBarManager.Instance != null)
        {
            if (headTransform == null) headTransform = transform; // fallback
            created = HealthBarManager.Instance.CreateFor(gameObject, headTransform);
            // set offset if follower exists
            var follower = created != null ? created.GetComponent<HealthBarFollower>() : null;
            if (follower != null) follower.worldOffset = offset;
        }
    }

    void OnDestroy()
    {
        if (HealthBarManager.Instance != null) HealthBarManager.Instance.RemoveFor(gameObject);
    }
}