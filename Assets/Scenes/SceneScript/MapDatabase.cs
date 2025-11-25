using UnityEngine;

[CreateAssetMenu(fileName = "MapDatabase", menuName = "Map/MapDatabase")]
public class MapDatabase : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string mapId;      // เช่น "MainMap", "Town", "Dungeon_01"
        public MapData mapData;
    }

    public Entry[] entries;

    // helper หา MapData โดย id
    public MapData GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var e in entries)
        {
            if (e != null && e.mapId == id) return e.mapData;
        }
        return null;
    }
}