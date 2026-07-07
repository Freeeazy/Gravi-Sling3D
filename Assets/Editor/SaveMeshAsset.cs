using UnityEngine;
using UnityEditor;
using System.IO;

public static class SaveMeshAsset
{
    [MenuItem("CONTEXT/MeshFilter/Save Mesh As Asset")]
    private static void SaveMeshFromContext(MenuCommand command)
    {
        MeshFilter meshFilter = command.context as MeshFilter;

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No MeshFilter or sharedMesh found.");
            return;
        }

        SaveMesh(meshFilter, bakeWorldTransform: false);
    }

    [MenuItem("CONTEXT/MeshFilter/Save Mesh As Asset - Bake World Transform")]
    private static void SaveBakedMeshFromContext(MenuCommand command)
    {
        MeshFilter meshFilter = command.context as MeshFilter;

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No MeshFilter or sharedMesh found.");
            return;
        }

        SaveMesh(meshFilter, bakeWorldTransform: true);
    }

    [MenuItem("Tools/Mesh/Save Selected MeshFilter As Asset")]
    private static void SaveSelectedMesh()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("No GameObject selected.");
            return;
        }

        MeshFilter meshFilter = selected.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("Selected GameObject has no MeshFilter with a mesh.");
            return;
        }

        SaveMesh(meshFilter, bakeWorldTransform: false);
    }

    private static void SaveMesh(MeshFilter meshFilter, bool bakeWorldTransform)
    {
        Mesh sourceMesh = meshFilter.sharedMesh;

        string defaultName = meshFilter.gameObject.name + "_Mesh.asset";

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Mesh Asset",
            defaultName,
            "asset",
            "Choose where to save the mesh asset."
        );

        if (string.IsNullOrEmpty(path))
            return;

        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.name = Path.GetFileNameWithoutExtension(path);

        if (bakeWorldTransform)
        {
            BakeWorldTransformIntoMesh(newMesh, meshFilter.transform);
        }

        AssetDatabase.CreateAsset(newMesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newMesh;

        Debug.Log($"Saved mesh asset to: {path}");
    }

    private static void BakeWorldTransformIntoMesh(Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        Matrix4x4 matrix = transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = matrix.inverse.transpose;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
        }

        if (normals != null && normals.Length == vertices.Length)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            }

            mesh.normals = normals;
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }
}