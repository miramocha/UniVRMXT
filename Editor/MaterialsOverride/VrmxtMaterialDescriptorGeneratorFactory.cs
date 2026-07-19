namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Editor integration placeholder for UniVRM project settings.
    /// <see cref="VrmxtMaterialsOverrideImportHookBootstrap"/> already covers the
    /// materials-override MVP: it applies <c>VRMXT_materials_override</c> as a
    /// post-import swap (stock materials build first, then this package re-shades them
    /// once <c>Vrm10ImportExtensionRegistry</c> hooks run). Wrapping
    /// <c>VRM10.Editor.MaterialDescriptorGeneratorFactory</c> so the override is present
    /// at first material build instead — e.g. for hosts that generate thumbnails mid-import
    /// — is optional and not required for the MVP.
    /// When <c>com.vrmc.vrm</c> is installed and that earlier hook point is needed, subclass
    /// <c>VRM10.Editor.MaterialDescriptorGeneratorFactory</c> and return a wrapper around
    /// the stock VRM 1.0 generator plus <c>VrmxtMaterialsOverrideGenerator</c>.
    /// </summary>
    public static class VrmxtMaterialDescriptorGeneratorFactory
    {
        public const string IntegrationNote =
            "VrmxtMaterialsOverrideImportHookBootstrap already applies VRMXT_materials_override as a post-import swap. " +
            "Assigning a MaterialDescriptorGeneratorFactory in UniVRM project settings (wrapping the stock generator " +
            "with VrmxtMaterialsOverrideGenerator) is optional, only needed for hosts that require the override before first material build.";
    }
}
