using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtVfxFormatTests
    {
        private const string ValidExtensionJson = """
            {
              "specVersion": "1.0",
              "emitters": [
                {
                  "name": "HandSpark",
                  "type": "particle",
                  "node": 2,
                  "particle": {
                    "emissionRate": 20.0,
                    "maxParticles": 32
                  }
                }
              ]
            }
            """;

        [Test]
        public void TryParse_ValidExtension_ReturnsEmitter()
        {
            Assert.IsTrue(VrmxtVfx.TryParse(ValidExtensionJson, out var extension));
            Assert.AreEqual(1, extension.Emitters.Count);
            Assert.AreEqual("HandSpark", extension.Emitters[0].Name);
            Assert.AreEqual(2, extension.Emitters[0].Node);
            Assert.AreEqual(20f, extension.Emitters[0].Particle.EmissionRate);
            Assert.AreEqual(32, extension.Emitters[0].Particle.MaxParticles);
        }

        [Test]
        public void TryParse_AppliesParticleDefaults()
        {
            const string json = """
                {
                  "specVersion": "1.0",
                  "emitters": [
                    {
                      "type": "particle",
                      "node": 0,
                      "particle": {}
                    }
                  ]
                }
                """;

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var extension));
            var particle = extension.Emitters[0].Particle;
            Assert.AreEqual(VrmxtVfx.DefaultEmissionRate, particle.EmissionRate);
            Assert.AreEqual(VrmxtVfx.DefaultMaxParticles, particle.MaxParticles);
            Assert.AreEqual(VrmxtVfx.DefaultLifetime, particle.Lifetime);
            Assert.AreEqual(VrmxtVfx.DefaultStartSize, particle.StartSize);
            Assert.AreEqual(VrmxtVfx.DefaultStartSpeed, particle.StartSpeed);
            Assert.AreEqual(1f, particle.StartColor[3]);
        }

        [Test]
        public void TryParse_SkipsUnknownType()
        {
            const string json = """
                {
                  "specVersion": "1.0",
                  "emitters": [
                    {
                      "type": "ribbon",
                      "node": 0
                    },
                    {
                      "type": "particle",
                      "node": 1,
                      "particle": {}
                    }
                  ]
                }
                """;

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var extension));
            Assert.AreEqual(1, extension.Emitters.Count);
            Assert.AreEqual(1, extension.Emitters[0].Node);
        }

        [Test]
        public void TryParse_RejectsWrongSpecVersion()
        {
            const string json = """
                {
                  "specVersion": "2.0",
                  "emitters": []
                }
                """;

            Assert.IsFalse(VrmxtVfx.TryParse(json, out _));
        }

        [Test]
        public void TryParse_SkipsInvalidParticleValues()
        {
            const string json = """
                {
                  "specVersion": "1.0",
                  "emitters": [
                    {
                      "type": "particle",
                      "node": 0,
                      "particle": {
                        "maxParticles": 0
                      }
                    },
                    {
                      "type": "particle",
                      "node": 1,
                      "particle": {}
                    }
                  ]
                }
                """;

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var extension));
            Assert.AreEqual(1, extension.Emitters.Count);
            Assert.AreEqual(1, extension.Emitters[0].Node);
        }
    }
}
