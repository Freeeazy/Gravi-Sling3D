using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach anywhere in Assets (does NOT need an Editor folder).
/// Exports this object/hierarchy with a 256x1 albedo palette and no emission.
/// Requires Unity's FBX Exporter package (com.unity.formats.fbx).
/// </summary>
public class VoxelPaletteFbxExporter : MonoBehaviour
{
    [Header("Export")]
    [Tooltip("Relative to Assets/. An FBX, PNG and material assets will be created here.")]
    public string exportFolder = "Exports/VoxelFBX";

    [Tooltip("Empty = name of this GameObject")]
    public string fileName = "";

    [Tooltip("Normalize HDR color values to preserve hue instead of clipping them.")]
    public bool removeHDRIntensity = true;

#if UNITY_EDITOR
    // The Shader Graph uses floor(saturate(UV0.x) * 256), clamped to 0..255.
    private static readonly int[] PaletteIndices = { 80, 168, 169, 170, 176, 177, 178, 179 };

    [ContextMenu("Export Palette FBX")]
    public void Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Export from Edit Mode, not Play Mode.", this);
            return;
        }

        Type exporterType = Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
        if (exporterType == null)
        {
            Debug.LogError("Install 'FBX Exporter' from Window > Package Manager first.", this);
            return;
        }

        // Explicitly request binary FBX rather than relying on Unity's default (often ASCII).
        Type optionsType = exporterType.Assembly.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelOptions");
        Type formatType = exporterType.Assembly.GetType("UnityEditor.Formats.Fbx.Exporter.ExportFormat");
        if (optionsType == null || formatType == null)
        {
            Debug.LogError("FBX Exporter package does not expose binary export options. Update FBX Exporter.", this);
            return;
        }

        MethodInfo exportMethod = exporterType.GetMethod("ExportObject", new[]
        {
            typeof(string), typeof(UnityEngine.Object), optionsType
        });
        if (exportMethod == null)
        {
            Debug.LogError("This FBX Exporter version doesn't support ExportObject(path, object, options).", this);
            return;
        }
        object exportOptions = Activator.CreateInstance(optionsType);
        PropertyInfo formatProperty = optionsType.GetProperty("ExportFormat");
        if (formatProperty == null)
        {
            Debug.LogError("Unable to set FBX export format.", this);
            return;
        }
        formatProperty.SetValue(exportOptions, Enum.Parse(formatType, "Binary"));

        var sourceRenderers = GetComponentsInChildren<Renderer>(true);
        if (sourceRenderers.Length == 0)
        {
            Debug.LogError("No MeshRenderer or SkinnedMeshRenderer found.", this);
            return;
        }

        string folder = ("Assets/" + exportFolder.Trim().Trim('/', '\\')).TrimEnd('/');
        if (folder == "Assets") folder = "Assets/Exports";
        if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
        {
            Debug.LogError("Export folder must be inside Assets/ and cannot contain '..'.", this);
            return;
        }

        EnsureFolder(folder);
        string safeName = Sanitize(string.IsNullOrWhiteSpace(fileName) ? gameObject.name : fileName);
        string rootPath = folder + "/" + safeName;
        string absoluteFbx = Path.GetFullPath(rootPath + ".fbx");

        GameObject copy = null;
        var meshCopies = new Dictionary<Mesh, Mesh>();
        var tempMeshes = new List<Mesh>();

        try
        {
            // Clone ONLY for export. Never alter the original scene's meshes or materials.
            copy = Instantiate(gameObject);
            copy.name = safeName;
            copy.transform.SetParent(null, true);
            copy.hideFlags = HideFlags.HideAndDontSave;

            MonoBehaviour[] components = copy.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var component in components)
                if (component != null) component.enabled = false;

            Renderer[] clonedRenderers = copy.GetComponentsInChildren<Renderer>(true);
            int converted = 0;
            var materialCache = new Dictionary<Material, Material>();
            for (int i = 0; i < clonedRenderers.Length; i++)
            {
                Renderer renderer = clonedRenderers[i];
                var mats = renderer.sharedMaterials;
                for (int submesh = 0; submesh < mats.Length; submesh++)
                {
                    Material originalMat = mats[submesh];
                    if (originalMat == null) continue;
                    if (!materialCache.TryGetValue(originalMat, out Material convertedMat))
                    {
                        Color[] palette = BuildPalette(originalMat);
                        string baseAssetPath = rootPath + "_Palette" + materialCache.Count;
                        string pngPath = baseAssetPath + ".png";
                        string matPath = baseAssetPath + ".mat";

                        // PNG is horizontal 256 x 1; importer must use Point, Clamp, no mipmaps.
                        Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false, false);
                        tex.SetPixels(palette);
                        tex.Apply(false);
                        File.WriteAllBytes(Path.GetFullPath(pngPath), tex.EncodeToPNG());
                        DestroyImmediate(tex);
                        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);

                        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.textureType = TextureImporterType.Default;
                            importer.filterMode = FilterMode.Point;
                            importer.wrapMode = TextureWrapMode.Clamp;
                            importer.mipmapEnabled = false;
                            importer.sRGBTexture = true;
                            importer.alphaSource = TextureImporterAlphaSource.None;
                            importer.SaveAndReimport();
                        }

                        Shader shader = Shader.Find("Standard");
                        if (shader == null) throw new Exception("Built-in Standard shader could not be found.");
                        convertedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (convertedMat == null)
                        {
                            convertedMat = new Material(shader);
                            AssetDatabase.CreateAsset(convertedMat, matPath);
                        }
                        else convertedMat.shader = shader;

                        convertedMat.SetColor("_Color", Color.white);
                        convertedMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath));
                        convertedMat.SetFloat("_Metallic", 0f);
                        convertedMat.SetFloat("_Glossiness", 0f);
                        convertedMat.DisableKeyword("_EMISSION");
                        convertedMat.SetColor("_EmissionColor", Color.black);
                        EditorUtility.SetDirty(convertedMat);
                        materialCache.Add(originalMat, convertedMat);
                    }
                    mats[submesh] = convertedMat;
                    converted++;
                }
                renderer.sharedMaterials = mats;

                // Each distinct source mesh is copied and its UV0 remapped to the PNG.
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    mf.sharedMesh = CloneMesh(mf.sharedMesh, meshCopies, tempMeshes);
                SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                if (skin != null && skin.sharedMesh != null)
                    skin.sharedMesh = CloneMesh(skin.sharedMesh, meshCopies, tempMeshes);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string result = exportMethod.Invoke(null, new object[] { absoluteFbx, copy, exportOptions }) as string;
            if (string.IsNullOrEmpty(result)) throw new Exception("FBX Exporter reported failure.");

            // Binary FBX starts with the exact magic header below. Reject ASCII output.
            byte[] expectedHeader = System.Text.Encoding.ASCII.GetBytes("Kaydara FBX Binary");
            using (FileStream stream = File.OpenRead(absoluteFbx))
            {
                byte[] actualHeader = new byte[expectedHeader.Length];
                if (stream.Read(actualHeader, 0, actualHeader.Length) != actualHeader.Length)
                    throw new Exception("Exported FBX is too small.");
                for (int i = 0; i < expectedHeader.Length; i++)
                    if (actualHeader[i] != expectedHeader[i])
                        throw new Exception("FBX was exported as ASCII instead of binary; Blender cannot import it.");
            }


            AssetDatabase.Refresh();
            Debug.Log($"Exported {converted} material slots to {rootPath}.fbx plus palette PNG(s). " +
                      "Keep the FBX and its PNGs together when sharing.", this);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rootPath + ".fbx"));
        }
        catch (Exception e)
        {
            Debug.LogError("Palette FBX export failed: " + (e.InnerException ?? e), this);
        }
        finally
        {
            if (copy != null) DestroyImmediate(copy);
            foreach (Mesh m in tempMeshes)
                if (m != null) DestroyImmediate(m);
        }
    }

    private static Mesh CloneMesh(Mesh src, Dictionary<Mesh, Mesh> cache, List<Mesh> temps)
    {
        if (cache.TryGetValue(src, out Mesh clone)) return clone;
        clone = Instantiate(src);
        clone.name = src.name + "_PaletteUV";
        Vector2[] uv = clone.uv;
        if (uv == null || uv.Length != clone.vertexCount)
        {
            Debug.LogWarning($"Mesh {src.name} has no UV0, cannot reproduce palette indices.");
        }
        else
        {
            for (int i = 0; i < uv.Length; i++)
            {
                // Match graph: index=floor(saturate(U)*256), clamped 0..255.
                int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(uv[i].x) * 256f), 0, 255);
                uv[i] = new Vector2((index + 0.5f) / 256f, 0.5f);
            }
            clone.uv = uv;
        }
        cache.Add(src, clone);
        temps.Add(clone);
        return clone;
    }

    private Color[] BuildPalette(Material material)
    {
        var result = new Color[256];
        for (int i = 0; i < 256; i++) result[i] = Color.black;

        Shader shader = material.shader;
        int found = 0;
        for (int p = 0; p < shader.GetPropertyCount(); p++)
        {
            if (shader.GetPropertyType(p) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
            string label = shader.GetPropertyDescription(p);
            string property = shader.GetPropertyName(p);
            foreach (int index in PaletteIndices)
            {
                if (!label.StartsWith("Color" + index + "(", StringComparison.OrdinalIgnoreCase) &&
                    !label.Equals("Color" + index, StringComparison.OrdinalIgnoreCase)) continue;

                Color c = material.GetColor(property);
                // HDR intensity is not baked into the base color.
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (removeHDRIntensity && max > 1f) c /= max;
                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                c.a = 1f;
                // Texture PNG is sRGB. Convert material linear RGB to gamma RGB in linear projects.
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    c = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g),
                                  Mathf.LinearToGammaSpace(c.b), 1f);
                result[index] = c;
                found++;
                break;
            }
        }
        if (found == 0)
            Debug.LogWarning($"No named palette properties found on '{material.name}'. " +
                             "Check the material uses 256VoxelMap_Detailed.", this);
        return result;
    }

    private static void EnsureFolder(string path)
    {
        string parent = "Assets";
        string[] pieces = path.Substring("Assets/".Length).Split('/');
        foreach (string piece in pieces)
        {
            if (string.IsNullOrEmpty(piece)) continue;
            string candidate = parent + "/" + piece;
            if (!AssetDatabase.IsValidFolder(candidate)) AssetDatabase.CreateFolder(parent, piece);
            parent = candidate;
        }
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value;
    }
#endif
}
