using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;
using Object = UnityEngine.Object;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtMaterialsMtoonxtApplierTests
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
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" }
                    }
                  }
                }
              ]
            }";

        private const string GltfRetiredMtoonxt =
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
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" }
                    }
                  }
                }
              ]
            }";

        private const string GltfRelationshipBaseline =
            @"
            {
              ""extensions"": {
                ""VRMXT_materials_mtoonxt"": {
                  ""specVersion"": ""1.0"",
                  ""stencilRelationships"": [{
                    ""writers"": [0],
                    ""readers"": [1]
                  }]
                }
              },
              ""materials"": [
                { ""name"": ""Writer"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" }
                }},
                { ""name"": ""Reader"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" }
                }}
              ]
            }";

        private const string GltfRelationshipShowThrough =
            @"
            {
              ""extensions"": {
                ""VRMXT_materials_mtoonxt"": {
                  ""specVersion"": ""1.0"",
                  ""stencilRelationships"": [{
                    ""writers"": [0],
                    ""readers"": [1],
                    ""showWritersThroughOccluders"": true,
                    ""writersSelfOcclude"": false
                  }]
                }
              },
              ""materials"": [
                { ""name"": ""Writer"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" }
                }},
                { ""name"": ""Reader"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" }
                }}
              ]
            }";

        [Test]
        public void Apply_RelationshipBaseline_UsesConfirmedWriterThenReaderQueues()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("root");
            var writerObject = new GameObject("writer");
            var readerObject = new GameObject("reader");
            writerObject.transform.SetParent(root.transform, false);
            readerObject.transform.SetParent(root.transform, false);
            var writer = new Material(shader) { name = "Writer" };
            var reader = new Material(shader) { name = "Reader" };
            writerObject.AddComponent<MeshRenderer>().sharedMaterial = writer;
            readerObject.AddComponent<MeshRenderer>().sharedMaterial = reader;

            try
            {
                Assert.AreEqual(
                    2,
                    VrmxtMaterialsMtoonxtApplier.Apply(
                        root,
                        GltfRelationshipBaseline,
                        name => IsMtoonxtForkName(name) ? shader : null
                    )
                );
                Assert.AreEqual(2451, writer.renderQueue);
                Assert.AreEqual(2452, reader.renderQueue);
                Assert.IsNull(root.GetComponent<VrmxtMaterialsMtoonxtAuxiliaryRenderer>());
            }
            finally
            {
                Object.DestroyImmediate(writer);
                Object.DestroyImmediate(reader);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReapplyRelationships_RestoresShowThroughAuxiliaryDrawAfterReload()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("root");
            var writerObject = new GameObject("writer");
            var readerObject = new GameObject("reader");
            writerObject.transform.SetParent(root.transform, false);
            readerObject.transform.SetParent(root.transform, false);
            var writer = new Material(shader) { name = "Writer" };
            var reader = new Material(shader) { name = "Reader" };
            writerObject.AddComponent<MeshRenderer>().sharedMaterial = writer;
            readerObject.AddComponent<MeshRenderer>().sharedMaterial = reader;

            try
            {
                Assert.AreEqual(
                    2,
                    VrmxtMaterialsMtoonxtApplier.Apply(
                        root,
                        GltfRelationshipShowThrough,
                        name => IsMtoonxtForkName(name) ? shader : null
                    )
                );
                var store = root.GetComponent<VrmxtMaterialsMtoonxtInstance>();
                var auxiliary = root.GetComponent<VrmxtMaterialsMtoonxtAuxiliaryRenderer>();
                Assert.IsNotNull(store);
                Assert.IsNotNull(auxiliary);
                Assert.AreEqual(1, auxiliary.DrawCount);
                Assert.AreEqual(2452, writer.renderQueue);
                Assert.AreEqual(2451, reader.renderQueue);

                auxiliary.Configure(null, null);
                Assert.AreEqual(0, auxiliary.DrawCount);
                auxiliary.enabled = true;
                Assert.AreEqual(1, auxiliary.DrawCount);
                Assert.AreEqual(2452, writer.renderQueue);
                Assert.AreEqual(2451, reader.renderQueue);
            }
            finally
            {
                Object.DestroyImmediate(writer);
                Object.DestroyImmediate(reader);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRelationshipPass_PreservesDoubleSidedCulling()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            var pass = new VrmxtMtoonxtRelationshipPass(
                "notEqual",
                "keep",
                "lessEqual",
                zWrite: true,
                cullBack: true,
                writeColor: true
            );

            try
            {
                material.SetInt("_DoubleSided", 1);
                VrmxtMaterialsMtoonxtApplier.ApplyRelationshipPass(material, pass, 1, 0);
                Assert.AreEqual(0f, material.GetFloat("_M_CullMode"));

                material.SetInt("_DoubleSided", 0);
                VrmxtMaterialsMtoonxtApplier.ApplyRelationshipPass(material, pass, 1, 0);
                Assert.AreEqual(2f, material.GetFloat("_M_CullMode"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

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
                    ""VRMXT_materials_mtoonxt"": {
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
            VrmxtMaterialsMtoonxtStencilRefs.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VrmxtMaterialsMtoonxtStencilRefs.Reset();
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfMtoonxt,
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(1, applied);
                Assert.AreEqual(fork, material.shader);
                if (material.HasProperty(VrmxtMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(1f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
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

            const string gltf =
                @"
            {
              ""materials"": [
                { ""name"": ""Iris"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""inside"", ""materials"": [1] }
                    }
                }},
                { ""name"": ""White"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMXT_materials_mtoonxt"": {
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
                    root,
                    gltf,
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(2, applied);
                if (iris.HasProperty(VrmxtMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, white.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(32f, white.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(8f, white.GetFloat(VrmxtMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(2f, white.GetFloat(VrmxtMaterialsMtoonxt.StencilPropPass));
                    Assert.AreEqual(1f, iris.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(32f, iris.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(3f, iris.GetFloat(VrmxtMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(0f, iris.GetFloat(VrmxtMaterialsMtoonxt.StencilPropPass));
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
        public void UsesOverlayDepth_InsideOverlayAndSame()
        {
            var overlay = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("insideOverlay", new[] { 0 }),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null)
            );
            Assert.IsTrue(VrmxtMaterialsMtoonxtApplier.UsesOverlayDepth(overlay));
            Assert.IsTrue(VrmxtMaterialsMtoonxtApplier.UsesOutlineOverlayDepth(overlay));

            var inside = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("inside", new[] { 0 }),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null)
            );
            Assert.IsFalse(VrmxtMaterialsMtoonxtApplier.UsesOverlayDepth(inside));
            Assert.IsFalse(VrmxtMaterialsMtoonxtApplier.UsesOutlineOverlayDepth(inside));
        }

        [Test]
        public void UsesOverlayDepth_OutlineOnly_DoesNotForceBody()
        {
            var xt = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                VrmxtMaterialsMtoonxtStencil.FromOp("insideOverlay", new[] { 0 })
            );
            Assert.IsFalse(VrmxtMaterialsMtoonxtApplier.UsesOverlayDepth(xt));
            Assert.IsTrue(VrmxtMaterialsMtoonxtApplier.UsesOutlineOverlayDepth(xt));
        }

        [Test]
        public void Apply_OpInsideOverlay_WritesEqualRefAndZTestAlways()
        {
            var fork = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" }
                    }
                }},
                { ""name"": ""Skeleton"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMXT_materials_mtoonxt"": {
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
                    root,
                    gltf,
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(2, applied);
                if (bone.HasProperty(VrmxtMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(1f, bone.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
                    Assert.AreEqual(3f, bone.GetFloat(VrmxtMaterialsMtoonxt.StencilPropComp));
                    Assert.AreEqual(0f, bone.GetFloat(VrmxtMaterialsMtoonxt.StencilPropPass));
                }

                if (bone.HasProperty(VrmxtMaterialsMtoonxt.ZTestProp))
                {
                    Assert.AreEqual(8f, bone.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
                    Assert.AreEqual(4f, suit.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
                }

                if (bone.HasProperty("_M_ZWrite"))
                {
                    Assert.AreEqual(0f, bone.GetFloat("_M_ZWrite"));
                    Assert.AreEqual(1f, suit.GetFloat("_M_ZWrite"));
                }

                Assert.IsTrue(bone.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OverlayDepthKeyword));
                Assert.IsTrue(
                    bone.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OutlineOverlayDepthKeyword)
                );
                Assert.IsFalse(suit.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OverlayDepthKeyword));
                Assert.IsFalse(
                    suit.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OutlineOverlayDepthKeyword)
                );

                Assert.AreEqual(suit.renderQueue + 3, bone.renderQueue);

                if (
                    string.Equals(
                        fork.name,
                        VrmxtMaterialsMtoonxt.BuiltinShaderName,
                        StringComparison.Ordinal
                    )
                )
                {
                    Assert.IsFalse(bone.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBase));
                    Assert.IsTrue(
                        bone.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBaseOverlay)
                    );
                    Assert.IsTrue(suit.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBase));
                    Assert.IsFalse(
                        suit.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBaseOverlay)
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(suit);
                Object.DestroyImmediate(bone);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_OutlineInsideOverlay_LeavesBodyZTest()
        {
            var fork = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (fork == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            const string gltf =
                @"
            {
              ""materials"": [
                { ""name"": ""Body"", ""extensions"": {
                    ""VRMC_materials_mtoon"": { ""specVersion"": ""1.0"" },
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0"",
                      ""stencil"": { ""op"": ""write"" },
                      ""outlineStencil"": { ""op"": ""insideOverlay"", ""materials"": [0] }
                    }
                }}
              ]
            }";

            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(fork) { name = "Body" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
                    root,
                    gltf,
                    name => IsMtoonxtForkName(name) ? fork : null
                );

                Assert.AreEqual(1, applied);
                Assert.AreEqual(4f, material.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
                Assert.AreEqual(1f, material.GetFloat("_M_ZWrite"));
                Assert.IsFalse(material.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OverlayDepthKeyword));
                Assert.IsTrue(
                    material.IsKeywordEnabled(VrmxtMaterialsMtoonxt.OutlineOverlayDepthKeyword)
                );
                Assert.IsTrue(material.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBase));
                Assert.IsFalse(
                    material.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBaseOverlay)
                );
                Assert.IsFalse(
                    material.GetShaderPassEnabled(VrmxtMaterialsMtoonxt.PassForwardBaseOutline)
                );
                Assert.IsTrue(
                    material.GetShaderPassEnabled(
                        VrmxtMaterialsMtoonxt.PassForwardBaseOutlineOverlay
                    )
                );
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(root, GltfMtoonxt, _ => null);
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
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
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
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
                    VrmxtMaterialsMtoonxtRuntime.TryAttachFromGltfJson(
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
        public void TryAttachFromGltfJson_IgnoresRetiredGltfKey()
        {
            var root = new GameObject("root");
            try
            {
                Assert.IsTrue(
                    VrmxtMaterialsMtoonxtRuntime.TryAttachFromGltfJson(
                        root,
                        GltfRetiredMtoonxt,
                        out var store
                    )
                );
                Assert.IsNotNull(store);
                Assert.AreEqual(0, store.Pairs.Count);
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
                VrmxtMaterialsMtoonxt.BuiltinShaderName,
                VrmxtMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Builtin)
            );
            Assert.AreEqual(
                VrmxtMaterialsMtoonxt.UrpShaderName,
                VrmxtMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Urp)
            );
            Assert.IsNull(
                VrmxtMaterialsMtoonxtApplier.ShaderNameForPipeline(RenderPipelineVariant.Hdrp)
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
                    ""VRMXT_materials_mtoonxt"": {
                      ""specVersion"": ""1.0""
                    }
                  }
                }
              ]
            }";

        [Test]
        public void ApplyStencilOffDefaults_WritesAlwaysComp()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmxtMaterialsMtoonxt.StencilPropComp, 0f);
                material.SetFloat(VrmxtMaterialsMtoonxt.OutlineStencilPropComp, 0f);
                VrmxtMaterialsMtoonxtApplier.ApplyStencilOffDefaults(material);
                Assert.AreEqual(8f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropComp));
                Assert.AreEqual(0f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
                Assert.AreEqual(8f, material.GetFloat(VrmxtMaterialsMtoonxt.OutlineStencilPropComp));
                Assert.AreEqual(255f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropReadMask));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Apply_NoStencilObject_WritesAlwaysComp()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);
            var material = new Material(shader) { name = "Face" };
            material.SetFloat(VrmxtMaterialsMtoonxt.StencilPropComp, 0f);
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            try
            {
                var applied = VrmxtMaterialsMtoonxtApplier.Apply(
                    root,
                    GltfMtoonxtNoStencil,
                    name => IsMtoonxtForkName(name) ? shader : null
                );
                Assert.AreEqual(1, applied);
                Assert.AreEqual(8f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropComp));
                Assert.AreEqual(0f, material.GetFloat(VrmxtMaterialsMtoonxt.StencilPropEnabled));
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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
                VrmxtMaterialsMtoonxtApplier.RestoreUnityMtoonPassSettings(material);
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmxtMaterialsMtoonxt.ZTestProp, 0f);
                VrmxtMaterialsMtoonxtApplier.ApplyZTest(material, "always");
                Assert.AreEqual(8f, material.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyZTest_Uninitialized_WritesLessEqual()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmxtMaterialsMtoonxt.ZTestProp, 0f);
                VrmxtMaterialsMtoonxtApplier.ApplyZTest(material, null);
                Assert.AreEqual(4f, material.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void EnsureStencilOffIfUninitialized_RecoversZTest()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat(VrmxtMaterialsMtoonxt.ZTestProp, 0f);
                VrmxtMaterialsMtoonxtApplier.EnsureStencilOffIfUninitialized(material);
                Assert.AreEqual(4f, material.GetFloat(VrmxtMaterialsMtoonxt.ZTestProp));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ApplyStencilDrawOrder_Write_SubtractsTwo()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmxtMaterialsMtoonxtStencil.Compiled(1, "always", "replace");
                VrmxtMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmxtMaterialsMtoonxtStencil.Compiled(1, "equal", "keep");
                VrmxtMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmxtMaterialsMtoonxtStencil.Compiled(1, "equal", "keep");
                VrmxtMaterialsMtoonxtApplier.ApplyStencilDrawOrder(
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.renderQueue = 2450;
                var compiled = VrmxtMaterialsMtoonxtStencil.Compiled(1, "notEqual", "keep");
                VrmxtMaterialsMtoonxtApplier.ApplyStencilDrawOrder(material, compiled);
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var material = new Material(shader);
            try
            {
                material.SetFloat("_M_ZWrite", 1f);
                VrmxtMaterialsMtoonxtApplier.ApplyZWrite(material, false);
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
                    VrmxtMaterialsMtoonxtApplier.Apply(
                        rootA,
                        GltfMtoonxt,
                        name => IsMtoonxtForkName(name) ? fork : null
                    )
                );
                Assert.AreEqual(
                    1,
                    VrmxtMaterialsMtoonxtApplier.Apply(
                        rootB,
                        GltfMtoonxt,
                        name => IsMtoonxtForkName(name) ? fork : null
                    )
                );

                if (matA.HasProperty(VrmxtMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, matA.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(33f, matB.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
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
                Assert.AreEqual(1, VrmxtMaterialsMtoonxtApplier.Apply(rootA, GltfMtoonxt, Resolve));
                Assert.AreEqual(1, VrmxtMaterialsMtoonxtApplier.Apply(rootB, GltfMtoonxt, Resolve));
                Object.DestroyImmediate(rootA);
                rootA = null;
                Assert.AreEqual(1, VrmxtMaterialsMtoonxtApplier.Apply(rootC, GltfMtoonxt, Resolve));

                if (matC.HasProperty(VrmxtMaterialsMtoonxt.StencilPropRef))
                {
                    Assert.AreEqual(32f, matC.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                    Assert.AreEqual(33f, matB.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                }

                Assert.AreEqual(34, VrmxtMaterialsMtoonxtStencilRefs.Acquire(999, 1));
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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
                    VrmxtMaterialsMtoonxtApplier.Apply(idle, GltfMtoonxtNoStencil, Resolve)
                );
                Assert.AreEqual(1, VrmxtMaterialsMtoonxtApplier.Apply(writer, GltfMtoonxt, Resolve));
                Assert.AreEqual(32f, writerMat.GetFloat(VrmxtMaterialsMtoonxt.StencilPropRef));
                Assert.AreEqual(33, VrmxtMaterialsMtoonxtStencilRefs.Acquire(999, 1));
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
            var builtin = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (builtin == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            Assert.AreEqual(VrmxtMaterialsMtoonxt.BuiltinShaderName, builtin.name);

            var urp = Shader.Find(VrmxtMaterialsMtoonxt.UrpShaderName);
            if (urp == null)
            {
                Assert.Ignore(
                    "VRMXT/Universal Render Pipeline/MToonXT10 not imported (no URP package)."
                );
            }

            Assert.AreEqual(VrmxtMaterialsMtoonxt.UrpShaderName, urp.name);
        }

        private static bool IsMtoonxtForkName(string name)
        {
            return string.Equals(
                    name,
                    VrmxtMaterialsMtoonxt.BuiltinShaderName,
                    StringComparison.Ordinal
                )
                || string.Equals(
                    name,
                    VrmxtMaterialsMtoonxt.UrpShaderName,
                    StringComparison.Ordinal
                );
        }
    }
}
