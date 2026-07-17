using System;
using System.Collections.Generic;
using UniVRMXT.Format;
using UnityEngine;

namespace UniVRMXT.Vfx
{
    public static class VrmxtVfxImporter
    {
        public static bool TryImport(
            string json,
            Func<int, string> resolveNode,
            out VrmxtVfxData data)
        {
            data = null;
            if (!VrmxtVfx.TryParse(json, out var extension))
            {
                return false;
            }

            var emitters = new List<VrmxtVfxEmitterData>();
            foreach (var emitter in extension.Emitters)
            {
                if (!IsNodeResolved(emitter.Node, resolveNode))
                {
                    continue;
                }

                emitters.Add(ToEmitterData(emitter));
            }

            data = ScriptableObject.CreateInstance<VrmxtVfxData>();
            data.SetEmitters(emitters);
            return true;
        }

        private static bool IsNodeResolved(int nodeIndex, Func<int, string> resolveNode)
        {
            if (resolveNode == null)
            {
                return false;
            }

            var nodeName = resolveNode(nodeIndex);
            return !string.IsNullOrEmpty(nodeName);
        }

        private static VrmxtVfxEmitterData ToEmitterData(VrmxtVfxEmitter emitter)
        {
            var particle = emitter.Particle;
            var startColor = particle.StartColor;

            return new VrmxtVfxEmitterData
            {
                Name = emitter.Name,
                Type = emitter.Type,
                Node = emitter.Node,
                LocalPosition = ToVector3(emitter.LocalPosition),
                LocalRotation = ToQuaternion(emitter.LocalRotation),
                Particle = new VrmxtVfxParticleData
                {
                    HasTexture = particle.Texture.HasValue,
                    TextureIndex = particle.Texture ?? -1,
                    EmissionRate = particle.EmissionRate,
                    MaxParticles = particle.MaxParticles,
                    Lifetime = particle.Lifetime,
                    StartSize = particle.StartSize,
                    StartSpeed = particle.StartSpeed,
                    StartColor = new Color(
                        startColor[0],
                        startColor[1],
                        startColor[2],
                        startColor[3]),
                },
            };
        }

        private static Vector3 ToVector3(IReadOnlyList<float> values)
        {
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ToQuaternion(IReadOnlyList<float> values)
        {
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }
    }
}
