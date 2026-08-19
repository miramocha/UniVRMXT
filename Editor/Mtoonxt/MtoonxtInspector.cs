using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;
using VRM10.MToon10.Editor;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Reuses UniVRM <see cref="MToonInspector"/>. Stencil ops match the Blender panel
    /// and write into <see cref="VrmcMaterialsMtoonxtInstance"/> pair JSON.
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
            VrmcMaterialsMtoonxtApplier.RestoreUnityMtoonPassSettings(material);
            VrmcMaterialsMtoonxtApplier.ApplyStencilOffDefaults(material);
            VrmcMaterialsMtoonxtApplier.ApplyZTest(material, VrmcMaterialsMtoonxt.ZTestDefault);
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

                VrmcMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);

                if (!VrmcMaterialsMtoonxtStencilGui.TryFindPair(
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
                var pairIndex = VrmcMaterialsMtoonxtStencilGui.IndexOfPair(instance, pair);
                VrmcMaterialsMtoonxtStencilGui.DrawPair(
                    so,
                    instance,
                    pair,
                    pairIndex);
            }

            if (drewAny)
            {
                return;
            }

            if (firstMissing != null &&
                VrmcMaterialsMtoonxtStencilGui.TryFindAvatarRoot(firstMissing, out _))
            {
                EditorGUILayout.HelpBox(
                    "No stencil settings on this avatar yet. Click Add MToonXT extras, then set Write or clip.",
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

                        VrmcMaterialsMtoonxtStencilGui.TryAddExtras(material, out _, out _);
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
