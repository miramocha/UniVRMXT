using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsMtoonxtFormatTests
    {
        [Test]
        public void Serialize_RetiredOperations_AreNeverEmitted()
        {
            var extension = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null));
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(extension), Does.Not.Contain("stencil"));
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(extension), Does.Not.Contain("outlineStencil"));
        }

        [Test]
        public void TryParse_RetiredMaterialStencil_IsIgnored()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""write"" },
              ""outlineStencil"": { ""op"": ""outside"", ""materials"": [0] },
              ""faceSdf"": { ""enabled"": true }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
            Assert.IsNull(xt.OutlineStencil);
        }

        [Test]
        public void TryParse_BadOp_SkipsThatStencilObject()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""nope"" },
              ""outlineStencil"": { ""op"": ""write"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
            Assert.IsNull(xt.OutlineStencil);
        }

        [Test]
        public void TryParse_MissingOp_SkipsStencil()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""ref"": 1, ""comp"": ""always"", ""pass"": ""replace"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_NegativeMaterialIndex_SkipsStencil()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""inside"", ""materials"": [-1] }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_WrongSpecVersion_Fails()
        {
            const string json = @"{ ""specVersion"": ""0.9"" }";
            Assert.IsFalse(VrmxtMaterialsMtoonxt.TryParse(json, out _));
        }

        [Test]
        public void TryParse_RetiredGltfKey_Fails()
        {
            const string json =
                @"{
              ""VRMC_materials_mtoonxt"": {
                ""specVersion"": ""1.0"",
                ""stencil"": { ""op"": ""write"" }
              }
            }";

            Assert.IsFalse(VrmxtMaterialsMtoonxt.TryParse(json, out _));
        }

        [Test]
        public void TryMap_UnknownCompare_Fails()
        {
            Assert.IsFalse(VrmxtMaterialsMtoonxt.TryMapCompareFunction("Always", out _));
            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryMapCompareFunction("always", out var always));
            Assert.AreEqual(8, always);
        }

        [Test]
        public void TryParse_ZTestAlways_MapsCompare()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""zTest"": ""always""
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("always", xt.ZTest);
            Assert.AreEqual(8, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_MissingZTest_DefaultsLessEqual()
        {
            const string json = @"{ ""specVersion"": ""1.0"" }";
            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("lessEqual", xt.ZTest);
            Assert.AreEqual(4, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_ZWriteFalse_MapsFlag()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""zWrite"": false
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsTrue(xt.ZWrite.HasValue);
            Assert.IsFalse(xt.ZWrite.Value);
        }

        [Test]
        public void TryParse_BadZTest_DefaultsLessEqual()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""zTest"": ""nope""
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("lessEqual", xt.ZTest);
            Assert.AreEqual(4, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_UnknownRenderQueueOffset_NotEmitted()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""renderQueueOffset"": -1
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("renderQueueOffset"));
        }

        [Test]
        public void TryParse_RetiredInside_IsIgnored()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""inside"", ""materials"": [3] }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_RetiredInsideOverlay_IsIgnored()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""insideOverlay"", ""materials"": [0] }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_OpWriteWithMaterials_SkipsStencil()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""write"", ""materials"": [1] }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_SameOnBody_SkipsStencil()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""same"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_RetiredOutlineSame_IsIgnored()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""outlineStencil"": { ""op"": ""same"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.OutlineStencil);
        }

        [Test]
        public void Compile_InsideWhite_AssignsSharedRef()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[4];
            extras[1] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("inside", new[] { 3 }),
                null
            );
            extras[3] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                null
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.IsTrue(body[3].Enabled);
            Assert.AreEqual(1, body[3].Ref);
            Assert.AreEqual("always", body[3].Comp);
            Assert.AreEqual("replace", body[3].Pass);
            Assert.AreEqual(1, body[1].Ref);
            Assert.AreEqual("equal", body[1].Comp);
            Assert.AreEqual("keep", body[1].Pass);
            Assert.IsNull(outline[1]);
            Assert.IsNull(outline[3]);
        }

        [Test]
        public void Compile_InsideOverlayWhite_AssignsEqualKeep()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[2];
            extras[0] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                null
            );
            extras[1] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("insideOverlay", new[] { 0 }),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null)
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.AreEqual(1, body[1].Ref);
            Assert.AreEqual("equal", body[1].Comp);
            Assert.AreEqual("keep", body[1].Pass);
            Assert.AreEqual(body[1].Ref, outline[1].Ref);
            Assert.AreEqual("equal", outline[1].Comp);
        }

        [Test]
        public void Compile_GpuStateWithoutOp_IsDropped()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[1];
            extras[0] = new VrmxtMaterialsMtoonxtExtension(
                new VrmxtMaterialsMtoonxtStencil(
                    true,
                    7,
                    255,
                    255,
                    "always",
                    "replace",
                    "keep",
                    "keep"
                ),
                null
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out _);
            Assert.IsNull(body[0]);
        }

        [Test]
        public void Compile_OutlineSame_CopiesBody()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[2];
            extras[0] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null)
            );
            extras[1] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("outside", new[] { 0 }),
                VrmxtMaterialsMtoonxtStencil.FromOp("same", null)
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.AreEqual(body[1].Ref, outline[1].Ref);
            Assert.AreEqual(body[1].Comp, outline[1].Comp);
            Assert.AreEqual(body[0].Ref, outline[0].Ref);
        }

        [Test]
        public void Compile_OutlineInside_UsesBodyWriteRef()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[4];
            extras[1] = new VrmxtMaterialsMtoonxtExtension(
                null,
                VrmxtMaterialsMtoonxtStencil.FromOp("inside", new[] { 3 })
            );
            extras[3] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                null
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.AreEqual(1, body[3].Ref);
            Assert.AreEqual("replace", body[3].Pass);
            Assert.IsNull(body[1]);
            Assert.AreEqual(1, outline[1].Ref);
            Assert.AreEqual("equal", outline[1].Comp);
            Assert.AreEqual("keep", outline[1].Pass);
        }

        [Test]
        public void Compile_OutlineWrite_SharesBodyWriterRef()
        {
            var extras = new VrmxtMaterialsMtoonxtExtension[4];
            extras[1] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("inside", new[] { 3 }),
                VrmxtMaterialsMtoonxtStencil.FromOp("inside", new[] { 3 })
            );
            extras[3] = new VrmxtMaterialsMtoonxtExtension(
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null),
                VrmxtMaterialsMtoonxtStencil.FromOp("write", null)
            );

            VrmxtMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.AreEqual(body[3].Ref, outline[3].Ref);
            Assert.AreEqual(body[1].Ref, outline[1].Ref);
            Assert.AreEqual(body[3].Ref, body[1].Ref);
            Assert.AreEqual("replace", outline[3].Pass);
        }

        [Test]
        public void TryMapClipMaterialIndices_Miss_Fails()
        {
            Assert.IsFalse(
                VrmxtMaterialsMtoonxt.TryMapClipMaterialIndices(new[] { 3 }, _ => null, out _)
            );
        }

        [Test]
        public void TryMapClipMaterialIndices_MapsUnique()
        {
            Assert.IsTrue(
                VrmxtMaterialsMtoonxt.TryMapClipMaterialIndices(
                    new[] { 3, 3 },
                    i => i + 1,
                    out var mapped
                )
            );
            Assert.AreEqual(1, mapped.Length);
            Assert.AreEqual(4, mapped[0]);
        }
    }
}
