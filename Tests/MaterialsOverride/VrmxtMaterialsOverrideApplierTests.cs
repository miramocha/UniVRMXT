using NUnit.Framework;
using UniVRMXT.MaterialsOverride;
using UnityEngine;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideApplierTests
    {
        private const string GltfWithBindingsAndPropertiesJson = @"
            {
              ""materials"": [
                {
                  ""name"": ""Hair"",
                  ""extensions"": {
                    ""VRMC_materials_mtoon"": {
                      ""specVersion"": ""1.0"",
                      ""shadeColorFactor"": [1.0, 0.0, 0.0],
                      ""shadingToonyFactor"": 0.42
                    },
                    ""VRMXT_materials_override"": {
                      ""specVersion"": ""1.0"",
                      ""overrides"": [
                        {
                          ""engine"": ""unity"",
                          ""material"": {
                            ""idType"": ""shaderName"",
                            ""id"": ""Standard""
                          },
                          ""bindings"": [
                            {
                              ""source"": ""shadeColorFactor"",
                              ""target"": ""_Color"",
                              ""targetType"": ""vector""
                            },
                            {
                              ""source"": ""shadingToonyFactor"",
                              ""target"": ""_Metallic"",
                              ""targetType"": ""scalar""
                            }
                          ],
                          ""properties"": [
                            { ""name"": ""_Glossiness"", ""type"": ""scalar"", ""value"": 0.25 }
                          ]
                        }
                      ]
                    }
                  }
                }
              ]
            }
            ";

        private static GameObject CreateRootWithNamedMaterial(string materialName, out Material material)
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);

            material = new Material(Shader.Find("Standard")) { name = materialName };
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return root;
        }

        [Test]
        public void Apply_MissingShader_LeavesStockAndDoesNotApply()
        {
            var root = CreateRootWithNamedMaterial("Hair", out var material);
            var originalShader = material.shader;
            try
            {
                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(
                    root,
                    @"{
                      ""materials"": [{
                        ""name"": ""Hair"",
                        ""extensions"": {
                          ""VRMXT_materials_override"": {
                            ""specVersion"": ""1.0"",
                            ""overrides"": [{
                              ""engine"": ""unity"",
                              ""material"": {
                                ""idType"": ""shaderName"",
                                ""id"": ""Definitely/Missing/OverrideShader""
                              }
                            }]
                          }
                        }
                      }]
                    }",
                    out var store));

                store.TryGetPair("Hair", out var pair);
                pair.SourceMaterial = material;

                var applied = VrmxtMaterialsOverrideApplier.Apply(
                    root,
                    store,
                    "{}",
                    RenderPipelineVariant.Builtin);

                Assert.AreEqual(0, applied);
                Assert.AreSame(originalShader, material.shader);
                Assert.AreSame(material, root.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_SetsShaderAndPropertiesAndBindings_BindingSourceFromSiblingMtoon()
        {
            var root = CreateRootWithNamedMaterial("Hair", out var material);
            try
            {
                var applied = VrmxtMaterialsOverrideApplier.Apply(
                    root,
                    GltfWithBindingsAndPropertiesJson,
                    RenderPipelineVariant.Builtin);

                Assert.AreEqual(1, applied);
                Assert.AreEqual("Standard", material.shader.name);

                Assert.AreEqual(new Color(1f, 0f, 0f, 1f), material.GetColor("_Color"));
                Assert.AreEqual(0.42f, material.GetFloat("_Metallic"), 1e-4f);
                Assert.AreEqual(0.25f, material.GetFloat("_Glossiness"), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_NoSiblingMtoonExtension_IgnoresBindingsButKeepsProperties()
        {
            const string json = @"
                {
                  ""materials"": [
                    {
                      ""name"": ""Hair"",
                      ""extensions"": {
                        ""VRMXT_materials_override"": {
                          ""specVersion"": ""1.0"",
                          ""overrides"": [
                            {
                              ""engine"": ""unity"",
                              ""material"": { ""idType"": ""shaderName"", ""id"": ""Standard"" },
                              ""bindings"": [
                                { ""source"": ""shadeColorFactor"", ""target"": ""_Color"", ""targetType"": ""vector"" }
                              ],
                              ""properties"": [
                                { ""name"": ""_Glossiness"", ""type"": ""scalar"", ""value"": 0.6 }
                              ]
                            }
                          ]
                        }
                      }
                    }
                  ]
                }
                ";

            var root = CreateRootWithNamedMaterial("Hair", out var material);
            var defaultColor = material.GetColor("_Color");
            try
            {
                var applied = VrmxtMaterialsOverrideApplier.Apply(root, json, RenderPipelineVariant.Builtin);

                Assert.AreEqual(1, applied);
                Assert.AreEqual(defaultColor, material.GetColor("_Color"));
                Assert.AreEqual(0.6f, material.GetFloat("_Glossiness"), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_VariantMismatch_LeavesStockMaterialUntouched()
        {
            var root = CreateRootWithNamedMaterial("Hair", out var material);
            var originalShader = material.shader;
            try
            {
                const string json = @"
                    {
                      ""materials"": [
                        {
                          ""name"": ""Hair"",
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": [
                                {
                                  ""engine"": ""unity"",
                                  ""material"": {
                                    ""idType"": ""shaderName"",
                                    ""id"": ""Standard"",
                                    ""variant"": ""hdrp""
                                  }
                                }
                              ]
                            }
                          }
                        }
                      ]
                    }
                    ";

                var applied = VrmxtMaterialsOverrideApplier.Apply(root, json, RenderPipelineVariant.Builtin);

                Assert.AreEqual(0, applied);
                Assert.AreSame(originalShader, material.shader);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_UnknownShader_LeavesStockMaterialUntouched()
        {
            var root = CreateRootWithNamedMaterial("Hair", out var material);
            var originalShader = material.shader;
            try
            {
                const string json = @"
                    {
                      ""materials"": [
                        {
                          ""name"": ""Hair"",
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": [
                                {
                                  ""engine"": ""unity"",
                                  ""material"": {
                                    ""idType"": ""shaderName"",
                                    ""id"": ""Definitely/Not/A/Real/Shader""
                                  }
                                }
                              ]
                            }
                          }
                        }
                      ]
                    }
                    ";

                var applied = VrmxtMaterialsOverrideApplier.Apply(root, json, RenderPipelineVariant.Builtin);

                Assert.AreEqual(0, applied);
                Assert.AreSame(originalShader, material.shader);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_DuplicateMaterialNames_AppliesEachOverrideToItsOwnMaterialOnly()
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);

            var firstMaterial = new Material(Shader.Find("Standard")) { name = "Hair" };
            var secondMaterial = new Material(Shader.Find("Standard")) { name = "Hair" };
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { firstMaterial, secondMaterial };

            try
            {
                const string json = @"
                    {
                      ""materials"": [
                        {
                          ""name"": ""Hair"",
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": [
                                {
                                  ""engine"": ""unity"",
                                  ""material"": { ""idType"": ""shaderName"", ""id"": ""Standard"" },
                                  ""properties"": [
                                    { ""name"": ""_Glossiness"", ""type"": ""scalar"", ""value"": 0.1 }
                                  ]
                                }
                              ]
                            }
                          }
                        },
                        {
                          ""name"": ""Hair"",
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": [
                                {
                                  ""engine"": ""unity"",
                                  ""material"": { ""idType"": ""shaderName"", ""id"": ""Standard"" },
                                  ""properties"": [
                                    { ""name"": ""_Glossiness"", ""type"": ""scalar"", ""value"": 0.9 }
                                  ]
                                }
                              ]
                            }
                          }
                        }
                      ]
                    }
                    ";

                var applied = VrmxtMaterialsOverrideApplier.Apply(root, json, RenderPipelineVariant.Builtin);

                Assert.AreEqual(2, applied);
                Assert.AreEqual(0.1f, firstMaterial.GetFloat("_Glossiness"), 1e-4f);
                Assert.AreEqual(0.9f, secondMaterial.GetFloat("_Glossiness"), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(firstMaterial);
                Object.DestroyImmediate(secondMaterial);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DetectActivePipeline_DefaultTestEnvironment_ReturnsBuiltinOrRecognizedPipeline()
        {
            // No render pipeline asset assigned in test environment → Builtin. Assert the
            // helper does not throw and returns a defined enum value either way.
            // Implementation must not use Object.GetType() (Warudo/UMod Reflection ban).
            var pipeline = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
            Assert.IsTrue(
                pipeline == RenderPipelineVariant.Builtin ||
                pipeline == RenderPipelineVariant.Urp ||
                pipeline == RenderPipelineVariant.Hdrp);
        }
    }
}
