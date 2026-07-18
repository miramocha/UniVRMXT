using System;
using System.IO;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// Fallback when Extended-UniVRM import hooks are absent or disabled: build sibling
    /// <c>*.vrmxt.prefab</c>. When hooks are available and Project Settings/VRM10 enable them,
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

            // Extended-UniVRM + Project Settings enable: VFX already on .vrm via import hooks.
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
                VrmxtVfxImportSubAssets.PersistToPrefabAsset(
                    companionPath,
                    editable,
                    glbTextures);
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
    }
}
