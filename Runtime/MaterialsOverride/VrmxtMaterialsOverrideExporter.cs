using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UniVRMXT.MaterialsOverride
{
    /// <summary>
    /// Builds export-ready <c>VRMXT_materials_override</c> extension bytes from a
    /// <see cref="VrmxtMaterialsOverrideInstance"/>. Does not reference UniGLTF/VRM10; the
    /// export hook supplies texture registration and material index resolution.
    /// </summary>
    public static class VrmxtMaterialsOverrideExporter
    {
        /// <summary>
        /// Parse every stored entry into a mutable clone. Entries that fail to parse (should
        /// not happen for data the runtime stored, but a hand-edited or corrupted store is
        /// possible) are skipped rather than failing the whole export.
        /// </summary>
        public static List<VrmxtMaterialsOverridePendingEntry> BuildPending(GameObject root)
        {
            var pending = new List<VrmxtMaterialsOverridePendingEntry>();
            if (root == null)
            {
                return pending;
            }

            var store = VrmxtInstance.FindMaterialsOverride(root);
            return BuildPending(store);
        }

        public static List<VrmxtMaterialsOverridePendingEntry> BuildPending(VrmxtMaterialsOverrideInstance store)
        {
            var pending = new List<VrmxtMaterialsOverridePendingEntry>();
            if (store == null)
            {
                return pending;
            }

            // Refresh ExtensionJson from any assigned OverrideMaterial before export.
            VrmxtMaterialsOverrideAuthoring.SyncAllFromOverrideMaterials(store);

            foreach (var entry in store.Pairs)
            {
                if (entry == null || string.IsNullOrEmpty(entry.MaterialName) ||
                    string.IsNullOrWhiteSpace(entry.ExtensionJson))
                {
                    continue;
                }

                JObject parsed;
                try
                {
                    parsed = JToken.Parse(entry.ExtensionJson) as JObject;
                }
                catch (JsonReaderException)
                {
                    parsed = null;
                }
                catch (JsonException)
                {
                    parsed = null;
                }

                if (parsed == null)
                {
                    continue;
                }

                pending.Add(new VrmxtMaterialsOverridePendingEntry(entry.MaterialName, parsed));
            }

            return pending;
        }

        /// <summary>
        /// Re-snapshot <c>unity</c> <c>properties[].texture</c> entries from the live
        /// <see cref="Material"/> on <paramref name="root"/> when the property's Unity
        /// texture is not already tracked by the imported file (MVP re-snapshot path).
        /// Mutates the cloned JSON in <paramref name="pending"/>, never the source
        /// <see cref="VrmxtMaterialsOverrideInstance"/> entry — the caller still owns whether
        /// to persist the rewritten extension back onto the store.
        /// </summary>
        public static void PrepareTextures(
            IReadOnlyList<VrmxtMaterialsOverridePendingEntry> pending,
            GameObject root,
            Func<Texture, bool, int> registerSrgbTexture)
        {
            PrepareTextures(pending, root, registerSrgbTexture, instance: null);
        }

        /// <summary>
        /// Prefer <see cref="VrmxtMaterialsOverridePair.OverrideMaterial"/> for texture
        /// remap when present (export PreHierarchy may have restored SourceMaterial onto
        /// renderers for UniVRM mesh export).
        /// </summary>
        public static void PrepareTextures(
            IReadOnlyList<VrmxtMaterialsOverridePendingEntry> pending,
            GameObject root,
            Func<Texture, bool, int> registerSrgbTexture,
            VrmxtMaterialsOverrideInstance instance)
        {
            if (pending == null || root == null || registerSrgbTexture == null)
            {
                return;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                RemapLiveTextureProperties(pending[i], root, registerSrgbTexture, instance);
                pending[i].MarkTexturesPrepared();
            }
        }

        /// <summary>
        /// UTF-8 JSON for one material's (possibly texture-remapped) extension object.
        /// </summary>
        public static bool TryBuildUtf8Extension(
            IReadOnlyList<VrmxtMaterialsOverridePendingEntry> pending,
            string materialName,
            out byte[] utf8Json)
        {
            utf8Json = null;
            var entry = Find(pending, materialName);
            if (entry == null)
            {
                return false;
            }

            SanitizeUnpreparedTextureProperties(entry);
            utf8Json = Encoding.UTF8.GetBytes(entry.Json.ToString(Formatting.None));
            return true;
        }

        /// <summary>
        /// UTF-8 JSON for every pending entry, keyed by material name.
        /// </summary>
        public static IReadOnlyDictionary<string, byte[]> BuildAllUtf8Extensions(
            IReadOnlyList<VrmxtMaterialsOverridePendingEntry> pending)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (pending == null)
            {
                return result;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var entry = pending[i];
                if (entry == null || string.IsNullOrEmpty(entry.MaterialName))
                {
                    continue;
                }

                SanitizeUnpreparedTextureProperties(entry);
                result[entry.MaterialName] = Encoding.UTF8.GetBytes(entry.Json.ToString(Formatting.None));
            }

            return result;
        }

        /// <summary>
        /// Pure merge helper (base-spec / UniVRM profile "variant survival" rule): an
        /// existing <c>material.variant</c> always wins. Only a brand-new <c>unity</c>
        /// entry with no <c>variant</c> yet gets one filled from the active render
        /// pipeline. Never called by the default write-through path above (which preserves
        /// stored JSON as-is); exposed for callers that author a new override entry.
        /// </summary>
        public static string ResolveUnityVariant(string existingVariant, RenderPipelineVariant activePipeline)
        {
            return !string.IsNullOrEmpty(existingVariant)
                ? existingVariant
                : UnityOverrideSelector.RenderPipelineVariantToVariantString(activePipeline);
        }

        public static string RenderPipelineVariantToVariantString(RenderPipelineVariant pipeline)
        {
            return UnityOverrideSelector.RenderPipelineVariantToVariantString(pipeline);
        }

        private static VrmxtMaterialsOverridePendingEntry Find(
            IReadOnlyList<VrmxtMaterialsOverridePendingEntry> pending,
            string materialName)
        {
            if (pending == null)
            {
                return null;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                if (string.Equals(pending[i]?.MaterialName, materialName, StringComparison.Ordinal))
                {
                    return pending[i];
                }
            }

            return null;
        }

        private static void RemapLiveTextureProperties(
            VrmxtMaterialsOverridePendingEntry entry,
            GameObject root,
            Func<Texture, bool, int> registerSrgbTexture,
            VrmxtMaterialsOverrideInstance instance)
        {
            if (entry?.Json == null ||
                !entry.Json.TryGetValue("overrides", StringComparison.Ordinal, out var overridesToken) ||
                overridesToken is not JArray overrides)
            {
                return;
            }

            Material liveMaterial = null;
            var resolvedMaterial = false;
            var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                VrmxtMaterialsOverrideApplier.DetectActivePipeline());
            var unityOverrideCount = CountUnityOverrides(overrides);

            foreach (var overrideToken in overrides)
            {
                if (overrideToken is not JObject overrideObject ||
                    !overrideObject.TryGetValue("engine", StringComparison.Ordinal, out var engineToken) ||
                    engineToken.Type != JTokenType.String ||
                    !string.Equals(engineToken.Value<string>(), "unity", StringComparison.Ordinal))
                {
                    continue;
                }

                // Remap textures only on the active (unity, variant) slot; sibling pipeline
                // slots write through their stored texture indices unchanged. A lone unity
                // entry (any variant) still remaps so single-slot re-export from any RP works.
                if (!IsActiveUnitySlot(overrideObject, activeVariant, unityOverrideCount))
                {
                    continue;
                }

                if (!overrideObject.TryGetValue("properties", StringComparison.Ordinal, out var propertiesToken) ||
                    propertiesToken is not JArray properties)
                {
                    continue;
                }

                // Snapshot first: texture properties that fail to remap are removed from
                // `properties` below, which would otherwise corrupt in-place iteration.
                foreach (var propertyToken in new List<JToken>(properties))
                {
                    if (propertyToken is not JObject propertyObject ||
                        !propertyObject.TryGetValue("type", StringComparison.Ordinal, out var typeToken) ||
                        typeToken.Type != JTokenType.String ||
                        !string.Equals(typeToken.Value<string>(), "texture", StringComparison.Ordinal))
                    {
                        // Not a texture property; nothing to remap or drop.
                        continue;
                    }

                    if (!resolvedMaterial)
                    {
                        liveMaterial = ResolveTextureSourceMaterial(root, entry.MaterialName, instance);
                        resolvedMaterial = true;
                    }

                    if (!TryRemapTextureProperty(propertyObject, liveMaterial, registerSrgbTexture, out var newIndex))
                    {
                        // No live material, missing name, missing property, null texture,
                        // or a failed register call: never carry the stale imported glTF
                        // texture index into a new export — drop the property entirely.
                        propertyToken.Remove();
                        continue;
                    }

                    propertyObject["texture"] = newIndex;
                }
            }
        }

        private static int CountUnityOverrides(JArray overrides)
        {
            var count = 0;
            if (overrides == null)
            {
                return 0;
            }

            foreach (var overrideToken in overrides)
            {
                if (overrideToken is JObject overrideObject &&
                    overrideObject.TryGetValue("engine", StringComparison.Ordinal, out var engineToken) &&
                    engineToken.Type == JTokenType.String &&
                    string.Equals(engineToken.Value<string>(), "unity", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Active slot = exact variant match, empty/omitted variant, or the sole unity entry.
        /// </summary>
        private static bool IsActiveUnitySlot(
            JObject overrideObject,
            string activeVariant,
            int unityOverrideCount)
        {
            if (overrideObject == null)
            {
                return false;
            }

            string variant = null;
            if (overrideObject.TryGetValue("material", StringComparison.Ordinal, out var materialToken) &&
                materialToken is JObject materialObject &&
                materialObject.TryGetValue("variant", StringComparison.Ordinal, out var variantToken) &&
                variantToken.Type == JTokenType.String)
            {
                variant = variantToken.Value<string>();
            }

            if (string.IsNullOrEmpty(variant))
            {
                return true;
            }

            if (string.Equals(variant, activeVariant, StringComparison.Ordinal))
            {
                return true;
            }

            // Single-slot file re-exported from a different RP: still remap that slot.
            return unityOverrideCount == 1;
        }

        /// <summary>
        /// Prefer authored OverrideMaterial when export restored Source onto renderers.
        /// </summary>
        private static Material ResolveTextureSourceMaterial(
            GameObject root,
            string materialName,
            VrmxtMaterialsOverrideInstance instance)
        {
            if (instance != null &&
                instance.TryGetPair(materialName, out var pair) &&
                pair.OverrideMaterial != null)
            {
                return pair.OverrideMaterial;
            }

            return ResolveFirstMaterial(root, materialName);
        }

        private static bool TryRemapTextureProperty(
            JObject propertyObject,
            Material liveMaterial,
            Func<Texture, bool, int> registerSrgbTexture,
            out int newIndex)
        {
            newIndex = 0;

            if (!propertyObject.TryGetValue("name", StringComparison.Ordinal, out var nameToken) ||
                nameToken.Type != JTokenType.String)
            {
                return false;
            }

            var propertyName = nameToken.Value<string>();
            if (string.IsNullOrEmpty(propertyName) ||
                liveMaterial == null ||
                !liveMaterial.HasProperty(propertyName))
            {
                return false;
            }

            var texture = liveMaterial.GetTexture(propertyName);
            if (texture == null)
            {
                return false;
            }

            try
            {
                newIndex = registerSrgbTexture(texture, false);
            }
            catch (Exception)
            {
                return false;
            }

            return newIndex >= 0;
        }

        /// <summary>
        /// Drop texture-typed <c>properties[]</c> entries from an entry that never went
        /// through <see cref="PrepareTextures"/> (its texture indices still point at the
        /// imported glTF, not this export) so a write can never leak a stale index.
        /// </summary>
        private static void SanitizeUnpreparedTextureProperties(VrmxtMaterialsOverridePendingEntry entry)
        {
            if (entry == null || entry.TexturesPrepared || entry.Json == null ||
                !entry.Json.TryGetValue("overrides", StringComparison.Ordinal, out var overridesToken) ||
                overridesToken is not JArray overrides)
            {
                return;
            }

            foreach (var overrideToken in overrides)
            {
                if (overrideToken is not JObject overrideObject ||
                    !overrideObject.TryGetValue("properties", StringComparison.Ordinal, out var propertiesToken) ||
                    propertiesToken is not JArray properties)
                {
                    continue;
                }

                foreach (var propertyToken in new List<JToken>(properties))
                {
                    if (propertyToken is JObject propertyObject &&
                        propertyObject.TryGetValue("type", StringComparison.Ordinal, out var typeToken) &&
                        typeToken.Type == JTokenType.String &&
                        string.Equals(typeToken.Value<string>(), "texture", StringComparison.Ordinal))
                    {
                        propertyToken.Remove();
                    }
                }
            }
        }

        private static Material ResolveFirstMaterial(GameObject root, string materialName)
        {
            foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(root, materialName))
            {
                return material;
            }

            return null;
        }
    }

    /// <summary>
    /// One material's <c>VRMXT_materials_override</c> extension staged for export, parsed
    /// from the store so <see cref="PrepareTextures"/> can rewrite texture indices without
    /// touching the source <see cref="VrmxtMaterialsOverrideInstance"/> entry.
    /// </summary>
    public sealed class VrmxtMaterialsOverridePendingEntry
    {
        public VrmxtMaterialsOverridePendingEntry(string materialName, JObject json)
        {
            MaterialName = materialName ?? throw new ArgumentNullException(nameof(materialName));
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public string MaterialName { get; }

        public JObject Json { get; }

        /// <summary>
        /// Whether <see cref="VrmxtMaterialsOverrideExporter.PrepareTextures"/> has run for
        /// this entry (regardless of per-property outcome). When false at write time, every
        /// texture-typed property still carries an imported (stale) glTF texture index and
        /// must be dropped rather than written into a new export.
        /// </summary>
        public bool TexturesPrepared { get; private set; }

        public void MarkTexturesPrepared()
        {
            TexturesPrepared = true;
        }
    }
}
