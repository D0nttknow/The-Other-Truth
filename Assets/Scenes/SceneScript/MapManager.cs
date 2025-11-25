using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    [Header("Data/Prefabs")]
    public MapData mapData;                // ถ้าอยากผูกโดยตรง
    public MapDatabase mapDatabase;        // หรือผูก database แล้วโหลดโดย id
    public GameObject mapPointPrefab;
    public RectTransform parent;           // RectTransform ของ Map Image

    // เรียกเพื่อสร้างจุดจาก MapData ปัจจุบัน
    void Start()
    {
        if (mapData != null)
            BuildFromMapData(mapData);
    }

    public void LoadMapById(string mapId)
    {
        if (mapDatabase == null)
        {
            Debug.LogWarning("MapManager: No MapDatabase assigned.");
            return;
        }

        var md = mapDatabase.GetById(mapId);
        if (md == null)
        {
            Debug.LogWarning($"MapManager: MapData not found for id '{mapId}'");
            return;
        }

        // หากต้องการลบจุดเก่า:
        ClearMapPoints();

        mapData = md;
        BuildFromMapData(mapData);
    }

    public void BuildFromMapData(MapData md)
    {
        if (md == null || mapPointPrefab == null || parent == null) return;

        foreach (var p in md.points)
        {
            var go = Instantiate(mapPointPrefab, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = p.anchoredPosition;
            var mp = go.GetComponent<MapPoint>();
            if (mp != null) mp.targetSceneName = p.targetScene;
            var img = go.GetComponent<Image>();
            if (img != null && p.icon != null) img.sprite = p.icon;
            go.name = "Point_" + p.name;
        }
    }

    void ClearMapPoints()
    {
        // สมมติว่า prefab ทุกตัวเป็นลูกของ parent — ลบทั้งหมดก่อนสร้างใหม่
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            // ปลอดภัย: อย่าไปลบ Map background หรืออื่น ๆ ถ้าซ้อนอยู่ด้วย
            // คุณอาจจะใช้ tag/named container แยกเฉพาะ container สำหรับ points
            Destroy(child.gameObject);
        }
    }
}