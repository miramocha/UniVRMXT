using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Editor-only Materialize: VRMXT override JSON → durable <c>.mat</c> assets assigned to
    /// <see cref="VrmxtMaterialsOverridePair.OverrideMaterial"/>, then swapped into matching
    /// renderer slots (SourceMaterial stays stock MToon; not mutated).
    /// </summary>
    public static class VrmxtMaterialsOverrideMaterialize
    {
        /// <summary>
        /// Materialize every pair that has a selectable unity override and resolvable shader.
        /// Returns how many pairs produced or updated a <c>.mat</c>.
        /// </summary>
        public static int MaterializeAll(
            VrmxtMaterialsOverrideInstance instance,
            string folderAssetPath = null
        )
        {
            if (instance == null)
            {
                return 0;
            }

            folderAssetPath = EnsureFolder(instance, folderAssetPath);
            if (string.IsNullOrEmpty(folderAssetPath))
            {
                return 0;
            }

            Undo.RecordObject(instance, "Materialize All Materials");
            var gltfJson = TryLoadGltfJsonForInstance(instance);
            var pipeline = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
            Func<int, Texture> resolveTexture = index =>
                instance.TryGetImportedTexture(index, out var texture) ? texture : null;

            var count = 0;
            var pairs = instance.Pairs;
            for (var i = 0; i < pairs.Count; i++)
            {
                if (
                    TryMaterializePairCore(
                        instance,
                        i,
                        folderAssetPath,
                        gltfJson,
                        pipeline,
                        resolveTexture
                    )
                )
                {
                    count++;
                }
            }

            if (count > 0)
            {
                EditorUtility.SetDirty(instance);
                AssetDatabase.SaveAssets();
                VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(
                    instance.gameObject,
                    instance
                );
            }

            return count;
        }

        /// <summary>
        /// Materialize one pair by index. Returns false when the pair cannot be materialized
        /// (no selectable override, missing shader, bad index).
        /// </summary>
        public static bool MaterializePair(
            VrmxtMaterialsOverrideInstance instance,
            int pairIndex,
            string folderAssetPath = null
        )
        {
            if (instance == null || pairIndex < 0 || pairIndex >= instance.Pairs.Count)
            {
                return false;
            }

            folderAssetPath = EnsureFolder(instance, folderAssetPath);
            if (string.IsNullOrEmpty(folderAssetPath))
            {
                return false;
            }

            Undo.RecordObject(instance, "Materialize Material");
            var gltfJson = TryLoadGltfJsonForInstance(instance);
            var pipeline = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
            Func<int, Texture> resolveTexture = index =>
                instance.TryGetImportedTexture(index, out var texture) ? texture : null;

            if (
                !TryMaterializePairCore(
                    instance,
                    pairIndex,
                    folderAssetPath,
                    gltfJson,
                    pipeline,
                    resolveTexture
                )
            )
            {
                return false;
            }

            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
            VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(
                instance.gameObject,
                instance
            );
            return true;
        }

        /// <summary>
        /// True when the pair JSON has a selectable unity override for the active pipeline
        /// (shader presence not required for button enable).
        /// </summary>
        public static bool CanMaterializePair(VrmxtMaterialsOverridePair pair)
        {
            if (pair == null || string.IsNullOrWhiteSpace(pair.ExtensionJson))
            {
                return false;
            }

            if (!VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension))
            {
                return false;
            }

            return UnityOverrideSelector.TrySelectUnityEngineOverride(
                extension,
                VrmxtMaterialsOverrideApplier.DetectActivePipeline(),
                out _
            );
        }

        private static bool TryMaterializePairCore(
            VrmxtMaterialsOverrideInstance instance,
            int pairIndex,
            string folderAssetPath,
            string gltfJson,
            RenderPipelineVariant pipeline,
            Func<int, Texture> resolveTexture
        )
        {
            var pair = instance.Pairs[pairIndex];
            if (pair == null || string.IsNullOrEmpty(pair.MaterialName))
            {
                return false;
            }

            if (!VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension))
            {
                return false;
            }

            if (
                !UnityOverrideSelector.TrySelectUnityEngineOverride(
                    extension,
                    pipeline,
                    out var engineOverride
                )
            )
            {
                return false;
            }

            var unityOverride = engineOverride.Material as UnityMaterialOverride;
            if (unityOverride == null)
            {
                return false;
            }

            var shader = VrmxtMaterialsOverrideApplier.ResolveShader(unityOverride.ShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    "VRMXT Materialize: shader '"
                        + unityOverride.ShaderName
                        + "' unresolved for material '"
                        + pair.MaterialName
                        + "'. Skip."
                );
                return false;
            }

            var assetPath = BuildMaterialAssetPath(folderAssetPath, pair.MaterialName);
            Material materialAsset;
            var createdNew = false;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                materialAsset = existing;
                Undo.RecordObject(materialAsset, "Materialize Material");
            }
            else
            {
                materialAsset = new Material(shader) { name = SanitizeFileName(pair.MaterialName) };
                AssetDatabase.CreateAsset(materialAsset, assetPath);
                createdNew = true;
            }

            if (
                !VrmxtMaterialsOverrideApplier.TryWritePairOverrideOntoMaterial(
                    materialAsset,
                    pair,
                    gltfJson,
                    pipeline,
                    resolveTexture
                )
            )
            {
                Debug.LogWarning(
                    "VRMXT Materialize: write failed for material '" + pair.MaterialName + "'."
                );
                if (createdNew)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                return false;
            }

            if (createdNew)
            {
                Undo.RegisterCreatedObjectUndo(materialAsset, "Materialize Material");
            }

            EditorUtility.SetDirty(materialAsset);
            pair.OverrideMaterial = materialAsset;
            return true;
        }

        private static string EnsureFolder(
            VrmxtMaterialsOverrideInstance instance,
            string folderAssetPath
        )
        {
            if (!string.IsNullOrEmpty(folderAssetPath))
            {
                folderAssetPath = folderAssetPath.Replace('\\', '/').TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(folderAssetPath))
                {
                    EnsureFolderHierarchy(folderAssetPath);
                }

                return folderAssetPath;
            }

            var derived = DeriveDefaultFolder(instance);
            if (string.IsNullOrEmpty(derived))
            {
                Debug.LogWarning(
                    "VRMXT Materialize: could not derive an Assets/ folder for .mat output."
                );
                return null;
            }

            if (!AssetDatabase.IsValidFolder(derived))
            {
                EnsureFolderHierarchy(derived);
            }

            return derived;
        }

        private static string DeriveDefaultFolder(VrmxtMaterialsOverrideInstance instance)
        {
            string baseDir = null;
            string rootName = null;

            if (instance != null)
            {
                rootName = instance.gameObject != null ? instance.gameObject.name : null;
                foreach (var pair in instance.Pairs)
                {
                    if (pair?.SourceMaterial == null)
                    {
                        continue;
                    }

                    var sourcePath = AssetDatabase.GetAssetPath(pair.SourceMaterial);
                    if (string.IsNullOrEmpty(sourcePath))
                    {
                        continue;
                    }

                    baseDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
                    if (string.IsNullOrEmpty(rootName))
                    {
                        rootName = Path.GetFileNameWithoutExtension(sourcePath);
                    }

                    break;
                }
            }

            if (
                string.IsNullOrEmpty(baseDir)
                || !(
                    string.Equals(baseDir, "Assets", StringComparison.Ordinal)
                    || baseDir.StartsWith("Assets/", StringComparison.Ordinal)
                )
            )
            {
                baseDir = "Assets";
            }

            if (string.IsNullOrEmpty(rootName))
            {
                rootName = "VRMXT";
            }

            return baseDir + "/" + SanitizeFileName(rootName) + "_VRMXTMaterials";
        }

        private static void EnsureFolderHierarchy(string folderAssetPath)
        {
            folderAssetPath = folderAssetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderAssetPath))
            {
                return;
            }

            var parts = folderAssetPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                return;
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string BuildMaterialAssetPath(string folderAssetPath, string materialName)
        {
            var fileName = SanitizeFileName(materialName) + ".mat";
            return folderAssetPath.TrimEnd('/') + "/" + fileName;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Material";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var bad = false;
                for (var j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                // Also flatten path separators that can appear in disambiguated keys.
                if (bad || c == '/' || c == '\\' || c == ':')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Material" : result;
        }

        /// <summary>
        /// Prefer glTF JSON from the imported <c>.vrm</c> so Materialize can resolve
        /// sibling <c>VRMC_materials_mtoon</c> for bindings.
        /// </summary>
        private static string TryLoadGltfJsonForInstance(VrmxtMaterialsOverrideInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            foreach (var pair in instance.Pairs)
            {
                if (pair?.SourceMaterial == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(pair.SourceMaterial);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (
                    !path.EndsWith(".vrm", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                try
                {
                    var bytes = File.ReadAllBytes(path);
                    if (GlbChunks.TryExtractJson(bytes, out var json))
                    {
                        return json;
                    }
                }
                catch (IOException)
                {
                    // Fall through — Materialize still writes properties without bindings.
                }
            }

            return null;
        }
    }
}
