using System;
using System.Collections.Generic;
using System.Text.Json;

namespace UniVRMXT.Format
{
    public static class VrmxtMaterialsOverride
    {
        public const string ExtensionName = "VRMXT_materials_override";
        public const string SpecVersionValue = "1.0";

        public const string EngineUnity = "unity";
        public const string EngineUnreal = "unreal";

        public const string UnityMaterialKindShader = "shader";
        public const string UnrealMaterialKindMaterialSet = "materialSet";

        public static bool TryParse(string json, out VrmxtMaterialsOverrideExtension result)
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

        public static bool TryParse(JsonElement root, out VrmxtMaterialsOverrideExtension result)
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

            if (!extension.TryGetProperty("overrides", out var overridesElement) ||
                overridesElement.ValueKind != JsonValueKind.Array ||
                overridesElement.GetArrayLength() == 0)
            {
                return false;
            }

            var overrides = new List<VrmxtMaterialEngineOverride>();
            var engines = new HashSet<string>(StringComparer.Ordinal);

            foreach (var overrideElement in overridesElement.EnumerateArray())
            {
                if (!TryParseOverride(overrideElement, out var engineOverride))
                {
                    return false;
                }

                if (!engines.Add(engineOverride.Engine))
                {
                    return false;
                }

                overrides.Add(engineOverride);
            }

            result = new VrmxtMaterialsOverrideExtension(overrides);
            return true;
        }

