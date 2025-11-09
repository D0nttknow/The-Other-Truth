using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ปรับปรุง StartMenu: เพิ่มการตรวจสอบ SceneLoader, fallback เป็น SceneManager,
/// ปุ่มเปิด/ปิด Settings (ผูก settingsPanel ใน Inspector) และรองรับ Quit ใน Editor
/// </summary>
public class StartMenu : MonoBehaviour
{
    [Header("Settings Panel (optional)")]
    [Tooltip("ลาก Settings Panel ที่ต้องการให้เปิดเมื่อกดปุ่ม Settings")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene")]
    [Tooltip("ชื่อ scene หลักที่จะโหลด (fallback ถ้า SceneLoader ไม่มี)")]
    [SerializeField] private string mainMapSceneName = "MainMapScene";

    // ปุ่ม Start จะเรียกเมธอดนี้ (hook ใน Inspector)
    public void OnStartButtonPressed()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(mainMapSceneName);
            return;
        }

        // fallback ถ้าไม่มี SceneLoader (เช่น ลืมวาง prefab หรือกำลังทดสอบอย่างง่าย)
        Debug.LogWarning("SceneLoader not found — falling back to SceneManager.LoadScene");
        SceneManager.LoadScene(mainMapSceneName);
    }

    // ปุ่ม Settings — เปิด Panel ที่ผูกไว้
    public void OnSettingsButtonPressed()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("StartMenu: settingsPanel not assigned in Inspector.");
        }
    }

    // ปิด settings (ผูกให้ปุ่ม Close ใน panel เรียกเมธอดนี้)
    public void OnCloseSettingsPressed()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        // ใน Editor ให้หยุด Play mode แทนการปิดแอป
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}