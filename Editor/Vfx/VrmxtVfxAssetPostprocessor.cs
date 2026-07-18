using System;
using System.IO;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// After UniVRM <c>VrmScriptedImporter</c> finishes, build a companion
    /// <c>*.vrmxt.prefab</c> with <c>VRMXT_vfx</c> attached.
    /// ScriptedImporter main assets reject <c>AddComponent</c> in postprocessors, so VFX
    /// cannot be written onto the <c>.vrm</c> itself (see docs/architecture.md).
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
                PrefabUtility.SaveAsPrefabAsset(editable, companionPath);
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
