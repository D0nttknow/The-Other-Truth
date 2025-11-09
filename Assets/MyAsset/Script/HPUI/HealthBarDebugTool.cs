using System.Reflection;
using UnityEngine;

public class HealthBarDebugTool : MonoBehaviour
{
    // Call from Inspector context menu or from other script
    [ContextMenu("Log All HealthBars")]
    void LogAll()
    {
        var hbs = GameObject.FindObjectsOfType<Transform>();
        int count = 0;
        foreach (var t in hbs)
        {
            if (t.name.Contains("HPBackground"))
            {
                count++;
                Debug.Log($"Found {t.name}: active={t.gameObject.activeSelf}, parent={t.parent?.name}, pos={t.position}, scale={t.localScale}");
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) Debug.Log($"  CanvasGroup: alpha={cg.alpha}, interact={cg.interactable}, blocks={cg.blocksRaycasts}");
                var ui = t.GetComponent<UnityEngine.UI.Image>();
                if (ui != null) Debug.Log($"  Image: source={(ui.sprite != null ? ui.sprite.name : "NULL")}, type={ui.type}, color.a={ui.color.a}");
                var follower = t.GetComponent<HealthBarFollower>();
                if (follower != null)
                {
                    string followerInfo = $"  Follower: target={(follower.target != null ? follower.target.name : "NULL")}, uiCamera={(follower.uiCamera != null ? follower.uiCamera.name : "NULL")}";
                    // try to discover a "hide if behind camera" boolean field/property via reflection (supports different naming)
                    if (TryGetHideBehindFlag(follower, out bool hideFlag, out string memberName))
                    {
                        followerInfo += $", {memberName}={hideFlag}";
                    }
                    Debug.Log(followerInfo);
                }
            }
        }
        Debug.Log($"Checked HPBackground count: {count}");
    }

    [ContextMenu("Force Show All HealthBars")]
    void ForceShowAll()
    {
        foreach (var t in GameObject.FindObjectsOfType<Transform>())
        {
            if (t.name.Contains("HPBackground"))
            {
                t.gameObject.SetActive(true);
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
                var imgs = t.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in imgs)
                {
                    img.enabled = true;
                }

                // if follower has a boolean field/property that hides it when behind camera, try to set it false
                var follower = t.GetComponent<HealthBarFollower>();
                if (follower != null && TrySetHideBehindFlagFalse(follower, out string memberName))
                {
                    Debug.Log($"Forced show and set {memberName}=false on {t.name}");
                }
                else
                {
                    Debug.Log($"Forced show: {t.name}");
                }
            }
        }
    }

    // Reflection helpers: try to find a bool member that sounds like "hide behind camera"
    static bool TryGetHideBehindFlag(object obj, out bool value, out string memberName)
    {
        value = false;
        memberName = null;
        if (obj == null) return false;

        var type = obj.GetType();
        // candidate substrings to look for in member names
        string[] substrings = new[] { "hide", "behind", "camera", "behindCamera", "hideIfBehind", "hideIfBehindCamera", "hideWhenBehind", "hideWhenBehindCamera" };

        // check properties first
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (prop.PropertyType == typeof(bool))
            {
                var lower = prop.Name.ToLower();
                foreach (var s in substrings)
                {
                    if (lower.Contains(s.ToLower()))
                    {
                        try
                        {
                            object v = prop.GetValue(obj, null);
                            if (v is bool b)
                            {
                                value = b;
                                memberName = prop.Name;
                                return true;
                            }
                        }
                        catch { /* ignore get exceptions */ }
                    }
                }
            }
        }

        // then check fields
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(bool))
            {
                var lower = field.Name.ToLower();
                foreach (var s in substrings)
                {
                    if (lower.Contains(s.ToLower()))
                    {
                        try
                        {
                            object v = field.GetValue(obj);
                            if (v is bool b)
                            {
                                value = b;
                                memberName = field.Name;
                                return true;
                            }
                        }
                        catch { /* ignore get exceptions */ }
                    }
                }
            }
        }

        // fallback: no matching member
        return false;
    }

    static bool TrySetHideBehindFlagFalse(object obj, out string memberName)
    {
        memberName = null;
        if (obj == null) return false;

        var type = obj.GetType();
        string[] substrings = new[] { "hide", "behind", "camera", "behindCamera", "hideIfBehind", "hideIfBehindCamera", "hideWhenBehind", "hideWhenBehindCamera" };

        // properties
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!prop.CanWrite) continue;
            if (prop.PropertyType == typeof(bool))
            {
                var lower = prop.Name.ToLower();
                foreach (var s in substrings)
                {
                    if (lower.Contains(s.ToLower()))
                    {
                        try
                        {
                            prop.SetValue(obj, false, null);
                            memberName = prop.Name;
                            return true;
                        }
                        catch { /* ignore set exceptions */ }
                    }
                }
            }
        }

        // fields
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(bool))
            {
                var lower = field.Name.ToLower();
                foreach (var s in substrings)
                {
                    if (lower.Contains(s.ToLower()))
                    {
                        try
                        {
                            field.SetValue(obj, false);
                            memberName = field.Name;
                            return true;
                        }
                        catch { /* ignore set exceptions */ }
                    }
                }
            }
        }

        return false;
    }
}