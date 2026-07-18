using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxImporterTests
    {
        private const string TwoEmittersJson = """
            {
              "extensions": {
                "VRMXT_vfx": {
                  "specVersion": "1.0",
                  "emitters": [
                    {
                      "name": "Keep",
                      "type": "particle",
                      "node": 1,
                      "localPosition": [0.1, 0.2, 0.3],
                      "particle": {
                        "emissionRate": 17.5,
                        "maxParticles": 42,
                        "texture": 0
                      }
                    },
                    {
                      "name": "Skip",
                      "type": "particle",
                      "node": 99,
                      "particle": {}
                    }
                  ]
                }
              }
            }
            """;

        [Test]
        public void TryImport_ByTransformList_SkipsUnresolvedNodes()
        {
            var nodes = new List<Transform>
            {
                new GameObject("n0").transform,
                new GameObject("n1").transform,
            };

            try
            {
                Assert.IsTrue(
                    VrmxtVfxImporter.TryImport(TwoEmittersJson, nodes, out var emitters));
                Assert.AreEqual(1, emitters.Count);
                Assert.AreEqual("Keep", emitters[0].Name);
                Assert.AreEqual(1, emitters[0].Node);
                Assert.AreSame(nodes[1], emitters[0].NodeTransform);
                Assert.AreEqual(17.5f, emitters[0].Particle.EmissionRate);
                Assert.AreEqual(42, emitters[0].Particle.MaxParticles);
                Assert.IsTrue(emitters[0].Particle.HasTexture);
                Assert.AreEqual(0, emitters[0].Particle.TextureIndex);
                Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.3f), emitters[0].LocalPosition);
            }
            finally
            {
                foreach (var node in nodes)
                {
                    Object.DestroyImmediate(node.gameObject);
                }
            }
        }

        [Test]
        public void TryImport_ByName_SkipsEmptyNames()
        {
            Assert.IsTrue(
                VrmxtVfxImporter.TryImport(
                    TwoEmittersJson,
                    index => index == 1 ? "Hand" : null,
                    out var data));
            Assert.AreEqual(1, data.Emitters.Count);
            Assert.AreEqual("Keep", data.Emitters[0].Name);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void TryImport_RejectsWrongSpecVersion()
        {
            const string json = """
                {
                  "specVersion": "2.0",
                  "emitters": []
                }
                """;

            Assert.IsFalse(
                VrmxtVfxImporter.TryImport(json, _ => "node", out VrmxtVfxData _));
            Assert.IsFalse(
                VrmxtVfxImporter.TryImport(
                    json,
                    new List<Transform>(),
                    out List<VrmxtVfxResolvedEmitter> _));
        }

        [Test]
        public void TryAttach_AddsInstanceWithResolvedEmitters()
        {
            var root = new GameObject("AvatarRoot");
            var nodes = new List<Transform>
            {
                new GameObject("n0").transform,
                new GameObject("n1").transform,
            };

            try
            {
                Assert.IsTrue(
                    VrmxtVfxRuntime.TryAttach(
                        root,
                        TwoEmittersJson,
                        nodes,
                        out var instance));
                Assert.IsNotNull(instance);
                Assert.AreSame(root.GetComponent<VrmxtVfxInstance>(), instance);
                Assert.AreEqual(1, instance.Emitters.Count);
                Assert.AreEqual("Keep", instance.Emitters[0].Name);
                Assert.AreSame(nodes[1], instance.Emitters[0].NodeTransform);
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var node in nodes)
                {
                    Object.DestroyImmediate(node.gameObject);
                }
            }
        }

        [Test]
        public void TryAttach_NullRoot_ReturnsFalse()
        {
            Assert.IsFalse(
                VrmxtVfxRuntime.TryAttach(
                    null,
                    TwoEmittersJson,
                    new List<Transform>(),
                    out _));
        }
    }
}
