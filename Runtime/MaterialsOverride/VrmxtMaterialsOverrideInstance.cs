using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRMXT.MaterialsOverride
{
    /// <summary>
    /// Runtime holder for <c>VRMXT_materials_override</c> on a loaded avatar root.
    /// Each pair keys a glTF material name to optional authoring <see cref="OverrideMaterial"/>
    /// and keeps full extension JSON (all engines) for round-trip export.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VrmxtMaterialsOverrideInstance : MonoBehaviour
    {
        [SerializeField]
        private List<VrmxtMaterialsOverridePair> pairs = new();

        public IReadOnlyList<VrmxtMaterialsOverridePair> Pairs => pairs;

        /// <summary>Alias for callers migrating from the former Entries API.</summary>
        public IReadOnlyList<VrmxtMaterialsOverridePair> Entries => pairs;

        public void SetPairs(IEnumerable<VrmxtMaterialsOverridePair> values)
        {
            pairs.Clear();
            if (values == null)
            {
                return;
            }

            pairs.AddRange(values);
        }

        /// <summary>Alias for <see cref="SetPairs"/>.</summary>
        public void SetEntries(IEnumerable<VrmxtMaterialsOverridePair> values) => SetPairs(values);

        public void Clear()
        {
            pairs.Clear();
        }

        public bool TryGetPair(string materialName, out VrmxtMaterialsOverridePair pair)
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                if (string.Equals(pairs[i]?.MaterialName, materialName, StringComparison.Ordinal))
                {
                    pair = pairs[i];
                    return true;
                }
            }

            pair = null;
            return false;
        }

        /// <summary>Alias for <see cref="TryGetPair"/>.</summary>
        public bool TryGetEntry(string materialName, out VrmxtMaterialsOverridePair entry) =>
            TryGetPair(materialName, out entry);

        public bool RemovePair(string materialName)
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                if (string.Equals(pairs[i]?.MaterialName, materialName, StringComparison.Ordinal))
                {
                    pairs.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Alias for <see cref="RemovePair"/>.</summary>
        public bool RemoveEntry(string materialName) => RemovePair(materialName);

        /// <summary>
        /// Re-resolve <see cref="VrmxtMaterialsOverridePair.SourceMaterial"/> from renderers
        /// under this GameObject's root (this transform).
        /// </summary>
        public void RefreshSourceMaterials()
        {
            var root = gameObject;
            for (var i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i];
                if (pair == null || string.IsNullOrEmpty(pair.MaterialName))
                {
                    continue;
                }

                Material resolved = null;
                foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root, pair.MaterialName))
                {
                    resolved = material;
                    break;
                }

                if (pair.SourceMaterial != resolved)
                {
                    pair.SourceMaterial = resolved;
                }
            }
        }

        /// <summary>
        /// Add pairs for unique renderer material names under this root that lack an entry.
        /// </summary>
        [ContextMenu("Populate Pairs From Renderers")]
        public void PopulatePairsFromRenderers()
        {
            var root = gameObject;
            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < pairs.Count; i++)
            {
                if (!string.IsNullOrEmpty(pairs[i]?.MaterialName))
                {
                    existing.Add(pairs[i].MaterialName);
                }
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                // VFX preview ParticleSystems are not glTF materials — skip them.
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                var shared = renderer.sharedMaterials;
                for (var j = 0; j < shared.Length; j++)
                {
                    var material = shared[j];
                    if (material == null)
                    {
                        continue;
                    }

                    var name = StripInstanceSuffix(material.name);
                    if (string.IsNullOrEmpty(name) || !existing.Add(name))
                    {
                        continue;
                    }

                    pairs.Add(new VrmxtMaterialsOverridePair(name, null)
                    {
                        SourceMaterial = material,
                    });
                }
            }
        }

        /// <summary>
        /// Sync <see cref="ExtensionJson"/> from assigned override materials and push
        /// override shader/props onto matching live materials.
        /// </summary>
        public void SyncFromOverrideMaterials()
        {
            VrmxtMaterialsOverrideAuthoring.SyncAllFromOverrideMaterials(this);
            VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(gameObject, this);
        }

        private void OnValidate()
        {
            if (this == null)
            {
                return;
            }

            RefreshSourceMaterials();
            SyncFromOverrideMaterials();
        }

        private static string StripInstanceSuffix(string unityMaterialName)
        {
            const string instanceSuffix = " (Instance)";
            if (unityMaterialName != null &&
                unityMaterialName.EndsWith(instanceSuffix, StringComparison.Ordinal))
            {
                return unityMaterialName.Substring(0, unityMaterialName.Length - instanceSuffix.Length);
            }

            return unityMaterialName;
        }
    }

    /// <summary>
    /// One glTF material ↔ optional Unity override Material, plus verbatim extension JSON.
    /// </summary>
    [Serializable]
    public sealed class VrmxtMaterialsOverridePair
    {
        public string MaterialName;
        public Material SourceMaterial;
        public Material OverrideMaterial;
        public string ExtensionJson;

        public VrmxtMaterialsOverridePair()
        {
        }

        public VrmxtMaterialsOverridePair(string materialName, string extensionJson)
        {
            MaterialName = materialName;
            ExtensionJson = extensionJson;
        }
    }

}
