using System;
using UniVRMXT.Format;

namespace UniVRMXT.MaterialsOverride
{
    public enum RenderPipelineVariant
    {
        Builtin,
        Urp,
        Hdrp,
    }

    public static class UnityOverrideSelector
    {
        public static bool TrySelectUnityOverride(
            VrmxtMaterialsOverrideExtension extension,
            RenderPipelineVariant activePipeline,
            out UnityMaterialOverride unityOverride)
        {
            unityOverride = null;
            if (extension == null)
            {
                return false;
            }

            if (!VrmxtMaterialsOverride.TryGetUnityOverride(extension, out var candidate))
            {
                return false;
            }

            if (!string.Equals(candidate.IdType, VrmxtMaterialsOverride.UnityMaterialIdTypeShaderName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!IsVariantCompatible(candidate.Variant, activePipeline))
            {
                return false;
            }

            unityOverride = candidate;
            return true;
        }

        public static bool IsVariantCompatible(string variant, RenderPipelineVariant activePipeline)
        {
            if (string.IsNullOrEmpty(variant))
            {
                return true;
            }

            if (string.Equals(variant, "builtin", StringComparison.Ordinal))
            {
                return activePipeline == RenderPipelineVariant.Builtin;
            }

            if (string.Equals(variant, "urp", StringComparison.Ordinal))
            {
                return activePipeline == RenderPipelineVariant.Urp;
            }

            if (string.Equals(variant, "hdrp", StringComparison.Ordinal))
            {
                return activePipeline == RenderPipelineVariant.Hdrp;
            }

            return false;
        }
    }
}
