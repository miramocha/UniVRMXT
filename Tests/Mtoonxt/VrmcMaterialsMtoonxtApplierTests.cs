using NUnit.Framework;
using System;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;
using UnityEngine;
using UnityEngine.Rendering;

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
                      ""stencil"": { ""op"": ""write"" }
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
                      ""stencil"": { ""op"": ""write"" }
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
                      ""stencil"": { ""op"": ""write"" }
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
        public void Apply_OpInside_WritesEqualRef()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(fork);

            const string gltf = @"
            {
              ""materials"": [
                { ""name"": ""Iris"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""inside"", ""materials"": [1] }
                    }
                }},
                { ""name"": ""White"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" }
                    }
                }}
              ]
            }";

            var root = new GameObject("root");
            var irisGo = new GameObject("iris");
            var whiteGo = new GameObject("white");
            irisGo.transform.SetParent(root.transform, false);
            whiteGo.transform.SetParent(root.transform, false);
            var iris = new Material(Shader.Find("Standard")) { name = "Iris" };
            var white = new Material(Shader.Find("Standard")) { name = "White" };
            irisGo.AddComponent<MeshRenderer>().sharedMaterial = iris;
            whiteGo.AddComponent<MeshRenderer>().sharedMaterial = white;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    gltf,
                    name => IsMtoonxtForkName(name) ? fork : null);

                Assert.AreEqual(2, applied);
                if (iris.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(1f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(8f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(2f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropPass));
                    Assert.AreEqual(1f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(1f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(3f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(0f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropPass));
                }
            }
            finally
            {
                Object.DestroyImmediate(iris);
                Object.DestroyImmediate(white);
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
        public void RestoreUnityMtoonPassSettings_Transparent_SetsBlendAndQueue()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetInt("_AlphaMode", 2);
                material.SetInt("_TransparentWithZWrite", 0);
                material.SetFloat("_M_SrcBlend", 0f);
                material.SetFloat("_M_DstBlend", 0f);
                material.renderQueue = 2000;
                VrmcMaterialsMtoonxtApplier.RestoreUnityMtoonPassSettings(material);
                Assert.AreEqual((float)BlendMode.SrcAlpha, material.GetFloat("_M_SrcBlend"));
                Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, material.GetFloat("_M_DstBlend"));
                Assert.AreEqual(0f, material.GetFloat("_M_ZWrite"));
                Assert.AreEqual(3000, material.renderQueue);
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHABLEND_ON"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyZTest_Always_WritesCompareAlways()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmcMaterialsMtoonxt.ZTestProp, 0f);
                VrmcMaterialsMtoonxtApplier.ApplyZTest(material, "always");
                Assert.AreEqual(8f, material.GetFloat(VrmcMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyZTest_Uninitialized_WritesLessEqual()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmcMaterialsMtoonxt.ZTestProp, 0f);
                VrmcMaterialsMtoonxtApplier.ApplyZTest(material, null);
                Assert.AreEqual(4f, material.GetFloat(VrmcMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void EnsureStencilOffIfUninitialized_RecoversZTest()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmcMaterialsMtoonxt.ZTestProp, 0f);
                VrmcMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);
                Assert.AreEqual(4f, material.GetFloat(VrmcMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyStencilDrawOrder_Write_SubtractsTwo()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmcMaterialsMtoonxtStencil.Compiled(1, "always", "replace");
                VrmcMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
                Assert.AreEqual(2448, material.renderQueue);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyStencilDrawOrder_Inside_SubtractsOne()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmcMaterialsMtoonxtStencil.Compiled(1, "equal", "keep");
                VrmcMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
                Assert.AreEqual(2449, material.renderQueue);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyStencilDrawOrder_Outside_LeavesQueue()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmcMaterialsMtoonxtStencil.Compiled(1, "notEqual", "keep");
                VrmcMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
                Assert.AreEqual(2450, material.renderQueue);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyZWrite_False_ClearsUnityZWrite()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat("_M_ZWrite", 1f);
                VrmcMaterialsMtoonxtApplier.ApplyZWrite(material, false);
                Assert.AreEqual(0f, material.GetFloat("_M_ZWrite"));
            }
            finally
            {
                Object.DestroyImmediate(material);
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
