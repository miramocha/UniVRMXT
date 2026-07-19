using System.Text;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UnityEngine;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideExporterTests
    {
        private const string StoredExtensionJson = @"
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
                  ""properties"": [
                    { ""name"": ""_MainTex"", ""type"": ""texture"", ""texture"": 0 }
                  ]
                }
              ]
            }
            ";

        [Test]
        public void ResolveUnityVariant_KeepsExistingVariant_EvenWhenActivePipelineDiffers()
        {
            Assert.AreEqual(
                "urp",
                VrmxtMaterialsOverrideExporter.ResolveUnityVariant("urp", RenderPipelineVariant.Builtin));
        }

        [Test]
        public void ResolveUnityVariant_FillsFromActivePipeline_WhenMissing()
        {
            Assert.AreEqual(
                "builtin",
                VrmxtMaterialsOverrideExporter.ResolveUnityVariant(null, RenderPipelineVariant.Builtin));
            Assert.AreEqual(
                "urp",
                VrmxtMaterialsOverrideExporter.ResolveUnityVariant(string.Empty, RenderPipelineVariant.Urp));
            Assert.AreEqual(
                "hdrp",
                VrmxtMaterialsOverrideExporter.ResolveUnityVariant(null, RenderPipelineVariant.Hdrp));
        }

        [Test]
        public void BuildPending_TryBuildUtf8Extension_WritesStoredJsonAsIs()
        {
            var root = new GameObject("root");
            try
            {
                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                Assert.AreEqual(1, pending.Count);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.Contains("\"variant\":\"urp\"", json);
                StringAssert.Contains("Example/SkinToon", json);

                var all = VrmxtMaterialsOverrideExporter.BuildAllUtf8Extensions(pending);
                Assert.AreEqual(1, all.Count);
                Assert.IsTrue(all.ContainsKey("Hair"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryBuildUtf8Extension_UnknownMaterialName_ReturnsFalse()
        {
            var root = new GameObject("root");
            try
            {
                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                Assert.IsFalse(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Face", out var utf8));
                Assert.IsNull(utf8);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildPending_MultiUnitySlots_KeepsSiblingAfterSync()
        {
            var root = new GameObject("root");
            var shader = Shader.Find("Standard");
            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                const string multiSlotJson = @"
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

                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetPairs(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", multiSlotJson)
                    {
                        OverrideMaterial = overrideMat,
                    },
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                Assert.AreEqual(1, pending.Count);
                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.Contains("\"variant\":\"builtin\"", json);
                StringAssert.Contains("\"variant\":\"urp\"", json);
                StringAssert.Contains("Urp/Shader", json);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrepareTextures_ForeignVariantOnly_DoesNotDropTextureProperties()
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Hair" };

            try
            {
                // Stock mesh mat has no _MainTex — old sole-slot fallback would remap the
                // foreign variant against this mat and drop the texture property.
                meshGo.AddComponent<MeshRenderer>().sharedMaterial = material;

                var active = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
                var foreignVariant = active == RenderPipelineVariant.Builtin ? "urp" : "builtin";
                var foreignJson =
                    "{\"specVersion\":\"1.0\",\"overrides\":[{\"engine\":\"unity\",\"material\":{" +
                    "\"idType\":\"shaderName\",\"id\":\"Foreign/Shader\",\"variant\":\"" +
                    foreignVariant +
                    "\"},\"properties\":[{\"name\":\"_MainTex\",\"type\":\"texture\",\"texture\":3}," +
                    "{\"name\":\"_Color\",\"type\":\"vector\",\"value\":[0,1,0,1]}]}]}";

                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(foreignJson, out _));

                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", foreignJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                VrmxtMaterialsOverrideExporter.PrepareTextures(pending, root, (tex, needsAlpha) => 7);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.Contains("\"texture\":3", json);
                StringAssert.Contains("\"variant\":\"" + foreignVariant + "\"", json);
                StringAssert.Contains("_Color", json);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrepareTextures_RemapsOnlySelectedSlot_LeavesSiblingTextureIndex()
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);
            var texture = new Texture2D(2, 2);
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Hair" };

            try
            {
                material.SetTexture("_MainTex", texture);
                meshGo.AddComponent<MeshRenderer>().sharedMaterial = material;

                const string multiSlotJson = @"
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
                            { ""name"": ""_MainTex"", ""type"": ""texture"", ""texture"": 3 }
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
                            { ""name"": ""_MainTex"", ""type"": ""texture"", ""texture"": 0 }
                          ]
                        },
                        {
                          ""engine"": ""unity"",
                          ""material"": {
                            ""idType"": ""shaderName"",
                            ""id"": ""Empty/Shader""
                          },
                          ""properties"": [
                            { ""name"": ""_MainTex"", ""type"": ""texture"", ""texture"": 9 }
                          ]
                        }
                      ]
                    }
                    ";

                // Three unity slots: typed builtin + urp + empty. Empty must not be remapped
                // when a typed active match exists.
                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(multiSlotJson, out _));

                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", multiSlotJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                VrmxtMaterialsOverrideExporter.PrepareTextures(pending, root, (tex, needsAlpha) => 7);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                // Sibling empty slot write-through keeps its original texture index when a
                // typed active match exists (BIRP/URP hosts).
                var active = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
                if (active == RenderPipelineVariant.Builtin)
                {
                    StringAssert.Contains("\"texture\":7", json);
                    StringAssert.Contains("\"texture\":0", json);
                    StringAssert.Contains("\"texture\":9", json);
                }
                else if (active == RenderPipelineVariant.Urp)
                {
                    StringAssert.Contains("\"texture\":7", json);
                    StringAssert.Contains("\"texture\":3", json);
                    StringAssert.Contains("\"texture\":9", json);
                }
                else
                {
                    // HDRP: no exact typed match → empty selected; its index remaps to 7.
                    StringAssert.Contains("\"texture\":7", json);
                    StringAssert.Contains("\"texture\":3", json);
                    StringAssert.Contains("\"texture\":0", json);
                    StringAssert.DoesNotContain("\"texture\":9", json);
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrepareTextures_DropsTextureProperty_WhenNoLiveMaterialMatches()
        {
            // No renderer/material named "Hair" exists on root, so the remap can never
            // resolve a live material for the "_MainTex" property.
            var root = new GameObject("root");
            try
            {
                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                VrmxtMaterialsOverrideExporter.PrepareTextures(pending, root, (tex, needsAlpha) => 7);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.DoesNotContain("_MainTex", json);
                StringAssert.DoesNotContain("\"texture\"", json);
                StringAssert.Contains("\"variant\":\"urp\"", json);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrepareTextures_DropsTextureProperty_WhenRegisterFails()
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);
            var texture = new Texture2D(2, 2);
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Hair" };

            try
            {
                material.SetTexture("_MainTex", texture);
                var renderer = meshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                // Simulate a failed registration (e.g. exporter texture slot exhausted).
                VrmxtMaterialsOverrideExporter.PrepareTextures(pending, root, (tex, needsAlpha) => -1);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.DoesNotContain("_MainTex", json);
                StringAssert.DoesNotContain("\"texture\"", json);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryBuildUtf8Extension_DropsTextureProperty_WhenPrepareTexturesNeverRan()
        {
            // PrepareTextures is intentionally never called (e.g. the export host has no
            // RegisterSRgbTexture hook); the stored texture index is stale on any new
            // export and must never reach the written JSON.
            var root = new GameObject("root");
            try
            {
                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.DoesNotContain("_MainTex", json);
                StringAssert.DoesNotContain("\"texture\"", json);
                StringAssert.Contains("\"variant\":\"urp\"", json);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrepareTextures_RemapsLiveTextureProperty_PreservesVariant()
        {
            var root = new GameObject("root");
            var meshGo = new GameObject("mesh");
            meshGo.transform.SetParent(root.transform, false);
            var texture = new Texture2D(2, 2);
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Hair" };

            try
            {
                material.SetTexture("_MainTex", texture);
                var renderer = meshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.SetEntries(new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", StoredExtensionJson),
                });

                var pending = VrmxtMaterialsOverrideExporter.BuildPending(store);
                VrmxtMaterialsOverrideExporter.PrepareTextures(pending, root, (tex, needsAlpha) => 7);

                Assert.IsTrue(VrmxtMaterialsOverrideExporter.TryBuildUtf8Extension(pending, "Hair", out var utf8));
                var json = Encoding.UTF8.GetString(utf8);

                StringAssert.Contains("\"texture\":7", json);
                StringAssert.Contains("\"variant\":\"urp\"", json);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }
    }
}
