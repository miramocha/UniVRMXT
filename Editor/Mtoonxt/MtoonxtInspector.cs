using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;
using VRM10.MToon10.Editor;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Reuses UniVRM <see cref="MToonInspector"/>. Stencil is authored once on the avatar's
    /// <see cref="VrmxtMaterialsMtoonxtInstance"/> root graph.
    /// </summary>
    public sealed class MtoonxtInspector : ShaderGUI
    {
        private readonly MToonInspector _mtoon = new MToonInspector();

        public override void AssignNewShaderToMaterial(
            Material material,
            Shader oldShader,
            Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            VrmxtMaterialsMtoonxtApplier.RestoreUnityMtoonPassSettings(material);
            VrmxtMaterialsMtoonxtApplier.ApplyStencilOffDefaults(material);
            VrmxtMaterialsMtoonxtApplier.ApplyZTest(material, VrmxtMaterialsMtoonxt.ZTestDefault);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _mtoon.OnGUI(materialEditor, properties);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MToonXT stencil", EditorStyles.boldLabel);

            var drewAny = false;
            Material firstMissing = null;
            foreach (var target in materialEditor.targets)
            {
                var material = target as Material;
                if (material == null)
                {
                    continue;
                }

                VrmxtMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);

                if (!VrmxtMaterialsMtoonxtStencilGui.TryFindPair(
                        material,
                        out var instance,
                        out var pair))
                {
                    if (firstMissing == null)
                    {
                        firstMissing = material;
                    }

                    continue;
                }

                drewAny = true;
                if (materialEditor.targets.Length > 1)
                {
                    EditorGUILayout.LabelField(material.name, EditorStyles.boldLabel);
                }

                var so = new SerializedObject(instance);
                so.Update();
                EditorGUILayout.PropertyField(
                    so.FindProperty("stencilRelationships"), new GUIContent("Stencil"),
                    includeChildren: true);
                so.ApplyModifiedProperties();
            }

            if (drewAny)
            {
                return;
            }

            if (firstMissing != null &&
                VrmxtMaterialsMtoonxtStencilGui.TryFindAvatarRoot(firstMissing, out _))
            {
                EditorGUILayout.HelpBox(
                    "No stencil graph on this avatar yet. Add MToonXT extras, then configure writer and reader materials.",
                    MessageType.Info);
                if (GUILayout.Button("Add MToonXT extras"))
                {
                    foreach (var target in materialEditor.targets)
                    {
                        var material = target as Material;
                        if (material == null)
                        {
                            continue;
                        }

                        VrmxtMaterialsMtoonxtStencilGui.TryAddExtras(material, out _, out _);
                    }

                    GUIUtility.ExitGUI();
                }

                return;
            }

            EditorGUILayout.HelpBox(
                "Assign this MToonXT material on an avatar mesh (select the avatar), "
                    + "then click Add MToonXT extras. Switch stock MToon to VRMXT/MToonXT10 first.",
                MessageType.Info);
        }
    }
}
