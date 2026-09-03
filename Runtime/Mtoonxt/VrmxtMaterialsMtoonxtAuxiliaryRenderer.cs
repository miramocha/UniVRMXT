using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniVRMXT.Mtoonxt
{
    public sealed class VrmxtMtoonxtAuxiliaryDraw
    {
        public Renderer Renderer;
        public Material Material;
        public int Submesh;
        public bool AfterOpaque;
        public int BodyPass = -1;
        public int OutlinePass = -1;
    }

    /// <summary>
    /// Retained Built-in-pipeline passes for relationship modes that cannot be
    /// represented by one material pass (M02/M07/M08 and coverage masks).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VrmxtMaterialsMtoonxtAuxiliaryRenderer : MonoBehaviour
    {
        private readonly List<VrmxtMtoonxtAuxiliaryDraw> draws =
            new List<VrmxtMtoonxtAuxiliaryDraw>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly Dictionary<Camera, CameraBuffers> cameras =
            new Dictionary<Camera, CameraBuffers>();

        public int DrawCount => draws.Count;

        public void Configure(
            IEnumerable<VrmxtMtoonxtAuxiliaryDraw> values,
            IEnumerable<Material> materials
        )
        {
            RemoveAllCameraBuffers();
            DestroyOwnedMaterials();
            draws.Clear();
            if (values != null)
            {
                draws.AddRange(values);
            }

            if (materials != null)
            {
                ownedMaterials.AddRange(materials);
            }

            enabled = draws.Count > 0;
        }

        private void OnEnable()
        {
            if (draws.Count == 0)
            {
                var store = GetComponent<VrmxtMaterialsMtoonxtInstance>();
                if (store != null && store.StencilRelationships.Count > 0)
                {
                    VrmxtMaterialsMtoonxtApplier.ReapplyRelationships(gameObject, store);
                }
            }

            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            RemoveAllCameraBuffers();
        }

        private void OnDestroy()
        {
            OnDisable();
            DestroyOwnedMaterials();
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (
                camera == null
                || draws.Count == 0
                || GraphicsSettings.currentRenderPipeline != null
                || cameras.ContainsKey(camera)
            )
            {
                return;
            }

            var before = new CommandBuffer { name = "UniVRMXT Stencil Relationship Prepass" };
            var after = new CommandBuffer { name = "UniVRMXT Stencil Relationship Overlay" };
            for (var i = 0; i < draws.Count; i++)
            {
                var draw = draws[i];
                if (draw == null || draw.Renderer == null || draw.Material == null)
                {
                    continue;
                }

                var buffer = draw.AfterOpaque ? after : before;
                if (draw.BodyPass >= 0)
                {
                    buffer.DrawRenderer(
                        draw.Renderer,
                        draw.Material,
                        draw.Submesh,
                        draw.BodyPass
                    );
                }

                if (draw.OutlinePass >= 0)
                {
                    buffer.DrawRenderer(
                        draw.Renderer,
                        draw.Material,
                        draw.Submesh,
                        draw.OutlinePass
                    );
                }
            }

            camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, before);
            camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, after);
            cameras[camera] = new CameraBuffers(before, after);
        }

        private void RemoveAllCameraBuffers()
        {
            foreach (var pair in cameras)
            {
                var camera = pair.Key;
                if (camera != null)
                {
                    camera.RemoveCommandBuffer(
                        CameraEvent.BeforeForwardOpaque,
                        pair.Value.Before
                    );
                    camera.RemoveCommandBuffer(
                        CameraEvent.AfterForwardOpaque,
                        pair.Value.After
                    );
                }

                pair.Value.Before.Release();
                pair.Value.After.Release();
            }

            cameras.Clear();
        }

        private void DestroyOwnedMaterials()
        {
            for (var i = 0; i < ownedMaterials.Count; i++)
            {
                var material = ownedMaterials[i];
                if (material != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(material);
                    }
                    else
                    {
                        DestroyImmediate(material);
                    }
                }
            }

            ownedMaterials.Clear();
        }

        private readonly struct CameraBuffers
        {
            public CameraBuffers(CommandBuffer before, CommandBuffer after)
            {
                Before = before;
                After = after;
            }

            public CommandBuffer Before { get; }
            public CommandBuffer After { get; }
        }
    }
}
