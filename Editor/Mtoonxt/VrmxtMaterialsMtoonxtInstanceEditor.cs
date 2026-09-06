using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Author the portable root stencil graph and its material targets.
    /// </summary>
    [CustomEditor(typeof(VrmxtMaterialsMtoonxtInstance))]
    public sealed class VrmxtMaterialsMtoonxtInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var instance = (VrmxtMaterialsMtoonxtInstance)target;

            if (GUILayout.Button("Register MToonXT materials"))
            {
                VrmxtMaterialsMtoonxtStencilGui.AddExtrasFromRenderers(instance);
            }

            serializedObject.Update();

            EditorGUILayout.LabelField("Stencil", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Portable root-level writer/reader presentation. These settings round-trip "
                    + "with Blender VRMXT and compile to Unity stencil, depth, cull, and retained auxiliary passes.",
                MessageType.Info
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("stencils"), new GUIContent("Stencil"),
                includeChildren: true
            );
            serializedObject.ApplyModifiedProperties();
        }
    }
}
