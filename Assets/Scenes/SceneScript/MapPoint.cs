using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MapPoint : MonoBehaviour
{
    [Tooltip("ชื่อ scene ของ Map เล็กที่ต้องการเปิดเมื่อกด")]
    public string targetSceneName;

    Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClicked);
    }

    public void OnClicked()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("MapPoint: targetSceneName is empty");
            return;
        }

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader not found in scene. Add SceneLoader prefab to StartScene.");
            return;
        }

        SceneLoader.Instance.LoadScene(targetSceneName);
    }

    void OnDestroy()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnClicked);
    }
}