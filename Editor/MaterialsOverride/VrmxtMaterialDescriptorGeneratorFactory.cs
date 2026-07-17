namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Editor integration placeholder for UniVRM project settings.
    /// When <c>com.vrmc.vrm</c> is installed, subclass
    /// <c>VRM10.Editor.MaterialDescriptorGeneratorFactory</c> and return a wrapper around
    /// the stock VRM 1.0 generator plus <c>VrmxtMaterialsOverrideGenerator</c>.
    /// </summary>
    public static class VrmxtMaterialDescriptorGeneratorFactory
    {
        public const string IntegrationNote =
            "Assign a MaterialDescriptorGeneratorFactory in UniVRM project settings that wraps the stock generator with VrmxtMaterialsOverrideGenerator.";
    }
}
