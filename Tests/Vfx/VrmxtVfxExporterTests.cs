using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxExporterTests
    {
        [Test]
        public void TryBuildUtf8Json_WritesResolvedNodeAndTexture()
        {
            var root = new GameObject("root");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);

            try
            {
                var emitter = new VrmxtVfxResolvedEmitter
                {
                    Name = "HandSpark",
                    Node = 99,
                    NodeTransform = bone.transform,
                    Particle = new VrmxtVfxParticleData
                    {
                        EmissionRate = 12f,
                        MaxParticles = 16,
                        Color = new Color(1f, 0f, 0f, 1f),
                    },
                };

                var texture = new Texture2D(2, 2);
                var pending = new List<VrmxtVfxPendingEmitter>
                {
                    new VrmxtVfxPendingEmitter(emitter, texture)
                    {
                        ExportTextureIndex = 4,
                    },
                };

                Assert.IsTrue(
                    VrmxtVfxExporter.TryBuildUtf8Json(
                        pending,
                        t => t == bone.transform ? 7 : (int?)null,
                        out var utf8));

                var json = VrmxtVfxExporter.Utf8ToString(utf8);
                Assert.IsTrue(VrmxtVfx.TryParse(json, out var parsed));
                Assert.AreEqual(1, parsed.Emitters.Count);
                Assert.AreEqual(7, parsed.Emitters[0].Node);
                Assert.AreEqual(4, parsed.Emitters[0].Texture);
                Assert.AreEqual(12f, parsed.Emitters[0].EmissionRate);
                Assert.AreEqual("HandSpark", parsed.Emitters[0].Name);

                Object.DestroyImmediate(texture);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CaptureAndClearParticleSystems_KeepsTextureWithoutLiveParticleSystem()
        {
            var root = new GameObject("root");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);

            try
            {
                var instance = root.AddComponent<VrmxtVfxInstance>();
                var texture = new Texture2D(2, 2);
                instance.SetEmitters(new[]
                {
                    new VrmxtVfxResolvedEmitter
                    {
                        Name = "Spark",
                        NodeTransform = bone.transform,
                        Particle = new VrmxtVfxParticleData
                        {
                            HasTexture = true,
                            TextureIndex = 0,
                            Texture = texture,
                        },
                    },
                });

                var pending = VrmxtVfxExporter.CaptureAndClearParticleSystems(root);
                Assert.AreEqual(1, pending.Count);
                Assert.AreSame(texture, pending[0].Texture);

                pending[0].ExportTextureIndex = 3;
                Assert.IsTrue(
                    VrmxtVfxExporter.TryBuildUtf8Json(
                        pending,
                        t => t == bone.transform ? 1 : (int?)null,
                        out var utf8));
                var json = VrmxtVfxExporter.Utf8ToString(utf8);
                Assert.IsTrue(VrmxtVfx.TryParse(json, out var parsed));
                Assert.AreEqual(3, parsed.Emitters[0].Texture);

                Object.DestroyImmediate(texture);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BindTexturesFromResolver_SetsParticleTextureWithoutBuildingSystems()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                var emitters = new[]
                {
                    new VrmxtVfxResolvedEmitter
                    {
                        Particle = new VrmxtVfxParticleData
                        {
                            HasTexture = true,
                            TextureIndex = 0,
                        },
                    },
                };

                VrmxtVfxInstance.BindTexturesFromResolver(emitters, _ => texture);
                Assert.AreSame(texture, emitters[0].Particle.Texture);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CaptureAndClearParticleSystems_RemovesPreviewChildren()
        {
            var root = new GameObject("root");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);

            try
            {
                var instance = root.AddComponent<VrmxtVfxInstance>();
                var texture = new Texture2D(2, 2);
                instance.SetEmitters(new[]
                {
                    new VrmxtVfxResolvedEmitter
                    {
                        Name = "Spark",
                        NodeTransform = bone.transform,
                        Particle = new VrmxtVfxParticleData
                        {
                            HasTexture = true,
                            Texture = texture,
                            Color = Color.white,
                        },
                    },
                });
                instance.BuildParticleSystems(_ => texture);
                Assert.AreEqual(1, instance.ParticleSystems.Count);
                Assert.AreEqual(1, bone.transform.childCount);

                var main = instance.ParticleSystems[0].main;
                main.startColor = Color.blue;

                var pending = VrmxtVfxExporter.CaptureAndClearParticleSystems(root);
                Assert.AreEqual(1, pending.Count);
                Assert.AreSame(texture, pending[0].Texture);
                Assert.AreEqual(Color.blue, pending[0].Emitter.Particle.Color);
                Assert.AreEqual(0, instance.ParticleSystems.Count);
                Assert.AreEqual(0, bone.transform.childCount);

                Object.DestroyImmediate(texture);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReadFromParticleSystem_CopiesColorAndSize()
        {
            var go = new GameObject("ps");
            try
            {
                var particleSystem = go.AddComponent<ParticleSystem>();
                var main = particleSystem.main;
                main.startColor = new Color(0.1f, 0.2f, 0.9f, 0.75f);
                main.startLifetime = 2.5f;
                main.startSize3D = true;
                main.startSizeX = 0.2f;
                main.startSizeY = 0.3f;
                main.maxParticles = 40;
                var emission = particleSystem.emission;
                emission.rateOverTime = 15f;
                var velocity = particleSystem.velocityOverLifetime;
                velocity.enabled = true;
                velocity.y = 0.4f;

                var emitter = new VrmxtVfxResolvedEmitter
                {
                    Particle = new VrmxtVfxParticleData(),
                };
                VrmxtVfxParticleSystemMapper.ReadFromParticleSystem(particleSystem, emitter);

                Assert.AreEqual(new Color(0.1f, 0.2f, 0.9f, 0.75f), emitter.Particle.Color);
                Assert.AreEqual(2.5f, emitter.Particle.Lifetime, 1e-4f);
                Assert.AreEqual(0.2f, emitter.Particle.SizeX, 1e-4f);
                Assert.AreEqual(0.3f, emitter.Particle.SizeY, 1e-4f);
                Assert.AreEqual(40, emitter.Particle.MaxParticles);
                Assert.AreEqual(15f, emitter.Particle.EmissionRate, 1e-4f);
                Assert.AreEqual(0.4f, emitter.Particle.StartSpeed, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
