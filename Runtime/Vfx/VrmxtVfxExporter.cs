using System;
using System.Collections.Generic;
using System.Text;
using UniVRMXT.Format;
using UnityEngine;
// VrmxtInstance facade lives in root namespace UniVRMXT.

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Builds portable <c>VRMXT_vfx</c> JSON from <see cref="VrmxtVfxInstance"/> for export.
    /// Does not reference UniGLTF/VRM10; the export hook supplies node/texture registration.
    /// </summary>
    public static class VrmxtVfxExporter
    {
        public const string PendingUserDataKey = "UniVRMXT.Vfx.PendingEmitters";

        /// <summary>
        /// Capture albedo textures from live ParticleSystems (or
        /// <see cref="VrmxtVfxParticleData.Texture"/>), then clear preview systems so
        /// ModelExporter does not pick up VFX children / materials.
        /// </summary>
        public static List<VrmxtVfxPendingEmitter> CaptureAndClearParticleSystems(GameObject root)
        {
            var pending = new List<VrmxtVfxPendingEmitter>();
            if (root == null)
            {
                return pending;
            }

            var instance = VrmxtInstance.FindVfx(root);
            if (instance == null)
            {
                return pending;
            }

            // Ensure Particle.Texture is populated before PS clear (import may only have
            // HasTexture + material slots until Sync / Bind runs).
            instance.SyncTexturesFromParticleMaterials();

            var emitters = instance.Emitters;
            for (var i = 0; i < emitters.Count; i++)
            {
                var emitter = emitters[i];
                if (emitter == null)
                {
                    continue;
                }

                var particleSystem = FindParticleSystem(emitter, instance);
                if (particleSystem != null)
                {
                    // Inspector edits live on ParticleSystem; fold them back before clear.
                    VrmxtVfxParticleSystemMapper.ReadFromParticleSystem(particleSystem, emitter);
                }

                var texture = ResolveExportTexture(emitter, instance, particleSystem);
                if (texture != null && emitter.Particle != null)
                {
                    emitter.Particle.Texture = texture;
                    emitter.Particle.HasTexture = true;
                }

                pending.Add(new VrmxtVfxPendingEmitter(emitter, texture));
            }

            instance.ClearParticleSystems(destroyOwnedMaterials: false);
            return pending;
        }

        /// <summary>
        /// Register captured textures and assign export-time texture indices.
        /// </summary>
        public static void RegisterTextures(
            IList<VrmxtVfxPendingEmitter> pending,
            Func<Texture, int> registerSRgbTexture)
        {
            if (pending == null || registerSRgbTexture == null)
            {
                return;
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var item = pending[i];
                if (item == null || item.Texture == null)
                {
                    continue;
                }

                item.ExportTextureIndex = registerSRgbTexture(item.Texture);
            }
        }

        /// <summary>
        /// Build extension JSON using current node indices from
        /// <paramref name="resolveNodeIndex"/>.
        /// </summary>
        public static bool TryBuildUtf8Json(
            IList<VrmxtVfxPendingEmitter> pending,
            Func<Transform, int?> resolveNodeIndex,
            out byte[] utf8Json)
        {
            utf8Json = null;
            if (pending == null || pending.Count == 0 || resolveNodeIndex == null)
            {
                return false;
            }

            var emitters = new List<VrmxtVfxEmitter>();
            for (var i = 0; i < pending.Count; i++)
            {
                var item = pending[i];
                if (item?.Emitter == null)
                {
                    continue;
                }

                var nodeIndex = resolveNodeIndex(item.Emitter.NodeTransform);
                if (!nodeIndex.HasValue || nodeIndex.Value < 0)
                {
                    continue;
                }

                emitters.Add(ToFormatEmitter(item.Emitter, nodeIndex.Value, item.ExportTextureIndex));
            }

            if (emitters.Count == 0)
            {
                return false;
            }

            utf8Json = VrmxtVfx.ToUtf8Json(new VrmxtVfxExtension(emitters));
            return true;
        }

        public static string Utf8ToString(byte[] utf8Json)
        {
            return utf8Json == null ? null : Encoding.UTF8.GetString(utf8Json);
        }

        private static Texture ResolveExportTexture(
            VrmxtVfxResolvedEmitter emitter,
            VrmxtVfxInstance instance,
            ParticleSystem particleSystem = null)
        {
            if (emitter.Particle?.Texture != null)
            {
                return emitter.Particle.Texture;
            }

            particleSystem ??= FindParticleSystem(emitter, instance);
            if (particleSystem == null)
            {
                return null;
            }

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            return VrmxtVfxParticleSystemMapper.ReadAssignedTexture(renderer != null
                ? renderer.sharedMaterial
                : null);
        }

        private static ParticleSystem FindParticleSystem(
            VrmxtVfxResolvedEmitter emitter,
            VrmxtVfxInstance instance)
        {
            if (emitter.NodeTransform == null)
            {
                return null;
            }

            var expectedName = VrmxtVfxParticleSystemMapper.BuildObjectName(emitter);
            for (var i = 0; i < emitter.NodeTransform.childCount; i++)
            {
                var child = emitter.NodeTransform.GetChild(i);
                if (child == null || child.name != expectedName)
                {
                    continue;
                }

                var particleSystem = child.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    return particleSystem;
                }
            }

            var systems = instance.ParticleSystems;
            for (var i = 0; i < systems.Count; i++)
            {
                var particleSystem = systems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                if (particleSystem.transform.parent == emitter.NodeTransform &&
                    particleSystem.gameObject.name == expectedName)
                {
                    return particleSystem;
                }
            }

            return null;
        }

        private static VrmxtVfxEmitter ToFormatEmitter(
            VrmxtVfxResolvedEmitter emitter,
            int nodeIndex,
            int? exportTextureIndex)
        {
            var particle = emitter.Particle ?? new VrmxtVfxParticleData();
            int? texture = null;
            if (exportTextureIndex.HasValue && exportTextureIndex.Value >= 0)
            {
                texture = exportTextureIndex.Value;
            }
            else if (particle.HasTexture && particle.TextureIndex >= 0 && particle.Texture == null)
            {
                // No live texture to re-embed; omit texture field rather than write a stale index.
                texture = null;
            }

            var startColor = particle.StartColor;
            return new VrmxtVfxEmitter(
                emitter.Name,
                string.IsNullOrEmpty(emitter.Type) ? "particle" : emitter.Type,
                nodeIndex,
                new[] { emitter.LocalPosition.x, emitter.LocalPosition.y, emitter.LocalPosition.z },
                new[]
                {
                    emitter.LocalRotation.x,
                    emitter.LocalRotation.y,
                    emitter.LocalRotation.z,
                    emitter.LocalRotation.w,
                },
                new VrmxtVfxParticle(
                    texture,
                    particle.EmissionRate,
                    particle.MaxParticles,
                    particle.Lifetime,
                    particle.StartSize,
                    particle.StartSpeed,
                    new[] { startColor.r, startColor.g, startColor.b, startColor.a }));
        }
    }

    /// <summary>
    /// One emitter staged for export between PreHierarchy and WriteExtensions.
    /// </summary>
    public sealed class VrmxtVfxPendingEmitter
    {
        public VrmxtVfxPendingEmitter(VrmxtVfxResolvedEmitter emitter, Texture texture)
        {
            Emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            Texture = texture;
        }

        public VrmxtVfxResolvedEmitter Emitter { get; }

        public Texture Texture { get; }

        public int? ExportTextureIndex { get; set; }
    }
}
