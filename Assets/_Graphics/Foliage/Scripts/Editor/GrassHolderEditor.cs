using Grass.Core;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassHolder))]
    public class GrassHolderEditor : UnityEditor.Editor
    {
        private SerializedProperty mesh;
        private SerializedProperty materialSystem;
        private SerializedProperty normalLimit;
        private SerializedProperty chunkGridResolution;
        private SerializedProperty boundsPadding;
        private SerializedProperty useGpuCulling;
        private SerializedProperty frustumCullingCompute;
        private SerializedProperty maxDrawDistance;
        private SerializedProperty cullingPositionThreshold;
        private SerializedProperty cullingRotationThreshold;
        private SerializedProperty drawBounds;
        private SerializedProperty highlightRenderedCells;
        private SerializedProperty GrassDataSource;
        private SerializedProperty renderingLayerMask;

        private void OnEnable()
        {
            mesh = serializedObject.FindProperty("mesh");
            materialSystem = serializedObject.FindProperty("materialSystem");
            normalLimit = serializedObject.FindProperty("normalLimit");
            chunkGridResolution = serializedObject.FindProperty("chunkGridResolution");
            boundsPadding = serializedObject.FindProperty("boundsPadding");
            useGpuCulling = serializedObject.FindProperty("useGpuCulling");
            frustumCullingCompute = serializedObject.FindProperty("frustumCullingCompute");
            maxDrawDistance = serializedObject.FindProperty("maxDrawDistance");
            cullingPositionThreshold = serializedObject.FindProperty("cullingPositionThreshold");
            cullingRotationThreshold = serializedObject.FindProperty("cullingRotationThreshold");
            drawBounds = serializedObject.FindProperty("drawBounds");
            highlightRenderedCells = serializedObject.FindProperty("highlightRenderedCells");
            GrassDataSource = serializedObject.FindProperty("GrassDataSource");
            renderingLayerMask = serializedObject.FindProperty("renderingLayerMask");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var script = (GrassHolder)target;

            if (script.GrassDataSource == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Grass Data", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(GrassDataSource, new GUIContent("Grass Data Source"));

                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grass Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(GrassDataSource, new GUIContent("Grass Data Source"));

            EditorGUILayout.Space();
            DrawMaterialSystem();
            EditorGUILayout.PropertyField(mesh, new GUIContent("Mesh"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(normalLimit, new GUIContent("Slope Limit"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rendering Settings", EditorStyles.boldLabel);
            DrawRenderingLayerMaskField();
            EditorGUILayout.PropertyField(useGpuCulling, new GUIContent("Use GPU Culling"));
            if (useGpuCulling.boolValue)
                EditorGUILayout.PropertyField(frustumCullingCompute, new GUIContent("Culling Compute"));
            EditorGUILayout.PropertyField(maxDrawDistance, new GUIContent("Max Draw Distance"));
            EditorGUILayout.PropertyField(cullingPositionThreshold, new GUIContent("Culling Position Threshold"));
            EditorGUILayout.PropertyField(cullingRotationThreshold, new GUIContent("Culling Rotation Threshold"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Baked Chunk Settings", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(chunkGridResolution, 1, 64, new GUIContent("Chunk Grid Resolution"));
            EditorGUILayout.PropertyField(boundsPadding, new GUIContent("Bounds Padding"));
            EditorGUILayout.PropertyField(drawBounds, new GUIContent("Draw Bounds"));
            EditorGUILayout.PropertyField(highlightRenderedCells, new GUIContent("Highlight Rendered Cells"));
            if (script.TotalChunkCount > 0)
            {
                EditorGUILayout.LabelField($"Visible chunks: {script.VisibleChunkCount}/{script.TotalChunkCount}");
                EditorGUILayout.LabelField($"Visible ranges: {script.VisibleRangeCount}/{script.TotalRangeCount}");
                EditorGUILayout.LabelField($"Draw commands after merge: {script.VisibleDrawCommandCount}");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMaterialSystem()
        {
            EditorGUILayout.LabelField("Material Variants", EditorStyles.boldLabel);

            SerializedProperty grassClusterScale = materialSystem.FindPropertyRelative("grassClusterScale");
            SerializedProperty variants = materialSystem.FindPropertyRelative("variants");

            EditorGUILayout.PropertyField(grassClusterScale, new GUIContent("Grass Patch Variation"));

            EditorGUILayout.Space(4);

            for (int i = 0; i < variants.arraySize; i++)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(i);
                DrawVariant(variants, variant, i);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Grass"))
                AddVariant(variants, GrassVariantKind.Grass);
            if (GUILayout.Button("Add Flower"))
                AddVariant(variants, GrassVariantKind.Flower);
            if (GUILayout.Button("Add Custom"))
                AddVariant(variants, GrassVariantKind.Custom);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawVariant(SerializedProperty variants, SerializedProperty variant, int index)
        {
            SerializedProperty name = variant.FindPropertyRelative("name");
            SerializedProperty material = variant.FindPropertyRelative("material");
            SerializedProperty kind = variant.FindPropertyRelative("kind");
            SerializedProperty weight = variant.FindPropertyRelative("weight");
            SerializedProperty useTextureColor = variant.FindPropertyRelative("useTextureColor");
            SerializedProperty clumpScale = variant.FindPropertyRelative("clumpScale");
            SerializedProperty clumpThreshold = variant.FindPropertyRelative("clumpThreshold");
            SerializedProperty clumpDensity = variant.FindPropertyRelative("clumpDensity");
            SerializedProperty seed = variant.FindPropertyRelative("seed");
            SerializedProperty normalNudgeProbability = variant.FindPropertyRelative("normalNudgeProbability");
            SerializedProperty normalNudgeStrength = variant.FindPropertyRelative("normalNudgeStrength");

            string title = string.IsNullOrWhiteSpace(name.stringValue) ? $"Variant {index + 1}" : name.stringValue;
            if (material.objectReferenceValue != null)
                title += $" ({material.objectReferenceValue.name})";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            variant.isExpanded = EditorGUILayout.Foldout(variant.isExpanded, title, true);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                variants.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (variant.isExpanded)
            {
                EditorGUILayout.PropertyField(name, new GUIContent("Name"));
                EditorGUILayout.PropertyField(material, new GUIContent("Material"));
                EditorGUILayout.PropertyField(kind, new GUIContent("Type"));
                EditorGUILayout.PropertyField(weight, new GUIContent("Abundance"));
                EditorGUILayout.PropertyField(useTextureColor, new GUIContent("Use Sprite Colors"));

                GrassVariantKind variantKind = (GrassVariantKind)kind.enumValueIndex;
                if (variantKind == GrassVariantKind.Flower)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Flower Patch Controls", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(clumpScale, new GUIContent("Patch Size"));
                    EditorGUILayout.PropertyField(clumpThreshold, new GUIContent("Patch Rarity"));
                    EditorGUILayout.PropertyField(clumpDensity, new GUIContent("Flowers Inside Patch"));
                    EditorGUILayout.PropertyField(seed, new GUIContent("Patch Seed"));
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Generated Normal Variation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(normalNudgeProbability, new GUIContent("Chance"));
                EditorGUILayout.PropertyField(normalNudgeStrength, new GUIContent("Strength"));
            }

            EditorGUILayout.EndVertical();
        }

        private static void AddVariant(SerializedProperty variants, GrassVariantKind kind)
        {
            int index = variants.arraySize;
            variants.InsertArrayElementAtIndex(index);

            SerializedProperty variant = variants.GetArrayElementAtIndex(index);
            variant.isExpanded = true;
            variant.FindPropertyRelative("name").stringValue = kind == GrassVariantKind.Flower
                ? $"Flower {index + 1}"
                : $"Grass {index + 1}";
            variant.FindPropertyRelative("material").objectReferenceValue = null;
            variant.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            variant.FindPropertyRelative("weight").floatValue = 1f;
            variant.FindPropertyRelative("useTextureColor").boolValue = kind == GrassVariantKind.Flower;
            variant.FindPropertyRelative("clumpScale").floatValue = 0.12f;
            variant.FindPropertyRelative("clumpThreshold").floatValue = 0.7f;
            variant.FindPropertyRelative("clumpDensity").floatValue = 0.2f;
            variant.FindPropertyRelative("seed").intValue = index + 1;
            variant.FindPropertyRelative("normalNudgeProbability").floatValue = 0.05f;
            variant.FindPropertyRelative("normalNudgeStrength").floatValue = 0.08f;
        }
        
        private void DrawRenderingLayerMaskField()
        {
            uint currentMask = (uint)renderingLayerMask.intValue;
            string[] layerNames = GetRenderingLayerNames();
            int newMask = EditorGUILayout.MaskField("Rendering Layer Mask", (int)currentMask, layerNames);

            if (newMask != (int)currentMask)
                renderingLayerMask.intValue = newMask;
        }

        private string[] GetRenderingLayerNames()
        {
            string[] layerNames = new string[32];
            var renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

            if (renderPipeline != null && renderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
            {
                try
                {
                    var property = renderPipeline.GetType().GetProperty("renderingLayerMaskNames");
                    if (property != null)
                    {
                        var names = property.GetValue(renderPipeline) as string[];
                        if (names != null && names.Length > 0)
                        {
                            for (int i = 0; i < Mathf.Min(names.Length, 32); i++)
                            {
                                layerNames[i] = !string.IsNullOrEmpty(names[i]) ? names[i] : $"Layer {i}";
                            }

                            return layerNames;
                        }
                    }
                }
                catch
                {
                }
            }

            for (int i = 0; i < 32; i++)
            {
                layerNames[i] = $"Layer {i}";
            }

            return layerNames;
        }
    }
}
