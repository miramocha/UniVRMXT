using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;
using VRM10.MToon10.Editor;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Reuses UniVRM <see cref="MToonInspector"/>. Stencil clip targets live on
    /// <see cref="VrmcMaterialsMtoonxtInstance"/>.
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

            EditorGUILayout.HelpBox(
                "Stencil write / clip inside / clip outside is on the avatar MToonXT component. "
                    + "Z test Always draws over the whole scene.",
                MessageType.Info);

            DrawIfPresent(materialEditor, properties, VrmcMaterialsMtoonxt.ZTestProp, "Z test");

            foreach (var target in materialEditor.targets)
            {
                var material = target as Material;
                if (material != null)
                {
                    VrmcMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);
                }
            }
        }

        private static void DrawIfPresent(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string name,
            string label)
        {
            var property = FindProperty(name, properties, false);
            if (property != null)
            {
                materialEditor.ShaderProperty(property, label);
            }
        }
    }
}
