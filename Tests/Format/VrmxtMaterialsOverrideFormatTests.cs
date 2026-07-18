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
                    ""kind"": ""shader"",
                    ""name"": ""Example/SkinToon"",
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
            Assert.AreEqual("shader", unityOverride.Kind);
            Assert.AreEqual("Example/SkinToon", unityOverride.ShaderName);
            Assert.AreEqual("urp", unityOverride.Variant);
            Assert.AreEqual(1, extension.Overrides[0].Bindings.Count);
        }

        [Test]
        public void TryParse_RejectsDuplicateEngines()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""kind"": ""shader"",
                        ""name"": ""A""
                      }
                    },
                    {
                      ""engine"": ""unity"",
                      ""material"": {
                        ""kind"": ""shader"",
                        ""name"": ""B""
                      }
                    }
                  ]
                }
                ";

            Assert.IsFalse(VrmxtMaterialsOverride.TryParse(json, out _));
        }

        [Test]
        public void TryParse_ParsesUnrealMaterialSet()
        {
            const string json = @"
                {
                  ""specVersion"": ""1.0"",
                  ""overrides"": [
                    {
                      ""engine"": ""unreal"",
                      ""material"": {
                        ""kind"": ""materialSet"",
                        ""variants"": {
                          ""opaque"": ""/Game/M_Opaque""
                        }
                      }
                    }
                  ]
                }
                ";

            Assert.IsTrue(VrmxtMaterialsOverride.TryParse(json, out var extension));
            var material = extension.Overrides[0].Material as UnrealMaterialOverride;
            Assert.IsNotNull(material);
            Assert.AreEqual("materialSet", material.Kind);
            Assert.AreEqual("/Game/M_Opaque", material.Variants["opaque"]);
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
