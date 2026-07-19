using NUnit.Framework;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxInstanceSyncTests
    {
        [Test]
        public void SyncParticleSystemsFromEmitters_AppliesStartColor()
        {
            var root = new GameObject("root");
            var node = new GameObject("node");
            node.transform.SetParent(root.transform, false);

            var instance = root.AddComponent<VrmxtVfxInstance>();
            var emitter = new VrmxtVfxResolvedEmitter
            {
                Name = "spark",
                NodeTransform = node.transform,
                Particle = new VrmxtVfxParticleData
                {
                    StartColor = Color.red,
                    EmissionRate = 1f,
                    MaxParticles = 8,
                    Lifetime = 1f,
                    StartSize = 0.1f,
                    StartSpeed = 0.5f,
                },
            };
            instance.SetEmitters(new[] { emitter });
            instance.BuildParticleSystems();

            try
            {
                Assert.AreEqual(1, instance.ParticleSystems.Count);
                emitter.Particle.StartColor = Color.blue;
                instance.SyncParticleSystemsFromEmitters();

                var main = instance.ParticleSystems[0].main;
                Assert.AreEqual(Color.blue, main.startColor.color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void SyncEmittersFromParticleSystems_ReadsStartColor()
        {
            var root = new GameObject("root");
            var node = new GameObject("node");
            node.transform.SetParent(root.transform, false);

            var instance = root.AddComponent<VrmxtVfxInstance>();
            var emitter = new VrmxtVfxResolvedEmitter
            {
                Name = "spark",
                NodeTransform = node.transform,
                Particle = new VrmxtVfxParticleData
                {
                    StartColor = Color.red,
                    EmissionRate = 1f,
                    MaxParticles = 8,
                    Lifetime = 1f,
                    StartSize = 0.1f,
                    StartSpeed = 0.5f,
                },
            };
            instance.SetEmitters(new[] { emitter });
            instance.BuildParticleSystems();

            try
            {
                var main = instance.ParticleSystems[0].main;
                main.startColor = Color.green;
                // Bypass suppress window from Build/Apply.
                instance.SyncEmittersFromParticleSystems();
                // First call may no-op under suppress; clear by waiting isn't reliable in
                // tests — call Read path directly after forcing suppress expiry via second push.
                var field = typeof(VrmxtVfxInstance).GetField(
                    "_suppressEmitterPullUntil",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                field?.SetValue(instance, 0.0);
                instance.SyncEmittersFromParticleSystems();

                Assert.AreEqual(Color.green, emitter.Particle.StartColor);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
