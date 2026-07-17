using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

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
                using var document = JsonDocument.Parse(json);
                return TryParse(document.RootElement, out result);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParse(JsonElement root, out VrmxtVfxExtension result)
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

            if (!extension.TryGetProperty("emitters", out var emittersElement) ||
                emittersElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var emitters = new List<VrmxtVfxEmitter>();
            foreach (var emitterElement in emittersElement.EnumerateArray())
            {
                if (TryParseEmitter(emitterElement, out var emitter))
                {
                    emitters.Add(emitter);
                }
            }

            result = new VrmxtVfxExtension(emitters);
            return true;
        }

        private static bool TryGetExtensionObject(JsonElement root, out JsonElement extension)
        {
            extension = default;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(ExtensionName, out extension))
            {
                return extension.ValueKind == JsonValueKind.Object;
            }

            if (root.TryGetProperty("extensions", out var extensions) &&
                extensions.ValueKind == JsonValueKind.Object &&
                extensions.TryGetProperty(ExtensionName, out extension))
            {
                return extension.ValueKind == JsonValueKind.Object;
            }

            return false;
        }

        private static bool TryReadSpecVersion(JsonElement extension, out string specVersion)
        {
            specVersion = null;
            if (!extension.TryGetProperty("specVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            specVersion = versionElement.GetString();
            return string.Equals(specVersion, SpecVersionValue, StringComparison.Ordinal);
        }

        private static bool TryParseEmitter(JsonElement emitterElement, out VrmxtVfxEmitter emitter)
        {
            emitter = null;

            if (emitterElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!emitterElement.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var type = typeElement.GetString();
            if (!string.Equals(type, "particle", StringComparison.Ordinal))
            {
                return false;
            }

            if (!emitterElement.TryGetProperty("node", out var nodeElement) ||
                nodeElement.ValueKind != JsonValueKind.Number ||
                !nodeElement.TryGetInt32(out var node) ||
                node < 0)
            {
                return false;
            }

            if (!TryReadFloatArray(emitterElement, "localPosition", 3, DefaultLocalPosition, out var localPosition))
            {
                return false;
            }

            if (!TryReadFloatArray(emitterElement, "localRotation", 4, DefaultLocalRotation, out var localRotation))
            {
                return false;
            }

            if (!IsValidQuaternion(localRotation))
            {
                return false;
            }

            if (!emitterElement.TryGetProperty("particle", out var particleElement) ||
                particleElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryParseParticle(particleElement, out var particle))
            {
                return false;
            }

            string name = null;
            if (emitterElement.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                name = nameElement.GetString();
            }

            emitter = new VrmxtVfxEmitter(name, type, node, localPosition, localRotation, particle);
            return true;
        }

        private static bool TryParseParticle(JsonElement particleElement, out VrmxtVfxParticle particle)
        {
            particle = null;

            int? texture = null;
            if (particleElement.TryGetProperty("texture", out var textureElement))
            {
                if (textureElement.ValueKind != JsonValueKind.Number ||
                    !textureElement.TryGetInt32(out var textureIndex) ||
                    textureIndex < 0)
                {
                    return false;
                }

                texture = textureIndex;
            }

            if (!TryReadNonNegativeFloat(particleElement, "emissionRate", DefaultEmissionRate, out var emissionRate))
            {
                return false;
            }

            if (!TryReadPositiveInt(particleElement, "maxParticles", DefaultMaxParticles, out var maxParticles))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleElement, "lifetime", DefaultLifetime, out var lifetime))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleElement, "startSize", DefaultStartSize, out var startSize))
            {
                return false;
            }

            if (!TryReadNonNegativeFloat(particleElement, "startSpeed", DefaultStartSpeed, out var startSpeed))
            {
                return false;
            }

            if (!TryReadFloatArray(particleElement, "startColor", 4, DefaultStartColor, out var startColor))
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
            JsonElement parent,
            string propertyName,
            int length,
            float[] defaults,
            out float[] values)
        {
            values = (float[])defaults.Clone();

            if (!parent.TryGetProperty(propertyName, out var element))
            {
                return true;
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var items = new List<float>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number))
                {
                    return false;
                }

                if (!IsFinite(number))
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
            JsonElement parent,
            string propertyName,
            float defaultValue,
            out float value)
        {
            value = defaultValue;
            if (!parent.TryGetProperty(propertyName, out var element))
            {
                return true;
            }

            if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var number))
            {
                return false;
            }

            if (!IsFinite(number) || number < 0d)
            {
                return false;
            }

            value = (float)number;
            return true;
        }

        private static bool TryReadPositiveInt(
            JsonElement parent,
            string propertyName,
            int defaultValue,
            out int value)
        {
            value = defaultValue;
            if (!parent.TryGetProperty(propertyName, out var element))
            {
                return true;
            }

            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var number))
            {
                return false;
            }

            if (number < 1)
            {
                return false;
            }

            value = number;
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
