using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// CharacterInfoPanel (TextMeshPro) - updated to reliably show HP (slider + text)
/// - Waits for UI/layout to finish before computing width/positions
/// - Provides slideRight behavior and recalculation helpers
/// - Ensures HP UI is activated and updated from IHpProvider or fallback ICharacterStat (with reflection for max)
/// - Animates HP changes (smooth decrease/increase) and colorizes hpText when not full
/// - Starts with the bar full on SetTarget, animates only on subsequent changes
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CharacterInfoPanel : MonoBehaviour
{
    [Header("Sliding")]
    public RectTransform panelRect;       // root panel RectTransform (this)
    public Button handleButton;           // small visible handle button
    public RectTransform handleRect;      // handle rect (optional)
    public float handleVisibleWidth = 28f;
    public float slideDuration = 0.25f;
    public bool startClosed = true;

    [Tooltip("If true, the panel will NOT overwrite the anchoredPosition set in the Editor on Start.")]
    public bool respectEditorPosition = true;

    [Tooltip("If true, the panel's Canvas order will be set to last sibling when opening.")]
    public bool bringToFrontOnOpen = true;

    [Tooltip("If true, the panel will slide to the right when closed (closedPos = baseline + (width - visible)).")]
    public bool slideRight = false;

    [Header("Overlay (optional)")]
    public Button overlayButton;

    [Header("Portrait / HP")]
    public Image portraitImage;
    public Image portraitFrameImage;
    public Slider hpSlider;
    public TextMeshProUGUI hpText;

    [Header("Level (progress)")]
    public TextMeshProUGUI levelLabelText;
    public Slider levelSlider;
    public TextMeshProUGUI levelValueText;

    [Header("Attributes (read-only)")]
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI speedText;

    [Header("General Info")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("Canvas override (to keep panel on top)")]
    public bool ensureOverrideCanvas = true;
    public int overrideSortingOrder = 200;

    // HP animation settings
    [Header("HP Animation")]
    [Tooltip("Duration (seconds) to animate HP changes.")]
    public float hpAnimateDuration = 0.35f;

    // runtime
    RectTransform rt;
    Vector2 baselineAnchoredPos;
    Vector2 closedPos;
    Vector2 openedPos;
    Coroutine slideCoroutine;
    bool isOpen = false;

    // target bindings
    GameObject target;
    IHpProvider subscribedHpSource;
    PlayerStat subscribedPlayerStat;

    // cached canvas reference if we create one
    Canvas panelCanvas;

    // coroutine handle for HP animation
    private Coroutine hpAnimCoroutine = null;

    // last known HP to avoid re-animating each frame
    int lastHp = -1;
    int lastMax = -1;

    void Reset()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        if (handleButton == null)
        {
            var b = transform.Find("Handle")?.GetComponent<Button>();
            if (b != null) handleButton = b;
        }
        if (handleRect == null && handleButton != null) handleRect = handleButton.GetComponent<RectTransform>();
    }

    void Awake()
    {
        rt = panelRect != null ? panelRect : GetComponent<RectTransform>();
        if (handleButton != null) handleButton.onClick.AddListener(Toggle);
        if (overlayButton != null) overlayButton.onClick.AddListener(Close);
    }

    IEnumerator Start()
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        baselineAnchoredPos = rt.anchoredPosition;
        RecalculatePositions();

        isOpen = !startClosed;

        if (!respectEditorPosition)
        {
            rt.anchoredPosition = isOpen ? openedPos : closedPos;
        }

        UpdateHandleAnchor();
        EnsureOverrideCanvas();

        if (overlayButton != null) overlayButton.gameObject.SetActive(isOpen);

        Debug.Log($"[CharacterInfoPanel] Start: baseline={baselineAnchoredPos} opened={openedPos.x} closed={closedPos.x} slideRight={slideRight} overrideCanvas={(panelCanvas != null ? "yes" : "no")}");
    }

    void EnsureOverrideCanvas()
    {
        if (!ensureOverrideCanvas) return;

        panelCanvas = GetComponent<Canvas>();
        bool added = false;
        if (panelCanvas == null)
        {
            panelCanvas = gameObject.AddComponent<Canvas>();
            added = true;
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = overrideSortingOrder;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (added)
            Debug.Log($"[CharacterInfoPanel] Added Canvas component and set overrideSorting={panelCanvas.overrideSorting} order={panelCanvas.sortingOrder}");
        else
            Debug.Log($"[CharacterInfoPanel] Using existing Canvas and set overrideSorting={panelCanvas.overrideSorting} order={panelCanvas.sortingOrder}");
    }

    void UpdateHandleAnchor()
    {
        if (handleRect == null) return;

        handleRect.pivot = new Vector2(0.5f, 0.5f);
        if (slideRight)
        {
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.anchoredPosition = new Vector2(-handleVisibleWidth * 0.5f, 0f);
        }
        else
        {
            handleRect.anchorMin = new Vector2(1f, 0.5f);
            handleRect.anchorMax = new Vector2(1f, 0.5f);
            handleRect.anchoredPosition = new Vector2(handleVisibleWidth * 0.5f, 0f);
        }
    }

    public void RecalculatePositions(bool forceBaselineFromCurrent = false)
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        float w = Mathf.Max(0.0001f, rt.rect.width);

        if (forceBaselineFromCurrent)
        {
            baselineAnchoredPos = rt.anchoredPosition;
        }
        openedPos = new Vector2(baselineAnchoredPos.x, baselineAnchoredPos.y);

        if (slideRight)
            closedPos = new Vector2(baselineAnchoredPos.x + (w - handleVisibleWidth), baselineAnchoredPos.y);
        else
            closedPos = new Vector2(baselineAnchoredPos.x - (w - handleVisibleWidth), baselineAnchoredPos.y);
    }

    void Update()
    {
        if (target != null) RefreshFromTarget();
    }

    public void Toggle()
    {
        RecalculatePositions(false);

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(!isOpen));
    }

    public void Open()
    {
        if (isOpen) return;
        if (bringToFrontOnOpen && rt != null) rt.SetAsLastSibling();

        EnsureOverrideCanvas();

        if (overlayButton != null) overlayButton.gameObject.SetActive(true);
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        RecalculatePositions(false);
        slideCoroutine = StartCoroutine(SlideTo(true));
    }

    public void Close()
    {
        if (!isOpen) return;
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        RecalculatePositions(false);
        slideCoroutine = StartCoroutine(SlideTo(false));
    }

    IEnumerator SlideTo(bool open)
    {
        RecalculatePositions(false);

        isOpen = open;
        Vector2 from = rt.anchoredPosition;
        Vector2 to = open ? openedPos : closedPos;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            rt.anchoredPosition = Vector2.Lerp(from, to, f);
            yield return null;
        }

        rt.anchoredPosition = to;
        slideCoroutine = null;

        if (!isOpen && overlayButton != null) overlayButton.gameObject.SetActive(false);

        Debug.Log($"[CharacterInfoPanel] Slide finished. isOpen={isOpen} anchoredPos={rt.anchoredPosition} openedX={openedPos.x} closedX={closedPos.x}");
    }

    // --- HP display helper (animated) ---
    /// <summary>
    /// Set HP UI (slider + text). Ensures slider is active and text formatted.
    /// animate=true -> smooth animation from current slider.value to target
    /// animate=false -> set immediately (used for initial full display)
    /// </summary>
    public void ShowHp(int current, int max, bool animate = true)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (hpSlider != null)
        {
            if (!hpSlider.gameObject.activeSelf) hpSlider.gameObject.SetActive(true);
            // always ensure maxValue updated
            hpSlider.maxValue = max;

            // If animate is false (initial setup), set value to max then update text
            if (!animate)
            {
                // Make bar full at start then set last known values accordingly
                hpSlider.value = max;
                UpdateHpText(max, max);
                lastHp = max;
                lastMax = max;
                // ensure fill color
                EnsureFillColorRed();
                return;
            }

            // animate only if the requested value differs from last known
            if (lastHp == current && lastMax == max)
            {
                // nothing changed
                return;
            }

            // stop any running animation
            if (hpAnimCoroutine != null) StopCoroutine(hpAnimCoroutine);
            hpAnimCoroutine = StartCoroutine(AnimateHp(hpSlider.value, current, hpAnimateDuration));

            // update last values (will be finalized by coroutine)
            lastHp = current;
            lastMax = max;
        }
        else
        {
            // no slider -> update text immediately
            UpdateHpText(current, max);
            lastHp = current;
            lastMax = max;
        }

        if (hpText != null)
        {
            if (!hpText.gameObject.activeSelf) hpText.gameObject.SetActive(true);
        }

        EnsureFillColorRed();
    }

    IEnumerator AnimateHp(float fromValue, float toValue, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            float value = Mathf.Lerp(fromValue, toValue, Mathf.SmoothStep(0f, 1f, t));
            if (hpSlider != null) hpSlider.value = value;
            UpdateHpText(Mathf.RoundToInt(value), (hpSlider != null ? Mathf.RoundToInt(hpSlider.maxValue) : Mathf.Max(1, Mathf.RoundToInt(value))));
            yield return null;
        }

        if (hpSlider != null) hpSlider.value = toValue;
        UpdateHpText(Mathf.RoundToInt(toValue), (hpSlider != null ? Mathf.RoundToInt(hpSlider.maxValue) : Mathf.Max(1, Mathf.RoundToInt(toValue))));
        hpAnimCoroutine = null;
    }

    void UpdateHpText(int cur, int max)
    {
        if (hpText == null) return;
        hpText.text = $"{cur}/{max}";

        // colorize: red when below max, white when full (customize as needed)
        if (cur < max) hpText.color = Color.red;
        else hpText.color = Color.white;
    }

    void EnsureFillColorRed()
    {
        if (hpSlider != null && hpSlider.fillRect != null)
        {
            var fillImage = hpSlider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = Color.red;
            }
        }
    }

    // Set the panel to show info for this GameObject
    public void SetTarget(GameObject go)
    {
        UnsubscribeFromTarget();

        target = go;
        Debug.Log($"[CharacterInfoPanel] SetTarget called for: {(go != null ? go.name : "null")}");

        if (target == null)
        {
            ClearUI();
            return;
        }

        // read static attributes (one-time) first
        var cs2 = target.GetComponent<ICharacterStat>();
        if (cs2 != null)
        {
            if (nameText != null) nameText.text = cs2.Name ?? target.name;
            if (atkText != null) atkText.text = $"ATK: {cs2.atk}";
            if (defText != null) defText.text = $"DEF: {cs2.def}";
            if (speedText != null) speedText.text = $"SPD: {cs2.speed}";
        }

        // Try to initialize HP display as FULL immediately (do not animate)
        var hpProv = target.GetComponent<IHpProvider>();
        if (hpProv != null)
        {
            subscribedHpSource = hpProv;
            try { hpProv.OnHpChanged += OnTargetHpChanged; } catch { }
            try
            {
                int cur = hpProv.CurrentHp;
                int max = hpProv.MaxHp;
                // show as full at start
                ShowHp(max, max, animate: false);
                // but if current < max (already damaged) animate to actual current for the first time
                if (cur < max)
                {
                    // animate down to current
                    ShowHp(cur, max, animate: true);
                    Debug.Log($"[CharacterInfoPanel] Initial HP from IHpProvider (damaged): {cur}/{max}");
                }
                else
                {
                    Debug.Log($"[CharacterInfoPanel] Initial HP from IHpProvider (full): {cur}/{max}");
                }
            }
            catch
            {
                // fallthrough - will try other fallbacks below
            }
        }
        else
        {
            var cs = target.GetComponent<ICharacterStat>();
            if (cs != null)
            {
                int cur = 0;
                try { cur = cs.hp; } catch { cur = 0; }
                int max = TryGetMaxHpFromCharacter(cs, cur);
                // show full at start
                ShowHp(max, max, animate: false);
                if (cur < max)
                {
                    ShowHp(cur, max, animate: true);
                    if (hpText != null) Debug.Log($"[CharacterInfoPanel] Initial HP from ICharacterStat (damaged): {cur}/{max}");
                }
                else
                {
                    if (hpText != null) Debug.Log($"[CharacterInfoPanel] Initial HP from ICharacterStat (full): {cur}/{max}");
                }
            }
            else
            {
                if (hpSlider != null) hpSlider.gameObject.SetActive(false);
                if (hpText != null) hpText.gameObject.SetActive(false);
            }
        }

        // subscribe to exp/level events if available
        var ps = target.GetComponent<PlayerStat>();
        if (ps != null)
        {
            subscribedPlayerStat = ps;
            try { ps.OnExpChanged += OnTargetExpChanged; } catch { }
            try { ps.OnLevelUp += OnTargetLevelUp; } catch { }
            OnTargetExpChanged(ps.currentExp, ps.ExpToNext);
        }
    }

    int TryGetMaxHpFromCharacter(object csObj, int fallback)
    {
        if (csObj == null) return Mathf.Max(1, fallback);
        var t = csObj.GetType();

        string[] propNames = { "maxHp", "MaxHp", "hpMax", "HpMax", "maxHP", "MaxHP", "max_health", "maxHealth" };
        foreach (var n in propNames)
        {
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
            if (p != null)
            {
                try { return Convert.ToInt32(p.GetValue(csObj)); } catch { }
            }
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
            {
                try { return Convert.ToInt32(f.GetValue(csObj)); } catch { }
            }
        }

        return Mathf.Max(1, fallback);
    }

    void UnsubscribeFromTarget()
    {
        if (subscribedHpSource != null)
        {
            try { subscribedHpSource.OnHpChanged -= OnTargetHpChanged; } catch { }
            subscribedHpSource = null;
        }
        if (subscribedPlayerStat != null)
        {
            try { subscribedPlayerStat.OnExpChanged -= OnTargetExpChanged; } catch { }
            try { subscribedPlayerStat.OnLevelUp -= OnTargetLevelUp; } catch { }
            subscribedPlayerStat = null;
        }
    }

    void OnTargetHpChanged(int current, int uiMax)
    {
        // animate down/up only if changed
        if (current != lastHp || uiMax != lastMax)
        {
            ShowHp(current, uiMax, animate: true);
            Debug.Log($"[CharacterInfoPanel] OnTargetHpChanged -> {current}/{uiMax}");
        }
    }

    void OnTargetExpChanged(int currentExp, int reqExp)
    {
        if (levelSlider != null)
        {
            levelSlider.gameObject.SetActive(true);
            levelSlider.maxValue = Mathf.Max(1, reqExp);
            levelSlider.value = Mathf.Clamp(currentExp, 0, reqExp);
        }
        if (levelValueText != null && subscribedPlayerStat != null)
        {
            float pct = reqExp > 0 ? (100f * currentExp / reqExp) : 0f;
            levelValueText.text = $"Lv {subscribedPlayerStat.level} — {Mathf.RoundToInt(pct)}% ({currentExp}/{reqExp})";
        }
    }

    void OnTargetLevelUp(int oldLevel, int newLevel)
    {
        if (levelValueText != null)
        {
            levelValueText.text = $"Lv {newLevel}";
        }
    }

    void ClearUI()
    {
        if (portraitImage != null) portraitImage.sprite = null;

        if (hpSlider != null) { hpSlider.maxValue = 1; hpSlider.value = 0; hpSlider.gameObject.SetActive(false); }
        if (hpText != null) { hpText.text = ""; hpText.gameObject.SetActive(false); }

        if (levelSlider != null) { levelSlider.maxValue = 1; levelSlider.value = 0; levelSlider.gameObject.SetActive(false); }
        if (levelValueText != null) levelValueText.text = "";
        if (nameText != null) nameText.text = "";
        if (descriptionText != null) descriptionText.text = "";
        if (atkText != null) atkText.text = "";
        if (defText != null) defText.text = "";
        if (speedText != null) speedText.text = "";
    }

    void RefreshFromTarget()
    {
        if (target == null) return;

        var hpProv = target.GetComponent<IHpProvider>();
        if (hpProv != null)
        {
            if (hpProv.CurrentHp != lastHp || hpProv.MaxHp != lastMax)
                ShowHp(hpProv.CurrentHp, hpProv.MaxHp, animate: true);
        }
        else
        {
            var cs = target.GetComponent<ICharacterStat>();
            if (cs != null && hpSlider != null)
            {
                int cur = cs.hp;
                int max = TryGetMaxHpFromCharacter(cs, cur);
                if (cur != lastHp || max != lastMax)
                    ShowHp(cur, max, animate: true);
            }
        }

        var ps = target.GetComponent<PlayerStat>();
        if (ps != null)
        {
            OnTargetExpChanged(ps.currentExp, ps.ExpToNext);
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromTarget();
        if (handleButton != null) handleButton.onClick.RemoveListener(Toggle);
        if (overlayButton != null) overlayButton.onClick.RemoveListener(Close);
    }

    public void SetAnchoredPosition(Vector2 anchoredPos)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        baselineAnchoredPos = rt.anchoredPosition;
        RecalculatePositions(false);
    }

    public void MoveBy(float dx)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        SetAnchoredPosition(rt.anchoredPosition + new Vector2(dx, 0f));
    }

    public void DebugShowBringToFront()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one;
        if (overlayButton != null) overlayButton.gameObject.SetActive(isOpen);
        Debug.Log($"CharacterInfoPanel DebugShowBringToFront anchoredPos={rt.anchoredPosition} size={rt.sizeDelta} openedX={openedPos.x} closedX={closedPos.x}");
    }
}