using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtVfxFormatTests
    {
        private const string ValidExtensionJson = @"
            {
              ""specVersion"": ""1.0"",
              ""emitters"": [
                {
                  ""name"": ""HandSpark"",
                  ""node"": 2,
                  ""emissionRate"": 20.0,
                  ""maxParticles"": 32
                }
              ]
            }
            ";

        [Test]
        public void TryParse_ValidExtension_ReturnsEmitter()
        {
            Assert.IsTrue(VrmxtVfx.TryParse(ValidExtensionJson, out var extension));
            Assert.AreEqual(1, extension.Emitters.Count);
            Assert.AreEqual("HandSpark", extension.Emitters[0].Name);
            Assert.AreEqual(2, extension.Emitters[0].Node);
            Assert.AreEqual(20f, extension.Emitters[0].EmissionRate);
            Assert.AreEqual(32, extension.Emitters[0].MaxParticles);
        }

        [Test]
        public void TryParse_AppliesDefaults()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""emitters"": [
                    {
                      ""node"": 0
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var extension));
            var emitter = extension.Emitters[0];
            Assert.AreEqual(VrmxtVfx.DefaultEmissionRate, emitter.EmissionRate);
            Assert.AreEqual(VrmxtVfx.DefaultMaxParticles, emitter.MaxParticles);
            Assert.AreEqual(VrmxtVfx.DefaultLifetime, emitter.Lifetime);
            Assert.AreEqual(VrmxtVfx.DefaultStartSpeed, emitter.StartSpeed);
            Assert.AreEqual(VrmxtVfx.DefaultSize[0], emitter.Size[0]);
            Assert.AreEqual(VrmxtVfx.DefaultSize[1], emitter.Size[1]);
            Assert.AreEqual(1f, emitter.Color[3]);
        }

        [Test]
        public void TryParse_SkipsInvalidEmitterValues()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""emitters"": [
                    {
                      ""node"": 0,
                      ""maxParticles"": 0
                    },
                    {
                      ""node"": 1
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var extension));
            Assert.AreEqual(1, extension.Emitters.Count);
            Assert.AreEqual(1, extension.Emitters[0].Node);
        }

        [Test]
        public void TryParse_RejectsWrongSpecVersion()
        {
            const string json = @"
                {
                  ""specVersion"": ""2.0"",
                  ""emitters"": []
                }
                ";

            Assert.IsFalse(VrmxtVfx.TryParse(json, out _));
        }

        [Test]
        public void TryParse_IgnoresLegacyVfxRoot()
        {
            const string json = @"
                {
                  ""extensions"": {
                    ""VRMXT_vfx"": {
                      ""specVersion"": ""1.0"",
                      ""emitters"": [
                        {
                          ""type"": ""particle"",
                          ""node"": 0,
                          ""particle"": {}
                        }
                      ]
                    }
                  }
                }
                ";

            Assert.IsFalse(VrmxtVfx.TryParse(json, out _));
        }

        [Test]
        public void TryParse_IgnoresLegacyParticleRoot()
        {
            const string json = @"
                {
                  ""extensions"": {
                    ""VRMXT_particle"": {
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

            Assert.IsFalse(VrmxtVfx.TryParse(json, out _));
        }

        [Test]
        public void ToJson_RoundTripsMinimalEmitter()
        {
            Assert.IsTrue(VrmxtVfx.TryParse(ValidExtensionJson, out var parsed));
            var json = VrmxtVfx.ToJson(parsed);
            Assert.IsTrue(VrmxtVfx.TryParse(json, out var again));
            Assert.AreEqual(1, again.Emitters.Count);
            Assert.AreEqual("HandSpark", again.Emitters[0].Name);
            Assert.AreEqual(2, again.Emitters[0].Node);
            Assert.AreEqual(20f, again.Emitters[0].EmissionRate);
            Assert.AreEqual(32, again.Emitters[0].MaxParticles);
        }

        [Test]
        public void ToJson_OmitsLegacyKeys()
        {
            var extension = new VrmxtVfxExtension(new[]
            {
                new VrmxtVfxEmitter(
                    "Spark",
                    3,
                    5,
                    new[] { 0.04f, 0.06f },
                    new[] { 1f, 0f, 0f, 0.5f },
                    VrmxtVfx.DefaultEmissionRate,
                    VrmxtVfx.DefaultMaxParticles,
                    VrmxtVfx.DefaultLifetime,
                    VrmxtVfx.DefaultStartSpeed),
            });

            var json = VrmxtVfx.ToJson(extension);
            Assert.IsFalse(json.Contains("type"));
            Assert.IsFalse(json.Contains("particle"));
            Assert.IsFalse(json.Contains("localPosition"));
            Assert.IsFalse(json.Contains("localRotation"));
            Assert.IsFalse(json.Contains("startSize"));
            Assert.IsFalse(json.Contains("startColor"));
            Assert.IsFalse(json.Contains("billboard"));
            Assert.IsFalse(json.Contains("facing"));

            Assert.IsTrue(VrmxtVfx.TryParse(json, out var parsed));
            Assert.AreEqual(5, parsed.Emitters[0].Texture);
            Assert.AreEqual(0.04f, parsed.Emitters[0].Size[0], 1e-5f);
            Assert.AreEqual(0.5f, parsed.Emitters[0].Color[3], 1e-5f);
        }
    }
}
