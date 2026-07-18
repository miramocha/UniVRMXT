using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Maps portable <c>VRMXT_vfx</c> particle fields onto Unity <see cref="ParticleSystem"/>.
    /// See <c>docs/vfx-particle-mapping.md</c> for the field table.
    /// </summary>
    public static class VrmxtVfxParticleSystemMapper
    {
        public const string EmitterObjectNamePrefix = "VRMXT_vfx_";

        /// <summary>
        /// Create a child under <see cref="VrmxtVfxResolvedEmitter.NodeTransform"/> with
        /// emitter local TR, then configure a <see cref="ParticleSystem"/>.
        /// </summary>
        /// <param name="texture">
        /// Optional glTF texture. When null, keep the default particle material and tint with
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
            ApplyTexture(renderer, texture);
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

        private static void ApplyTexture(ParticleSystemRenderer renderer, Texture texture)
        {
            if (renderer == null || texture == null)
            {
                return;
            }

            var material = renderer.material;
            if (material == null)
            {
                return;
            }

            material.mainTexture = texture;
        }
    }
}
