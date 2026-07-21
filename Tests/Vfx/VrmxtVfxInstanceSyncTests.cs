using NUnit.Framework;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxInstanceSyncTests
    {
        [Test]
        public void SyncParticleSystemsFromEmitters_AppliesColor()
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
                    Color = Color.red,
                    EmissionRate = 1f,
                    MaxParticles = 8,
                    Lifetime = 1f,
                    SizeX = 0.1f,
                    SizeY = 0.1f,
                    StartSpeed = 0.5f,
                },
            };
            instance.SetEmitters(new[] { emitter });
            instance.BuildParticleSystems();

            try
            {
                Assert.AreEqual(1, instance.ParticleSystems.Count);
                emitter.Particle.Color = Color.blue;
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
        public void SyncEmittersFromParticleSystems_ReadsColor()
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
                    Color = Color.red,
                    EmissionRate = 1f,
                    MaxParticles = 8,
                    Lifetime = 1f,
                    SizeX = 0.1f,
                    SizeY = 0.1f,
                    StartSpeed = 0.5f,
                },
            };
            instance.SetEmitters(new[] { emitter });
            instance.BuildParticleSystems();

            try
            {
                var main = instance.ParticleSystems[0].main;
                main.startColor = Color.green;
                instance.SyncEmittersFromParticleSystems();
                VrmxtVfxParticleSystemMapper.ReadFromParticleSystem(
                    instance.ParticleSystems[0],
                    emitter);
                Assert.AreEqual(Color.green, emitter.Particle.Color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
