using Grass.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;

namespace Grass.Editor
{
    public class GrassMakerWindow : EditorWindow
    {
        [FormerlySerializedAs("grassObject")] [SerializeField]
        private GameObject grassHolderObject;

        [SerializeField] private int grassCount = 1000;
        [SerializeField] private bool clearBeforeGenerate = true;

        private GrassHolder _grassHolder;
        private Vector2 scrollPos;

        public LayerMask cullGrassMask;


        [MenuItem("Tools/Grass Maker")]
        static void Init()
        {
            GrassMakerWindow window = (GrassMakerWindow)GetWindow(typeof(GrassMakerWindow), false, "Grass Maker", true);
            window.titleContent = new GUIContent("Grass Maker");
            window.Show();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            grassHolderObject = (GameObject)EditorGUILayout.ObjectField("Grass Holder",
                grassHolderObject,
                typeof(GameObject),
                true);
            
            if (grassHolderObject == null) {
                grassHolderObject = FindFirstObjectByType<GrassHolder>()?.gameObject;
            }
            
            if (grassHolderObject == null){
                if (GUILayout.Button("Create Grass Holder")) {
                    CreateNewGrassHolder();
                }

                EditorGUILayout.LabelField("No Grass Holder found, create a new one", EditorStyles.label);
                EditorGUILayout.EndScrollView();
                return;
            }
            
            _grassHolder = grassHolderObject?.GetComponent<GrassHolder>();

            if (_grassHolder is null)
            {
                EditorGUILayout.LabelField(
                    "One of necessary component are missing(GrassHolder). Creating grass is impossible",
                    EditorStyles.helpBox);
                EditorGUILayout.EndScrollView();
                return;
            }

            ShowGeneratePanel();

            GUILayout.FlexibleSpace();


            EditorGUILayout.LabelField($"Total grass instances: {_grassHolder.grassData.Count}",
                EditorStyles.boldLabel);

            if (GUILayout.Button("Clear Grass"))
            {
                if (EditorUtility.DisplayDialog("Clear All Grass?",
                        "Are you sure you want to clear the grass?", "Clear", "Don't Clear"))
                    if (GrassDataManager.TryClearGrassData(_grassHolder))
                        Debug.Log($"Clear Grass Success");
                    else
                        Debug.LogError($"Clear Grass Failed");
            }

            if (GUILayout.Button("Save Positions"))
            {
                if (GrassDataManager.TrySaveGrassData(_grassHolder))
                    Debug.Log("Grass Data Saved");
                else
                    Debug.LogError("Grass Data Not Saved");
            }

            if (GUILayout.Button("Load Positions"))
            {
                if (GrassDataManager.TryLoadGrassData(_grassHolder))
                {
                    _grassHolder.Reinitialize();
                    Debug.Log("Grass Data Loaded and Reinitialized");
                }
                else
                {
                    Debug.LogError("Grass Data Not Loaded");
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowGeneratePanel()
        {
            EditorGUILayout.LabelField("Generate From Selection", EditorStyles.boldLabel);

            int selectedCount = Selection.gameObjects?.Length ?? 0;
            EditorGUILayout.LabelField("Selected Objects", selectedCount.ToString());

            cullGrassMask = DrawLayerMaskField("Obstacle Mask", cullGrassMask);

            grassHolderObject ??= FindFirstObjectByType<GrassHolder>()?.gameObject;

            if (grassHolderObject != null)
            {
                EditorGUILayout.Space(5);
                _grassHolder.normalLimit = EditorGUILayout.Slider(
                    "Slope Tolerance", _grassHolder.normalLimit, 0f, 1f);

                EditorGUILayout.Space(5);
                grassCount = Mathf.Max(0, EditorGUILayout.IntField("Blades per Target", grassCount));
                clearBeforeGenerate = EditorGUILayout.Toggle("Replace Existing Grass", clearBeforeGenerate);

                EditorGUILayout.Space(10);

                if (_grassHolder.materialSystem == null || !_grassHolder.materialSystem.IsValid())
                {
                    EditorGUILayout.LabelField("No valid grass variants are configured on the Grass Holder.", EditorStyles.helpBox);
                }

                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Generate Selected Surfaces", GUILayout.Height(32)))
                {
                    GameObject[] selectedObjects = Selection.gameObjects;
                    if (selectedObjects == null || selectedObjects.Length == 0)
                    {
                        Debug.LogError("GrassMaker: No objects selected!");
                        return;
                    }

                    int successCount = 0;
                    int failCount = 0;

                    if (clearBeforeGenerate)
                        _grassHolder.PrepareForRegeneration();

                    foreach (var obj in selectedObjects)
                    {
                        bool canGenerateOnObject = obj.GetComponent<MeshFilter>() != null || obj.GetComponent<Terrain>() != null;

                        if (canGenerateOnObject)
                        {
                            if (GrassCreator.TryGeneratePoints(_grassHolder,
                                    obj,
                                    grassCount,
                                    cullGrassMask,
                                    _grassHolder.normalLimit,
                                    _grassHolder.materialSystem))
                            {
                                successCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        else
                        {
                            var childMeshes = obj.GetComponentsInChildren<MeshFilter>();
                            var childTerrains = obj.GetComponentsInChildren<Terrain>();

                            if (childMeshes.Length > 0 || childTerrains.Length > 0)
                            {
                                foreach (var childMesh in childMeshes)
                                {
                                    if (GrassCreator.TryGeneratePoints(_grassHolder,
                                            childMesh.gameObject,
                                            grassCount,
                                            cullGrassMask,
                                            _grassHolder.normalLimit,
                                            _grassHolder.materialSystem))
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        failCount++;
                                    }
                                }

                                foreach (var childTerrain in childTerrains)
                                {
                                    if (GrassCreator.TryGeneratePoints(_grassHolder,
                                            childTerrain.gameObject,
                                            grassCount,
                                            cullGrassMask,
                                            _grassHolder.normalLimit,
                                            _grassHolder.materialSystem))
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        failCount++;
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"GrassMaker: {obj.name} has no Mesh Filter or Terrain on itself or children!");
                                failCount++;
                            }
                        }
                    }
                    
                    if (GrassDataManager.TrySaveGrassData(_grassHolder))
                        Debug.Log($"Grass generated on {successCount} target(s), {failCount} failed. Grass data saved.");
                    else
                        Debug.LogError("Grass Data Not Saved");
                }
            }
            else
            {
                if (GUILayout.Button("Create Grass Holder"))
                {
                    CreateNewGrassHolder();
                }

                EditorGUILayout.LabelField("No Grass Holder found, create a new one", EditorStyles.label);
            }
        }

        void CreateNewGrassHolder()
        {
            grassHolderObject = new GameObject();
            grassHolderObject.name = "Grass Holder";
            grassHolderObject.layer = LayerMask.NameToLayer("Grass");
            _grassHolder = grassHolderObject.AddComponent<GrassHolder>();
        }

        private static LayerMask DrawLayerMaskField(string label, LayerMask layerMask)
        {
            int concatenatedMask = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(layerMask);
            int newConcatenatedMask = EditorGUILayout.MaskField(label, concatenatedMask, InternalEditorUtility.layers);
            return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(newConcatenatedMask);
        }

    }
}
