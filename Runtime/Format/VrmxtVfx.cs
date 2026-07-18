using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UniVRMXT.Format
{
    public static class VrmxtVfx
    {
        public const string ExtensionName = "VRMXT_vfx";
        public const string SpecVersionValue = "1.0";

        public const float DefaultEmissionRate = 10f;
        public const int DefaultMaxParticles = 64;
        public const float DefaultLifetime = 1f;
        public const float DefaultStartSize = 0.05f;
        public const float DefaultStartSpeed = 0.1f;

        public static readonly float[] DefaultStartColor = { 1f, 1f, 1f, 1f };
        public static readonly float[] DefaultLocalPosition = { 0f, 0f, 0f };
        public static readonly float[] DefaultLocalRotation = { 0f, 0f, 0f, 1f };

        public static bool TryParse(string json, out VrmxtVfxExtension result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var root = JToken.Parse(json);
                return TryParse(root, out result);
            }
            catch (JsonReaderException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParse(JToken root, out VrmxtVfxExtension result)
        {
            result = null;

            if (!TryGetExtensionObject(root, out var extension))
            {
                return false;
            }

            if (!TryReadSpecVersion(extension, out _))
            {
                return false;
            }

            if (!TryGetProperty(extension, "emitters", out var emittersToken) ||
                emittersToken.Type != JTokenType.Array)
            {
                return false;
            }

            var emitters = new List<VrmxtVfxEmitter>();
            foreach (var emitterToken in (JArray)emittersToken)
            {
                if (TryParseEmitter(emitterToken, out var emitter))
                {
                    emitters.Add(emitter);
                }
            }

            result = new VrmxtVfxExtension(emitters);
            return true;
        }

        private static bool TryGetExtensionObject(JToken root, out JObject extension)
        {
            extension = null;
            if (root is not JObject rootObject)
            {
                return false;
            }

            if (TryGetProperty(rootObject, ExtensionName, out var direct) &&
                direct is JObject directObject)
            {
                extension = directObject;
                return true;
            }

            if (TryGetProperty(rootObject, "extensions", out var extensionsToken) &&
                extensionsToken is JObject extensions &&
                TryGetProperty(extensions, ExtensionName, out var nested) &&
                nested is JObject nestedObject)
            {
                extension = nestedObject;
                return true;
            }

            // Bare extension object (already extracted from glTF extensions map).
            if (TryGetProperty(rootObject, "specVersion", out _))
            {
                extension = rootObject;
                return true;
            }

            return false;
        }

        private static bool TryReadSpecVersion(JObject extension, out string specVersion)
        {
            specVersion = null;
            if (!TryGetProperty(extension, "specVersion", out var versionToken) ||
                versionToken.Type != JTokenType.String)
            {
                return false;
            }

            specVersion = versionToken.Value<string>();
            return string.Equals(specVersion, SpecVersionValue, StringComparison.Ordinal);
        }

        private static bool TryParseEmitter(JToken emitterToken, out VrmxtVfxEmitter emitter)
        {
            emitter = null;

            if (emitterToken is not JObject emitterObject)
            {
                return false;
            }

            if (!TryGetProperty(emitterObject, "type", out var typeToken) ||
                typeToken.Type != JTokenType.String)
            {
                return false;
            }

            var type = typeToken.Value<string>();
            if (!string.Equals(type, "particle", StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetProperty(emitterObject, "node", out var nodeToken) ||
                !TryGetInt32(nodeToken, out var node) ||
                node < 0)
            {
                return false;
            }

            if (!TryReadFloatArray(emitterObject, "localPosition", 3, DefaultLocalPosition, out var localPosition))
            {
                return false;
            }

            if (!TryReadFloatArray(emitterObject, "localRotation", 4, DefaultLocalRotation, out var localRotation))
            {
                return false;
            }

            if (!IsValidQuaternion(localRotation))
            {
                return false;
            }

            if (!TryGetProperty(emitterObject, "particle", out var particleToken) ||
                particleToken is not JObject particleObject)
            {
                return false;
            }

            if (!TryParseParticle(particleObject, out var particle))
            {
                return false;
            }

            string name = null;
            if (TryGetProperty(emitterObject, "name", out var nameToken) &&
                nameToken.Type == JTokenType.String)
            {
                name = nameToken.Value<string>();
            }

            emitter = new VrmxtVfxEmitter(name, type, node, localPosition, localRotation, particle);
            return true;
        }

        private static bool TryParseParticle(JObject particleObject, out VrmxtVfxParticle particle)
        {
            particle = null;

            int? texture = null;
            if (TryGetProperty(particleObject, "texture", out var textureToken))
            {
                if (!TryGetInt32(textureToken, out var textureIndex) || textureIndex < 0)
                {
                    return false;
                }

                texture = textureIndex;
            }

            if (!TryReadNonNegativeFloat(particleObject, "emissionRate", DefaultEmissionRate, out var emissionRate))
            {
                return false;
            }

            if (!TryReadPositiveInt(particleObject, "maxParticles", DefaultMaxParticles, out var maxParticles))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleObject, "lifetime", DefaultLifetime, out var lifetime))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleObject, "startSize", DefaultStartSize, out var startSize))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleObject, "startSpeed", DefaultStartSpeed, out var startSpeed))
            {
                return false;
            }

            if (!TryReadFloatArray(particleObject, "startColor", 4, DefaultStartColor, out var startColor))
            {
                return false;
            }

            particle = new VrmxtVfxParticle(
                texture,
                emissionRate,
                maxParticles,
                lifetime,
                startSize,
                startSpeed,
                startColor);
            return true;
        }

        private static bool TryReadFloatArray(
            JObject parent,
            string propertyName,
            int length,
            float[] defaults,
            out float[] values)
        {
            values = (float[])defaults.Clone();

            if (!TryGetProperty(parent, propertyName, out var token))
            {
                return true;
            }

            if (token.Type != JTokenType.Array)
            {
                return false;
            }

            var items = new List<float>();
            foreach (var item in (JArray)token)
            {
                if (!TryGetDouble(item, out var number) || !IsFinite(number))
                {
                    return false;
                }

                items.Add((float)number);
            }

            if (items.Count != length)
            {
                return false;
            }

            values = items.ToArray();
            return true;
        }

        private static bool TryReadNonNegativeFloat(
            JObject parent,
            string propertyName,
            float defaultValue,
            out float value)
        {
            value = defaultValue;
            if (!TryGetProperty(parent, propertyName, out var token))
            {
                return true;
            }

            if (!TryGetDouble(token, out var number) || !IsFinite(number) || number < 0d)
            {
                return false;
            }

            value = (float)number;
            return true;
        }

        private static bool TryReadPositiveInt(
            JObject parent,
            string propertyName,
            int defaultValue,
            out int value)
        {
            value = defaultValue;
            if (!TryGetProperty(parent, propertyName, out var token))
            {
                return true;
            }

            if (!TryGetInt32(token, out var number) || number < 1)
            {
                return false;
            }

            value = number;
            return true;
        }

        private static bool TryGetProperty(JObject parent, string propertyName, out JToken token)
        {
            return parent.TryGetValue(propertyName, StringComparison.Ordinal, out token);
        }

        private static bool TryGetInt32(JToken token, out int value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return false;
            }

            var number = token.Value<double>();
            if (!IsFinite(number) || number != Math.Truncate(number) ||
                number < int.MinValue || number > int.MaxValue)
            {
                return false;
            }

            value = (int)number;
            return true;
        }

        private static bool TryGetDouble(JToken token, out double value)
        {
            value = 0d;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return false;
            }

            value = token.Value<double>();
            return true;
        }

        private static bool IsValidQuaternion(float[] quaternion)
        {
            var magnitudeSquared = 0f;
            for (var i = 0; i < quaternion.Length; i++)
            {
                magnitudeSquared += quaternion[i] * quaternion[i];
            }

            return magnitudeSquared > 0f;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class VrmxtVfxExtension
    {
        public VrmxtVfxExtension(IReadOnlyList<VrmxtVfxEmitter> emitters)
        {
            Emitters = emitters ?? Array.Empty<VrmxtVfxEmitter>();
        }

        public IReadOnlyList<VrmxtVfxEmitter> Emitters { get; }
    }

    public sealed class VrmxtVfxEmitter
    {
        public VrmxtVfxEmitter(
            string name,
            string type,
            int node,
            IReadOnlyList<float> localPosition,
            IReadOnlyList<float> localRotation,
            VrmxtVfxParticle particle)
        {
            Name = name;
            Type = type;
            Node = node;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Particle = particle;
        }

        public string Name { get; }
        public string Type { get; }
        public int Node { get; }
        public IReadOnlyList<float> LocalPosition { get; }
        public IReadOnlyList<float> LocalRotation { get; }
        public VrmxtVfxParticle Particle { get; }
    }

    public sealed class VrmxtVfxParticle
    {
        public VrmxtVfxParticle(
            int? texture,
            float emissionRate,
            int maxParticles,
            float lifetime,
            float startSize,
            float startSpeed,
            IReadOnlyList<float> startColor)
        {
            Texture = texture;
            EmissionRate = emissionRate;
            MaxParticles = maxParticles;
            Lifetime = lifetime;
            StartSize = startSize;
            StartSpeed = startSpeed;
            StartColor = startColor;
        }

        public int? Texture { get; }
        public float EmissionRate { get; }
        public int MaxParticles { get; }
        public float Lifetime { get; }
        public float StartSize { get; }
        public float StartSpeed { get; }
        public IReadOnlyList<float> StartColor { get; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "particle(rate={0}, max={1})",
                EmissionRate,
                MaxParticles);
        }
    }
}
