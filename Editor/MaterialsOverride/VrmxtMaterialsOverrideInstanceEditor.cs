using System.Text;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Hybrid pair inspector: read-only glTF name + source material; editable override.
    /// Shows per-pair status so imported JSON overrides are visible without an Override Material.
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
                "Override Material is optional authoring: assign one only when you want to " +
                "rewrite the Unity override from that asset. Imported VRMs keep overrides in " +
                "extension JSON — an empty Override Material after import is normal. See each " +
                "pair's Status line.",
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
                // Reload + exit: ApplyModifiedProperties below would otherwise write stale
                // SerializedProperty values back over the mutated instance.
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Clear Material Overrides"))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                Undo.RecordObject(instance, "Clear Material Overrides");
                instance.ClearOverrides();
                EditorUtility.SetDirty(instance);
                serializedObject.Update();
                GUIUtility.ExitGUI();
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

        private void DrawPair(SerializedProperty element, int index)
        {
            var nameProp = element.FindPropertyRelative("MaterialName");
            var sourceProp = element.FindPropertyRelative("SourceMaterial");
            var overrideProp = element.FindPropertyRelative("OverrideMaterial");
            var jsonProp = element.FindPropertyRelative("ExtensionJson");

            var overrideMat = overrideProp?.objectReferenceValue as Material;
            var json = jsonProp?.stringValue;
            BuildPairStatus(json, overrideMat != null, out var statusLabel, out var detail);
            var canClear = statusLabel != "Stock";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Material Name", nameProp?.stringValue ?? string.Empty);
            EditorGUILayout.ObjectField(
                "Source Material",
                sourceProp?.objectReferenceValue,
                typeof(Material),
                true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status", statusLabel, EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(!canClear);
            if (GUILayout.Button("Clear", GUILayout.Width(56f)))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                var label = nameProp?.stringValue;
                Undo.RecordObject(
                    instance,
                    string.IsNullOrEmpty(label)
                        ? "Clear Material Override"
                        : $"Clear Material Override ({label})");
                instance.ClearOverrideAt(index);
                EditorUtility.SetDirty(instance);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(detail))
            {
                EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
            }

            if (overrideProp != null)
            {
                EditorGUILayout.PropertyField(
                    overrideProp,
                    new GUIContent(
                        "Override Material",
                        "Optional. Assign to author/rewrite the Unity override from this asset. " +
                        "Leave empty when using imported extension JSON only."));
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Stock | Imported | Authored | Imported + Authored, plus a one-line unity/engine summary.
        /// </summary>
        private static void BuildPairStatus(
            string extensionJson,
            bool hasOverrideMaterial,
            out string statusLabel,
            out string detail)
        {
            detail = null;
            var hasFileJson = !string.IsNullOrWhiteSpace(extensionJson);
            VrmxtMaterialsOverrideExtension extension = null;
            var parsed = hasFileJson &&
                         VrmxtMaterialsOverride.TryParse(extensionJson, out extension);
            var hasFileOverride = parsed && extension != null && extension.Overrides.Count > 0;

            if (hasFileOverride && hasOverrideMaterial)
            {
                statusLabel = "Imported + Authored";
            }
            else if (hasFileOverride)
            {
                statusLabel = "Imported";
            }
            else if (hasFileJson && !parsed)
            {
                statusLabel = "Invalid JSON";
                detail = "Extension JSON present but failed to parse.";
            }
            else if (hasOverrideMaterial)
            {
                statusLabel = "Authored";
                detail = "Local Override Material assigned; sync writes unity into extension JSON.";
            }
            else
            {
                statusLabel = "Stock";
                detail = "No VRMXT_materials_override on this material.";
                return;
            }

            if (!parsed)
            {
                return;
            }

            detail = BuildDetail(extension, hasOverrideMaterial);
        }

        private static string BuildDetail(
            VrmxtMaterialsOverrideExtension extension,
            bool hasOverrideMaterial)
        {
            var sb = new StringBuilder();
            var unityCount = 0;

            foreach (var entry in extension.Overrides)
            {
                if (entry == null ||
                    !string.Equals(
                        entry.Engine,
                        VrmxtMaterialsOverride.EngineUnity,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                var unity = entry.Material as UnityMaterialOverride;
                if (unity == null)
                {
                    continue;
                }

                if (unityCount > 0)
                {
                    sb.Append(" · ");
                }

                sb.Append("unity");
                if (!string.IsNullOrEmpty(unity.Variant))
                {
                    sb.Append('[');
                    sb.Append(unity.Variant);
                    sb.Append(']');
                }

                sb.Append(" · ");
                sb.Append(unity.ShaderName ?? unity.Id ?? "(no id)");
                unityCount++;
            }

            if (unityCount == 0)
            {
                sb.Append("no unity engine entry");
            }

            foreach (var entry in extension.Overrides)
            {
                if (entry == null ||
                    string.Equals(
                        entry.Engine,
                        VrmxtMaterialsOverride.EngineUnity,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                sb.Append(" · +");
                sb.Append(entry.Engine);
                var unreal = entry.Material as UnrealMaterialOverride;
                if (unreal != null && !string.IsNullOrEmpty(unreal.Variant))
                {
                    sb.Append('[');
                    sb.Append(unreal.Variant);
                    sb.Append(']');
                }
            }

            if (hasOverrideMaterial)
            {
                sb.Append(" · local Override Material assigned (sync upserts active unity slot only)");
            }

            return sb.ToString();
        }
    }
}
