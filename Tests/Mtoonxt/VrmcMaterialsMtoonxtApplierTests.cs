using NUnit.Framework;
using System;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;
using UnityEngine;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmcMaterialsMtoonxtApplierTests
    {
        private const string GltfMtoonxt = @"
            {
              ""materials"": [
                {
                  ""name"": ""Face"",
                  ""extensions"": {
                    ""VRMC_materials_mtoon"": {
                      ""specVersion"": ""1.0""
                    },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": {
                        ""enabled"": true,
                        ""ref"": 1,
                        ""comp"": ""always"",
                        ""pass"": ""replace""
                      }
                    }
                  }
                }
              ]
            }";

        private const string GltfMissingSibling = @"
            {
              ""materials"": [
                {
                  ""name"": ""Face"",
                  ""extensions"": {
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""ref"": 1, ""pass"": ""replace"" }
                    }
                  }
                }
              ]
            }";

        private const string GltfWithOverride = @"
            {
              ""materials"": [
                {
                  ""name"": ""Face"",
                  ""extensions"": {
                    ""VRMC_materials_mtoon"": {
                      ""specVersion"": ""1.0""
                    },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""ref"": 1, ""pass"": ""replace"" }
                    },
                    ""VRMXT_materials_override"": {
                      ""specVersion"": ""1.0"",
                      ""overrides"": [
                        {
                          ""engine"": ""unity"",
                          ""material"": {
                            ""idType"": ""shaderName"",
                            ""id"": ""Hidden/InternalErrorShader""
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }";

        [Test]
        public void Apply_SwapsAndWritesStencil_WhenShaderResolves()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(fork);

            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(Shader.Find("Standard")) { name = "Face" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfMtoonxt,
                    name => IsMtoonxtForkName(name) ? fork : null);

                Assert.AreEqual(1, applied);
                Assert.AreEqual(fork, material.shader);
                if (material.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(1f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_MissingShader_LeavesStock()
        {
            var stock = Shader.Find("Standard");
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(stock) { name = "Face" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(root, GltfMtoonxt, _ => null);
                Assert.AreEqual(0, applied);
                Assert.AreEqual(stock, material.shader);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_MissingSiblingMtoon_Skips()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            var stock = Shader.Find("Standard");
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(stock) { name = "Face" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfMissingSibling,
                    name => IsMtoonxtForkName(name) ? fork : null);
                Assert.AreEqual(0, applied);
                Assert.AreEqual(stock, material.shader);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_OverrideWouldApply_SkipsSwap()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            var stock = Shader.Find("Standard");
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(stock) { name = "Face" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfWithOverride,
                    name =>
                    {
                        if (IsMtoonxtForkName(name) ||
                            name == "Hidden/InternalErrorShader")
                        {
                            return fork;
                        }

                        return null;
                    });
                Assert.AreEqual(0, applied);
                Assert.AreEqual(stock, material.shader);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryAttachFromGltfJson_StoresValidXt()
        {
            var root = new GameObject("root");
            try
            {
                Assert.IsTrue(VrmcMaterialsMtoonxtRuntime.TryAttachFromGltfJson(
                    root,
                    GltfMtoonxt,
                    out var store));
                Assert.IsNotNull(store);
                Assert.AreEqual(1, store.Pairs.Count);
                Assert.AreEqual("Face", store.Pairs[0].MaterialName);
                Assert.AreEqual(0, store.Pairs[0].GltfMaterialIndex);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShaderNameForPipeline_PicksBuiltinAndUrp()
        {
            Assert.AreEqual(
                VrmcMaterialsMtoonxt.BuiltinShaderName,
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Builtin));
            Assert.AreEqual(
                VrmcMaterialsMtoonxt.UrpShaderName,
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Urp));
            Assert.IsNull(
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Hdrp));
        }

        private const string GltfMtoonxtNoStencil = @"
            {
              ""materials"": [
                {
                  ""name"": ""Face"",
                  ""extensions"": {
                    ""VRMC_materials_mtoon"": {
                      ""specVersion"": ""1.0""
                    },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0""
                    }
                  }
                }
              ]
            }";

        [Test]
        public void ApplyStencilOffDefaults_WritesAlwaysComp()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmcMaterialsMtoonxt.StencilPropComp, 0f);
                material.SetFloat(VrmcMaterialsMtoonxt.OutlineStencilPropComp, 0f);
                VrmcMaterialsMtoonxtApplier.ApplyStencilOffDefaults(material);
                Assert.AreEqual(8f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                Assert.AreEqual(0f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                Assert.AreEqual(8f, material.GetFloat(VrmcMaterialsMtoonxt.OutlineStencilPropComp));
                Assert.AreEqual(255f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropReadMask));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Apply_NoStencilObject_WritesAlwaysComp()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(shader) { name = "Face" };
            material.SetFloat(VrmcMaterialsMtoonxt.StencilPropComp, 0f);
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfMtoonxtNoStencil,
                    name => IsMtoonxtForkName(name) ? shader : null);
                Assert.AreEqual(1, applied);
                Assert.AreEqual(8f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                Assert.AreEqual(0f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PackagedShaders_FindWhenImported()
        {
            var builtin = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (builtin == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            Assert.AreEqual(VrmcMaterialsMtoonxt.BuiltinShaderName, builtin.name);

            var urp = Shader.Find(VrmcMaterialsMtoonxt.UrpShaderName);
            if (urp == null)
            {
                Assert.Ignore("VRMXT/Universal Render Pipeline/MToonXT10 not imported (no URP package).");
            }

            Assert.AreEqual(VrmcMaterialsMtoonxt.UrpShaderName, urp.name);
        }

        private static bool IsMtoonxtForkName(string name)
        {
            return string.Equals(name, VrmcMaterialsMtoonxt.BuiltinShaderName, StringComparison.Ordinal)
                || string.Equals(name, VrmcMaterialsMtoonxt.UrpShaderName, StringComparison.Ordinal);
        }
    }
}
