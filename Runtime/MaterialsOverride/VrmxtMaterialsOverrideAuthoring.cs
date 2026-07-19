using System;
using System.Collections.Generic;
using UniVRMXT.Format;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniVRMXT.MaterialsOverride
{
    /// <summary>
    /// Authoring helpers: capture Unity override JSON from a Material asset and apply
    /// override materials onto matching avatar renderer slots.
    /// </summary>
    public static class VrmxtMaterialsOverrideAuthoring
    {
        public const string DefaultProviderId = "com.miramocha.univrmxt";

        public static void SyncAllFromOverrideMaterials(VrmxtMaterialsOverrideInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            foreach (var pair in instance.Pairs)
            {
                if (pair?.OverrideMaterial == null)
                {
                    continue;
                }

                SyncUnityOverrideFromMaterial(pair);
            }
        }

        /// <summary>
        /// Merge a <c>unity</c> engine block from <see cref="VrmxtMaterialsOverridePair.OverrideMaterial"/>
        /// into <see cref="VrmxtMaterialsOverridePair.ExtensionJson"/>. Preserves other engines
        /// and existing <c>bindings[]</c>. Fills <c>variant</c> only when missing.
        /// </summary>
        public static void SyncUnityOverrideFromMaterial(VrmxtMaterialsOverridePair pair)
        {
            if (pair?.OverrideMaterial == null || pair.OverrideMaterial.shader == null)
            {
                return;
            }

            var material = pair.OverrideMaterial;
            var shaderName = material.shader.name;
            var properties = CaptureProperties(material);

            var existingVariant = (string)null;
            MaterialProvider existingProvider = null;
            IReadOnlyList<VrmxtMaterialBinding> existingBindings = Array.Empty<VrmxtMaterialBinding>();
            var otherOverrides = new List<VrmxtMaterialEngineOverride>();

            if (VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var existing))
            {
                foreach (var entry in existing.Overrides)
                {
                    if (string.Equals(entry.Engine, VrmxtMaterialsOverride.EngineUnity, StringComparison.Ordinal))
                    {
                        if (entry.Material is UnityMaterialOverride unity)
                        {
                            existingVariant = unity.Variant;
                            existingProvider = unity.Provider;
                        }

                        existingBindings = entry.Bindings;
                    }
                    else
                    {
                        otherOverrides.Add(entry);
                    }
                }
            }

            var variant = existingVariant;
            if (string.IsNullOrEmpty(variant))
            {
                variant = VrmxtMaterialsOverrideExporter.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline());
            }

            var provider = existingProvider ?? new MaterialProvider(
                DefaultProviderId,
                ResolvePackageVersion());

            var unityMaterial = new UnityMaterialOverride(
                VrmxtMaterialsOverride.UnityMaterialIdTypeShaderName,
                shaderName,
                variant,
                provider);

            var unityOverride = new VrmxtMaterialEngineOverride(
                VrmxtMaterialsOverride.EngineUnity,
                unityMaterial,
                existingBindings,
                properties);

            var overrides = new List<VrmxtMaterialEngineOverride> { unityOverride };
            overrides.AddRange(otherOverrides);

            pair.ExtensionJson = VrmxtMaterialsOverride.ToJson(
                new VrmxtMaterialsOverrideExtension(overrides));
        }

        public static void ApplyOverrideMaterialsToRenderers(
            GameObject root,
            VrmxtMaterialsOverrideInstance instance)
        {
            if (root == null || instance == null)
            {
                return;
            }

            foreach (var pair in instance.Pairs)
            {
                if (pair?.OverrideMaterial == null || string.IsNullOrEmpty(pair.MaterialName))
                {
                    continue;
                }

                var source = pair.OverrideMaterial;
                if (source.shader == null)
                {
                    continue;
                }

                foreach (var target in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root, pair.MaterialName))
                {
                    CopyMaterialState(source, target);
                }
            }
        }

        public static List<VrmxtMaterialProperty> CaptureProperties(Material material)
        {
            var list = new List<VrmxtMaterialProperty>();
            if (material == null || material.shader == null)
            {
                return list;
            }

            var shader = material.shader;
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                var flags = shader.GetPropertyFlags(i);
                if ((flags & ShaderPropertyFlags.HideInInspector) != 0)
                {
                    continue;
                }

                var name = shader.GetPropertyName(i);
                if (string.IsNullOrEmpty(name) || !material.HasProperty(name))
                {
                    continue;
                }

                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                    {
                        var c = material.GetColor(name);
                        list.Add(new VrmxtMaterialProperty(
                            name,
                            VrmxtMaterialsOverride.TargetTypeVector,
                            null,
                            new[] { c.r, c.g, c.b, c.a },
                            null,
                            null));
                        break;
                    }
                    case ShaderPropertyType.Vector:
                    {
                        var v = material.GetVector(name);
                        list.Add(new VrmxtMaterialProperty(
                            name,
                            VrmxtMaterialsOverride.TargetTypeVector,
                            null,
                            new[] { v.x, v.y, v.z, v.w },
                            null,
                            null));
                        break;
                    }
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                    {
                        list.Add(new VrmxtMaterialProperty(
                            name,
                            VrmxtMaterialsOverride.TargetTypeScalar,
                            material.GetFloat(name),
                            null,
                            null,
                            null));
                        break;
                    }
                    case ShaderPropertyType.Texture:
                    {
                        if (material.GetTexture(name) == null)
                        {
                            break;
                        }

                        // Placeholder index; export PrepareTextures remaps from live material.
                        list.Add(new VrmxtMaterialProperty(
                            name,
                            VrmxtMaterialsOverride.TargetTypeTexture,
                            null,
                            null,
                            null,
                            0));
                        break;
                    }
                }
            }

            CaptureShaderFeatures(material, list);
            return list;
        }

        private static void CaptureShaderFeatures(Material material, List<VrmxtMaterialProperty> list)
        {
            var shader = material.shader;
            if (shader == null)
            {
                return;
            }

            try
            {
                foreach (var keyword in material.enabledKeywords)
                {
                    var name = keyword.name;
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    list.Add(new VrmxtMaterialProperty(
                        name,
                        VrmxtMaterialsOverride.TargetTypeShaderFeature,
                        null,
                        null,
                        true,
                        null));
                }
            }
            catch (Exception)
            {
                // LocalKeyword API may be unavailable on older pipelines; skip features.
            }
        }

        private static void CopyMaterialState(Material source, Material target)
        {
            if (source == null || target == null || source.shader == null)
            {
                return;
            }

            target.shader = source.shader;
            target.CopyPropertiesFromMaterial(source);
        }

        private static string ResolvePackageVersion()
        {
            return "0.1.0";
        }
    }
}
