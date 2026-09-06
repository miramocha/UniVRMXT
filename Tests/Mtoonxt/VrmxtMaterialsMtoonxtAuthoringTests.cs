using NUnit.Framework;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtMaterialsMtoonxtAuthoringTests
    {
        [Test]
        public void ToExtension_RetiredClipList_DoesNotExport()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("MtoonxtAuthoringRoot");
            var authored = new Material(shader) { name = "HairStencil" };
            var copy = new Material(authored) { name = authored.name };
            try
            {
                AddMesh(root, "HairMesh", copy);
                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var writer = new VrmxtMaterialsMtoonxtPair("Writer", null, 0);
                var clipper = new VrmxtMaterialsMtoonxtPair("HairStencil", null, 1);
                store.SetPairs(new[] { writer, clipper });
                VrmxtMaterialsMtoonxtAuthoring.RegisterExportStockCopy(authored, copy);

                var xt = VrmxtMaterialsMtoonxtAuthoring.ToExtension(root, store, clipper);
                Assert.IsNotNull(xt);
                Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("stencil"));
                Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("outlineStencil"));
            }
            finally
            {
                VrmxtMaterialsMtoonxtAuthoring.ClearExportStockCopies();
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ToExtension_RetiredPairs_DoNotExportStencil()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            if (shader == null)
            {
                Assert.Ignore("VRMXT/MToonXT10 not imported yet.");
            }

            var root = new GameObject("MtoonxtAuthoringDupRoot");
            var hair1 = new Material(shader) { name = "Hair" };
            var hair2 = new Material(shader) { name = "Hair" };
            var copy1 = new Material(hair1) { name = hair1.name };
            var copy2 = new Material(hair2) { name = hair2.name };
            try
            {
                AddMesh(root, "HairMesh1", copy1);
                AddMesh(root, "HairMesh2", copy2);
                var store = root.AddComponent<VrmxtMaterialsMtoonxtInstance>();
                var pair1 = new VrmxtMaterialsMtoonxtPair("Hair#1", null, 0);
                var pair2 = new VrmxtMaterialsMtoonxtPair("Hair#2", null, 1);
                store.SetPairs(new[] { pair1, pair2 });
                VrmxtMaterialsMtoonxtAuthoring.RegisterExportStockCopy(hair1, copy1);
                VrmxtMaterialsMtoonxtAuthoring.RegisterExportStockCopy(hair2, copy2);

                var xt = VrmxtMaterialsMtoonxtAuthoring.ToExtension(root, store, pair2);
                Assert.IsNotNull(xt);
                Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("stencil"));
                Assert.That(VrmxtMaterialsMtoonxt.ToJson(xt), Does.Not.Contain("outlineStencil"));
            }
            finally
            {
                VrmxtMaterialsMtoonxtAuthoring.ClearExportStockCopies();
                Object.DestroyImmediate(hair1);
                Object.DestroyImmediate(hair2);
                Object.DestroyImmediate(copy1);
                Object.DestroyImmediate(copy2);
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
