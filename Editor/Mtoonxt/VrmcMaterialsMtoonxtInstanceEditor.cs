using System.Collections.Generic;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
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
        private static readonly string[] BodyOps = { "Off", "Write", "Clip inside", "Clip outside" };
        private static readonly string[] OutlineOps =
        {
            "Off",
            "Same as body",
            "Write",
            "Clip inside",
            "Clip outside",
        };

        public override void OnInspectorGUI()
        {
            var instance = (VrmcMaterialsMtoonxtInstance)target;
            var root = instance != null ? instance.gameObject : null;

            EditorGUILayout.HelpBox(
                "Clip inside / clip outside lists avatar materials. Export writes glTF material indices.",
                MessageType.Info);

            if (instance == null || instance.Pairs.Count == 0)
            {
                EditorGUILayout.LabelField("No MToonXT extras attached.");
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
                DrawPair(instance, root, pair);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPair(VrmcMaterialsMtoonxtInstance instance, GameObject root, VrmcMaterialsMtoonxtPair pair)
        {
            if (!VrmcMaterialsMtoonxt.TryParse(pair.ExtensionJson, out var xt))
            {
                EditorGUILayout.HelpBox("Invalid extension JSON.", MessageType.Warning);
                return;
            }

            var bodyOp = OpToBodyIndex(xt.Stencil);
            var outlineOp = OpToOutlineIndex(xt.OutlineStencil);
            var bodyTargets = ToMaterialList(root, instance, xt.Stencil);
            var outlineTargets = ToMaterialList(root, instance, xt.OutlineStencil);

            EditorGUI.BeginChangeCheck();
            bodyOp = EditorGUILayout.Popup("Stencil", bodyOp, BodyOps);
            if (bodyOp == 2 || bodyOp == 3)
            {
                DrawMaterialList("Stencil targets", bodyTargets);
            }

            var outlineWidthOn = OutlineWidthOn(root, pair);
            EditorGUI.BeginDisabledGroup(!outlineWidthOn && outlineOp == 0);
            outlineOp = EditorGUILayout.Popup("Outline stencil", outlineOp, OutlineOps);
            EditorGUI.EndDisabledGroup();
            if (outlineOp == 3 || outlineOp == 4)
            {
                DrawMaterialList("Outline stencil targets", outlineTargets);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(instance, "Edit MToonXT stencil");
            var body = BodyFromUi(bodyOp, instance, root, bodyTargets);
            var outline = OutlineFromUi(outlineOp, instance, root, outlineTargets);
            var next = new VrmcMaterialsMtoonxtExtension(
                body,
                outline,
                xt.ZTest,
                xt.ZWrite);
            pair.ExtensionJson = VrmcMaterialsMtoonxt.ToJson(next);
            EditorUtility.SetDirty(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }

        private static void DrawMaterialList(string label, List<Material> list)
        {
            EditorGUILayout.LabelField(label);
            for (var i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                list[i] = (Material)EditorGUILayout.ObjectField(list[i], typeof(Material), true);
                if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                {
                    list.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add material"))
            {
                list.Add(null);
            }
        }

        private static int OpToBodyIndex(VrmcMaterialsMtoonxtStencil stencil)
        {
            if (stencil == null || !stencil.HasOp)
            {
                return 0;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpWrite)
            {
                return 1;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpInside)
            {
                return 2;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpOutside)
            {
                return 3;
            }

            return 0;
        }

        private static int OpToOutlineIndex(VrmcMaterialsMtoonxtStencil stencil)
        {
            if (stencil == null || !stencil.HasOp)
            {
                return 0;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpSame)
            {
                return 1;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpWrite)
            {
                return 2;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpInside)
            {
                return 3;
            }

            if (stencil.Op == VrmcMaterialsMtoonxtStencil.OpOutside)
            {
                return 4;
            }

            return 0;
        }

        private static VrmcMaterialsMtoonxtStencil BodyFromUi(
            int opIndex,
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            List<Material> targets)
        {
            switch (opIndex)
            {
                case 1:
                    return VrmcMaterialsMtoonxtStencil.FromOp(VrmcMaterialsMtoonxtStencil.OpWrite, null);
                case 2:
                    return VrmcMaterialsMtoonxtStencil.FromOp(
                        VrmcMaterialsMtoonxtStencil.OpInside,
                        MaterialsToIndices(instance, root, targets));
                case 3:
                    return VrmcMaterialsMtoonxtStencil.FromOp(
                        VrmcMaterialsMtoonxtStencil.OpOutside,
                        MaterialsToIndices(instance, root, targets));
                default:
                    return null;
            }
        }

        private static VrmcMaterialsMtoonxtStencil OutlineFromUi(
            int opIndex,
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            List<Material> targets)
        {
            switch (opIndex)
            {
                case 1:
                    return VrmcMaterialsMtoonxtStencil.FromOp(VrmcMaterialsMtoonxtStencil.OpSame, null);
                case 2:
                    return VrmcMaterialsMtoonxtStencil.FromOp(VrmcMaterialsMtoonxtStencil.OpWrite, null);
                case 3:
                    return VrmcMaterialsMtoonxtStencil.FromOp(
                        VrmcMaterialsMtoonxtStencil.OpInside,
                        MaterialsToIndices(instance, root, targets));
                case 4:
                    return VrmcMaterialsMtoonxtStencil.FromOp(
                        VrmcMaterialsMtoonxtStencil.OpOutside,
                        MaterialsToIndices(instance, root, targets));
                default:
                    return null;
            }
        }

        private static List<Material> ToMaterialList(
            GameObject root,
            VrmcMaterialsMtoonxtInstance instance,
            VrmcMaterialsMtoonxtStencil stencil)
        {
            var list = new List<Material>();
            if (stencil == null || stencil.Materials == null)
            {
                return list;
            }

            for (var i = 0; i < stencil.Materials.Count; i++)
            {
                list.Add(FindMaterial(instance, root, stencil.Materials[i]));
            }

            return list;
        }

        private static List<int> MaterialsToIndices(
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            List<Material> materials)
        {
            var indices = new List<int>();
            if (materials == null)
            {
                return indices;
            }

            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    continue;
                }

                var index = FindGltfIndex(instance, root, material);
                if (index >= 0 && !indices.Contains(index))
                {
                    indices.Add(index);
                }
            }

            return indices;
        }

        private static Material FindMaterial(
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            int gltfIndex)
        {
            if (instance == null || root == null)
            {
                return null;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var pair = instance.Pairs[i];
                if (pair == null || pair.GltfMaterialIndex != gltfIndex)
                {
                    continue;
                }

                foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root,
                             pair.MaterialName))
                {
                    if (material != null)
                    {
                        return material;
                    }
                }
            }

            return null;
        }

        private static int FindGltfIndex(
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            Material material)
        {
            if (instance == null || material == null)
            {
                return -1;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var pair = instance.Pairs[i];
                if (pair == null)
                {
                    continue;
                }

                foreach (var candidate in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root,
                             pair.MaterialName))
                {
                    if (candidate == material)
                    {
                        return pair.GltfMaterialIndex;
                    }
                }
            }

            return -1;
        }

        private static bool OutlineWidthOn(GameObject root, VrmcMaterialsMtoonxtPair pair)
        {
            if (root == null || pair == null)
            {
                return false;
            }

            foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                         root,
                         pair.MaterialName))
            {
                if (material != null &&
                    material.HasProperty("_OutlineWidthMode") &&
                    material.GetInt("_OutlineWidthMode") != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
