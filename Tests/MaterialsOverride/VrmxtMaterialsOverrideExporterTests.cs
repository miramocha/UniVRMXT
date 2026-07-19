using System.Text;
using NUnit.Framework;
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
