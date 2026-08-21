using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

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
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
            Assert.IsTrue(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankOpaque
                )
            );
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmcMaterialsMtoonxtDrawOrder.RankBlend
                )
            );
            Assert.IsFalse(
                VrmcMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmcMaterialsMtoonxtDrawOrder.RankOpaque,
                    VrmcMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
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
                    hairWarn[0].Headline
                );
                Assert.AreEqual(
                    "This material is Cutout. Write may draw too late for clip",
                    hairWarn[0].Detail
                );

                var browWarn = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, browPair);
                Assert.AreEqual(1, browWarn.Count);
                Assert.AreEqual(
                    "Hair-Highlight is Cutout and clips this Write material",
                    browWarn[0].Headline
                );
                Assert.AreEqual(
                    "This material is Transparent. Write may draw too late for clip",
                    browWarn[0].Detail
                );
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

                Assert.AreEqual(
                    0,
                    VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, irisPair).Count
                );
                Assert.AreEqual(
                    0,
                    VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, whitePair).Count
                );
            }
            finally
            {
                Object.DestroyImmediate(white);
                Object.DestroyImmediate(iris);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollectForPair_InsideOverlay_SameRank_NoWarn()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("DrawOrderRoot");
            var suit = new Material(shader) { name = "Swimsuit" };
            var bone = new Material(shader) { name = "Skeleton" };
            try
            {
                suit.SetInt("_AlphaMode", 1);
                bone.SetInt("_AlphaMode", 1);
                AddMesh(root, "SuitMesh", suit);
                AddMesh(root, "BoneMesh", bone);

                var store = root.AddComponent<VrmcMaterialsMtoonxtInstance>();
                var suitPair = new VrmcMaterialsMtoonxtPair("Swimsuit", null, 0)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.Write,
                };
                var bonePair = new VrmcMaterialsMtoonxtPair("Skeleton", null, 1)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.ClipInsideOverlay,
                    StencilTargets = new List<Material> { suit },
                };
                store.SetPairs(new[] { suitPair, bonePair });

                Assert.AreEqual(
                    0,
                    VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, bonePair).Count
                );
            }
            finally
            {
                Object.DestroyImmediate(suit);
                Object.DestroyImmediate(bone);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulateFromExtensionJson_InsideOverlay_SetsEnum()
        {
            var root = new GameObject("AuthoringRoot");
            try
            {
                var store = root.AddComponent<VrmcMaterialsMtoonxtInstance>();
                const string json =
                    @"{""specVersion"":""1.0"",""stencil"":{""op"":""insideOverlay"",""materials"":[0]}}";
                var pair = new VrmcMaterialsMtoonxtPair("Skeleton", json, 1);
                store.SetPairs(new[] { pair });
                VrmcMaterialsMtoonxtAuthoring.PopulateFromExtensionJson(root, store, pair);
                Assert.AreEqual(VrmcMtoonxtBodyStencilOp.ClipInsideOverlay, pair.BodyOp);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollectForPair_CutoutWrite_OpaqueReader_Warns()
        {
            var shader = Shader.Find(VrmcMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("DrawOrderRoot");
            var writer = new Material(shader) { name = "White" };
            var reader = new Material(shader) { name = "Body_Skin-Highlight" };
            try
            {
                writer.SetInt("_AlphaMode", 1);
                reader.SetInt("_AlphaMode", 0);
                AddMesh(root, "WhiteMesh", writer);
                AddMesh(root, "BodyMesh", reader);

                var store = root.AddComponent<VrmcMaterialsMtoonxtInstance>();
                var writePair = new VrmcMaterialsMtoonxtPair("White", null, 0)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.Write,
                };
                var readPair = new VrmcMaterialsMtoonxtPair("Body_Skin-Highlight", null, 1)
                {
                    BodyOp = VrmcMtoonxtBodyStencilOp.ClipOutside,
                    StencilTargets = new List<Material> { writer },
                };
                store.SetPairs(new[] { writePair, readPair });

                var readWarn = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, readPair);
                Assert.AreEqual(1, readWarn.Count);
                Assert.AreEqual("White is Cutout and set to Write", readWarn[0].Headline);
                Assert.AreEqual(
                    "This material is Opaque. Write may draw too late for clip",
                    readWarn[0].Detail
                );

                var writeWarn = VrmcMaterialsMtoonxtDrawOrder.CollectForPair(store, writePair);
                Assert.AreEqual(1, writeWarn.Count);
                Assert.AreEqual(
                    "Body_Skin-Highlight is Opaque and clips this Write material",
                    writeWarn[0].Headline
                );
                Assert.AreEqual(
                    "This material is Cutout. Write may draw too late for clip",
                    writeWarn[0].Detail
                );
            }
            finally
            {
                Object.DestroyImmediate(writer);
                Object.DestroyImmediate(reader);
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
