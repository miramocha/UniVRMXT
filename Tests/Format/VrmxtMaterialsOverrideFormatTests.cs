using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsOverrideFormatTests
    {
        private const string ValidUnityOverrideJson = @"
            {
              ""specVersion"": ""1.0"",
              ""overrides"": [
                {
                  ""engine"": ""unity"",
                  ""material"": {
                    ""idType"": ""shaderName"",
                    ""id"": ""Example/SkinToon"",
                    ""variant"": ""urp""
                  },
                  ""bindings"": [
                    {
                      ""source"": ""shadeColorFactor"",
                      ""target"": ""_ShadeColor"",
                      ""targetType"": ""vector""
                    }
                  ]
                }
              ]
            }
            ";

        [Test]
        public void TryParse_ValidUnityOverride_ParsesShaderProfile()
        {
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(ValidUnityOverrideJson, out var extension));
            Assert.IsTrue(VrmxtMaterialsOverride.TryGetUnityOverride(extension, out var unityOverride));
            Assert.AreEqual("shaderName", unityOverride.IdType);
            Assert.AreEqual("Example/SkinToon", unityOverride.Id);
            Assert.AreEqual("Example/SkinToon", unityOverride.ShaderName);
            Assert.AreEqual("urp", unityOverride.Variant);
            Assert.AreEqual(1, extension.Overrides[0].Bindings.Count);
        }

        [Test]
        public void TryParse_RejectsLegacyKindNameSchema()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""kind"": ""shader"",
                        ""name"": ""Example/SkinToon""
                      }
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_AcceptsMultiUnity_DistinctVariants()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""A/Builtin"",
                        ""variant"": ""builtin""
                      }
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""B/Urp"",
                        ""variant"": ""urp""
                      }
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            Assert.AreEqual(2, extension.Overrides.Count);
            Assert.IsTrue(VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var unitySlots));
            Assert.AreEqual(2, unitySlots.Count);
        }

        [Test]
        public void TryParse_RejectsDuplicateEngineVariant()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""A"",
                        ""variant"": ""urp""
                      }
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""B"",
                        ""variant"": ""urp""
                      }
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_RejectsDuplicateEmptyUnityVariants()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""A""
                      }
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""B""
                      }
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_RejectsUnrealMaterialSet()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unreal"",
                      ""material"": {
                        ""idType"": ""materialSet"",
                        ""variants"": {
                          ""opaque"": ""/Game/M_Opaque""
                        }
                      }
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_ParsesUnrealResourcePath()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unreal"",
                      ""material"": {
                        ""idType"": ""resourcePath"",
                        ""id"": ""/Game/M_Opaque"",
                        ""variant"": ""opaque""
                      }
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            var material = extension.Overrides[0].Material as UnrealMaterialOverride;
            Assert.IsNotNull(material);
            Assert.AreEqual("resourcePath", material.IdType);
            Assert.AreEqual("/Game/M_Opaque", material.Id);
            Assert.AreEqual("opaque", material.Variant);
        }

        [Test]
        public void TryParse_ParsesUnityAndUnrealTogether_PreservesUnityVariant()
        {
            const string json =
                @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""Old/Shader"",""variant"":""urp""},""bindings"":[{""source"":""shadeColorFactor"",""target"":""_Color"",""targetType"":""vector""}],""properties"":[]},{""engine"":""unreal"",""material"":{""idType"":""resourcePath"",""id"":""/Game/M"",""variant"":""opaque""}}]}";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            Assert.AreEqual(2, extension.Overrides.Count);
            Assert.IsTrue(VrmxtMaterialsOverride.TryGetUnityOverride(extension, out var unity));
            Assert.AreEqual("urp", unity.Variant);
            var unreal = extension.Overrides[1].Material as UnrealMaterialOverride;
            Assert.IsNotNull(unreal);
            Assert.AreEqual("/Game/M", unreal.Id);
        }

        [Test]
        public void TryParse_AcceptsShaderFeatureBindingTargetType()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Example/SkinToon""
                      },
                      ""bindings"": [
                        {
                          ""source"": ""shadingToonyFactor"",
                          ""target"": ""_UseToony"",
                          ""targetType"": ""shaderFeature""
                        }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            Assert.AreEqual("shaderFeature", extension.Overrides[0].Bindings[0].TargetType);
        }

        [Test]
        public void TryParse_RejectsStaticSwitchBindingTargetType()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Example/SkinToon""
                      },
                      ""bindings"": [
                        {
                          ""source"": ""shadingToonyFactor"",
                          ""target"": ""_UseToony"",
                          ""targetType"": ""staticSwitch""
                        }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_ParsesProperties_AllTypes()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Example/SkinToon""
                      },
                      ""properties"": [
                        { ""name"": ""_Metallic"", ""type"": ""scalar"", ""value"": 0.5 },
                        { ""name"": ""_ShadeColor"", ""type"": ""vector"", ""value"": [1.0, 0.5, 0.25, 1.0] },
                        { ""name"": ""_MainTex"", ""type"": ""texture"", ""texture"": 2 },
                        { ""name"": ""_UseToony"", ""type"": ""shaderFeature"", ""value"": true }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            var properties = extension.Overrides[0].Properties;
            Assert.AreEqual(4, properties.Count);

            Assert.AreEqual("scalar", properties[0].Type);
            Assert.AreEqual(0.5f, properties[0].ScalarValue);

            Assert.AreEqual("vector", properties[1].Type);
            Assert.AreEqual(4, properties[1].VectorValue.Count);
            Assert.AreEqual(0.25f, properties[1].VectorValue[2]);

            Assert.AreEqual("texture", properties[2].Type);
            Assert.AreEqual(2, properties[2].TextureIndex);

            Assert.AreEqual("shaderFeature", properties[3].Type);
            Assert.IsTrue(properties[3].BoolValue);
        }

        [Test]
        public void ToJson_RoundTripsUnityOverrideWithPropertiesAndBindings()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Example/SkinToon"",
                        ""variant"": ""urp"",
                        ""provider"": {
                          ""id"": ""com.example.vrmxt-materials"",
                          ""version"": ""1.0.0""
                        }
                      },
                      ""bindings"": [
                        {
                          ""source"": ""shadeColorFactor"",
                          ""target"": ""_ShadeColor"",
                          ""targetType"": ""vector""
                        }
                      ],
                      ""properties"": [
                        { ""name"": ""_Metallic"", ""type"": ""scalar"", ""value"": 0.5 }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var original));

            var roundTripJson = VrmxtMaterialsOverride.ToJson(original);
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(roundTripJson, out var roundTripped));

            var originalUnity = original.Overrides[0].Material as UnityMaterialOverride;
            var roundTrippedUnity = roundTripped.Overrides[0].Material as UnityMaterialOverride;
            Assert.IsNotNull(originalUnity);
            Assert.IsNotNull(roundTrippedUnity);
            Assert.AreEqual(originalUnity.IdType, roundTrippedUnity.IdType);
            Assert.AreEqual(originalUnity.Id, roundTrippedUnity.Id);
            Assert.AreEqual(originalUnity.Variant, roundTrippedUnity.Variant);
            Assert.AreEqual(originalUnity.Provider.Id, roundTrippedUnity.Provider.Id);
            Assert.AreEqual(originalUnity.Provider.Version, roundTrippedUnity.Provider.Version);

            Assert.AreEqual(1, roundTripped.Overrides[0].Bindings.Count);
            Assert.AreEqual("_ShadeColor", roundTripped.Overrides[0].Bindings[0].Target);

            Assert.AreEqual(1, roundTripped.Overrides[0].Properties.Count);
            Assert.AreEqual(0.5f, roundTripped.Overrides[0].Properties[0].ScalarValue);

            var utf8Json = VrmxtMaterialsOverride.ToUtf8Json(original);
            Assert.AreEqual(roundTripJson, System.Text.Encoding.UTF8.GetString(utf8Json));
        }

        [Test]
        public void ToJson_RoundTripsUnrealResourcePath()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unreal"",
                      ""material"": {
                        ""idType"": ""resourcePath"",
                        ""id"": ""/Game/M_Opaque"",
                        ""variant"": ""opaque""
                      }
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var original));

            var roundTripJson = VrmxtMaterialsOverride.ToJson(original);
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(roundTripJson, out var roundTripped));

            var material = roundTripped.Overrides[0].Material as UnrealMaterialOverride;
            Assert.IsNotNull(material);
            Assert.AreEqual("resourcePath", material.IdType);
            Assert.AreEqual("/Game/M_Opaque", material.Id);
            Assert.AreEqual("opaque", material.Variant);
        }

        [Test]
        public void ToJson_RoundTripsMultiUnitySlots()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""VRMXT/Samples/TestOverrideBuiltin"",
                        ""variant"": ""builtin""
                      },
                      ""properties"": [
                        { ""name"": ""_Color"", ""type"": ""vector"", ""value"": [0, 1, 0, 1] }
                      ]
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""VRMXT/Samples/TestOverrideURP"",
                        ""variant"": ""urp""
                      },
                      ""properties"": [
                        { ""name"": ""_Color"", ""type"": ""vector"", ""value"": [1, 1, 0, 1] }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var original));
            var roundTripJson = VrmxtMaterialsOverride.ToJson(original);
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(roundTripJson, out var roundTripped));
            Assert.AreEqual(2, roundTripped.Overrides.Count);

            Assert.IsTrue(UnityOverrideSelector.TrySelectUnityOverride(
                roundTripped, RenderPipelineVariant.Builtin, out var builtin));
            Assert.AreEqual("VRMXT/Samples/TestOverrideBuiltin", builtin.ShaderName);

            Assert.IsTrue(UnityOverrideSelector.TrySelectUnityOverride(
                roundTripped, RenderPipelineVariant.Urp, out var urp));
            Assert.AreEqual("VRMXT/Samples/TestOverrideURP", urp.ShaderName);
        }

        [Test]
        public void UnityOverrideSelector_RejectsMismatchedVariant()
        {
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(ValidUnityOverrideJson, out var extension));
            Assert.IsFalse(UnityOverrideSelector.TrySelectUnityOverride(
                extension,
                RenderPipelineVariant.Builtin,
                out _));
            Assert.IsTrue(UnityOverrideSelector.TrySelectUnityOverride(
                extension,
                RenderPipelineVariant.Urp,
                out var unityOverride));
            Assert.AreEqual("Example/SkinToon", unityOverride.ShaderName);
        }

        [Test]
        public void UnityOverrideSelector_PicksMatchingSlot_AmongMultiUnity()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Builtin/Shader"",
                        ""variant"": ""builtin""
                      },
                      ""properties"": [
                        { ""name"": ""_Color"", ""type"": ""vector"", ""value"": [0, 1, 0, 1] }
                      ]
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""idType"": ""shaderName"",
                        ""id"": ""Urp/Shader"",
                        ""variant"": ""urp""
                      },
                      ""properties"": [
                        { ""name"": ""_Color"", ""type"": ""vector"", ""value"": [1, 1, 0, 1] }
                      ]
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            Assert.IsTrue(UnityOverrideSelector.TrySelectUnityEngineOverride(
                extension, RenderPipelineVariant.Urp, out var selected));
            Assert.AreEqual("Urp/Shader", ((UnityMaterialOverride)selected.Material).ShaderName);
            Assert.AreEqual(1, selected.Properties.Count);

            Assert.IsFalse(UnityOverrideSelector.TrySelectUnityEngineOverride(
                extension, RenderPipelineVariant.Hdrp, out _));
        }

        [Test]
        public void Generator_BuildsDescriptorForCompatiblePipeline()
        {
            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(ValidUnityOverrideJson, out var extension));
            var generator = new VrmxtMaterialsOverrideGenerator();
            Assert.IsTrue(generator.TryBuildOverrideDescriptor(
                0,
                extension,
                RenderPipelineVariant.Urp,
                out var descriptor));
            Assert.AreEqual("Example/SkinToon", descriptor.ShaderName);
            Assert.AreEqual(1, descriptor.Bindings.Count);
        }
    }
}
