using UnityEditor;
using UnityEngine;

/// <summary>
/// Copies serialized properties from TurnManager components to TurnBaseSystem on the same GameObject,
/// then removes the old TurnManager component instance. Run BEFORE deleting legacy TurnManager class file.
/// </summary>
public class MigrateTurnManagerToTurnBaseSystem : EditorWindow
{
    [MenuItem("Tools/Migrate TurnManager -> TurnBaseSystem")]
    static void Run()
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        int migrated = 0;
        foreach (var go in all)
        {
            // skip asset definitions, keep scene & prefab instances
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go)) && PrefabUtility.IsPartOfPrefabAsset(go)) continue;

            var old = go.GetComponent("TurnManager"); // string-based lookup so it works even if type is missing
            if (old != null)
            {
                var tbs = go.GetComponent<TurnBaseSystem>();
                if (tbs == null) tbs = go.AddComponent<TurnBaseSystem>();

                var soOld = new SerializedObject((UnityEngine.Object)old);
                var soNew = new SerializedObject(tbs);

                var iter = soOld.GetIterator();
                iter.Next(true);
                while (iter.NextVisible(false))
                {
                    if (iter.name == "m_Script") continue;
                    var prop = soNew.FindProperty(iter.name);
                    if (prop != null)
                    {
                        prop.serializedObject.Update();
                        prop.serializedObject.CopyFromSerializedProperty(iter);
                        prop.serializedObject.ApplyModifiedProperties();
                    }
                }

                Object.DestroyImmediate((UnityEngine.Object)old, true);
                migrated++;
            }
        }
        Debug.Log($"MigrateTurnManagerToTurnBaseSystem: migrated {migrated} GameObjects.");
    }
}