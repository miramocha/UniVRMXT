using System;
using System.Collections.Generic;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// Persist VFX particle materials/textures as sub-assets. Textures must be added
    /// before materials; otherwise Unity drops texture slots on serialize.
    /// </summary>
    internal static class VrmxtVfxImportSubAssets
    {
        public static void Persist(
            GameObject root,
            VrmxtVfxGlbTextures glbTextures,
            Action<string, UnityEngine.Object> addObjectToAsset)
        {
            if (root == null || addObjectToAsset == null)
            {
                return;
            }

            var materialTextures = CaptureMaterialTextures(root);

            if (glbTextures != null)
            {
                var i = 0;
                foreach (var texture in glbTextures.CachedTextures)
                {
                    if (texture == null)
                    {
                        continue;
                    }

                    addObjectToAsset("VRMXT_sprite_particle_tex_" + i, texture);
                    i++;
                }

                glbTextures.ReleaseOwnership();
            }

            // Re-bind after textures are assets — AddObjectToAsset(material) first would
            // serialize null texture refs.
            foreach (var pair in materialTextures)
            {
                VrmxtVfxParticleSystemMapper.ApplyTextureToMaterial(pair.Key, pair.Value);
            }

            var instance = root.GetComponentInChildren<VrmxtVfxInstance>(true);
            instance?.SyncTexturesFromParticleMaterials();

            var matIndex = 0;
            foreach (var pair in materialTextures)
            {
                var material = pair.Key;
                if (material == null)
                {
                    continue;
                }

                addObjectToAsset("VRMXT_sprite_particle_mat_" + matIndex, material);
                matIndex++;
            }
        }

        public static void PersistToPrefabAsset(
            string prefabPath,
            GameObject editableRoot,
            VrmxtVfxGlbTextures glbTextures)
        {
            if (string.IsNullOrEmpty(prefabPath) || editableRoot == null)
            {
                return;
            }

            var materialTextures = CaptureMaterialTextures(editableRoot);

            // Create an empty prefab shell first so textures can be sub-asseted before any
            // material is written (SaveAsPrefabAsset with mats first drops albedo refs).
            var shellName = editableRoot.name;
            var shell = new GameObject(shellName);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(shell, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shell);
            }

            if (glbTextures != null)
            {
                foreach (var texture in glbTextures.CachedTextures)
                {
                    if (texture == null || AssetDatabase.Contains(texture))
                    {
                        continue;
                    }

                    AssetDatabase.AddObjectToAsset(texture, prefabPath);
                    EditorUtility.SetDirty(texture);
                }

                glbTextures.ReleaseOwnership();
            }

            foreach (var pair in materialTextures)
            {
                var material = pair.Key;
                var texture = pair.Value;
                if (material == null)
                {
                    continue;
                }

                VrmxtVfxParticleSystemMapper.ApplyTextureToMaterial(material, texture);

                if (!AssetDatabase.Contains(material))
                {
                    AssetDatabase.AddObjectToAsset(material, prefabPath);
                }

                EditorUtility.SetDirty(material);
            }

            var instance = editableRoot.GetComponentInChildren<VrmxtVfxInstance>(true);
            instance?.SyncTexturesFromParticleMaterials();

            // Hierarchy last — materials/textures are already assets, so refs survive.
            PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);

            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot != null)
            {
                EditorUtility.SetDirty(prefabRoot);
            }
        }

        private static List<KeyValuePair<Material, Texture>> CaptureMaterialTextures(GameObject root)
        {
            var list = new List<KeyValuePair<Material, Texture>>();
            if (root == null)
            {
                return list;
            }

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (!VrmxtVfxParticleSystemMapper.IsOwnedParticleMaterial(material))
                {
                    continue;
                }

                list.Add(new KeyValuePair<Material, Texture>(
                    material,
                    VrmxtVfxParticleSystemMapper.ReadAssignedTexture(material)));
            }

            return list;
        }
    }
}
