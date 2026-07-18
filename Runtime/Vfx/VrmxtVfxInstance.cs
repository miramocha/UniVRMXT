using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRMXT.Vfx
{
    /// <summary>
    /// Runtime holder for portable <c>VRMXT_vfx</c> emitters on a loaded avatar root.
    /// Optional <see cref="BuildParticleSystems"/> maps fields onto Unity
    /// <see cref="ParticleSystem"/> children under each resolved node.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VrmxtVfxInstance : MonoBehaviour
    {
        [SerializeField]
        private List<VrmxtVfxResolvedEmitter> emitters = new();

        [SerializeField]
        private List<ParticleSystem> particleSystems = new();

        public IReadOnlyList<VrmxtVfxResolvedEmitter> Emitters => emitters;

        public IReadOnlyList<ParticleSystem> ParticleSystems => particleSystems;

        public void SetEmitters(IEnumerable<VrmxtVfxResolvedEmitter> values)
        {
            ClearParticleSystems();
            emitters.Clear();
            if (values == null)
            {
                return;
            }

            emitters.AddRange(values);
        }

        /// <summary>
        /// Spawn <see cref="ParticleSystem"/> children for current emitters.
        /// <paramref name="resolveTexture"/> maps glTF <c>textures[]</c> indices; null or
        /// unresolved indices use the solid tint fallback (<see cref="VrmxtVfxParticleData.StartColor"/>).
        /// </summary>
        public void BuildParticleSystems(Func<int, Texture> resolveTexture = null)
        {
            ClearParticleSystems();
            particleSystems.AddRange(
                VrmxtVfxParticleSystemMapper.CreateAll(emitters, resolveTexture));
        }

        public void ClearParticleSystems()
        {
            for (var i = 0; i < particleSystems.Count; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                DestroyOwned(particleSystem.gameObject);
            }

            particleSystems.Clear();
        }

        private void OnDestroy()
        {
            ClearParticleSystems();
        }

        private static void DestroyOwned(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
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
