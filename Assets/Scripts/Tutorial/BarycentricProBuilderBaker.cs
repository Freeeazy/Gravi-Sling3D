using UnityEngine;
using UnityEngine.ProBuilder;

[RequireComponent(typeof(ProBuilderMesh))]
public class BarycentricProBuilderBaker : MonoBehaviour
{
    private void Start()
    {
        ProBuilderMesh pb = GetComponent<ProBuilderMesh>();

        pb.ToMesh();
        pb.Refresh();

        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh source = mf.sharedMesh;

        Vector3[] oldVerts = source.vertices;
        Vector3[] oldNormals = source.normals;
        Vector2[] oldUVs = source.uv;
        int[] oldTris = source.triangles;

        Vector3[] newVerts = new Vector3[oldTris.Length];
        Vector3[] newNormals = new Vector3[oldTris.Length];
        Vector2[] newUVs = new Vector2[oldTris.Length];
        Color[] colors = new Color[oldTris.Length];
        int[] newTris = new int[oldTris.Length];

        for (int i = 0; i < oldTris.Length; i += 3)
        {
            int i0 = oldTris[i];
            int i1 = oldTris[i + 1];
            int i2 = oldTris[i + 2];

            newVerts[i] = oldVerts[i0];
            newVerts[i + 1] = oldVerts[i1];
            newVerts[i + 2] = oldVerts[i2];

            if (oldNormals.Length > 0)
            {
                newNormals[i] = oldNormals[i0];
                newNormals[i + 1] = oldNormals[i1];
                newNormals[i + 2] = oldNormals[i2];
            }

            if (oldUVs.Length > 0)
            {
                newUVs[i] = oldUVs[i0];
                newUVs[i + 1] = oldUVs[i1];
                newUVs[i + 2] = oldUVs[i2];
            }

            colors[i] = new Color(1, 0, 0, 1);
            colors[i + 1] = new Color(0, 1, 0, 1);
            colors[i + 2] = new Color(0, 0, 1, 1);

            newTris[i] = i;
            newTris[i + 1] = i + 1;
            newTris[i + 2] = i + 2;
        }

        Mesh baked = new Mesh();
        baked.name = source.name + "_BarycentricRuntime";
        baked.vertices = newVerts;
        baked.normals = newNormals;
        baked.uv = newUVs;
        baked.colors = colors;
        baked.triangles = newTris;
        baked.RecalculateBounds();

        mf.sharedMesh = baked;
    }
}