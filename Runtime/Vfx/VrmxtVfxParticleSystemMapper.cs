using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Maps portable <c>VRMXT_vfx</c> particle fields onto Unity <see cref="ParticleSystem"/>.
    /// See <c>docs/vfx-particle-mapping.md</c> for the field table.
    /// </summary>
    public static class VrmxtVfxParticleSystemMapper
    {
        public const string EmitterObjectNamePrefix = "VRMXT_vfx_";
        public const string OwnedMaterialNamePrefix = "VRMXT_vfx_Particle";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>
        /// Create a child under <see cref="VrmxtVfxResolvedEmitter.NodeTransform"/> with
        /// emitter local TR, then configure a <see cref="ParticleSystem"/>.
        /// </summary>
        /// <param name="texture">
        /// Optional glTF texture. When null, use the pipeline particle material tinted by
        /// <see cref="VrmxtVfxParticleData.StartColor"/>.
        /// </param>
        public static ParticleSystem Create(
            VrmxtVfxResolvedEmitter emitter,
            Texture texture = null)
        {
            if (emitter == null)
            {
                throw new ArgumentNullException(nameof(emitter));
            }

            if (emitter.NodeTransform == null)
            {
                throw new ArgumentException("Emitter NodeTransform is null.", nameof(emitter));
            }

            var go = new GameObject(BuildObjectName(emitter));
            var transform = go.transform;
            transform.SetParent(emitter.NodeTransform, false);
            transform.localPosition = emitter.LocalPosition;
            transform.localRotation = emitter.LocalRotation;
            transform.localScale = Vector3.one;

            var particleSystem = go.AddComponent<ParticleSystem>();
            Apply(particleSystem, emitter.Particle, texture);
            return particleSystem;
        }

        public static List<ParticleSystem> CreateAll(
            IReadOnlyList<VrmxtVfxResolvedEmitter> emitters,
            Func<int, Texture> resolveTexture = null)
        {
            var created = new List<ParticleSystem>();
            if (emitters == null)
            {
                return created;
            }

            for (var i = 0; i < emitters.Count; i++)
            {
                var emitter = emitters[i];
                if (emitter?.NodeTransform == null)
                {
                    continue;
                }

                var texture = ResolveTexture(emitter.Particle, resolveTexture);
                created.Add(Create(emitter, texture));
            }

            return created;
        }

        public static void Apply(
            ParticleSystem particleSystem,
            VrmxtVfxParticleData particle,
            Texture texture = null)
        {
            if (particleSystem == null)
            {
                throw new ArgumentNullException(nameof(particleSystem));
            }

            if (particle == null)
            {
                throw new ArgumentNullException(nameof(particle));
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = Mathf.Max(1, particle.MaxParticles);
            main.startLifetime = particle.Lifetime;
            main.startSize = particle.StartSize;
            // Velocity comes from VelocityOverLifetime along local +Y (spec).
            main.startSpeed = 0f;
            main.startColor = particle.StartColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = particle.EmissionRate;

            var shape = particleSystem.shape;
            shape.enabled = false;

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(particle.StartSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            ApplyMaterial(renderer, texture);
        }

        public static string BuildObjectName(VrmxtVfxResolvedEmitter emitter)
        {
            if (emitter == null)
            {
                return EmitterObjectNamePrefix + "emitter";
            }

            if (!string.IsNullOrEmpty(emitter.Name))
            {
                return EmitterObjectNamePrefix + emitter.Name;
            }

            return EmitterObjectNamePrefix + emitter.Node;
        }

        public static bool IsOwnedParticleMaterial(Material material)
        {
            return material != null &&
                   material.name.StartsWith(OwnedMaterialNamePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Pick an unlit particle shader for the active pipeline (BIRP or URP).
        /// No hard URP package reference — uses <see cref="Shader.Find"/> + type name check.
        /// During ScriptedImporter, <see cref="Shader.Find"/> may return null; callers should
        /// fall back to the default <see cref="ParticleSystem"/> material.
        /// </summary>
        public static Shader ResolveParticleShader()
        {
            if (IsUniversalRenderPipeline())
            {
                var urp = FindFirstShader(
                    "Universal Render Pipeline/Particles/Unlit",
                    "Universal Render Pipeline/Particles/Simple Lit");
                if (urp != null)
                {
                    return urp;
                }
            }

            var builtin = FindFirstShader(
                "Particles/Standard Unlit",
                "Particles/Standard Surface",
                "Legacy Shaders/Particles/Alpha Blended Premultiply",
                "Legacy Shaders/Particles/Alpha Blended",
                "Mobile/Particles/Alpha Blended",
                "Particles/Alpha Blended");
            if (builtin != null)
            {
                return builtin;
            }

            // URP shaders may still exist when pipeline asset is temporarily null (import).
            var urpFallback = FindFirstShader(
                "Universal Render Pipeline/Particles/Unlit",
                "Universal Render Pipeline/Particles/Simple Lit");
            if (urpFallback != null)
            {
                return urpFallback;
            }

            return FindFirstShader(
                "Sprites/Default",
                "Unlit/Transparent",
                "Unlit/Color",
                "UI/Default");
        }

        /// <summary>
        /// Assign texture to BIRP (<c>_MainTex</c>) and URP (<c>_BaseMap</c>) slots when present.
        /// </summary>
        public static void ApplyTextureToMaterial(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }

            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            material.mainTexture = texture;
        }

        public static Texture ReadAssignedTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty(BaseMapId))
            {
                var baseMap = material.GetTexture(BaseMapId);
                if (baseMap != null)
                {
                    return baseMap;
                }
            }

            if (material.HasProperty(MainTexId))
            {
                var mainTex = material.GetTexture(MainTexId);
                if (mainTex != null)
                {
                    return mainTex;
                }
            }

            return material.mainTexture;
        }

        private static void ApplyMaterial(ParticleSystemRenderer renderer, Texture texture)
        {
            if (renderer == null)
            {
                return;
            }

            var material = CreateOwnedParticleMaterial(renderer);
            if (material == null)
            {
                Debug.LogWarning(
                    "UniVRMXT: could not resolve a particle shader/material; particles may render pink.");
                return;
            }

            renderer.sharedMaterial = material;
            if (renderer.GetComponent<VrmxtVfxOwnedParticleMaterial>() == null)
            {
                renderer.gameObject.AddComponent<VrmxtVfxOwnedParticleMaterial>();
            }

            ApplyTextureToMaterial(material, texture);
        }

        private static Material CreateOwnedParticleMaterial(ParticleSystemRenderer renderer)
        {
            var shader = ResolveParticleShader();
            if (IsUsableShader(shader))
            {
                return new Material(shader) { name = OwnedMaterialNamePrefix };
            }

            // ScriptedImporter / early boot: Shader.Find often fails. Clone Unity's default
            // ParticleSystem material (usually Default-Particle) instead of leaving shader null.
            var defaultMaterial = renderer != null ? renderer.sharedMaterial : null;
            if (IsUsableMaterial(defaultMaterial))
            {
                return new Material(defaultMaterial) { name = OwnedMaterialNamePrefix };
            }

            var builtinParticle = TryGetBuiltinParticleMaterial();
            if (IsUsableMaterial(builtinParticle))
            {
                return new Material(builtinParticle) { name = OwnedMaterialNamePrefix };
            }

            return null;
        }

        private static Material TryGetBuiltinParticleMaterial()
        {
            // May be null outside Editor; ParticleSystem.sharedMaterial clone is the primary fallback.
            return Resources.GetBuiltinResource<Material>("Default-Particle.mat");
        }

        private static Shader FindFirstShader(params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            for (var i = 0; i < names.Length; i++)
            {
                var shader = Shader.Find(names[i]);
                if (IsUsableShader(shader))
                {
                    return shader;
                }
            }

            return null;
        }

        private static bool IsUsableShader(Shader shader)
        {
            return shader != null && shader.name != "Hidden/InternalErrorShader";
        }

        private static bool IsUsableMaterial(Material material)
        {
            return material != null && IsUsableShader(material.shader);
        }

        private static bool IsUniversalRenderPipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            return asset != null &&
                   asset.GetType().Name.IndexOf("Universal", StringComparison.Ordinal) >= 0;
        }

        private static Texture ResolveTexture(
            VrmxtVfxParticleData particle,
            Func<int, Texture> resolveTexture)
        {
            if (particle == null || !particle.HasTexture || resolveTexture == null)
            {
                return null;
            }

            return resolveTexture(particle.TextureIndex);
        }
    }
}
