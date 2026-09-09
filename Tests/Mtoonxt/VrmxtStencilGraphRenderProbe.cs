using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    // Also runnable through the editor bridge without the test runner replacing a
    // user's unsaved scene. Numeric pixel checks, not a substitute for visual review.
    public static class VrmxtStencilGraphRenderProbe
    {
        public static object Run()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var allocated = new List<UnityEngine.Object>();
            var result = new List<string>();
            var previous = RenderTexture.active;
            try
            {
                var root = new GameObject("Graph regression (temporary)");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var cameraObject = new GameObject("Probe camera");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 2;
                camera.aspect = 1;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0, 0, -5);
                var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                target.Create(); allocated.Add(target); camera.targetTexture = target;
                var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
                allocated.Add(pixels);
                var shader = Shader.Find("VRMXT/MToonXT10");
                var colors = new[] { Color.red, Color.blue, Color.green, Color.yellow };
                var materials = new List<Material>();
                var objects = new List<GameObject>();
                for (var i = 0; i < 4; i++)
                {
                    var material = new Material(shader) { name = "Graph" + i };
                    allocated.Add(material); materials.Add(material);
                    material.SetColor("_Color", Color.black);
                    material.SetColor("_ShadeColor", Color.black);
                    material.SetColor("_EmissionColor", colors[i]);
                    material.EnableKeyword("_MTOON_EMISSIVEMAP");
                    material.SetFloat("_M_CullMode", 0);
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
                    obj.transform.SetParent(root.transform, false);
                    obj.GetComponent<Renderer>().sharedMaterial = material;
                    objects.Add(obj);
                }
                // Both red/blue writers lie behind the green reader. Yellow has no
                // relationship with either and must not be cut by their coverage.
                objects[0].transform.localPosition = new Vector3(-0.7f, 0.5f, 1);
                objects[1].transform.localPosition = new Vector3(0.7f, -0.5f, 1);
                objects[2].transform.localScale = new Vector3(3.5f, 3.5f, 1);
                objects[3].transform.localPosition = new Vector3(1.4f, 1.4f, -0.2f);
                var json = "{\"materials\":[" + string.Join(",", materials.Select(m =>
                    "{\"name\":\"" + m.name + "\",\"extensions\":{\"VRMC_materials_mtoon\":{\"specVersion\":\"1.0\"}}}"))
                    + "],\"extensions\":{\"VRMXT_materials_mtoonxt\":{\"specVersion\":\"1.0\",\"stencil\":["
                    + "{\"writers\":[0],\"readers\":[2]},"
                    + "{\"writers\":[1],\"readers\":[2,3]}]}}}";
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, json);
                var graph = root.GetComponent<UniVRMXT.Mtoonxt.VrmxtStencilGraphRenderer>();
                if (graph == null || graph.ReaderCount != 2) throw new Exception("Shared graph not selected");
                Action<string, float, float, Color> check = (label, x, y, expected) =>
                {
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                    var p = camera.WorldToViewportPoint(new Vector3(x, y, 0));
                    var actual = pixels.GetPixel((int)(p.x * 128), (int)(p.y * 128));
                    if (Mathf.Abs(actual.r - expected.r) > 0.15f
                        || Mathf.Abs(actual.g - expected.g) > 0.15f
                        || Mathf.Abs(actual.b - expected.b) > 0.15f)
                        throw new Exception(label + " expected " + expected + " got " + actual);
                    result.Add(label);
                };
                check("First writer survives shared reader", -0.7f, 0.5f, Color.red);
                check("Second writer survives shared reader", 0.7f, -0.5f, Color.blue);
                check("Reader remains outside writer coverage", 0, 1.3f, Color.green);
                // Move unrelated yellow in front of red: a non-edge must not become
                // an edge merely because yellow shares a DIFFERENT writer with green.
                objects[3].transform.localPosition = new Vector3(-0.7f, 0.5f, -0.2f);
                check("No transitive writer-reader edge", -0.7f, 0.5f, Color.yellow);
                objects[3].SetActive(false);
                // Move writer; cached commands must continue using live transforms.
                objects[0].transform.localPosition = new Vector3(-0.7f, -0.5f, 1);
                check("Old mask does not persist after movement", -0.7f, 0.5f, Color.green);
                check("Moving writer updates mask", -0.7f, -0.5f, Color.red);
                root.SetActive(false); root.SetActive(true);
                check("Re-enable rebuilds graph coverage", 0.7f, -0.5f, Color.blue);
                // Alpha-test changes must affect coverage, not only the color pass.
                materials[0].EnableKeyword("_ALPHATEST_ON");
                materials[0].SetFloat("_Cutoff", 0.5f);
                materials[0].SetColor("_Color", new Color(0, 0, 0, 0));
                check("Cutout writer does not cut reader", -0.7f, -0.5f, Color.green);
                materials[0].DisableKeyword("_ALPHATEST_ON");
                materials[0].SetColor("_Color", Color.black);
                // A second instance sharing the SAME material must not become a writer
                // for this instance's readers, even when projected over them.
                var other = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(other, scene);
                other.transform.position = new Vector3(0, 1.3f, 1);
                other.GetComponent<Renderer>().sharedMaterial = materials[0];
                check("Shared material on another avatar is not a graph edge", 0, 1.3f, Color.green);
                var stock = new Material(Shader.Find("VRM10/MToon10"));
                allocated.Add(stock);
                stock.SetColor("_Color", Color.black);
                stock.SetColor("_ShadeColor", Color.black);
                stock.SetColor("_EmissionColor", Color.yellow);
                stock.EnableKeyword("_MTOON_EMISSIVEMAP");
                stock.SetFloat("_M_CullMode", 0);
                other.GetComponent<Renderer>().sharedMaterial = stock;
                other.transform.position = new Vector3(-0.7f, -0.5f, -0.2f);
                check("Unrelated stock MToon in front occludes graph writer", -0.7f, -0.5f, Color.yellow);
                other.transform.position = new Vector3(-0.7f, -0.5f, 0.2f);
                check("Stock occluder behind reader prevents false mask hole", -0.7f, -0.5f, Color.green);
                UnityEngine.Object.DestroyImmediate(other);
                // A -> B and B -> C. B must retain its incoming mask while writing C.
                objects[0].transform.localPosition = new Vector3(-0.7f, 0.5f, 2);
                objects[1].transform.localPosition = new Vector3(0, 0, 1);
                objects[1].transform.localScale = new Vector3(3, 3, 1);
                var dualJson = json.Replace("{\"writers\":[0],\"readers\":[2]}",
                    "{\"writers\":[0],\"readers\":[1,2]}");
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, dualJson);
                check("Dual-role material reads A while writing C", -0.7f, 0.5f, Color.red);
                check("Dual-role material remains visible elsewhere", 0.7f, -0.5f, Color.blue);
                var reciprocal = dualJson.Replace("{\"writers\":[1],\"readers\":[2,3]}",
                    "{\"writers\":[1],\"readers\":[0,2,3]}");
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, reciprocal);
                check("Reciprocal edges preserve nearer blue surface", -0.7f, 0.5f, Color.blue);
                objects[0].transform.localPosition = new Vector3(-0.7f, 0.5f, 0.5f);
                check("Reciprocal edges preserve nearer red surface", -0.7f, 0.5f, Color.red);
                objects[0].transform.localPosition = new Vector3(-0.7f, 0.5f, 2);
                var downstreamCycle = dualJson.Replace("\"stencil\":[",
                    "\"stencil\":[{\"writers\":[2],\"readers\":[1]},");
                objects[2].transform.localPosition = new Vector3(0, 0, 3);
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, downstreamCycle);
                check("Upstream writer respects front surface in cyclic peer stage", -0.7f, 0.5f, Color.blue);
                objects[2].transform.localPosition = Vector3.zero;
                // Switching back to the existing compound path must release all graph
                // overrides. Exercise native lit layering and actual alpha compositing.
                objects[1].SetActive(false);
                var compound = json.Substring(0, json.IndexOf("\"stencil\":[", StringComparison.Ordinal))
                    + "\"stencil\":[{\"writers\":[0],\"readers\":[2],\"showWritersThroughOccluders\":true}]}}}";
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, compound);
                check("M02 native overlay still shows through reader", -0.7f, 0.5f, Color.red);
                materials[0].EnableKeyword("_ALPHABLEND_ON");
                materials[0].SetFloat("_AlphaMode", 2);
                materials[0].SetColor("_Color", new Color(0, 0, 0, 0.5f));
                materials[0].SetFloat("_M_SrcBlend", 5);
                materials[0].SetFloat("_M_DstBlend", 10);
                compound = compound.Replace("\"showWritersThroughOccluders\":true",
                    "\"showWritersThroughOccluders\":true,\"writersSelfOcclude\":false,\"writersWriteDepth\":false");
                UniVRMXT.Mtoonxt.VrmxtMaterialsMtoonxtApplier.Apply(root, compound);
                check("M07 native overlay retains transparent reader background", -0.7f, 0.5f, new Color(0.5f, 0.5f, 0));
                return result.ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                foreach (var value in allocated) UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
