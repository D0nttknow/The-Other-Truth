using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIFader : MonoBehaviour
{
    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
    }

    public IEnumerator FadeOut(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = 1 - Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 0;
    }

    public IEnumerator FadeIn(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1;
    }
}