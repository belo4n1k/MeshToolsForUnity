using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class CombineChildMeshesByColor
{
    [MenuItem("Tools/Combine Child Meshes By Color")]
    public static void CombineSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select a root GameObject first.");
            return;
        }

        GameObject root = Selection.activeGameObject;
        Transform rootT = root.transform;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("No MeshFilters found under root.");
            return;
        }

        // Dictionary: key (shader+color) → list of CombineInstance
        Dictionary<string, List<CombineInstance>> groups = new Dictionary<string, List<CombineInstance>>();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();

        Matrix4x4 rootWorldToLocal = rootT.worldToLocalMatrix;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            var mats = mr.sharedMaterials;
            for (int si = 0; si < mf.sharedMesh.subMeshCount; si++)
            {
                Material mat = (si < mats.Length) ? mats[si] : null;
                string key = MakeColorKey(mat);

                if (!groups.ContainsKey(key))
                    groups[key] = new List<CombineInstance>();
                if (!materials.ContainsKey(key))
                    materials[key] = mat; // take the first material we encounter

                var ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = si,
                    transform = rootWorldToLocal * mf.transform.localToWorldMatrix
                };
                groups[key].Add(ci);
            }
        }

        if (groups.Count == 0)
        {
            Debug.LogWarning("No valid submeshes found.");
            return;
        }

        // For each color, perform combine
        List<Mesh> partialMeshes = new List<Mesh>();
        List<Material> orderedMaterials = new List<Material>();

        foreach (var kvp in groups)
        {
            var ciList = kvp.Value;
            Mesh m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.CombineMeshes(ciList.ToArray(), true, true); // mergeSubMeshes = true (один сабмеш на цвет)
            partialMeshes.Add(m);
            orderedMaterials.Add(materials[kvp.Key]);
        }

        // Merge final mesh with submeshes = unique colors
        var finalCombines = new List<CombineInstance>();
        for (int i = 0; i < partialMeshes.Count; i++)
        {
            finalCombines.Add(new CombineInstance
            {
                mesh = partialMeshes[i],
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
        }

        Mesh combined = new Mesh { name = root.name + "_CombinedByColor" };
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(finalCombines.ToArray(), false, false);

        // Save into Assets
        string folderPath = "Assets/Meshes";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets", "Meshes");
        string meshPath = Path.Combine(folderPath, combined.name + ".asset");
        AssetDatabase.CreateAsset(combined, meshPath);

        // Create a new object for the result
        GameObject combinedGO = new GameObject(root.name + "_CombinedByColor");
        combinedGO.transform.SetParent(root.transform, false);

        var mfRoot = combinedGO.AddComponent<MeshFilter>();
        mfRoot.sharedMesh = combined;

        var mrRoot = combinedGO.AddComponent<MeshRenderer>();
        mrRoot.sharedMaterials = orderedMaterials.ToArray();

        // Delete old childes
        var toDelete = new List<GameObject>();
        for (int i = 0; i < rootT.childCount; i++)
            toDelete.Add(rootT.GetChild(i).gameObject);
        foreach (var go in toDelete)
        {
            if (go != combinedGO) // do not delete the newly combined object
                Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Combined {meshFilters.Length} MeshFilters into {groups.Count} color groups. Saved at {meshPath}");
    }

    static string MakeColorKey(Material mat)
    {
        if (mat == null) return "NULL";
        string shader = mat.shader != null ? mat.shader.name : "NoShader";
        Color c = mat.HasProperty("_Color") ? mat.color : Color.white;
        // round color to 3 decimal places
        string colKey = $"{Mathf.Round(c.r*1000f)/1000f}_{Mathf.Round(c.g*1000f)/1000f}_{Mathf.Round(c.b*1000f)/1000f}_{Mathf.Round(c.a*1000f)/1000f}";
        return shader + "_" + colKey;
    }
}
