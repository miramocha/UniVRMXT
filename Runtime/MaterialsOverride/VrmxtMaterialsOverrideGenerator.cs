using System.Collections.Generic;
using UniVRMXT.Format;

namespace UniVRMXT.MaterialsOverride
{
    /// <summary>
    /// In-repo mirror of the UniGLTF material descriptor flow. When UniVRM packages are
    /// present, wrap <c>IMaterialDescriptorGenerator</c> and delegate on failure.
    /// </summary>
    public interface IVrmxtMaterialDescriptorGenerator
    {
        bool TryBuildOverrideDescriptor(
            int materialIndex,
            VrmxtMaterialsOverrideExtension materialsOverride,
            RenderPipelineVariant activePipeline,
            out VrmxtMaterialOverrideDescriptor descriptor);
    }

    public sealed class VrmxtMaterialOverrideDescriptor
    {
        public VrmxtMaterialOverrideDescriptor(
            string shaderName,
            IReadOnlyList<VrmxtMaterialBinding> bindings)
        {
            ShaderName = shaderName;
            Bindings = bindings;
        }

        public string ShaderName { get; }
        public IReadOnlyList<VrmxtMaterialBinding> Bindings { get; }
    }

    /// <summary>
    /// Foundation stub for materials override import. Full UniVRM integration requires
    /// <c>com.vrmc.gltf</c> and returns a <c>MaterialDescriptor</c> from the wrapped
    /// stock VRM 1.0 generator when override resolution fails.
    /// </summary>
    public sealed class VrmxtMaterialsOverrideGenerator : IVrmxtMaterialDescriptorGenerator
    {
        public bool TryBuildOverrideDescriptor(
            int materialIndex,
            VrmxtMaterialsOverrideExtension materialsOverride,
            RenderPipelineVariant activePipeline,
            out VrmxtMaterialOverrideDescriptor descriptor)
        {
            descriptor = null;
            if (materialsOverride == null)
            {
                return false;
            }

            if (!UnityOverrideSelector.TrySelectUnityOverride(
                    materialsOverride,
                    activePipeline,
                    out var unityOverride))
            {
                return false;
            }

            descriptor = new VrmxtMaterialOverrideDescriptor(
                unityOverride.ShaderName,
                FindBindings(materialsOverride));
            return true;
        }

        private static IReadOnlyList<VrmxtMaterialBinding> FindBindings(
            VrmxtMaterialsOverrideExtension materialsOverride)
        {
            foreach (var entry in materialsOverride.Overrides)
            {
                if (string.Equals(entry.Engine, VrmxtMaterialsOverride.EngineUnity, System.StringComparison.Ordinal))
                {
                    return entry.Bindings;
                }
            }

            return System.Array.Empty<VrmxtMaterialBinding>();
        }
    }
}
