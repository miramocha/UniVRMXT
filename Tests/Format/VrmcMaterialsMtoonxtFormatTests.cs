using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmcMaterialsMtoonxtFormatTests
    {
        [Test]
        public void TryParse_HappyPath_MapsOpStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""write"" },
              ""outlineStencil"": { ""op"": ""outside"", ""materials"": [0] },
              ""faceSdf"": { ""enabled"": true }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNotNull(xt.Stencil);
            Assert.AreEqual("write", xt.Stencil.Op);
            Assert.IsNotNull(xt.OutlineStencil);
            Assert.AreEqual("outside", xt.OutlineStencil.Op);
            Assert.AreEqual(1, xt.OutlineStencil.Materials.Count);
            Assert.AreEqual(0, xt.OutlineStencil.Materials[0]);
        }

        [Test]
        public void TryParse_BadOp_SkipsThatStencilObject()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""nope"" },
              ""outlineStencil"": { ""op"": ""write"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
            Assert.IsNotNull(xt.OutlineStencil);
            Assert.AreEqual("write", xt.OutlineStencil.Op);
        }

        [Test]
        public void TryParse_MissingOp_SkipsStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""ref"": 1, ""comp"": ""always"", ""pass"": ""replace"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_NegativeMaterialIndex_SkipsStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""inside"", ""materials"": [-1] }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_WrongSpecVersion_Fails()
        {
            const string json = @"{ ""specVersion"": ""0.9"" }";
            Assert.IsFalse(VrmcMaterialsMtoonxt.TryParse(json, out _));
        }

        [Test]
        public void TryMap_UnknownCompare_Fails()
        {
            Assert.IsFalse(VrmcMaterialsMtoonxt.TryMapCompareFunction("Always", out _));
            Assert.IsTrue(VrmcMaterialsMtoonxt.TryMapCompareFunction("always", out var always));
            Assert.AreEqual(8, always);
        }

        [Test]
        public void TryParse_ZTestAlways_MapsCompare()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""zTest"": ""always""
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("always", xt.ZTest);
            Assert.AreEqual(8, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_MissingZTest_DefaultsLessEqual()
        {
            const string json = @"{ ""specVersion"": ""1.0"" }";
            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("lessEqual", xt.ZTest);
            Assert.AreEqual(4, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_ZWriteFalse_MapsFlag()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""zWrite"": false
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsTrue(xt.ZWrite.HasValue);
            Assert.IsFalse(xt.ZWrite.Value);
        }

        [Test]
        public void TryParse_BadZTest_DefaultsLessEqual()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""zTest"": ""nope""
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("lessEqual", xt.ZTest);
            Assert.AreEqual(4, xt.ZTestUnityInt);
        }

        [Test]
        public void TryParse_UnknownRenderQueueOffset_NotEmitted()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""renderQueueOffset"": -1
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.That(VrmcMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("renderQueueOffset"));
        }

        [Test]
        public void TryParse_OpInside_MapsMaterials()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""inside"", ""materials"": [3] }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNotNull(xt.Stencil);
            Assert.AreEqual("inside", xt.Stencil.Op);
            Assert.AreEqual(1, xt.Stencil.Materials.Count);
            Assert.AreEqual(3, xt.Stencil.Materials[0]);
        }

        [Test]
        public void TryParse_OpWriteWithMaterials_SkipsStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""write"", ""materials"": [1] }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_SameOnBody_SkipsStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""same"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
        }

        [Test]
        public void TryParse_OutlineSame_MapsOp()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""outlineStencil"": { ""op"": ""same"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("same", xt.OutlineStencil.Op);
        }

        [Test]
        public void Compile_InsideWhite_AssignsSharedRef()
        {
            var extras = new VrmcMaterialsMtoonxtExtension[4];
            extras[1] = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("inside", new[] { 3 }),
                null);
            extras[3] = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("write", null),
                null);

            VrmcMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
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
        public void Compile_GpuStateWithoutOp_IsDropped()
        {
            var extras = new VrmcMaterialsMtoonxtExtension[1];
            extras[0] = new VrmcMaterialsMtoonxtExtension(
                new VrmcMaterialsMtoonxtStencil(true, 7, 255, 255, "always", "replace", "keep", "keep"),
                null);

            VrmcMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out _);
            Assert.IsNull(body[0]);
        }

        [Test]
        public void Compile_OutlineSame_CopiesBody()
        {
            var extras = new VrmcMaterialsMtoonxtExtension[2];
            extras[0] = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("write", null),
                VrmcMaterialsMtoonxtStencil.FromOp("same", null));
            extras[1] = new VrmcMaterialsMtoonxtExtension(
                VrmcMaterialsMtoonxtStencil.FromOp("outside", new[] { 0 }),
                VrmcMaterialsMtoonxtStencil.FromOp("same", null));

            VrmcMaterialsMtoonxtStencilCompiler.Compile(extras, out var body, out var outline);
            Assert.AreEqual(body[1].Ref, outline[1].Ref);
            Assert.AreEqual(body[1].Comp, outline[1].Comp);
            Assert.AreEqual(body[0].Ref, outline[0].Ref);
        }
    }
}
