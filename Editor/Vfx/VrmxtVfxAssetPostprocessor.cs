using System;
using System.Collections.Generic;
using System.IO;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// Fallback when Extended-UniVRM import hooks are absent or disabled: build sibling
    /// <c>*.vrmxt.prefab</c>. When hooks are available and Preferences/VRM10 enable them,
    /// VFX is written onto the original <c>.vrm</c> during import and this postprocessor
    /// skips companion creation.
    /// </summary>
    public sealed class VrmxtVfxAssetPostprocessor : AssetPostprocessor
    {
        public const string CompanionExtension = ".vrmxt.prefab";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null)
            {
                return;
            }

            // Extended-UniVRM + Preferences enable: VFX already on .vrm via import hooks.
            if (VrmxtVfxImportHookBootstrap.ImportHooksAvailable)
            {
                for (var i = 0; i < importedAssets.Length; i++)
                {
                    TryDeleteStaleCompanion(importedAssets[i]);
                }

                return;
            }

            var dirty = false;
            for (var i = 0; i < importedAssets.Length; i++)
            {
                try
                {
                    if (TryProcessVrm(importedAssets[i]))
                    {
                        dirty = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (dirty)
            {
                AssetDatabase.SaveAssets();
            }
        }

        internal static bool TryProcessVrm(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.EndsWith(".vrm", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (sourceRoot == null)
            {
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(assetPath);
            }
            catch (IOException)
            {
                return false;
            }

            // Missing / invalid extension → no companion write.
            if (!GlbChunks.TryExtract(bytes, out var json, out _) ||
                !VrmxtVfx.TryParse(json, out _))
            {
                return false;
            }

            if (!VrmxtVfxNodeResolver.TryReadNodeNames(json, out var nodeNames))
            {
                return false;
            }

            // PrefabUtility keeps sub-asset material refs; Object.Instantiate breaks them
            // (null sharedMaterials → NRE in Vrm10InstanceEditor).
            var editable = PrefabUtility.InstantiatePrefab(sourceRoot) as GameObject;
            if (editable == null)
            {
                return false;
            }

            editable.name = sourceRoot.name;
            VrmxtVfxGlbTextures glbTextures = null;
            try
            {
                var resolveNode = VrmxtVfxNodeResolver.CreateResolver(
                    editable.transform,
                    nodeNames);

                // Second read of the same bytes: decode VFX-only textures UniVRM skipped.
                if (!VrmxtVfxRuntime.TryAttachFromGlb(
                        editable,
                        bytes,
                        resolveNode,
                        out _,
                        out glbTextures))
                {
                    return false;
                }

                var companionPath = GetCompanionPrefabPath(assetPath);
                var ownedMaterials = CollectOwnedParticleMaterials(editable);
                PrefabUtility.SaveAsPrefabAsset(editable, companionPath);
                PersistOwnedParticleMaterials(companionPath, ownedMaterials);
                PersistDecodedTextures(companionPath, glbTextures);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editable);
                glbTextures?.Dispose();
            }
        }

        public static string GetCompanionPrefabPath(string vrmAssetPath)
        {
            var extension = Path.GetExtension(vrmAssetPath);
            var withoutExt = vrmAssetPath.Substring(0, vrmAssetPath.Length - extension.Length);
            return withoutExt + CompanionExtension;
        }

        private static void TryDeleteStaleCompanion(string vrmAssetPath)
        {
            if (string.IsNullOrEmpty(vrmAssetPath) ||
                !vrmAssetPath.EndsWith(".vrm", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var companionPath = GetCompanionPrefabPath(vrmAssetPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(companionPath) != null)
            {
                AssetDatabase.DeleteAsset(companionPath);
            }
        }

        private static List<Material> CollectOwnedParticleMaterials(GameObject root)
        {
            var list = new List<Material>();
            if (root == null)
            {
                return list;
            }

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (var r = 0; r < renderers.Length; r++)
            {
                var material = renderers[r] != null ? renderers[r].sharedMaterial : null;
                if (!VrmxtVfxParticleSystemMapper.IsOwnedParticleMaterial(material))
                {
                    continue;
                }

                list.Add(material);
            }

            return list;
        }

        private static void PersistOwnedParticleMaterials(
            string prefabPath,
            List<Material> ownedMaterials)
        {
            if (string.IsNullOrEmpty(prefabPath) || ownedMaterials == null || ownedMaterials.Count == 0)
            {
                return;
            }

            for (var i = 0; i < ownedMaterials.Count; i++)
            {
                var material = ownedMaterials[i];
                if (material == null || AssetDatabase.Contains(material))
                {
                    continue;
                }

                AssetDatabase.AddObjectToAsset(material, prefabPath);
                EditorUtility.SetDirty(material);
            }

            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot != null)
            {
                EditorUtility.SetDirty(prefabRoot);
            }
        }

        private static void PersistDecodedTextures(string prefabPath, VrmxtVfxGlbTextures glbTextures)
        {
            if (glbTextures == null)
            {
                return;
            }

            foreach (var texture in glbTextures.CachedTextures)
            {
                if (texture == null || AssetDatabase.Contains(texture))
                {
                    continue;
                }

                AssetDatabase.AddObjectToAsset(texture, prefabPath);
                EditorUtility.SetDirty(texture);
            }

            // Prefab now owns the Texture2D objects — do not Destroy them on Dispose.
            glbTextures.ReleaseOwnership();
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }
    }
}
