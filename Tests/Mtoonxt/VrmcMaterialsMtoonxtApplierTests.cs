using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmcMaterialsMtoonxtApplierTests
    {
        private const string GltfMtoonxt =
            @"
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

        private const string GltfMissingSibling =
            @"
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

        private const string GltfWithOverride =
            @"
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

        [SetUp]
        public void SetUp()
        {
            VrmcMaterialsMtoonxtStencilRefs.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VrmcMaterialsMtoonxtStencilRefs.Reset();
        }

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
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(1, applied);
                Assert.AreEqual(fork, material.shader);
                if (material.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(1f, material.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                }

                Assert.IsTrue(material.GetShaderPassEnabled("ShadowCaster"));
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

            const string gltf =
                @"
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
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(2, applied);
                if (iris.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(32f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(8f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(2f, white.GetFloat(VrmcMaterialsMtoonxt.StencilPropPass));
                    Assert.AreEqual(1f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(32f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(3f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(0f, iris.GetFloat(VrmcMaterialsMtoonxt.StencilPropPass));
                }

                Assert.IsFalse(iris.GetShaderPassEnabled("ShadowCaster"));
                Assert.IsFalse(iris.GetShaderPassEnabled("DepthOnly"));
                Assert.IsFalse(iris.GetShaderPassEnabled("DepthNormals"));
                Assert.IsTrue(white.GetShaderPassEnabled("ShadowCaster"));
                Assert.IsTrue(white.GetShaderPassEnabled("DepthOnly"));
            }
            finally
            {
                Object.DestroyImmediate(iris);
                Object.DestroyImmediate(white);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UsesOverlayDepth_InsideOverlayAndSame()
        {
            var overlay = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("insideOverlay", new[] { 0 }),
                VrmcMaterialsMtoonxtStencil.FromOp("same", null)
            );
            Assert.IsTrue(VrmcMaterialsMtoonxtApplier.UsesOverlayDepth(overlay));

            var inside = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("inside", new[] { 0 }),
                VrmcMaterialsMtoonxtStencil.FromOp("same", null)
            );
            Assert.IsFalse(VrmcMaterialsMtoonxtApplier.UsesOverlayDepth(inside));
        }

        [Test]
        public void Apply_OpInsideOverlay_WritesEqualRefAndZTestAlways()
        {
            var fork = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (fork == null)
            {
                fork = Shader.Find("Hidden/InternalErrorShader");
            }

            Assert.IsNotNull(fork);

            const string gltf =
                @"
            {
              ""materials"": [
                { ""name"": ""Swimsuit"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" }
                    }
                }},
                { ""name"": ""Skeleton"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMC_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""insideOverlay"", ""materials"": [0] },
                      ""outlineStencil"": { ""op"": ""same"" }
                    }
                }}
              ]
            }";

            var root = new GameObject("root");
            var suitGo = new GameObject("suit");
            var boneGo = new GameObject("bone");
            suitGo.transform.SetParent(root.transform, false);
            boneGo.transform.SetParent(root.transform, false);
            var suit = new Material(fork) { name = "Swimsuit" };
            var bone = new Material(fork) { name = "Skeleton" };
            suitGo.AddComponent<MeshRenderer>().sharedMaterial = suit;
            boneGo.AddComponent<MeshRenderer>().sharedMaterial = bone;

            try
            {
                var applied = VrmcMaterialsMtoonxtApplier.Apply(
                    root,
                    gltf,
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(2, applied);
                if (bone.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, bone.GetFloat(VrmcMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(3f, bone.GetFloat(VrmcMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(0f, bone.GetFloat(VrmcMaterialsMtoonxt.StencilPropPass));
                }

                if (bone.HasProperty(VrmcMaterialsMtoonxt.ZTestProp))
                {
                    Assert.AreEqual(8f, bone.GetFloat(VrmcMaterialsMtoonxt.ZTestProp));
                    Assert.AreEqual(4f, suit.GetFloat(VrmcMaterialsMtoonxt.ZTestProp));
                }

                if (bone.HasProperty("_M_ZWrite"))
                {
                    Assert.AreEqual(0f, bone.GetFloat("_M_ZWrite"));
                    Assert.AreEqual(1f, suit.GetFloat("_M_ZWrite"));
                }

                Assert.IsTrue(bone.IsKeywordEnabled(VrmcMaterialsMtoonxt.OverlayDepthKeyword));
                Assert.IsFalse(suit.IsKeywordEnabled(VrmcMaterialsMtoonxt.OverlayDepthKeyword));
                Assert.IsFalse(bone.GetShaderPassEnabled("ShadowCaster"));
                Assert.IsTrue(suit.GetShaderPassEnabled("ShadowCaster"));
            }
            finally
            {
                Object.DestroyImmediate(suit);
                Object.DestroyImmediate(bone);
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
                    name => IsMtoonxtForkName(name) ? fork : null
                );
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
                        if (IsMtoonxtForkName(name) || name == "Hidden/InternalErrorShader")
                        {
                            return fork;
                        }

                        return null;
                    }
                );
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
                Assert.IsTrue(
                    VrmcMaterialsMtoonxtRuntime.TryAttachFromGltfJson(
                        root,
                        GltfMtoonxt,
                        out var store
                    )
                );
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
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Builtin)
            );
            Assert.AreEqual(
                VrmcMaterialsMtoonxt.UrpShaderName,
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Urp)
            );
            Assert.IsNull(
                VrmcMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Hdrp)
            );
        }

        private const string GltfMtoonxtNoStencil =
            @"
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
                    name => IsMtoonxtForkName(name) ? shader : null
                );
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
                Assert.AreEqual(
                    (float)BlendMode.OneMinusSrcAlpha,
                    material.GetFloat("_M_DstBlend")
                );
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
        public void ApplyStencilDrawOrder_InsideOverlay_AddsOne()
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
                VrmcMaterialsMtoonxtApplier.ApplyStencilDrawOrder(
                    material,
                    compiled,
                    overlay: true
                );
                Assert.AreEqual(2451, material.renderQueue);
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
        public void ApplyUtilityDepthPasses_Skip_DisablesShadowAndDepth()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader);

            var material = new Material(shader);
            try
            {
                VrmcMaterialsMtoonxtApplier.ApplyUtilityDepthPasses(material, skip: true);
                Assert.IsFalse(material.GetShaderPassEnabled("ShadowCaster"));
                Assert.IsFalse(material.GetShaderPassEnabled("DepthOnly"));
                VrmcMaterialsMtoonxtApplier.ApplyUtilityDepthPasses(material, skip: false);
                Assert.IsTrue(material.GetShaderPassEnabled("ShadowCaster"));
                Assert.IsTrue(material.GetShaderPassEnabled("DepthOnly"));
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
        public void Apply_TwoRoots_UseDistinctRefs()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(fork);

            var rootA = new GameObject("rootA");
            var meshA = new GameObject("meshA");
            meshA.transform.SetParent(rootA.transform, false);
            var matA = new Material(Shader.Find("Standard")) { name = "Face" };
            meshA.AddComponent<MeshRenderer>().sharedMaterial = matA;

            var rootB = new GameObject("rootB");
            var meshB = new GameObject("meshB");
            meshB.transform.SetParent(rootB.transform, false);
            var matB = new Material(Shader.Find("Standard")) { name = "Face" };
            meshB.AddComponent<MeshRenderer>().sharedMaterial = matB;

            try
            {
                Assert.AreEqual(
                    1,
                    VrmcMaterialsMtoonxtApplier.Apply(
                        rootA,
                        GltfMtoonxt,
                        name => IsMtoonxtForkName(name) ? fork : null
                    )
                );
                Assert.AreEqual(
                    1,
                    VrmcMaterialsMtoonxtApplier.Apply(
                        rootB,
                        GltfMtoonxt,
                        name => IsMtoonxtForkName(name) ? fork : null
                    )
                );

                if (matA.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, matA.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(33f, matB.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                }
            }
            finally
            {
                Object.DestroyImmediate(matA);
                Object.DestroyImmediate(matB);
                Object.DestroyImmediate(rootA);
                Object.DestroyImmediate(rootB);
            }
        }

        [Test]
        public void Apply_DestroyFirst_ThirdRootReusesBand()
        {
            var fork = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(fork);

            Shader Resolve(string name)
            {
                return IsMtoonxtForkName(name) ? fork : null;
            }

            var rootA = new GameObject("rootA");
            var meshA = new GameObject("meshA");
            meshA.transform.SetParent(rootA.transform, false);
            var matA = new Material(Shader.Find("Standard")) { name = "Face" };
            meshA.AddComponent<MeshRenderer>().sharedMaterial = matA;

            var rootB = new GameObject("rootB");
            var meshB = new GameObject("meshB");
            meshB.transform.SetParent(rootB.transform, false);
            var matB = new Material(Shader.Find("Standard")) { name = "Face" };
            meshB.AddComponent<MeshRenderer>().sharedMaterial = matB;

            var rootC = new GameObject("rootC");
            var meshC = new GameObject("meshC");
            meshC.transform.SetParent(rootC.transform, false);
            var matC = new Material(Shader.Find("Standard")) { name = "Face" };
            meshC.AddComponent<MeshRenderer>().sharedMaterial = matC;

            try
            {
                Assert.AreEqual(1, VrmcMaterialsMtoonxtApplier.Apply(rootA, GltfMtoonxt, Resolve));
                Assert.AreEqual(1, VrmcMaterialsMtoonxtApplier.Apply(rootB, GltfMtoonxt, Resolve));
                Object.DestroyImmediate(rootA);
                rootA = null;
                Assert.AreEqual(1, VrmcMaterialsMtoonxtApplier.Apply(rootC, GltfMtoonxt, Resolve));

                if (matC.HasProperty(VrmcMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, matC.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(33f, matB.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                }

                Assert.AreEqual(34, VrmcMaterialsMtoonxtStencilRefs.Acquire(999, 1));
            }
            finally
            {
                Object.DestroyImmediate(matA);
                Object.DestroyImmediate(matB);
                Object.DestroyImmediate(matC);
                if (rootA != null)
                {
                    Object.DestroyImmediate(rootA);
                }

                Object.DestroyImmediate(rootB);
                Object.DestroyImmediate(rootC);
            }
        }

        [Test]
        public void Apply_NoStencil_DoesNotLeaseBand()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            Shader Resolve(string name)
            {
                return IsMtoonxtForkName(name) ? shader : null;
            }

            var idle = new GameObject("idle");
            var idleMesh = new GameObject("idleMesh");
            idleMesh.transform.SetParent(idle.transform, false);
            var idleMat = new Material(shader) { name = "Face" };
            idleMesh.AddComponent<MeshRenderer>().sharedMaterial = idleMat;

            var writer = new GameObject("writer");
            var writerMesh = new GameObject("writerMesh");
            writerMesh.transform.SetParent(writer.transform, false);
            var writerMat = new Material(shader) { name = "Face" };
            writerMesh.AddComponent<MeshRenderer>().sharedMaterial = writerMat;

            try
            {
                Assert.AreEqual(
                    1,
                    VrmcMaterialsMtoonxtApplier.Apply(idle, GltfMtoonxtNoStencil, Resolve)
                );
                Assert.AreEqual(1, VrmcMaterialsMtoonxtApplier.Apply(writer, GltfMtoonxt, Resolve));
                Assert.AreEqual(32f, writerMat.GetFloat(VrmcMaterialsMtoonxt.StencilPropRef));
                Assert.AreEqual(33, VrmcMaterialsMtoonxtStencilRefs.Acquire(999, 1));
            }
            finally
            {
                Object.DestroyImmediate(idleMat);
                Object.DestroyImmediate(writerMat);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(writer);
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
                Assert.Ignore(
                    "VRMXT/Universal Render Pipeline/MToonXT10 not imported (no URP package)."
                );
            }

            Assert.AreEqual(VrmcMaterialsMtoonxt.UrpShaderName, urp.name);
        }

        private static bool IsMtoonxtForkName(string name)
        {
            return string.Equals(
                    name,
                    VrmcMaterialsMtoonxt.BuiltinShaderName,
                    StringComparison.Ordinal
                )
                || string.Equals(
                    name,
                    VrmcMaterialsMtoonxt.UrpShaderName,
                    StringComparison.Ordinal
                );
        }
    }
}
