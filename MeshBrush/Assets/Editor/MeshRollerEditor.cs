using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MeshRollerEditor : EditorWindow
{
    [MenuItem("Tool/MeshRoller")]
    static void ShowWindow()
    {
        MeshRollerEditor editor = GetWindow<MeshRollerEditor>("Mesh Roller");
        editor.Show();

        editor.InitDelegates();
    }


    GameObject _clone;

    bool _newPaintingMode;
    bool _selectedPaintingMode;

    int _brushRadius = 10;
    float _sensitivity = 5f;

    void InitDelegates()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        SceneView.onSceneGUIDelegate += OnSceneGUI;
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        _brushRadius = EditorGUILayout.IntField(_brushRadius);

        if (_newPaintingMode != (_newPaintingMode = EditorGUILayout.Toggle(_newPaintingMode)))
        {
            if (_newPaintingMode)
            {
                _clone = Instantiate(Selection.activeGameObject);
                //_clone.hideFlags = (HideFlags)(~0);

                Material material = Instantiate(Selection.activeGameObject.GetComponent<Renderer>().sharedMaterial);
                material.shader = Shader.Find("Unlit/Transparent");
                material.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
                if (material.mainTexture == null)
                    material.mainTexture = new Texture2D(1024, 1024);

                Texture2D mainTexture = material.mainTexture as Texture2D;
                Color[] colors = mainTexture.GetPixels();
                for (int i = 0; i < colors.Length; ++i)
                    colors[i] = new Color(1f, 1f, 1f, 0f);
                mainTexture.SetPixels(colors);
                mainTexture.Apply();

                _clone.GetComponent<Renderer>().material = material;
                _clone.GetComponent<MeshFilter>().mesh = Instantiate(_clone.GetComponent<MeshFilter>().sharedMesh);

                Selection.activeObject = _clone;
            }
        }

        if (_selectedPaintingMode != (_selectedPaintingMode = EditorGUILayout.Toggle(_selectedPaintingMode)))
        {
            if (_selectedPaintingMode)
            {
                _clone = Selection.activeGameObject;
            }
        }


        _sensitivity = EditorGUILayout.FloatField(_sensitivity);

        if (GUILayout.Button("Create Mesh"))
        {
            GameObject splitted = Instantiate(_clone);
            Mesh mesh = Instantiate(splitted.GetComponent<MeshFilter>().sharedMesh);
            splitted.GetComponent<MeshFilter>().mesh = mesh;

            Renderer renderer = splitted.GetComponent<Renderer>();
            Material newMaterial = Instantiate(renderer.sharedMaterial);

            Texture2D oldTex = _clone.GetComponent<Renderer>().sharedMaterial.mainTexture as Texture2D;
            Texture2D newTex = new Texture2D(oldTex.width, oldTex.height);
            newTex.SetPixels(oldTex.GetPixels());
            newTex.Apply();

            newMaterial.mainTexture = newTex;
            renderer.material = newMaterial;

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
                Texture2D tex = renderer.sharedMaterial.mainTexture as Texture2D;

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
        EditorGUILayout.EndVertical();
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject != _clone)
        {
            _newPaintingMode = false;

            Repaint();
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (_newPaintingMode == false && 
            _selectedPaintingMode == false)
            return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            foreach (RaycastHit hit in Physics.RaycastAll(ray))
            {
                if (hit.collider.gameObject == _clone)
                {
                    Mesh mesh = _clone.GetComponent<MeshFilter>().sharedMesh;
                    Renderer renderer = hit.collider.GetComponent<Renderer>();
                    if (renderer && renderer.sharedMaterial && renderer.sharedMaterial.mainTexture is Texture2D)
                    {
                        Texture2D tex = renderer.sharedMaterial.mainTexture as Texture2D;
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

                                tex.SetPixel(x, y, Color.red);
                            }
                        }
                        
                        tex.Apply();
                    }

                    break;
                }
            }
        }
    }

    void OnDestroy()
    {
        _newPaintingMode = false;
    }
}
