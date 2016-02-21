using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

public partial class MeshBrushEditor : EditorWindow
{
    [MenuItem("Tool/Mesh Brush")]
    static void ShowWindow()
    {
        MeshBrushEditor editor = GetWindow<MeshBrushEditor>("Mesh Roller");
        editor.Show();

        editor.InitDelegates();
    }

    void InitDelegates()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        SceneView.onSceneGUIDelegate += OnSceneGUI;
    }
}

partial class MeshBrushEditor
{ 
    const float LAYOUT_WIDTH = 200;

    GameObject _selection;
    Material _selectionMaterial;
    Renderer _selectionRenderer;

    int _brushRadius = 10;
    Color _brushColor = Color.red;

    float _sensitivity = 5f;


    void OnGUI()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LAYOUT_WIDTH));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Brush");
        EditorGUI.indentLevel = 2;
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Size", GUILayout.Width(150));
                _brushRadius = EditorGUILayout.IntField(_brushRadius, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Color", GUILayout.Width(150));
                _brushColor = EditorGUILayout.ColorField(_brushColor, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel = 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Painting");

        EditorGUI.indentLevel = 2;
        {
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("New"))
                    OnNewPainting();

                if (GUILayout.Button("Save"))
                    OnSavePainting();

                if (GUILayout.Button("Load"))
                    OnLoadPainting();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel = 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mesh");

        EditorGUI.indentLevel = 2;
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Sensitivity", GUILayout.Width(150));
                _sensitivity = EditorGUILayout.FloatField(_sensitivity, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel = 0;

        if (GUILayout.Button("Create Mesh"))
        {
            OnCreateMesh();
        }
        EditorGUILayout.EndVertical();
    }

    void OnNewPainting()
    {
        if (_selection)
        {
            if (EditorUtility.DisplayDialog("경고!", "기존 작업물이 있습니다. 저장하지 않은 작업물은 복구할 수 없습니다", "뭐래 고고", "헉 안돼 취소") == false)
                return;
        }

        CleanUp();

        _selection = Selection.activeGameObject;

        Material originalMaterial = _selection.GetComponent<Renderer>().sharedMaterial;
        _selectionMaterial = Instantiate(originalMaterial);
        _selectionMaterial.shader = Shader.Find("Unlit/Transparent");
        _selectionMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0f));

        if (originalMaterial.mainTexture == null)
            _selectionMaterial.mainTexture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        else
            _selectionMaterial.mainTexture = new Texture2D(originalMaterial.mainTexture.width, originalMaterial.mainTexture.height, TextureFormat.RGBA32, false);

        Texture2D mainTexture = _selectionMaterial.mainTexture as Texture2D;
        Color[] colors = mainTexture.GetPixels();
        for (int i = 0; i < colors.Length; ++i)
            colors[i] = new Color(1f, 1f, 1f, 0f);
        mainTexture.SetPixels(colors);
        mainTexture.Apply();

        _selectionRenderer = _selection.GetComponent<Renderer>();

        Material[] originalMaterials = _selectionRenderer.sharedMaterials;
        ArrayUtility.Add(ref originalMaterials, _selectionMaterial);
        _selectionRenderer.sharedMaterials = originalMaterials;
    }

    void OnSavePainting()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save", "New material", "mat", "");
        if (string.IsNullOrEmpty(path) == false)
        {
            Texture2D tex = _selectionMaterial.mainTexture as Texture2D;
            if (tex)
            {
                byte[] pngData = tex.EncodeToPNG();
                if (pngData != null)
                    File.WriteAllBytes(path + ".png", pngData);
            }

            AssetDatabase.CreateAsset(_selectionMaterial, path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TextureImporter texImporter = AssetImporter.GetAtPath(path + ".png") as TextureImporter;
            if (texImporter)
            {
                texImporter.isReadable = true;
                texImporter.textureFormat = TextureImporterFormat.RGBA32;
                AssetDatabase.ImportAsset(path + ".png", ImportAssetOptions.ForceUpdate);
            }
            _selectionMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
        }
    }

    void OnLoadPainting()
    {
        if (_selection)
        {
            if (EditorUtility.DisplayDialog("경고!", "기존 작업물이 있습니다. 저장하지 않은 작업물은 복구할 수 없습니다", "뭐래 고고", "헉 안돼 취소") == false)
                return;
        }

        string path = EditorUtility.OpenFilePanel("Load", Application.dataPath, "mat");
        if (string.IsNullOrEmpty(path) == false)
        {
            CleanUp();

            path = path.Replace(Application.dataPath + "/", "");
            path = "Assets/" + path;

            _selection = Selection.activeGameObject;
            _selectionRenderer = _selection.GetComponent<Renderer>();
            _selectionMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);

            Material[] originalMaterials = _selectionRenderer.sharedMaterials;
            ArrayUtility.Add(ref originalMaterials, _selectionMaterial);
            _selectionRenderer.sharedMaterials = originalMaterials;
        }
    }

    void OnCreateMesh()
    {
        GameObject splitted = Instantiate(_selection);
        Mesh mesh = Instantiate(splitted.GetComponent<MeshFilter>().sharedMesh);
        splitted.GetComponent<MeshFilter>().mesh = mesh;

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        Vector3[] normals = mesh.normals;
        List<Vector3> vertList = new List<Vector3>();
        List<Vector2> uvList = new List<Vector2>();
        List<Vector3> normalsList = new List<Vector3>();
        List<int> trianglesList = new List<int>();

        int i = 0;
        while (i < vertices.Length)
        {
            vertList.Add(vertices[i]);
            uvList.Add(uv[i]);
            normalsList.Add(normals[i]);
            i++;
        }
        for (int triCount = 0; triCount < triangles.Length; triCount += 3)
        {
            Texture2D tex = _selectionMaterial.mainTexture as Texture2D;

            bool isAllPainted = true;
            for (int triIndex = triCount; triIndex < triCount + 3; ++triIndex)
            {
                Vector2 pixelUV = mesh.uv[triangles[triIndex]];
                pixelUV.x *= tex.width;
                pixelUV.y *= tex.height;

                bool isPainted = false;
                for (i = (int)(-_sensitivity * 0.5f); i < _sensitivity * 0.5f; ++i)
                {
                    for (int j = (int)(-_sensitivity * 0.5f); j < _sensitivity * 0.5f; ++j)
                    {
                        int x = (int)pixelUV.x + i;
                        int y = (int)pixelUV.y + j;
                        if (x < 0 || y < 0 || x >= tex.width || y >= tex.height)
                            continue;

                        if (tex.GetPixel(x, y).a > 0)
                        {
                            isPainted = true;
                            break;
                        }
                    }
                    if (isPainted)
                        break;
                }

                if (isPainted == false)
                {
                    isAllPainted = false;
                    break;
                }

            }

            if (isAllPainted)
            {
                trianglesList.Add(triangles[triCount]);
                trianglesList.Add(triangles[triCount + 1]);
                trianglesList.Add(triangles[triCount + 2]);
            }
        }


        triangles = trianglesList.ToArray();
        vertices = vertList.ToArray();
        uv = uvList.ToArray();
        normals = normalsList.ToArray();
        //mesh.Clear();
        mesh.triangles = triangles;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.normals = normals;
    }

    void OnSelectionChange()
    {
        if (_selection && _selection != Selection.activeObject)
        {
            CleanUp();
            Repaint();
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (_selection == null)
            return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            foreach (RaycastHit hit in Physics.RaycastAll(ray))
            {
                if (hit.collider.gameObject == _selection)
                {
                    Texture2D tex = _selectionMaterial.mainTexture as Texture2D;
                    Vector2 pixelUV = hit.textureCoord;
                    pixelUV.x *= tex.width;
                    pixelUV.y *= tex.height;

                    for (int i = (int)(-_brushRadius * 0.5f); i < _brushRadius * 0.5f; ++i)
                    {
                        for (int j = (int)(-_brushRadius * 0.5f); j < _brushRadius * 0.5f; ++j)
                        {
                            int x = (int)pixelUV.x + i;
                            int y = (int)pixelUV.y + j;
                            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height)
                                continue;

                            tex.SetPixel(x, y, _brushColor);
                        }
                    }

                    tex.Apply();
                }

                break;
            }
        }
    }

    void OnDestroy()
    {
        if (_selection)
            CleanUp();
    }

    void CleanUp()
    {
        if (_selectionRenderer)
        {
            Material[] materials = _selectionRenderer.sharedMaterials;
            ArrayUtility.Remove(ref materials, _selectionMaterial);
            _selectionRenderer.sharedMaterials = materials;
        }

        _selection = null;
        _selectionMaterial = null;
        _selectionRenderer = null;
    }
}

