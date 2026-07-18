using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Runtime holder for portable <c>VRMXT_vfx</c> emitters on a loaded avatar root.
    /// Does not spawn Unity <see cref="ParticleSystem"/> components (data MVP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VrmxtVfxInstance : MonoBehaviour
    {
        [SerializeField]
        private List<VrmxtVfxResolvedEmitter> emitters = new();

        public IReadOnlyList<VrmxtVfxResolvedEmitter> Emitters => emitters;

        public void SetEmitters(IEnumerable<VrmxtVfxResolvedEmitter> values)
        {
            emitters.Clear();
            if (values == null)
            {
                return;
            }

            emitters.AddRange(values);
        }
    }

    [Serializable]
    public sealed class VrmxtVfxResolvedEmitter
    {
        public string Name;
        public string Type = "particle";
        public int Node;
        public Transform NodeTransform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public VrmxtVfxParticleData Particle = new();
    }
}
