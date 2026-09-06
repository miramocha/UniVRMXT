using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsMtoonxtFormatTests
    {
        [Test]
        public void Serialize_RetiredOperations_AreNeverEmitted()
        {
            var extension = new VrmxtMaterialsMtoonxtExtension();
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
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("stencil"));
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("outlineStencil"));
        }

        [Test]
        public void TryParse_RetiredOpObjects_StillParseMaterialExtras()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""op"": ""nope"" },
              ""outlineStencil"": { ""op"": ""write"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.AreEqual("lessEqual", xt.ZTest);
        }

        [Test]
        public void TryParse_RetiredGpuStencilObject_IsIgnored()
        {
            const string json =
                @"{
              ""specVersion"": ""1.0"",
              ""stencil"": { ""ref"": 1, ""comp"": ""always"", ""pass"": ""replace"" }
            }";

            Assert.IsTrue(VrmxtMaterialsMtoonxt.TryParse(json, out var xt));
            Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("stencil"));
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
    }
}
