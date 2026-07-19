using UniVRMXT.MaterialsOverride;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Hybrid pair inspector: read-only glTF name + source material; editable override.
    /// </summary>
    [CustomEditor(typeof(VrmxtMaterialsOverrideInstance))]
    public sealed class VrmxtMaterialsOverrideInstanceEditor : UnityEditor.Editor
    {
        private SerializedProperty _pairs;
        private bool _showAdvancedJson;

        private void OnEnable()
        {
            _pairs = serializedObject.FindProperty("pairs");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Material Override Pairs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "VRM/glTF side is read-only (name + source material). Assign Override Material to author.",
                MessageType.Info);

            if (_pairs != null)
            {
                for (var i = 0; i < _pairs.arraySize; i++)
                {
                    var element = _pairs.GetArrayElementAtIndex(i);
                    DrawPair(element, i);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Populate Pairs From Renderers"))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                Undo.RecordObject(instance, "Populate Materials Override Pairs");
                instance.PopulatePairsFromRenderers();
                EditorUtility.SetDirty(instance);
                serializedObject.Update();
            }

            if (GUILayout.Button("Clear Material Overrides"))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                Undo.RecordObject(instance, "Clear Material Overrides");
                instance.ClearOverrides();
                EditorUtility.SetDirty(instance);
                serializedObject.Update();
            }

            EditorGUILayout.EndHorizontal();

            _showAdvancedJson = EditorGUILayout.Foldout(_showAdvancedJson, "Advanced: Extension JSON", true);
            if (_showAdvancedJson && _pairs != null)
            {
                for (var i = 0; i < _pairs.arraySize; i++)
                {
                    var element = _pairs.GetArrayElementAtIndex(i);
                    var name = element.FindPropertyRelative("MaterialName")?.stringValue ?? $"[{i}]";
                    EditorGUILayout.LabelField(name, EditorStyles.miniBoldLabel);
                    var jsonProp = element.FindPropertyRelative("ExtensionJson");
                    if (jsonProp != null)
                    {
                        EditorGUILayout.PropertyField(jsonProp, GUIContent.none);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPair(SerializedProperty element, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var nameProp = element.FindPropertyRelative("MaterialName");
            var sourceProp = element.FindPropertyRelative("SourceMaterial");
            var overrideProp = element.FindPropertyRelative("OverrideMaterial");

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Material Name", nameProp?.stringValue ?? string.Empty);
            EditorGUILayout.ObjectField(
                "Source Material",
                sourceProp?.objectReferenceValue,
                typeof(Material),
                true);
            EditorGUI.EndDisabledGroup();

            if (overrideProp != null)
            {
                EditorGUILayout.PropertyField(overrideProp, new GUIContent("Override Material"));
            }

            EditorGUILayout.EndVertical();
        }
    }
}
