using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public static bool TryParse(JToken root, out VrmxtMaterialsOverrideExtension result)
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

            if (!TryGetProperty(extension, "overrides", out var overridesToken) ||
                overridesToken.Type != JTokenType.Array ||
                !((JArray)overridesToken).HasValues)
            {
                return false;
            }

            var overrides = new List<VrmxtMaterialEngineOverride>();
            var engines = new HashSet<string>(StringComparer.Ordinal);

            foreach (var overrideToken in (JArray)overridesToken)
            {
                if (!TryParseOverride(overrideToken, out var engineOverride))
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

            // Bare extension object (already extracted from a material extensions map).
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

        private static bool TryParseOverride(JToken overrideToken, out VrmxtMaterialEngineOverride engineOverride)
        {
            engineOverride = null;

            if (overrideToken is not JObject overrideObject)
            {
                return false;
            }

            if (!TryGetProperty(overrideObject, "engine", out var engineToken) ||
                engineToken.Type != JTokenType.String)
            {
                return false;
            }

            var engine = engineToken.Value<string>();
            if (string.IsNullOrEmpty(engine))
            {
                return false;
            }

            if (!TryGetProperty(overrideObject, "material", out var materialToken) ||
                materialToken is not JObject materialObject)
            {
                return false;
            }

            if (!TryParseMaterial(engine, materialObject, out var material))
            {
                return false;
            }

            var bindings = new List<VrmxtMaterialBinding>();
            if (TryGetProperty(overrideObject, "bindings", out var bindingsToken))
            {
                if (bindingsToken.Type != JTokenType.Array)
                {
                    return false;
                }

                foreach (var bindingToken in (JArray)bindingsToken)
                {
                    if (!TryParseBinding(bindingToken, out var binding))
                    {
                        return false;
                    }

                    bindings.Add(binding);
                }
            }

            engineOverride = new VrmxtMaterialEngineOverride(engine, material, bindings);
            return true;
        }

        private static bool TryParseMaterial(string engine, JObject materialObject, out IVrmxtMaterialDefinition material)
        {
            material = null;

            if (!TryGetProperty(materialObject, "kind", out var kindToken) ||
                kindToken.Type != JTokenType.String)
            {
                return false;
            }

            var kind = kindToken.Value<string>();
            MaterialProvider provider = null;
            if (TryGetProperty(materialObject, "provider", out var providerToken))
            {
                if (!TryParseProvider(providerToken, out provider))
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

                if (!TryGetProperty(materialObject, "name", out var nameToken) ||
                    nameToken.Type != JTokenType.String)
                {
                    return false;
                }

                var shaderName = nameToken.Value<string>();
                if (string.IsNullOrEmpty(shaderName))
                {
                    return false;
                }

                string variant = null;
                if (TryGetProperty(materialObject, "variant", out var variantToken) &&
                    variantToken.Type == JTokenType.String)
                {
                    variant = variantToken.Value<string>();
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

                if (!TryGetProperty(materialObject, "variants", out var variantsToken) ||
                    variantsToken is not JObject variantsObject)
                {
                    return false;
                }

                var variants = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in variantsObject.Properties())
                {
                    if (property.Value.Type != JTokenType.String)
                    {
                        return false;
                    }

                    var path = property.Value.Value<string>();
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

        private static bool TryParseProvider(JToken providerToken, out MaterialProvider provider)
        {
            provider = null;
            if (providerToken is not JObject providerObject)
            {
                return false;
            }

            if (!TryGetProperty(providerObject, "id", out var idToken) ||
                idToken.Type != JTokenType.String)
            {
                return false;
            }

            var id = idToken.Value<string>();
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            string version = null;
            if (TryGetProperty(providerObject, "version", out var versionToken) &&
                versionToken.Type == JTokenType.String)
            {
                version = versionToken.Value<string>();
            }

            provider = new MaterialProvider(id, version);
            return true;
        }

        private static bool TryParseBinding(JToken bindingToken, out VrmxtMaterialBinding binding)
        {
            binding = null;
            if (bindingToken is not JObject bindingObject)
            {
                return false;
            }

            if (!TryGetProperty(bindingObject, "source", out var sourceToken) ||
                sourceToken.Type != JTokenType.String)
            {
                return false;
            }

            if (!TryGetProperty(bindingObject, "target", out var targetToken) ||
                targetToken.Type != JTokenType.String)
            {
                return false;
            }

            if (!TryGetProperty(bindingObject, "targetType", out var targetTypeToken) ||
                targetTypeToken.Type != JTokenType.String)
            {
                return false;
            }

            var source = sourceToken.Value<string>();
            var target = targetToken.Value<string>();
            var targetType = targetTypeToken.Value<string>();
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

        private static bool TryGetProperty(JObject parent, string propertyName, out JToken token)
        {
            return parent.TryGetValue(propertyName, StringComparison.Ordinal, out token);
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
