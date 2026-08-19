using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Author stencil <c>op</c> and clip target materials; writes glTF indices into pair JSON.
    /// </summary>
    [CustomEditor(typeof(VrmcMaterialsMtoonxtInstance))]
    public sealed class VrmcMaterialsMtoonxtInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var instance = (VrmcMaterialsMtoonxtInstance)target;

            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Clip inside and Clip outside list materials on this avatar. Export writes them into the VRM. "
                    + "Switch mesh materials to VRMXT/MToonXT10, then click Add extras from MToonXT materials.",
                MessageType.Info);

            if (GUILayout.Button("Add extras from MToonXT materials"))
            {
                VrmcMaterialsMtoonxtStencilGui.AddExtrasFromRenderers(instance);
                GUIUtility.ExitGUI();
            }

            if (instance == null || instance.Pairs.Count == 0)
            {
                EditorGUILayout.LabelField("No stencil settings attached.");
                return;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var pair = instance.Pairs[i];
                if (pair == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(pair.MaterialName) ? "[" + i + "]" : pair.MaterialName,
                    EditorStyles.boldLabel);
                VrmcMaterialsMtoonxtStencilGui.DrawPair(
                    serializedObject,
                    instance,
                    pair,
                    i);
                EditorGUILayout.EndVertical();
            }
        }
    }
}
