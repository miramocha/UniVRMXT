using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxParticleSystemMapperTests
    {
        private const string EmitterJson = @"
            {
              ""extensions"": {
                ""VRMXT_sprite_particle"": {
                  ""specVersion"": ""1.0"",
                  ""emitters"": [
                    {
                      ""name"": ""HandSpark"",
                      ""node"": 0,
                      ""texture"": 0,
                      ""size"": [0.04, 0.06],
                      ""color"": [1.0, 0.85, 0.4, 1.0],
                      ""emissionRate"": 20.0,
                      ""maxParticles"": 32,
                      ""lifetime"": 0.8,
                      ""startSpeed"": 0.2
                    }
                  ]
                }
              }
            }
            ";

        private const string DefaultsJson = @"
            {
              ""extensions"": {
                ""VRMXT_sprite_particle"": {
                  ""specVersion"": ""1.0"",
                  ""emitters"": [
                    {
                      ""node"": 0
                    }
                  ]
                }
              }
            }
            ";

        [Test]
        public void Create_MapsPortableFieldsOntoParticleSystem()
        {
            var node = new GameObject("Hand").transform;
            Texture2D texture = null;

            try
            {
                Assert.IsTrue(
                    VrmxtVfxImporter.TryImport(
                        EmitterJson,
                        new List<Transform> { node },
                        out var emitters));

                texture = new Texture2D(2, 2);
                var ps = VrmxtVfxParticleSystemMapper.Create(emitters[0], texture);

                Assert.AreSame(node, ps.transform.parent);
                Assert.AreEqual(Vector3.zero, ps.transform.localPosition);
                Assert.AreEqual(Quaternion.identity, ps.transform.localRotation);
                Assert.AreEqual(Vector3.one, ps.transform.localScale);
                Assert.AreEqual("VRMXT_sprite_particle_HandSpark", ps.gameObject.name);

                var main = ps.main;
                Assert.AreEqual(32, main.maxParticles);
                Assert.AreEqual(0.8f, main.startLifetime.constant);
                Assert.AreEqual(ParticleSystemScalingMode.Local, main.scalingMode);
                Assert.IsTrue(main.startSize3D);
                Assert.AreEqual(0.04f, main.startSizeX.constant);
                Assert.AreEqual(0.06f, main.startSizeY.constant);
                Assert.AreEqual(0f, main.startSpeed.constant);
                Assert.AreEqual(new Color(1f, 0.85f, 0.4f, 1f), main.startColor.color);

                Assert.AreEqual(20f, ps.emission.rateOverTime.constant);
                Assert.IsFalse(ps.shape.enabled);

                var velocity = ps.velocityOverLifetime;
                Assert.IsTrue(velocity.enabled);
                Assert.AreEqual(ParticleSystemSimulationSpace.Local, velocity.space);
                Assert.AreEqual(0.2f, velocity.y.constant);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Assert.AreEqual(ParticleSystemRenderMode.Billboard, renderer.renderMode);
                Assert.AreEqual(ParticleSystemRenderSpace.View, renderer.alignment);
                Assert.IsTrue(
                    VrmxtVfxParticleSystemMapper.IsOwnedParticleMaterial(renderer.sharedMaterial));
                Assert.AreSame(
                    texture,
                    VrmxtVfxParticleSystemMapper.ReadAssignedTexture(renderer.sharedMaterial));
                AssertTextureSlots(renderer.sharedMaterial, texture);
                AssertTransparentBlendConfigured(renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(node.gameObject);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void ApplyWorldSpaceSize_IgnoresNonUniformParentScale()
        {
            var parent = new GameObject("scaled").transform;
            parent.localScale = new Vector3(2f, 4f, 1f);
            var child = new GameObject("ps").transform;
            child.SetParent(parent, false);
            var ps = child.gameObject.AddComponent<ParticleSystem>();

            try
            {
                var main = ps.main;
                VrmxtVfxParticleSystemMapper.ApplyWorldSpaceSize(main, child, 0.1f, 0.2f);

                Assert.IsTrue(main.startSize3D);
                // Local scalingMode + identity PS scale ⇒ start sizes are world meters.
                Assert.AreEqual(0.1f, main.startSizeX.constant, 1e-4f);
                Assert.AreEqual(0.2f, main.startSizeY.constant, 1e-4f);

                VrmxtVfxParticleSystemMapper.ReadWorldSpaceSize(
                    main,
                    child,
                    out var worldWidth,
                    out var worldHeight);
                Assert.AreEqual(0.1f, worldWidth, 1e-4f);
                Assert.AreEqual(0.2f, worldHeight, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void ConfigureTransparentAlphaBlending_SetsTransparentQueue()
        {
            var shader = VrmxtVfxParticleSystemMapper.ResolveParticleShader();
            if (shader == null)
            {
                Assert.Ignore("No particle shader available in this test runner.");
            }

            var material = new Material(shader);
            try
            {
                VrmxtVfxParticleSystemMapper.ConfigureTransparentAlphaBlending(material);
                AssertTransparentBlendConfigured(material);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Create_MissingTexture_UsesColorFallback()
        {
            var node = new GameObject("Hand").transform;

            try
            {
                Assert.IsTrue(
                    VrmxtVfxImporter.TryImport(
                        EmitterJson,
                        new List<Transform> { node },
                        out var emitters));

                var ps = VrmxtVfxParticleSystemMapper.Create(emitters[0], texture: null);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Assert.IsTrue(
                    VrmxtVfxParticleSystemMapper.IsOwnedParticleMaterial(renderer.sharedMaterial));
                Assert.IsNotNull(VrmxtVfxParticleSystemMapper.ResolveParticleShader());
                Assert.AreEqual(new Color(1f, 0.85f, 0.4f, 1f), ps.main.startColor.color);
            }
            finally
            {
                Object.DestroyImmediate(node.gameObject);
            }
        }

        [Test]
        public void ApplyTextureToMaterial_SetsMainTexAndBaseMapSlots()
        {
            var shader = Shader.Find("Sprites/Default");
            Assume.That(shader, Is.Not.Null);

            var material = new Material(shader);
            var texture = new Texture2D(2, 2);
            try
            {
                VrmxtVfxParticleSystemMapper.ApplyTextureToMaterial(material, texture);
                AssertTextureSlots(material, texture);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Create_OmittedFields_UseSpecDefaults()
        {
            var node = new GameObject("Root").transform;

            try
            {
                Assert.IsTrue(
                    VrmxtVfxImporter.TryImport(
                        DefaultsJson,
                        new List<Transform> { node },
                        out var emitters));

                var ps = VrmxtVfxParticleSystemMapper.Create(emitters[0]);
                var main = ps.main;
                Assert.AreEqual(VrmxtVfx.DefaultMaxParticles, main.maxParticles);
                Assert.AreEqual(VrmxtVfx.DefaultLifetime, main.startLifetime.constant);
                Assert.IsTrue(main.startSize3D);
                Assert.AreEqual(VrmxtVfx.DefaultSize[0], main.startSizeX.constant);
                Assert.AreEqual(VrmxtVfx.DefaultSize[1], main.startSizeY.constant);
                Assert.AreEqual(VrmxtVfx.DefaultEmissionRate, ps.emission.rateOverTime.constant);
                Assert.AreEqual(VrmxtVfx.DefaultStartSpeed, ps.velocityOverLifetime.y.constant);
                Assert.AreEqual(Color.white, main.startColor.color);
            }
            finally
            {
                Object.DestroyImmediate(node.gameObject);
            }
        }

        [Test]
        public void TryAttach_WithTextureResolver_BuildsParticleSystems()
        {
            var root = new GameObject("AvatarRoot");
            var node = new GameObject("Hand").transform;
            Texture2D texture = null;

            try
            {
                texture = new Texture2D(2, 2);
                Assert.IsTrue(
                    VrmxtVfxRuntime.TryAttach(
                        root,
                        EmitterJson,
                        new List<Transform> { node },
                        index => index == 0 ? texture : null,
                        out var instance));

                Assert.AreEqual(1, instance.ParticleSystems.Count);
                var material = instance.ParticleSystems[0]
                    .GetComponent<ParticleSystemRenderer>()
                    .sharedMaterial;
                Assert.AreSame(
                    texture,
                    VrmxtVfxParticleSystemMapper.ReadAssignedTexture(material));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(node.gameObject);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        private static void AssertTextureSlots(Material material, Texture texture)
        {
            Assert.IsNotNull(material);
            if (material.HasProperty("_MainTex"))
            {
                Assert.AreSame(texture, material.GetTexture("_MainTex"));
            }

            if (material.HasProperty("_BaseMap"))
            {
                Assert.AreSame(texture, material.GetTexture("_BaseMap"));
            }

            Assert.AreSame(
                texture,
                VrmxtVfxParticleSystemMapper.ReadAssignedTexture(material));
        }

        private static void AssertTransparentBlendConfigured(Material material)
        {
            Assert.IsNotNull(material);
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);

            if (material.HasProperty("_Surface"))
            {
                Assert.AreEqual(1f, material.GetFloat("_Surface"), 1e-4f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                Assert.AreEqual(
                    (float)UnityEngine.Rendering.BlendMode.SrcAlpha,
                    material.GetFloat("_SrcBlend"),
                    1e-4f);
            }
        }
    }
}