        public static bool TryGetUnityOverride(
            VrmxtMaterialsOverrideExtension extension,
            out UnityMaterialOverride unityOverride)
        {
            unityOverride = null;
            if (extension == null)
            {
                return false;
            }

            foreach (var entry in extension.Overrides)
            {
                if (!string.Equals(entry.Engine, EngineUnity, StringComparison.Ordinal))
                {
                    continue;
                }

                if (entry.Material is UnityMaterialOverride unity)
                {
                    unityOverride = unity;
                    return true;
                }
            }

            return false;
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

        private static bool TryParseOverride(JsonElement overrideElement, out VrmxtMaterialEngineOverride engineOverride)
        {
            engineOverride = null;

            if (overrideElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!overrideElement.TryGetProperty("engine", out var engineElement) ||
                engineElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var engine = engineElement.GetString();
            if (string.IsNullOrEmpty(engine))
            {
                return false;
            }

            if (!overrideElement.TryGetProperty("material", out var materialElement) ||
                materialElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryParseMaterial(engine, materialElement, out var material))
            {
                return false;
            }

            var bindings = new List<VrmxtMaterialBinding>();
            if (overrideElement.TryGetProperty("bindings", out var bindingsElement))
            {
                if (bindingsElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var bindingElement in bindingsElement.EnumerateArray())
                {
                    if (!TryParseBinding(bindingElement, out var binding))
                    {
                        return false;
                    }

                    bindings.Add(binding);
                }
            }

            engineOverride = new VrmxtMaterialEngineOverride(engine, material, bindings);
            return true;
        }

        private static bool TryParseMaterial(string engine, JsonElement materialElement, out IVrmxtMaterialDefinition material)
        {
            material = null;

            if (!materialElement.TryGetProperty("kind", out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var kind = kindElement.GetString();
            MaterialProvider provider = null;
            if (materialElement.TryGetProperty("provider", out var providerElement))
            {
                if (!TryParseProvider(providerElement, out provider))
                {
                    return false;
                }
            }

            if (string.Equals(engine, EngineUnity, StringComparison.Ordinal))
            {
                if (!string.Equals(kind, UnityMaterialKindShader, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!materialElement.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var shaderName = nameElement.GetString();
                if (string.IsNullOrEmpty(shaderName))
                {
                    return false;
                }

                string variant = null;
                if (materialElement.TryGetProperty("variant", out var variantElement) &&
                    variantElement.ValueKind == JsonValueKind.String)
                {
                    variant = variantElement.GetString();
                }

                material = new UnityMaterialOverride(kind, shaderName, variant, provider);
                return true;
            }

            if (string.Equals(engine, EngineUnreal, StringComparison.Ordinal))
            {
                if (!string.Equals(kind, UnrealMaterialKindMaterialSet, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!materialElement.TryGetProperty("variants", out var variantsElement) ||
                    variantsElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var variants = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in variantsElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    var path = property.Value.GetString();
                    if (string.IsNullOrEmpty(path))
                    {
                        return false;
                    }

                    variants[property.Name] = path;
                }

                if (variants.Count == 0)
                {
                    return false;
                }

                material = new UnrealMaterialOverride(kind, variants, provider);
                return true;
            }

            material = new UnknownMaterialOverride(kind, provider);
            return true;
        }

        private static bool TryParseProvider(JsonElement providerElement, out MaterialProvider provider)
        {
            provider = null;
            if (providerElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!providerElement.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            string version = null;
            if (providerElement.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.String)
            {
                version = versionElement.GetString();
            }

            provider = new MaterialProvider(id, version);
            return true;
        }

        private static bool TryParseBinding(JsonElement bindingElement, out VrmxtMaterialBinding binding)
        {
            binding = null;
            if (bindingElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!bindingElement.TryGetProperty("source", out var sourceElement) ||
                sourceElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!bindingElement.TryGetProperty("target", out var targetElement) ||
                targetElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!bindingElement.TryGetProperty("targetType", out var targetTypeElement) ||
                targetTypeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var source = sourceElement.GetString();
            var target = targetElement.GetString();
            var targetType = targetTypeElement.GetString();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(targetType))
            {
                return false;
            }

            if (!IsKnownTargetType(targetType))
            {
                return false;
            }

            binding = new VrmxtMaterialBinding(source, target, targetType);
            return true;
        }

        private static bool IsKnownTargetType(string targetType)
        {
            return string.Equals(targetType, "scalar", StringComparison.Ordinal) ||
                   string.Equals(targetType, "vector", StringComparison.Ordinal) ||
                   string.Equals(targetType, "texture", StringComparison.Ordinal) ||
                   string.Equals(targetType, "staticSwitch", StringComparison.Ordinal);
        }
    }

    public sealed class VrmxtMaterialsOverrideExtension
    {
        public VrmxtMaterialsOverrideExtension(IReadOnlyList<VrmxtMaterialEngineOverride> overrides)
        {
            Overrides = overrides ?? Array.Empty<VrmxtMaterialEngineOverride>();
        }

        public IReadOnlyList<VrmxtMaterialEngineOverride> Overrides { get; }
    }

    public sealed class VrmxtMaterialEngineOverride
    {
        public VrmxtMaterialEngineOverride(
            string engine,
            IVrmxtMaterialDefinition material,
            IReadOnlyList<VrmxtMaterialBinding> bindings)
        {
            Engine = engine;
            Material = material;
            Bindings = bindings ?? Array.Empty<VrmxtMaterialBinding>();
        }

        public string Engine { get; }
        public IVrmxtMaterialDefinition Material { get; }
        public IReadOnlyList<VrmxtMaterialBinding> Bindings { get; }
    }

    public interface IVrmxtMaterialDefinition
    {
        string Kind { get; }
        MaterialProvider Provider { get; }
    }

    public sealed class UnityMaterialOverride : IVrmxtMaterialDefinition
    {
        public UnityMaterialOverride(string kind, string shaderName, string variant, MaterialProvider provider)
        {
            Kind = kind;
            ShaderName = shaderName;
            Variant = variant;
            Provider = provider;
        }

        public string Kind { get; }
        public string ShaderName { get; }
        public string Variant { get; }
        public MaterialProvider Provider { get; }
    }

    public sealed class UnrealMaterialOverride : IVrmxtMaterialDefinition
    {
        public UnrealMaterialOverride(
            string kind,
            IReadOnlyDictionary<string, string> variants,
            MaterialProvider provider)
        {
            Kind = kind;
            Variants = variants;
            Provider = provider;
        }

        public string Kind { get; }
        public IReadOnlyDictionary<string, string> Variants { get; }
        public MaterialProvider Provider { get; }
    }

    public sealed class UnknownMaterialOverride : IVrmxtMaterialDefinition
    {
        public UnknownMaterialOverride(string kind, MaterialProvider provider)
        {
            Kind = kind;
            Provider = provider;
        }

        public string Kind { get; }
        public MaterialProvider Provider { get; }
    }

    public sealed class MaterialProvider
    {
        public MaterialProvider(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }
        public string Version { get; }
    }

    public sealed class VrmxtMaterialBinding
    {
        public VrmxtMaterialBinding(string source, string target, string targetType)
        {
            Source = source;
            Target = target;
            TargetType = targetType;
        }

        public string Source { get; }
        public string Target { get; }
        public string TargetType { get; }
    }
}
