using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Shared stencil authoring widgets. Edits Unity fields on the avatar instance.
    /// Export writes glTF JSON.
    /// </summary>
    internal static class VrmcMaterialsMtoonxtStencilGui
    {
        public static bool TryFindPair(
            Material material,
            out VrmcMaterialsMtoonxtInstance instance,
            out VrmcMaterialsMtoonxtPair pair
        )
        {
            instance = null;
            pair = null;
            if (material == null)
            {
                return false;
            }

            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var fromSelection = selected.GetComponentInParent<VrmcMaterialsMtoonxtInstance>();
                if (fromSelection != null && TryFindPair(fromSelection, material, out pair))
                {
                    instance = fromSelection;
                    return true;
                }
            }

            var found = UnityEngine.Object.FindObjectsByType<VrmcMaterialsMtoonxtInstance>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            for (var i = 0; i < found.Length; i++)
            {
                if (TryFindPair(found[i], material, out pair))
                {
                    instance = found[i];
                    return true;
                }
            }

            return false;
        }

        public static bool TryAddExtras(
            Material material,
            out VrmcMaterialsMtoonxtInstance instance,
            out VrmcMaterialsMtoonxtPair pair
        )
        {
            instance = null;
            pair = null;
            if (material == null || !IsMtoonxtShader(material))
            {
                return false;
            }

            if (TryFindPair(material, out instance, out pair))
            {
                return true;
            }

            if (!TryFindAvatarRoot(material, out var root) || root == null)
            {
                return false;
            }

            instance = EnsureInstance(root);
            if (instance == null)
            {
                return false;
            }

            pair = CreatePair(instance, root, material);
            if (pair == null)
            {
                return false;
            }

            AppendPair(instance, pair);
            return true;
        }

        public static int AddExtrasFromRenderers(VrmcMaterialsMtoonxtInstance instance)
        {
            if (instance == null)
            {
                return 0;
            }

            var root = instance.gameObject;
            var added = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterials;
                for (var j = 0; j < shared.Length; j++)
                {
                    var material = shared[j];
                    if (material == null || !IsMtoonxtShader(material))
                    {
                        continue;
                    }

                    if (TryFindPair(instance, material, out _))
                    {
                        continue;
                    }

                    var pair = CreatePair(instance, root, material);
                    if (pair == null)
                    {
                        continue;
                    }

                    AppendPair(instance, pair);
                    added++;
                }
            }

            return added;
        }

        public static bool TryFindAvatarRoot(Material material, out GameObject root)
        {
            root = null;
            if (material == null)
            {
                return false;
            }

            var selected = Selection.activeGameObject;
            if (selected != null && RendererUsesMaterial(selected, material, includeChildren: true))
            {
                root = ResolveAvatarRoot(selected);
                return root != null;
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!RendererUsesMaterial(renderers[i], material))
                {
                    continue;
                }

                root = ResolveAvatarRoot(renderers[i].gameObject);
                return root != null;
            }

            return false;
        }

        public static int IndexOfPair(
            VrmcMaterialsMtoonxtInstance instance,
            VrmcMaterialsMtoonxtPair pair
        )
        {
            if (instance == null || pair == null)
            {
                return -1;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                if (instance.Pairs[i] == pair)
                {
                    return i;
                }
            }

            return -1;
        }

        public static void DrawPair(
            SerializedObject serializedInstance,
            VrmcMaterialsMtoonxtInstance instance,
            VrmcMaterialsMtoonxtPair pair,
            int pairIndex
        )
        {
            if (instance == null || pair == null || serializedInstance == null)
            {
                return;
            }

            serializedInstance.Update();
            var pairsProperty = serializedInstance.FindProperty("pairs");
            if (pairsProperty == null || pairIndex < 0 || pairIndex >= pairsProperty.arraySize)
            {
                return;
            }

            var element = pairsProperty.GetArrayElementAtIndex(pairIndex);
            var bodyOp = element.FindPropertyRelative("BodyOp");
            var outlineOp = element.FindPropertyRelative("OutlineOp");
            var bodyList = element.FindPropertyRelative("StencilTargets");
            var outlineList = element.FindPropertyRelative("OutlineStencilTargets");

            EditorGUILayout.PropertyField(bodyOp, new GUIContent("Stencil"));
            var bodyOpValue =
                bodyOp != null
                    ? (VrmcMtoonxtBodyStencilOp)bodyOp.enumValueIndex
                    : VrmcMtoonxtBodyStencilOp.Off;
            var bodyIsInsideClip =
                bodyOpValue == VrmcMtoonxtBodyStencilOp.ClipInside
                || bodyOpValue == VrmcMtoonxtBodyStencilOp.ClipInsideOverlay;
            if (
                (
                    bodyIsInsideClip
                    || bodyOpValue == VrmcMtoonxtBodyStencilOp.ClipOutside
                )
                && bodyList != null
            )
            {
                EditorGUILayout.PropertyField(
                    bodyList,
                    new GUIContent("Clip against writers"),
                    true
                );
            }

            EditorGUILayout.PropertyField(outlineOp, new GUIContent("Outline stencil"));
            var outlineOpValue =
                outlineOp != null
                    ? (VrmcMtoonxtOutlineStencilOp)outlineOp.enumValueIndex
                    : VrmcMtoonxtOutlineStencilOp.Off;
            if (
                (
                    outlineOpValue == VrmcMtoonxtOutlineStencilOp.ClipInside
                    || outlineOpValue == VrmcMtoonxtOutlineStencilOp.ClipInsideOverlay
                    || outlineOpValue == VrmcMtoonxtOutlineStencilOp.ClipOutside
                )
                && outlineList != null
            )
            {
                EditorGUILayout.PropertyField(
                    outlineList,
                    new GUIContent("Outline clip against writers"),
                    true
                );
            }

            serializedInstance.ApplyModifiedProperties();

            ApplyUtilityDepthToPairMaterials(instance, pair, bodyIsInsideClip);

            var warnings = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(instance, pair);
            for (var i = 0; i < warnings.Count; i++)
            {
                var warning = warnings[i];
                EditorGUILayout.HelpBox(
                    warning.Headline + "\n" + warning.Detail,
                    MessageType.Warning
                );
            }
        }

        private static void ApplyUtilityDepthToPairMaterials(
            VrmcMaterialsMtoonxtInstance instance,
            VrmcMaterialsMtoonxtPair pair,
            bool skip
        )
        {
            if (instance == null || pair == null)
            {
                return;
            }

            foreach (
                var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                    instance.gameObject,
                    pair.MaterialName
                )
            )
            {
                VrmcMaterialsMtoonxtApplier.ApplyUtilityDepthPasses(material, skip);
            }
        }

        private static bool TryFindPair(
            VrmcMaterialsMtoonxtInstance instance,
            Material material,
            out VrmcMaterialsMtoonxtPair pair
        )
        {
            pair = null;
            if (instance == null || material == null)
            {
                return false;
            }

            var root = instance.gameObject;
            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var candidate = instance.Pairs[i];
                if (candidate == null)
                {
                    continue;
                }

                foreach (
                    var found in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                        root,
                        candidate.MaterialName
                    )
                )
                {
                    if (found == material)
                    {
                        pair = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static VrmcMaterialsMtoonxtPair CreatePair(
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            Material material
        )
        {
            return new VrmcMaterialsMtoonxtPair(
                MakeStoreKey(instance, root, material),
                null,
                NextGltfIndex(instance)
            );
        }

        private static void AppendPair(
            VrmcMaterialsMtoonxtInstance instance,
            VrmcMaterialsMtoonxtPair pair
        )
        {
            Undo.RecordObject(instance, "Add MToonXT extras");
            var next = new List<VrmcMaterialsMtoonxtPair>(instance.Pairs.Count + 1);
            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                next.Add(instance.Pairs[i]);
            }

            next.Add(pair);
            instance.SetPairs(next);
            EditorUtility.SetDirty(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }

        private static VrmcMaterialsMtoonxtInstance EnsureInstance(GameObject root)
        {
            var instance = root.GetComponent<VrmcMaterialsMtoonxtInstance>();
            if (instance != null)
            {
                return instance;
            }

            return Undo.AddComponent<VrmcMaterialsMtoonxtInstance>(root);
        }

        private static GameObject ResolveAvatarRoot(GameObject from)
        {
            if (from == null)
            {
                return null;
            }

            var xt = from.GetComponentInParent<VrmcMaterialsMtoonxtInstance>();
            if (xt != null)
            {
                return xt.gameObject;
            }

            var overrideStore = from.GetComponentInParent<VrmxtMaterialsOverrideInstance>();
            if (overrideStore != null)
            {
                return overrideStore.gameObject;
            }

            var animator = from.GetComponentInParent<Animator>();
            if (animator != null)
            {
                return animator.gameObject;
            }

            return from.transform.root.gameObject;
        }

        private static bool RendererUsesMaterial(
            GameObject gameObject,
            Material material,
            bool includeChildren
        )
        {
            if (gameObject == null || material == null)
            {
                return false;
            }

            var renderers = includeChildren
                ? gameObject.GetComponentsInChildren<Renderer>(true)
                : gameObject.GetComponents<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                if (RendererUsesMaterial(renderers[i], material))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RendererUsesMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null)
            {
                return false;
            }

            var shared = renderer.sharedMaterials;
            for (var i = 0; i < shared.Length; i++)
            {
                if (shared[i] == material)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMtoonxtShader(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            var name = material.shader.name;
            return name == VrmcMaterialsMtoonxt.BuiltinShaderName
                || name == VrmcMaterialsMtoonxt.UrpShaderName;
        }

        private static string MakeStoreKey(
            VrmcMaterialsMtoonxtInstance instance,
            GameObject root,
            Material material
        )
        {
            var baseName = VrmxtMaterialsOverrideRuntime.StripUnityInstanceSuffix(material.name);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = material.name;
            }

            if (!StoreKeyTaken(instance, baseName))
            {
                return baseName;
            }

            var occurrence = 0;
            foreach (
                var candidate in VrmxtMaterialsOverrideApplier.FindMaterialsByName(root, baseName)
            )
            {
                occurrence++;
                if (candidate == material)
                {
                    break;
                }
            }

            if (occurrence < 1)
            {
                occurrence = 1;
            }

            var keyed = baseName + "#" + occurrence;
            if (!StoreKeyTaken(instance, keyed))
            {
                return keyed;
            }

            var suffix = occurrence;
            while (StoreKeyTaken(instance, baseName + "#" + suffix))
            {
                suffix++;
            }

            return baseName + "#" + suffix;
        }

        private static bool StoreKeyTaken(VrmcMaterialsMtoonxtInstance instance, string key)
        {
            if (instance == null)
            {
                return false;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var pair = instance.Pairs[i];
                if (pair != null && string.Equals(pair.MaterialName, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int NextGltfIndex(VrmcMaterialsMtoonxtInstance instance)
        {
            var next = 0;
            if (instance == null)
            {
                return next;
            }

            for (var i = 0; i < instance.Pairs.Count; i++)
            {
                var pair = instance.Pairs[i];
                if (pair != null && pair.GltfMaterialIndex >= next)
                {
                    next = pair.GltfMaterialIndex + 1;
                }
            }

            return next;
        }
    }
}
