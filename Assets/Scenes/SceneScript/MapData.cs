using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    [System.Serializable]
    public class Point
    {
        public string name;
        public Vector2 anchoredPosition; // ¾Ô¡à«Åº¹ parent RectTransform
        public string targetScene;
        public Sprite icon;
    }

    public Point[] points;
}