using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;
using VRM10.MToon10.Editor;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Reuses UniVRM <see cref="MToonInspector"/>, then draws MToonXT stencil extras.
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
                "Hair overlay: do not stencil-punch hair (soft alpha cuts holes). Brow at queue 3000 with Z test LessEqual. Hair cutout zWrite off so the brow blends over it. Z test Always draws over the whole scene.",
                MessageType.Info);

            DrawIfPresent(materialEditor, properties, VrmcMaterialsMtoonxt.ZTestProp, "Z test");

            DrawStencilGroup(
                materialEditor,
                properties,
                "Stencil",
                "Enable stencil",
                VrmcMaterialsMtoonxt.StencilPropEnabled,
                VrmcMaterialsMtoonxt.StencilPropRef,
                VrmcMaterialsMtoonxt.StencilPropReadMask,
                VrmcMaterialsMtoonxt.StencilPropWriteMask,
                VrmcMaterialsMtoonxt.StencilPropComp,
                VrmcMaterialsMtoonxt.StencilPropPass,
                VrmcMaterialsMtoonxt.StencilPropFail,
                VrmcMaterialsMtoonxt.StencilPropZFail);

            DrawStencilGroup(
                materialEditor,
                properties,
                "Outline stencil",
                "Enable outline stencil",
                VrmcMaterialsMtoonxt.OutlineStencilPropEnabled,
                VrmcMaterialsMtoonxt.OutlineStencilPropRef,
                VrmcMaterialsMtoonxt.OutlineStencilPropReadMask,
                VrmcMaterialsMtoonxt.OutlineStencilPropWriteMask,
                VrmcMaterialsMtoonxt.OutlineStencilPropComp,
                VrmcMaterialsMtoonxt.OutlineStencilPropPass,
                VrmcMaterialsMtoonxt.OutlineStencilPropFail,
                VrmcMaterialsMtoonxt.OutlineStencilPropZFail);

            foreach (var target in materialEditor.targets)
            {
                var material = target as Material;
                if (material != null)
                {
                    VrmcMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);
                }
            }
        }

        private static void DrawStencilGroup(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string title,
            string enableLabel,
            string enableName,
            string refName,
            string readMaskName,
            string writeMaskName,
            string compName,
            string passName,
            string failName,
            string zfailName)
        {
            var enable = FindProperty(enableName, properties, false);
            if (enable == null)
            {
                return;
            }

            using (new LabelScope(title))
            {
                materialEditor.ShaderProperty(enable, enableLabel);
                var on = !enable.hasMixedValue && enable.floatValue >= 0.5f;
                EditorGUI.BeginDisabledGroup(!on);
                DrawIfPresent(materialEditor, properties, refName, "Ref");
                DrawIfPresent(materialEditor, properties, readMaskName, "Read mask");
                DrawIfPresent(materialEditor, properties, writeMaskName, "Write mask");
                DrawIfPresent(materialEditor, properties, compName, "Comp");
                DrawIfPresent(materialEditor, properties, passName, "Pass");
                DrawIfPresent(materialEditor, properties, failName, "Fail");
                DrawIfPresent(materialEditor, properties, zfailName, "ZFail");
                EditorGUI.EndDisabledGroup();
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
