using UnityEditor;
using UnityEngine;

public static class FindMissingScripts
{
    [MenuItem("Tools/Diagnostics/Find Missing Scripts (Open Scene)")]
    public static void FindMissingInOpenScene()
    {
        int missing = 0;
        var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);


        foreach (var go in all)
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    missing++;
                    Debug.LogWarning($"Missing script on: {GetPath(go)}", go);
                }
            }
        }

        Debug.Log($"Done. Missing script components found in open scene: {missing}");
    }

    [MenuItem("Tools/Diagnostics/Find Missing Scripts (Prefabs)")]
    public static void FindMissingInPrefabs()
    {
        int missing = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var comps = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    missing++;
                    Debug.LogWarning($"Missing script in prefab: {path}", prefab);
                    break; // one log per prefab
                }
            }
        }

        Debug.Log($"Done. Prefabs with missing scripts found: {missing}");
    }

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
