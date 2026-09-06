using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;
using Object = UnityEngine.Object;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtMaterialsMtoonxtNativeLayerTests
    {
        private GameObject root;
        private Mesh mesh;
        private Material writer;
        private Material reader;
        private SkinnedMeshRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find(VrmxtMaterialsMtoonxt.BuiltinShaderName);
            Assert.IsNotNull(shader, "MToonXT shader must be imported for these tests.");
            root = new GameObject("Native layer test");
            writer = new Material(shader) { name = "Writer" };
            reader = new Material(shader) { name = "Reader" };
            writer.SetFloat("_DoubleSided", 1);
            reader.SetFloat("_DoubleSided", 1);
            mesh = new Mesh { name = "Source" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bindposes = new[] { Matrix4x4.identity };
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 4).ToArray();
            mesh.AddBlendShapeFrame("Test", 100, new Vector3[4], new Vector3[4], new Vector3[4]);
            var child = new GameObject("writer");
            child.transform.SetParent(root.transform, false);
            renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = writer;
            renderer.bones = new[] { root.transform };
            renderer.localBounds = new Bounds(Vector3.one, Vector3.one * 3);
            var readerObject = new GameObject("reader");
            readerObject.transform.SetParent(root.transform, false);
            readerObject.AddComponent<MeshRenderer>().sharedMaterial = reader;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(writer);
            Object.DestroyImmediate(reader);
            Object.DestroyImmediate(mesh);
        }

        private VrmxtMaterialsMtoonxtAuxiliaryRenderer Apply(string extra = "")
        {
            var json = "{\"extensions\":{\"VRMXT_materials_mtoonxt\":{\"specVersion\":\"1.0\","
                + "\"stencil\":[{\"writers\":[0],\"readers\":[1],\"showWritersThroughOccluders\":true"
                + extra + "}]}},\"materials\":["
                + "{\"name\":\"Writer\",\"extensions\":{\"VRMC_materials_mtoon\":{\"specVersion\":\"1.0\"}}},"
                + "{\"name\":\"Reader\",\"extensions\":{\"VRMC_materials_mtoon\":{\"specVersion\":\"1.0\"}}}]}";
            Assert.AreEqual(2, VrmxtMaterialsMtoonxtApplier.Apply(root, json));
            return root.GetComponent<VrmxtMaterialsMtoonxtAuxiliaryRenderer>();
        }

        [Test]
        public void Import_M02_UsesNativeLitLayerWithoutDuplicateShadowCaster()
        {
            var bounds = renderer.localBounds;
            writer.SetColor("_Color", Color.green);
            var auxiliary = Apply();
            Assert.AreEqual(1, auxiliary.NativeRendererCount);
            Assert.AreSame(mesh, renderer.sharedMesh);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            var layer = renderer.sharedMaterials[1];
            Assert.AreEqual(2451, reader.renderQueue);
            Assert.AreEqual(2452, writer.renderQueue);
            Assert.AreEqual(2453, layer.renderQueue);
            Assert.AreEqual(0, writer.GetFloat("_M_CullMode"));
            Assert.AreEqual(2, layer.GetFloat("_M_CullMode"));
            Assert.AreEqual(4, writer.GetFloat("_M_ZTest"));
            Assert.AreEqual(8, layer.GetFloat("_M_ZTest"));
            Assert.AreEqual(1, layer.GetFloat("_M_ZWrite"));
            Assert.AreEqual(6, writer.GetFloat("_M_StencilComp"));
            Assert.AreEqual(3, layer.GetFloat("_M_StencilComp"));
            Assert.AreEqual(reader.GetFloat("_M_StencilRef"), layer.GetFloat("_M_StencilRef"));
            Assert.IsTrue(writer.GetShaderPassEnabled("ShadowCaster"));
            Assert.IsFalse(layer.GetShaderPassEnabled("ShadowCaster"));
            Assert.AreEqual(writer.GetColor("_Color"), layer.GetColor("_Color"));
            Assert.AreEqual(bounds, renderer.localBounds);
            Assert.AreEqual(Vector3.zero, renderer.transform.localPosition);
        }

        [Test]
        public void Import_M07_DoesNotEnableSelfOcclusion()
        {
            Apply(",\"writersSelfOcclude\":false,\"writersWriteDepth\":false");
            Assert.AreEqual(0, renderer.sharedMaterials[1].GetFloat("_M_CullMode"));
            Assert.AreEqual(0, renderer.sharedMaterials[1].GetFloat("_M_ZWrite"));
        }

        [Test]
        public void PersistedImport_RebuildsLayersFromGraphOnActivation()
        {
            var auxiliary = Apply();
            auxiliary.Configure(null, null); // Same cleanup as the asset import hook.
            Assert.AreEqual(1, renderer.sharedMaterials.Length);
            root.SetActive(false);
            root.SetActive(true);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            Assert.AreEqual(1, auxiliary.NativeRendererCount);
        }

        [Test]
        public void MultiSubmesh_TargetsWriterNotLastSubmesh_AndRestoresSkinAndBounds()
        {
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 1, 2, 3 }, 1);
            renderer.sharedMaterials = new[] { writer, reader };
            var bounds = renderer.localBounds;
            var auxiliary = Apply();
            var layered = renderer.sharedMesh;
            Assert.AreNotSame(mesh, layered);
            Assert.AreEqual(3, layered.subMeshCount);
            CollectionAssert.AreEqual(mesh.GetIndices(0), layered.GetIndices(2));
            CollectionAssert.AreEqual(mesh.GetIndices(1), layered.GetIndices(1));
            CollectionAssert.AreEqual(mesh.boneWeights, layered.boneWeights);
            CollectionAssert.AreEqual(mesh.bindposes, layered.bindposes);
            Assert.AreEqual(mesh.vertexCount, layered.vertexCount);
            Assert.AreEqual(mesh.blendShapeCount, layered.blendShapeCount);
            Assert.AreEqual(bounds, renderer.localBounds);
            for (var i = 0; i < 3; i++)
            {
                VrmxtMaterialsMtoonxtApplier.ReapplyStencils(root, root.GetComponent<VrmxtMaterialsMtoonxtInstance>());
                Assert.AreEqual(3, renderer.sharedMaterials.Length);
                Assert.AreEqual(3, renderer.sharedMesh.subMeshCount);
            }
            auxiliary.enabled = false;
            Assert.AreSame(mesh, renderer.sharedMesh);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            Assert.AreEqual(bounds, renderer.localBounds);
            auxiliary.enabled = true;
            Assert.AreEqual(3, renderer.sharedMesh.subMeshCount);
            auxiliary.Configure(null, null);
            Assert.AreSame(mesh, renderer.sharedMesh);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
        }

        [Test]
        public void M08_KeepsCoverageCommandBuffers_ButNeverManualLitOverlays()
        {
            var auxiliary = Apply(",\"ignoreOccludedReaderAreas\":false");
            var cameraObject = new GameObject("test camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                typeof(VrmxtMaterialsMtoonxtAuxiliaryRenderer).GetMethod("OnCameraPreCull", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(auxiliary, new object[] { camera });
                Assert.AreEqual(1, auxiliary.NativeRendererCount);
                Assert.AreEqual(1, camera.GetCommandBuffers(CameraEvent.BeforeForwardOpaque).Length);
                Assert.AreEqual(0, camera.GetCommandBuffers(CameraEvent.AfterForwardOpaque).Length);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void ExportCopy_StripsDerivedLayersWithoutDestroyingLiveResources()
        {
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 1, 2, 3 }, 1);
            renderer.sharedMaterials = new[] { writer, reader };
            Apply();
            var liveLayer = renderer.sharedMaterials[2];
            var liveMesh = renderer.sharedMesh;
            var copy = Object.Instantiate(root);
            try
            {
                copy.GetComponent<VrmxtMaterialsMtoonxtAuxiliaryRenderer>().PrepareExportCopy();
                Assert.AreEqual(2, copy.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.Length);
                Assert.IsTrue(liveLayer != null);
                Assert.IsTrue(liveMesh != null);
                Assert.AreSame(liveLayer, renderer.sharedMaterials[2]);
                Assert.AreSame(liveMesh, renderer.sharedMesh);
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
