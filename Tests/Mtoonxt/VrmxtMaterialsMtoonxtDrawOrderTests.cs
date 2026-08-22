using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtMaterialsMtoonxtDrawOrderTests
    {
        [Test]
        public void WriterDrawsAfterReader_Rank()
        {
            Assert.IsTrue(
                VrmxtMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmxtMaterialsMtoonxtDrawOrder.RankBlend,
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
            Assert.IsTrue(
                VrmxtMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmxtMaterialsMtoonxtDrawOrder.RankOpaque
                )
            );
            Assert.IsFalse(
                VrmxtMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
            Assert.IsFalse(
                VrmxtMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout,
                    VrmxtMaterialsMtoonxtDrawOrder.RankBlend
                )
            );
            Assert.IsFalse(
                VrmxtMaterialsMtoonxtDrawOrder.WriterDrawsAfterReader(
                    VrmxtMaterialsMtoonxtDrawOrder.RankOpaque,
                    VrmxtMaterialsMtoonxtDrawOrder.RankCutout
                )
            );
        }

        [Test]
        public void CollectForPair_TransparentWrite_CutoutReader_Warns()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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

                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var browPair = new VrmxtMaterialsMtoonxtPair("Brow_Face-NoRim", null, 0)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.Write,
                };
                var hairPair = new VrmxtMaterialsMtoonxtPair("Hair-Highlight", null, 1)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.ClipOutside,
                    StencilTargets = new List<Material> { brow },
                };
                store.SetPairs(new[] { browPair, hairPair });

                var hairWarn = VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, hairPair);
                Assert.AreEqual(1, hairWarn.Count);
                Assert.AreEqual(
                    "Brow_Face-NoRim is Transparent and set to Write",
                    hairWarn[0].Headline
                );
                Assert.AreEqual(
                    "This material is Cutout. Write may draw too late for clip",
                    hairWarn[0].Detail
                );

                var browWarn = VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, browPair);
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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

                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var whitePair = new VrmxtMaterialsMtoonxtPair("White", null, 0)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.Write,
                };
                var irisPair = new VrmxtMaterialsMtoonxtPair("Iris", null, 1)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.ClipInside,
                    StencilTargets = new List<Material> { white },
                };
                store.SetPairs(new[] { whitePair, irisPair });

                Assert.AreEqual(
                    0,
                    VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, irisPair).Count
                );
                Assert.AreEqual(
                    0,
                    VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, whitePair).Count
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
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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

                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var suitPair = new VrmxtMaterialsMtoonxtPair("Swimsuit", null, 0)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.Write,
                };
                var bonePair = new VrmxtMaterialsMtoonxtPair("Skeleton", null, 1)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.ClipInsideOverlay,
                    StencilTargets = new List<Material> { suit },
                };
                store.SetPairs(new[] { suitPair, bonePair });

                Assert.AreEqual(
                    0,
                    VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, bonePair).Count
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
                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                const string json =
                    @"{""specVersion"":""1.0"",""stencil"":{""op"":""insideOverlay"",""materials"":[0]}}";
                var pair = new VrmxtMaterialsMtoonxtPair("Skeleton", json, 1);
                store.SetPairs(new[] { pair });
                VrmxtMaterialsMtoonxtAuthoring.PopulateFromExtensionJson(root, store, pair);
                Assert.AreEqual(VrmxtMtoonxtBodyStencilOp.ClipInsideOverlay, pair.BodyOp);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollectForPair_CutoutWrite_OpaqueReader_Warns()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
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

                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var writePair = new VrmxtMaterialsMtoonxtPair("White", null, 0)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.Write,
                };
                var readPair = new VrmxtMaterialsMtoonxtPair("Body_Skin-Highlight", null, 1)
                {
                    BodyOp = VrmxtMtoonxtBodyStencilOp.ClipOutside,
                    StencilTargets = new List<Material> { writer },
                };
                store.SetPairs(new[] { writePair, readPair });

                var readWarn = VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, readPair);
                Assert.AreEqual(1, readWarn.Count);
                Assert.AreEqual("White is Cutout and set to Write", readWarn[0].Headline);
                Assert.AreEqual(
                    "This material is Opaque. Write may draw too late for clip",
                    readWarn[0].Detail
                );

                var writeWarn = VrmxtMaterialsMtoonxtDrawOrder.CollectForPair(store, writePair);
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
