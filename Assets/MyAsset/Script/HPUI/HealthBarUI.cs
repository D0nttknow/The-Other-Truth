using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("UI References")]
    public Image fillImage;            // foreground fill (type: Filled, Fill Method: Horizontal)
    public CanvasGroup canvasGroup;    // สำหรับ fade in/out หรือ hide

    [Header("Options")]
    public float smoothSpeed = 8f;     // ความเนียนของการลด HP

    private int currentHp;
    private int maxHp;
    private float displayedFill = 1f;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        float targetFill = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * smoothSpeed);
        if (fillImage != null) fillImage.fillAmount = displayedFill;
    }

    /// <summary>
    /// Set health values and update visibility.
    /// - If hp > 0 -> ensure UI is visible.
    /// - If hp <= 0 -> hide UI.
    /// Also bump displayedFill immediately to avoid waiting for lerp to show correct value.
    /// </summary>
    public void SetHealth(int hp, int max)
    {
        currentHp = hp;
        maxHp = max;

        // Immediately update displayedFill to reflect new values (so it doesn't stay at 0 while hidden)
        displayedFill = (maxHp > 0) ? (float)currentHp / maxHp : 0f;
        if (fillImage != null) fillImage.fillAmount = displayedFill;

        // Show when HP > 0, hide when HP <= 0
        if (currentHp <= 0)
            SetVisible(false);
        else
            SetVisible(true);
    }

    public void SetVisible(bool v)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
        }
        else
        {
            gameObject.SetActive(v);
        }
    }
}