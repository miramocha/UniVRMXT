using NUnit.Framework;
using UniVRMXT.MaterialsOverride;
using UnityEngine;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideRuntimeTests
    {
        private const string GltfWithOneOverrideJson = @"
            {
              ""materials"": [
                {
                  ""name"": ""Face""
                },
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
                  }
                }
              ]
            }
            ";

        [Test]
        public void TryAttachFromGltfJson_StoresOnlyMaterialsWithValidOverride()
        {
            var root = new GameObject("root");
            try
            {
                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(
                    root,
                    GltfWithOneOverrideJson,
                    out var store));

                Assert.IsNotNull(store);
                Assert.AreEqual(1, store.Entries.Count);
                Assert.IsTrue(store.TryGetEntry("Hair", out var entry));
                Assert.IsFalse(store.TryGetEntry("Face", out _));

                StringAssert.Contains("\"specVersion\":\"1.0\"", entry.ExtensionJson);
                StringAssert.Contains("Example/SkinToon", entry.ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryAttachFromGltfJson_NoOverrides_StillAddsInstance()
        {
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(Shader.Find("Standard")) { name = "Face" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                const string json = @"{ ""materials"": [ { ""name"": ""Face"" } ] }";
                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(root, json, out var store));
                Assert.IsNotNull(store);
                Assert.AreSame(store, root.GetComponent<VrmxtMaterialsOverrideInstance>());
                Assert.IsNotNull(root.GetComponent<VrmxtInstance>());
                Assert.AreEqual(1, store.Pairs.Count);
                Assert.AreEqual("Face", store.Pairs[0].MaterialName);
                Assert.IsTrue(string.IsNullOrEmpty(store.Pairs[0].ExtensionJson));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryAttachFromGltfJson_UnnamedMaterial_UsesIndexFallbackName()
        {
            var root = new GameObject("root");
            try
            {
                const string json = @"
                    {
                      ""materials"": [
                        {
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": [
                                {
                                  ""engine"": ""unity"",
                                  ""material"": { ""idType"": ""shaderName"", ""id"": ""Example/Skin"" }
                                }
                              ]
                            }
                          }
                        }
                      ]
                    }
                    ";

                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(root, json, out var store));
                Assert.IsTrue(store.TryGetEntry("material_0", out _));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryAttachFromGltfJson_DuplicateMaterialNames_GetDistinctStoreKeys()
        {
            var root = new GameObject("root");
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
                                  ""material"": { ""idType"": ""shaderName"", ""id"": ""Example/First"" }
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
                                  ""material"": { ""idType"": ""shaderName"", ""id"": ""Example/Second"" }
                                }
                              ]
                            }
                          }
                        }
                      ]
                    }
                    ";

                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(root, json, out var store));

                // Both entries survive under distinct keys instead of the second silently
                // overwriting the first.
                Assert.AreEqual(2, store.Entries.Count);
                Assert.IsFalse(store.TryGetEntry("Hair", out _));

                Assert.IsTrue(store.TryGetEntry("Hair#1", out var first));
                StringAssert.Contains("Example/First", first.ExtensionJson);

                Assert.IsTrue(store.TryGetEntry("Hair#2", out var second));
                StringAssert.Contains("Example/Second", second.ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryAttachFromGltfJson_InvalidExtension_IsSkipped()
        {
            var root = new GameObject("root");
            try
            {
                const string json = @"
                    {
                      ""materials"": [
                        {
                          ""name"": ""Broken"",
                          ""extensions"": {
                            ""VRMXT_materials_override"": {
                              ""specVersion"": ""1.0"",
                              ""overrides"": []
                            }
                          }
                        }
                      ]
                    }
                    ";

                // Instance is always attached for authoring; invalid extensions are not stored.
                Assert.IsTrue(VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(root, json, out var store));
                Assert.IsNotNull(store);
                Assert.IsFalse(store.TryGetEntry("Broken", out _));
                Assert.AreEqual(0, store.Pairs.Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
