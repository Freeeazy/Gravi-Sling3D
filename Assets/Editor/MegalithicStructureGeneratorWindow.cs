using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class MegalithicStructureGeneratorWindow : EditorWindow
{
    enum ShapeType { Cube, Pyramid, Sphere }

    ShapeType shape = ShapeType.Cube;

    [Header("Base Shape")]
    float size = 40f;
    int subdivisions = 3;
    bool addDiagonals = true;

    [Header("Recursive Struts")]
    int recursionDepth = 3;
    float innerScalePerLevel = 0.58f;
    bool connectRecursiveLevels = true;
    int levelConnectionStep = 2;

    [Header("Beam Settings")]
    float beamRadius = 0.8f;
    float beamRadiusFalloff = 0.65f;
    int beamSides = 8;

    [Header("Node Settings")]
    bool addNodes = true;
    float nodeScale = 1.7f;
    float nodeRadiusFalloff = 0.7f;

    [Header("Output")]
    Material material;
    bool saveMeshAsset = true;
    string saveFolder = "Assets/GeneratedMegaliths";

    struct PointData
    {
        public Vector3 position;
        public float radius;

        public PointData(Vector3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    struct EdgeData
    {
        public int a;
        public int b;
        public float radius;

        public EdgeData(int a, int b, float radius)
        {
            this.a = a;
            this.b = b;
            this.radius = radius;
        }
    }

    [MenuItem("Tools/Gravi-Sling/Megalithic Structure Generator")]
    public static void Open()
    {
        GetWindow<MegalithicStructureGeneratorWindow>("Megalith Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Megalithic Hollow Structure Generator", EditorStyles.boldLabel);

        shape = (ShapeType)EditorGUILayout.EnumPopup("Shape", shape);

        GUILayout.Space(8);
        GUILayout.Label("Base Shape", EditorStyles.boldLabel);

        size = EditorGUILayout.FloatField("Overall Size", size);
        subdivisions = EditorGUILayout.IntSlider("Subdivisions", subdivisions, 1, 8);
        addDiagonals = EditorGUILayout.Toggle("Add Diagonal Struts", addDiagonals);

        GUILayout.Space(8);
        GUILayout.Label("Recursive Struts", EditorStyles.boldLabel);

        recursionDepth = EditorGUILayout.IntSlider("Recursion Depth", recursionDepth, 1, 6);
        innerScalePerLevel = EditorGUILayout.Slider("Inner Scale Per Level", innerScalePerLevel, 0.2f, 0.9f);
        connectRecursiveLevels = EditorGUILayout.Toggle("Connect Recursive Levels", connectRecursiveLevels);
        levelConnectionStep = EditorGUILayout.IntSlider("Level Connection Step", levelConnectionStep, 1, 8);

        GUILayout.Space(8);
        GUILayout.Label("Beam Settings", EditorStyles.boldLabel);

        beamRadius = EditorGUILayout.FloatField("Main Beam Radius", beamRadius);
        beamRadiusFalloff = EditorGUILayout.Slider("Beam Radius Falloff", beamRadiusFalloff, 0.2f, 1f);
        beamSides = EditorGUILayout.IntSlider("Beam Sides", beamSides, 5, 24);

        GUILayout.Space(8);
        GUILayout.Label("Node Settings", EditorStyles.boldLabel);

        addNodes = EditorGUILayout.Toggle("Add Nodes", addNodes);
        nodeScale = EditorGUILayout.FloatField("Node Scale", nodeScale);
        nodeRadiusFalloff = EditorGUILayout.Slider("Node Radius Falloff", nodeRadiusFalloff, 0.2f, 1f);

        GUILayout.Space(8);
        GUILayout.Label("Output", EditorStyles.boldLabel);

        material = (Material)EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
        saveMeshAsset = EditorGUILayout.Toggle("Save Mesh Asset", saveMeshAsset);
        saveFolder = EditorGUILayout.TextField("Save Folder", saveFolder);

        GUILayout.Space(12);

        if (GUILayout.Button("Generate Megalith"))
        {
            Generate();
        }
    }

    void Generate()
    {
        GameObject root = new GameObject($"Megalith_{shape}_Recursive");
        Undo.RegisterCreatedObjectUndo(root, "Generate Recursive Megalith");

        if (connectRecursiveLevels)
        {
            GenerateAsSingleConnectedObject(root);
        }
        else
        {
            GenerateAsSeparateRecursiveObjects(root);
        }

        Selection.activeGameObject = root;
    }

    void GenerateAsSingleConnectedObject(GameObject root)
    {
        List<PointData> points = new List<PointData>();
        List<EdgeData> edges = new List<EdgeData>();
        List<List<int>> shellPointIndices = new List<List<int>>();

        for (int level = 0; level < recursionDepth; level++)
        {
            float shellScale = Mathf.Pow(innerScalePerLevel, level);
            float radius = beamRadius * Mathf.Pow(beamRadiusFalloff, level);
            float nodeRadius = beamRadius * nodeScale * Mathf.Pow(nodeRadiusFalloff, level);

            List<int> shellIndices = new List<int>();

            BuildShell(points, edges, shellIndices, shellScale, radius, nodeRadius);

            shellPointIndices.Add(shellIndices);

            if (level > 0)
            {
                ConnectShells(
                    shellPointIndices[level - 1],
                    shellPointIndices[level],
                    edges,
                    radius
                );
            }
        }

        Mesh mesh = BuildCombinedMesh(points, edges);
        FinalizeGeneratedObject(root, mesh, root.name);
    }

    void GenerateAsSeparateRecursiveObjects(GameObject root)
    {
        for (int level = 0; level < recursionDepth; level++)
        {
            float shellScale = Mathf.Pow(innerScalePerLevel, level);
            float radius = beamRadius * Mathf.Pow(beamRadiusFalloff, level);
            float nodeRadius = beamRadius * nodeScale * Mathf.Pow(nodeRadiusFalloff, level);

            List<PointData> points = new List<PointData>();
            List<EdgeData> edges = new List<EdgeData>();
            List<int> shellIndices = new List<int>();

            BuildShell(points, edges, shellIndices, shellScale, radius, nodeRadius);

            Mesh mesh = BuildCombinedMesh(points, edges);

            GameObject levelObject = new GameObject($"Level_{level}_Scale_{shellScale:F2}");
            levelObject.transform.SetParent(root.transform, false);

            FinalizeGeneratedObject(levelObject, mesh, $"{root.name}_Level_{level}");
        }
    }

    void BuildShell(
    List<PointData> points,
    List<EdgeData> edges,
    List<int> shellIndices,
    float shellScale,
    float radius,
    float nodeRadius
)
    {
        switch (shape)
        {
            case ShapeType.Cube:
                BuildCubeShell(points, edges, shellIndices, shellScale, radius, nodeRadius);
                break;

            case ShapeType.Pyramid:
                BuildPyramidShell(points, edges, shellIndices, shellScale, radius, nodeRadius);
                break;

            case ShapeType.Sphere:
                BuildSphereShell(points, edges, shellIndices, shellScale, radius, nodeRadius);
                break;
        }
    }

    void FinalizeGeneratedObject(GameObject target, Mesh mesh, string assetName)
    {
        MeshFilter mf = target.AddComponent<MeshFilter>();
        MeshRenderer mr = target.AddComponent<MeshRenderer>();
        MeshCollider mc = target.AddComponent<MeshCollider>();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;

        if (material != null)
            mr.sharedMaterial = material;
        else
            mr.sharedMaterial = new Material(Shader.Find("Standard"));

        if (saveMeshAsset)
            SaveMeshAsset(mesh, assetName);
    }

    void BuildCubeShell(
        List<PointData> points,
        List<EdgeData> edges,
        List<int> shellIndices,
        float scale,
        float radius,
        float nodeRadius
    )
    {
        float h = size * 0.5f * scale;

        Vector3[] corners =
        {
            new Vector3(-h,-h,-h),
            new Vector3( h,-h,-h),
            new Vector3( h,-h, h),
            new Vector3(-h,-h, h),

            new Vector3(-h, h,-h),
            new Vector3( h, h,-h),
            new Vector3( h, h, h),
            new Vector3(-h, h, h),
        };

        int[] cubeEdges =
        {
            0,1, 1,2, 2,3, 3,0,
            4,5, 5,6, 6,7, 7,4,
            0,4, 1,5, 2,6, 3,7
        };

        for (int i = 0; i < cubeEdges.Length; i += 2)
        {
            AddSubdividedEdge(
                points,
                edges,
                shellIndices,
                corners[cubeEdges[i]],
                corners[cubeEdges[i + 1]],
                radius,
                nodeRadius
            );
        }

        if (addDiagonals)
        {
            AddSubdividedEdge(points, edges, shellIndices, corners[0], corners[6], radius * 0.75f, nodeRadius);
            AddSubdividedEdge(points, edges, shellIndices, corners[1], corners[7], radius * 0.75f, nodeRadius);
            AddSubdividedEdge(points, edges, shellIndices, corners[2], corners[4], radius * 0.75f, nodeRadius);
            AddSubdividedEdge(points, edges, shellIndices, corners[3], corners[5], radius * 0.75f, nodeRadius);
        }
    }

    void BuildPyramidShell(
        List<PointData> points,
        List<EdgeData> edges,
        List<int> shellIndices,
        float scale,
        float radius,
        float nodeRadius
    )
    {
        float h = size * 0.5f * scale;

        Vector3 a = new Vector3(-h, -h, -h);
        Vector3 b = new Vector3(h, -h, -h);
        Vector3 c = new Vector3(h, -h, h);
        Vector3 d = new Vector3(-h, -h, h);
        Vector3 top = new Vector3(0, h, 0);

        AddSubdividedEdge(points, edges, shellIndices, a, b, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, b, c, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, c, d, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, d, a, radius, nodeRadius);

        AddSubdividedEdge(points, edges, shellIndices, a, top, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, b, top, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, c, top, radius, nodeRadius);
        AddSubdividedEdge(points, edges, shellIndices, d, top, radius, nodeRadius);

        if (addDiagonals)
        {
            AddSubdividedEdge(points, edges, shellIndices, a, c, radius * 0.75f, nodeRadius);
            AddSubdividedEdge(points, edges, shellIndices, b, d, radius * 0.75f, nodeRadius);
        }
    }

    void BuildSphereShell(
        List<PointData> points,
        List<EdgeData> edges,
        List<int> shellIndices,
        float scale,
        float radius,
        float nodeRadius
    )
    {
        int latCount = Mathf.Max(4, subdivisions + 3);
        int lonCount = Mathf.Max(8, subdivisions * 4);

        float r = size * 0.5f * scale;

        int[,] index = new int[latCount + 1, lonCount];

        for (int lat = 0; lat <= latCount; lat++)
        {
            float theta = Mathf.PI * lat / latCount;
            float y = Mathf.Cos(theta) * r;
            float ringRadius = Mathf.Sin(theta) * r;

            for (int lon = 0; lon < lonCount; lon++)
            {
                float phi = Mathf.PI * 2f * lon / lonCount;

                Vector3 p = new Vector3(
                    Mathf.Cos(phi) * ringRadius,
                    y,
                    Mathf.Sin(phi) * ringRadius
                );

                int id = AddPoint(points, p, nodeRadius);
                index[lat, lon] = id;
                shellIndices.Add(id);
            }
        }

        for (int lat = 0; lat <= latCount; lat++)
        {
            for (int lon = 0; lon < lonCount; lon++)
            {
                int nextLon = (lon + 1) % lonCount;

                AddEdge(edges, index[lat, lon], index[lat, nextLon], radius);

                if (lat < latCount)
                    AddEdge(edges, index[lat, lon], index[lat + 1, lon], radius);

                if (addDiagonals && lat < latCount)
                    AddEdge(edges, index[lat, lon], index[lat + 1, nextLon], radius * 0.65f);
            }
        }
    }

    void AddSubdividedEdge(
        List<PointData> points,
        List<EdgeData> edges,
        List<int> shellIndices,
        Vector3 start,
        Vector3 end,
        float radius,
        float nodeRadius
    )
    {
        int previous = AddPoint(points, start, nodeRadius);
        shellIndices.Add(previous);

        for (int i = 1; i <= subdivisions; i++)
        {
            float t = i / (float)subdivisions;
            Vector3 p = Vector3.Lerp(start, end, t);

            int current = AddPoint(points, p, nodeRadius);
            shellIndices.Add(current);

            AddEdge(edges, previous, current, radius);

            previous = current;
        }
    }

    int AddPoint(List<PointData> points, Vector3 p, float radius)
    {
        points.Add(new PointData(p, radius));
        return points.Count - 1;
    }

    void AddEdge(List<EdgeData> edges, int a, int b, float radius)
    {
        if (a == b)
            return;

        edges.Add(new EdgeData(a, b, radius));
    }

    void ConnectShells(
        List<int> outerShell,
        List<int> innerShell,
        List<EdgeData> edges,
        float radius
    )
    {
        int count = Mathf.Min(outerShell.Count, innerShell.Count);

        for (int i = 0; i < count; i += levelConnectionStep)
        {
            edges.Add(new EdgeData(
                outerShell[i],
                innerShell[i],
                radius * 0.8f
            ));
        }
    }

    Mesh BuildCombinedMesh(List<PointData> points, List<EdgeData> edges)
    {
        List<CombineInstance> combines = new List<CombineInstance>();

        foreach (EdgeData edge in edges)
        {
            AddBeamMesh(
                combines,
                points[edge.a].position,
                points[edge.b].position,
                edge.radius
            );
        }

        if (addNodes)
        {
            foreach (PointData p in points)
            {
                AddNodeMesh(
                    combines,
                    p.position,
                    p.radius
                );
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"Megalith_{shape}_Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.CombineMeshes(combines.ToArray(), true, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void AddBeamMesh(List<CombineInstance> combines, Vector3 start, Vector3 end, float radius)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;

        if (length <= 0.001f)
            return;

        Mesh cylinderMesh = BuildCylinderMesh(radius, length, beamSides);

        Vector3 mid = (start + end) * 0.5f;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

        combines.Add(new CombineInstance
        {
            mesh = cylinderMesh,
            transform = Matrix4x4.TRS(mid, rotation, Vector3.one)
        });
    }

    void AddNodeMesh(List<CombineInstance> combines, Vector3 position, float radius)
    {
        Mesh sphereMesh = BuildLowPolySphereMesh(radius, beamSides);

        combines.Add(new CombineInstance
        {
            mesh = sphereMesh,
            transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one)
        });
    }

    Mesh BuildCylinderMesh(float radius, float height, int sides)
    {
        Mesh mesh = new Mesh();

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float half = height * 0.5f;

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float u = i / (float)sides;

            verts.Add(new Vector3(x, -half, z));
            uvs.Add(new Vector2(u, 0));

            verts.Add(new Vector3(x, half, z));
            uvs.Add(new Vector2(u, height));
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            int b0 = i * 2;
            int t0 = i * 2 + 1;
            int b1 = next * 2;
            int t1 = next * 2 + 1;

            tris.Add(b0);
            tris.Add(t0);
            tris.Add(t1);

            tris.Add(b0);
            tris.Add(t1);
            tris.Add(b1);
        }

        int bottomCenter = verts.Count;
        verts.Add(new Vector3(0, -half, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));

        int topCenter = verts.Count;
        verts.Add(new Vector3(0, half, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            int b0 = i * 2;
            int b1 = next * 2;

            int t0 = i * 2 + 1;
            int t1 = next * 2 + 1;

            tris.Add(bottomCenter);
            tris.Add(b1);
            tris.Add(b0);

            tris.Add(topCenter);
            tris.Add(t0);
            tris.Add(t1);
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Mesh BuildLowPolySphereMesh(float radius, int sides)
    {
        Mesh mesh = new Mesh();

        int lat = Mathf.Max(4, sides / 2);
        int lon = Mathf.Max(6, sides);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int y = 0; y <= lat; y++)
        {
            float v = y / (float)lat;
            float theta = v * Mathf.PI;

            for (int x = 0; x < lon; x++)
            {
                float u = x / (float)lon;
                float phi = u * Mathf.PI * 2f;

                Vector3 p = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Cos(theta),
                    Mathf.Sin(theta) * Mathf.Sin(phi)
                ) * radius;

                verts.Add(p);
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                int nextX = (x + 1) % lon;

                int a = y * lon + x;
                int b = y * lon + nextX;
                int c = (y + 1) * lon + x;
                int d = (y + 1) * lon + nextX;

                tris.Add(a);
                tris.Add(b);
                tris.Add(c);

                tris.Add(b);
                tris.Add(d);
                tris.Add(c);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void SaveMeshAsset(Mesh mesh, string objectName)
    {
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedMegaliths");
        }

        string path = $"{saveFolder}/{objectName}_{System.DateTime.Now:HHmmss}.asset";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Saved megalith mesh to: {path}");
    }
}