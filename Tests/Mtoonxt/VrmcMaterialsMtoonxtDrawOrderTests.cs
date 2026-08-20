using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using UnityEngine;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmcMaterialsMtoonxtDrawOrderTests
    {
        [Test]
        public void WriterDrawsAfterReader_Rank()
        {
            Assert.IsTrue(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankBlend,
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout));
            Assert.IsTrue(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankOpaque));
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout));
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankBlend));
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankOpaque,
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout));
        }

        [Test]
        public void CollectForPair_TransparentWrite_CutoutReader_Warns()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("DrawOrderRoot");
            var brow = new Material(shader) { name = "Brow_Face-NoRim" };
            var hair = new Material(shader) { name = "Hair-Highlight" };
            try
            {
                brow.SetInt("_AlphaMode", 2);
                hair.SetInt("_AlphaMode", 1);
                AddMesh(root, "BrowMesh", brow);
                AddMesh(root, "HairMesh", hair);

                var store = root.AddComponent<VrmcMaterialsMtoonxtInstance>();
                var browPair = new VrmcMaterialsMtoonxtPair("Brow_Face-NoRim", null, 0)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.Write,
                };
                var hairPair = new VrmcMaterialsMtoonxtPair("Hair-Highlight", null, 1)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.ClipOutside,
                    StencilTargets = new List<Material> { brow },
                };
                store.SetPairs(new[] { browPair, hairPair });

                var hairWarn = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, hairPair);
                Assert.AreEqual(1, hairWarn.Count);
                Assert.AreEqual(
                    "Brow_Face-NoRim is Transparent and set to Write",
                    hairWarn[0].Headline);
                Assert.AreEqual(
                    "This material is Cutout. Write may draw too late for clip",
                    hairWarn[0].Detail);

                var browWarn = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, browPair);
                Assert.AreEqual(1, browWarn.Count);
                Assert.AreEqual(
                    "Hair-Highlight is Cutout and clips this Write material",
                    browWarn[0].Headline);
                Assert.AreEqual(
                    "This material is Transparent. Write may draw too late for clip",
                    browWarn[0].Detail);
            }
            finally
            {
                Object.DestroyImmediate(brow);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollectForPair_SameCutout_Silent()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("DrawOrderRoot");
            var white = new Material(shader) { name = "White" };
            var iris = new Material(shader) { name = "Iris" };
            try
            {
                white.SetInt("_AlphaMode", 1);
                iris.SetInt("_AlphaMode", 1);
                AddMesh(root, "WhiteMesh", white);
                AddMesh(root, "IrisMesh", iris);

                var store = root.AddComponent<VrmcMaterialsMtoonxtInstance>();
                var whitePair = new VrmcMaterialsMtoonxtPair("White", null, 0)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.Write,
                };
                var irisPair = new VrmcMaterialsMtoonxtPair("Iris", null, 1)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.ClipInside,
                    StencilTargets = new List<Material> { white },
                };
                store.SetPairs(new[] { whitePair, irisPair });

                Assert.AreEqual(0, VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, irisPair).Count);
                Assert.AreEqual(0, VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, whitePair).Count);
            }
            finally
            {
                Object.DestroyImmediate(white);
                Object.DestroyImmediate(iris);
                Object.DestroyImmediate(root);
            }
        }

        private static void AddMesh(GameObject root, string name, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
