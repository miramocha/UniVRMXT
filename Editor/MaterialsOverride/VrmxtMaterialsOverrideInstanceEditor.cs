using System.Text;
using UnityEditor;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;

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
        private SerializedProperty _applyOverridesToRenderers;
        private bool _showAdvancedJson;

        private void OnEnable()
        {
            _pairs = serializedObject.FindProperty("pairs");
            _applyOverridesToRenderers = serializedObject.FindProperty("applyOverridesToRenderers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Material Override Pairs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Import attaches override JSON and textures but leaves stock MToon on "
                    + "renderers. Materialize (all or per slot) creates .mat assets, assigns "
                    + "Override Material, and puts that asset into matching MeshRenderer "
                    + "slots (stock Source Material is not mutated). Use Show Override "
                    + "Materials to toggle renderer slots between Override Material assets "
                    + "and stock Source / MToon without clearing Override Material or "
                    + "extension JSON. Dragging a .mat into Override Material also swaps "
                    + "slots and Transfers into the active unity slot on export.",
                MessageType.Info
            );

            if (_pairs != null)
            {
                for (var i = 0; i < _pairs.arraySize; i++)
                {
                    var element = _pairs.GetArrayElementAtIndex(i);
                    DrawPair(element, i);
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Materialize All Materials"))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                var count = VrmxtMaterialsOverrideMaterialize.MaterializeAll(instance);
                if (count == 0)
                {
                    Debug.LogWarning(
                        "VRMXT Materialize: no pairs materialized (missing unity override "
                            + "or unresolved shader)."
                    );
                }

                EditorUtility.SetDirty(instance);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }

            DrawShowOverrideMaterialsToggle();

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

            if (GUILayout.Button("Dump Materials Debug (Console)"))
            {
                var instance = (VrmxtMaterialsOverrideInstance)target;
                VrmxtMaterialsOverrideDebug.Dump(instance);
            }

            EditorGUILayout.HelpBox(
                "Dump logs JSON vs live renderer vs Override Material (shader, MainTex, "
                    + "_Color, keywords, remembered texture indices). Compare pre-export "
                    + "authored avatar vs re-imported VRM.",
                MessageType.None
            );

            _showAdvancedJson = EditorGUILayout.Foldout(
                _showAdvancedJson,
                "Advanced: Extension JSON",
                true
            );
            if (_showAdvancedJson && _pairs != null)
            {
                for (var i = 0; i < _pairs.arraySize; i++)
                {
                    var element = _pairs.GetArrayElementAtIndex(i);
                    var name =
                        element.FindPropertyRelative("MaterialName")?.stringValue ?? $"[{i}]";
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
            var instance = (VrmxtMaterialsOverrideInstance)target;
            var pair =
                instance != null && index >= 0 && index < instance.Pairs.Count
                    ? instance.Pairs[index]
                    : null;
            var canMaterialize = VrmxtMaterialsOverrideMaterialize.CanMaterializePair(pair);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Material Name", nameProp?.stringValue ?? string.Empty);
            EditorGUILayout.ObjectField(
                "Source Material",
                sourceProp?.objectReferenceValue,
                typeof(Material),
                true
            );
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status", statusLabel, EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(!canMaterialize);
            if (GUILayout.Button("Materialize", GUILayout.Width(88f)))
            {
                var label = nameProp?.stringValue;
                if (!VrmxtMaterialsOverrideMaterialize.MaterializePair(instance, index))
                {
                    Debug.LogWarning(
                        "VRMXT Materialize: failed for '"
                            + (string.IsNullOrEmpty(label) ? ("[" + index + "]") : label)
                            + "'."
                    );
                }

                EditorUtility.SetDirty(instance);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!canClear);
            if (GUILayout.Button("Clear", GUILayout.Width(56f)))
            {
                var label = nameProp?.stringValue;
                Undo.RecordObject(
                    instance,
                    string.IsNullOrEmpty(label)
                        ? "Clear Material Override"
                        : $"Clear Material Override ({label})"
                );
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
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    overrideProp,
                    new GUIContent(
                        "Override Material",
                        "Optional. Assign to author/rewrite the active unity slot from this asset. "
                            + "Sibling pipeline slots in extension JSON are kept. "
                            + "Materialize fills this from extension JSON."
                    )
                );
                if (EditorGUI.EndChangeCheck())
                {
                    // Re-enable renderer slot swaps after Swap Back / Clear suppressed them.
                    instance.ApplyOverridesToRenderers = true;
                    // Apply OverrideMaterial first so OnValidate Sync can read siblings from
                    // ExtensionJson, then reload SO so a later ApplyModifiedProperties does
                    // not stomp the multi-slot JSON Sync just wrote.
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShowOverrideMaterialsToggle()
        {
            var instance = (VrmxtMaterialsOverrideInstance)target;
            var canToggle = HasAnyOverrideMaterial(instance);
            var showOverrides =
                _applyOverridesToRenderers != null
                    ? _applyOverridesToRenderers.boolValue
                    : instance.ApplyOverridesToRenderers;

            EditorGUI.BeginDisabledGroup(!canToggle);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show Override Materials",
                    "On: put assigned Override Material assets on matching MeshRenderer slots. "
                        + "Off: put stock Source / MToon back. Does not clear Override Material "
                        + "or extension JSON."
                ),
                showOverrides
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(instance, "Toggle Show Override Materials");
                if (next)
                {
                    VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(
                        instance.gameObject,
                        instance
                    );
                }
                else
                {
                    VrmxtMaterialsOverrideAuthoring.RestoreSourceMaterialsToRenderers(
                        instance.gameObject,
                        instance
                    );
                }

                EditorUtility.SetDirty(instance);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }

            EditorGUI.EndDisabledGroup();
            if (!canToggle)
            {
                EditorGUILayout.HelpBox(
                    "Assign or Materialize Override Materials to enable this toggle.",
                    MessageType.None
                );
            }
        }

        private static bool HasAnyOverrideMaterial(VrmxtMaterialsOverrideInstance instance)
        {
            if (instance == null)
            {
                return false;
            }

            foreach (var pair in instance.Pairs)
            {
                if (pair?.OverrideMaterial != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stock | Imported | Authored | Imported + Authored, plus a one-line unity/engine summary.
        /// </summary>
        private static void BuildPairStatus(
            string extensionJson,
            bool hasOverrideMaterial,
            out string statusLabel,
            out string detail
        )
        {
            detail = null;
            var hasFileJson = !string.IsNullOrWhiteSpace(extensionJson);
            VrmxtMaterialsOverrideExtension extension = null;
            var parsed =
                hasFileJson && VrmxtMaterialsOverride.TryParse(extensionJson, out extension);
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
            bool hasOverrideMaterial
        )
        {
            var sb = new StringBuilder();
            var unityCount = 0;

            foreach (var entry in extension.Overrides)
            {
                if (
                    entry == null
                    || !string.Equals(
                        entry.Engine,
                        VrmxtMaterialsOverride.EngineUnity,
                        System.StringComparison.Ordinal
                    )
                )
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
                if (
                    entry == null
                    || string.Equals(
                        entry.Engine,
                        VrmxtMaterialsOverride.EngineUnity,
                        System.StringComparison.Ordinal
                    )
                )
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
                sb.Append(
                    " · local Override Material assigned (sync upserts active unity slot only)"
                );
            }

            return sb.ToString();
        }
    }
}
