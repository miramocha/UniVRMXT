using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Attach parsed <c>VRMXT_vfx</c> data to a loaded avatar without referencing UniVRM types.
    /// Call after load with glTF JSON and <c>RuntimeGltfInstance.Nodes</c> (or equivalent).
    /// </summary>
    public static class VrmxtVfxRuntime
    {
        public static bool TryAttach(
            GameObject root,
            string gltfJson,
            IReadOnlyList<Transform> nodes,
            out VrmxtVfxInstance instance)
        {
            instance = null;
            if (root == null)
            {
                return false;
            }

            if (!VrmxtVfxImporter.TryImport(gltfJson, nodes, out var resolved))
            {
                return false;
            }

            instance = root.GetComponent<VrmxtVfxInstance>();
            if (instance == null)
            {
                instance = root.AddComponent<VrmxtVfxInstance>();
            }

            instance.SetEmitters(resolved);
            return true;
        }

        public static bool TryAttach(
            GameObject root,
            string gltfJson,
            Func<int, Transform> resolveNode,
            out VrmxtVfxInstance instance)
        {
            instance = null;
            if (root == null)
            {
                return false;
            }

            if (!VrmxtVfxImporter.TryImport(gltfJson, resolveNode, out var resolved))
            {
                return false;
            }

            instance = root.GetComponent<VrmxtVfxInstance>();
            if (instance == null)
            {
                instance = root.AddComponent<VrmxtVfxInstance>();
            }

            instance.SetEmitters(resolved);
            return true;
        }
    }
}
