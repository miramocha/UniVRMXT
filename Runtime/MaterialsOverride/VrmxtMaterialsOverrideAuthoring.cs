using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        /// and existing <c>bindings[]</c>. Fills <c>variant</c> only when missing
        /// (variant survival — see <see cref="VrmxtMaterialsOverrideExporter.ResolveUnityVariant"/>).
        /// </summary>
        public static void SyncUnityOverrideFromMaterial(VrmxtMaterialsOverridePair pair)
        {
            if (pair?.OverrideMaterial == null || pair.OverrideMaterial.shader == null)
            {
                return;
            }

            var material = pair.OverrideMaterial;
            var shaderName = material.shader.name;

            // Peek variant from raw JSON first so survival does not depend on the typed
            // Material cast succeeding after TryParse (same assembly, but defensive).
            TryPeekUnityVariant(pair.ExtensionJson, out var existingVariant);

            MaterialProvider existingProvider = null;
            IReadOnlyList<VrmxtMaterialBinding> existingBindings = Array.Empty<VrmxtMaterialBinding>();
            var otherOverrides = new List<VrmxtMaterialEngineOverride>();

            if (VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var existing))
            {
                foreach (var entry in existing.Overrides)
                {
                    if (string.Equals(entry.Engine, VrmxtMaterialsOverride.EngineUnity, StringComparison.Ordinal))
                    {
                        var unity = entry.Material as UnityMaterialOverride;
                        if (unity != null)
                        {
                            if (!string.IsNullOrEmpty(unity.Variant))
                            {
                                existingVariant = unity.Variant;
                            }

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

            var variant = VrmxtMaterialsOverrideExporter.ResolveUnityVariant(
                existingVariant,
                VrmxtMaterialsOverrideApplier.DetectActivePipeline());

            var provider = existingProvider ?? new MaterialProvider(
                DefaultProviderId,
                ResolvePackageVersion());

            var properties = CaptureProperties(material);

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

        /// <summary>
        /// Best-effort read of <c>overrides[engine=unity].material.variant</c> without full
        /// schema validation, so authoring sync can keep an existing variant when a sibling
        /// engine entry prevents <see cref="VrmxtMaterialsOverride.TryParse"/>.
        /// </summary>
        internal static bool TryPeekUnityVariant(string extensionJson, out string variant)
        {
            variant = null;
            if (string.IsNullOrWhiteSpace(extensionJson))
            {
                return false;
            }

            try
            {
                // Use `as` casts (not `is` pattern) — Unity + Newtonsoft type identity has
                // historically broken pattern matching against JObject across asmdef boundaries.
                var root = JToken.Parse(extensionJson) as JObject;
                var overrides = root?["overrides"] as JArray;
                if (overrides == null)
                {
                    return false;
                }

                foreach (var overrideToken in overrides)
                {
                    var overrideObject = overrideToken as JObject;
                    if (overrideObject == null)
                    {
                        continue;
                    }

                    var engine = overrideObject["engine"]?.Value<string>();
                    if (!string.Equals(engine, VrmxtMaterialsOverride.EngineUnity, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var materialObject = overrideObject["material"] as JObject;
                    var peeked = materialObject?["variant"]?.Value<string>();
                    if (string.IsNullOrEmpty(peeked))
                    {
                        return false;
                    }

                    variant = peeked;
                    return true;
                }
            }
            catch (JsonReaderException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
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

                ApplyOverrideToNamedSlots(
                    root,
                    pair.MaterialName,
                    source);
            }
        }

        /// <summary>
        /// Put <see cref="VrmxtMaterialsOverridePair.SourceMaterial"/> back onto matching
        /// renderer slots and optionally destroy non-persistent preview instances.
        /// </summary>
        /// <param name="destroyPreviewMaterials">
        /// When false (export throwaway copy), do not <c>DestroyImmediate</c> DontSave
        /// previews — <see cref="UnityEngine.Object.Instantiate"/> may still share them
        /// with the scene original.
        /// </param>
        public static void RestoreSourceMaterialsToRenderers(
            GameObject root,
            VrmxtMaterialsOverrideInstance instance,
            bool destroyPreviewMaterials = true)
        {
            if (root == null || instance == null)
            {
                return;
            }

            foreach (var pair in instance.Pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.MaterialName))
                {
                    continue;
                }

                RestoreSourceMaterial(
                    root,
                    pair.MaterialName,
                    pair.SourceMaterial,
                    destroyPreviewMaterials);
            }
        }

        /// <summary>
        /// Restore one material name's renderer slots to <paramref name="sourceMaterial"/>.
        /// </summary>
        public static void RestoreSourceMaterial(
            GameObject root,
            string materialName,
            Material sourceMaterial,
            bool destroyPreviewMaterials = true)
        {
            if (root == null || string.IsNullOrEmpty(materialName) || sourceMaterial == null)
            {
                return;
            }

            RestoreSourceToNamedSlots(root, materialName, sourceMaterial, destroyPreviewMaterials);
        }

        /// <summary>
        /// Swap matching renderer slots to a scene-owned clone of
        /// <paramref name="overrideMaterial"/> (never mutate imported asset materials).
        /// Clone keeps <paramref name="materialName"/> so export/applier name lookup still works.
        /// </summary>
        private static void ApplyOverrideToNamedSlots(
            GameObject root,
            string materialName,
            Material overrideMaterial)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                var shared = renderer.sharedMaterials;
                var changed = false;
                for (var j = 0; j < shared.Length; j++)
                {
                    var current = shared[j];
                    if (current == null || !MaterialNameMatches(current.name, materialName))
                    {
                        continue;
                    }

                    var previousIsPreview = (current.hideFlags & HideFlags.DontSave) != 0;

                    if (previousIsPreview)
                    {
                        // Prior scene preview instance — update in place.
                        CopyMaterialState(overrideMaterial, current);
                        current.name = materialName;
                        continue;
                    }

                    // Stock / override assets: never mutate — swap slot to a DontSave clone.
                    var preview = new Material(overrideMaterial)
                    {
                        name = materialName,
                        hideFlags = HideFlags.DontSave,
                    };
                    shared[j] = preview;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        private static void RestoreSourceToNamedSlots(
            GameObject root,
            string materialName,
            Material sourceMaterial,
            bool destroyPreviewMaterials)
        {
            if (sourceMaterial == null)
            {
                return;
            }

            // Resolve live mats for this store key (honors Name#N). Those are the slots
            // currently showing stock or a DontSave preview that we need to replace.
            var liveTargets = new HashSet<Material>();
            foreach (var live in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                         root, materialName))
            {
                if (live != null)
                {
                    liveTargets.Add(live);
                }
            }

            if (liveTargets.Count == 0)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                var shared = renderer.sharedMaterials;
                var changed = false;
                for (var j = 0; j < shared.Length; j++)
                {
                    var current = shared[j];
                    if (current == null || !liveTargets.Contains(current))
                    {
                        continue;
                    }

                    if (ReferenceEquals(current, sourceMaterial))
                    {
                        continue;
                    }

                    shared[j] = sourceMaterial;
                    changed = true;

                    if (destroyPreviewMaterials &&
                        (current.hideFlags & HideFlags.DontSave) != 0)
                    {
                        DestroyOwnedMaterial(current);
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        private static bool MaterialNameMatches(string unityMaterialName, string gltfMaterialName)
        {
            if (string.Equals(unityMaterialName, gltfMaterialName, StringComparison.Ordinal))
            {
                return true;
            }

            const string instanceSuffix = " (Instance)";
            if (unityMaterialName != null &&
                unityMaterialName.EndsWith(instanceSuffix, StringComparison.Ordinal))
            {
                var trimmed = unityMaterialName.Substring(
                    0,
                    unityMaterialName.Length - instanceSuffix.Length);
                return string.Equals(trimmed, gltfMaterialName, StringComparison.Ordinal);
            }

            return false;
        }

        private static void DestroyOwnedMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
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
