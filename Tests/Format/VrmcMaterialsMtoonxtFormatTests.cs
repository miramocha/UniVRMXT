using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmcMaterialsMtoonxtFormatTests
    {
        [Test]
        public void TryParse_HappyPath_MapsStencilEnums()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": {
                ""ref"": 1,
                ""comp"": ""always"",
                ""pass"": ""replace""
              },
              ""outlineStencil"": {
                ""ref"": 1,
                ""comp"": ""notEqual"",
                ""pass"": ""keep""
              },
              ""faceSdf"": { ""enabled"": true }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNotNull(xt.Stencil);
            Assert.AreEqual(1, xt.Stencil.Ref);
            Assert.IsFalse(xt.Stencil.Enabled);
            Assert.AreEqual(255, xt.Stencil.ReadMask);
            Assert.AreEqual(8, xt.Stencil.CompUnityInt);
            Assert.AreEqual(2, xt.Stencil.PassUnityInt);
            Assert.IsNotNull(xt.OutlineStencil);
            Assert.AreEqual(6, xt.OutlineStencil.CompUnityInt);
            Assert.AreEqual(0, xt.OutlineStencil.PassUnityInt);
        }

        [Test]
        public void TryParse_BadEnum_SkipsThatStencilObject()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""comp"": ""nope"" },
              ""outlineStencil"": { ""ref"": 2, ""pass"": ""keep"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsNull(xt.Stencil);
            Assert.IsNotNull(xt.OutlineStencil);
            Assert.AreEqual(2, xt.OutlineStencil.Ref);
        }

        [Test]
        public void TryParse_OutOfRangeRef_SkipsStencil()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""ref"": 300 }
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
        public void TryParse_EnabledTrue_MapsFlag()
        {
            const string json = @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""enabled"": true, ""ref"": 1, ""pass"": ""replace"" }
            }";

            Assert.IsTrue(VrmcMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.IsTrue(xt.Stencil.Enabled);
            Assert.AreEqual(1, xt.Stencil.Ref);
            Assert.AreEqual(2, xt.Stencil.PassUnityInt);
        }
    }
}
