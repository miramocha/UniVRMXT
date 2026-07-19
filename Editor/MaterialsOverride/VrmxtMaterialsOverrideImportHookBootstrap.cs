using System;
using System.IO;
using System.Reflection;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Soft-detect Extended-UniVRM <c>Vrm10ImportExtensionRegistry</c> and apply
    /// <c>VRMXT_materials_override</c> onto the imported <c>.vrm</c> when hooks exist and
    /// are enabled in Project Settings/VRM10. Stock UniVRM or hooks disabled → materials
    /// stay on stock import (no companion path for the materials-override MVP).
    /// </summary>
    [InitializeOnLoad]
    public static class VrmxtMaterialsOverrideImportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ImportExtensionRegistry, VRM10.Editor";

        private static readonly Action<object> Handler = OnVrmImported;
        private static bool s_registered;

        static VrmxtMaterialsOverrideImportHookBootstrap()
        {
            TryRegister();
        }

        /// <summary>
        /// True when the materials-override import handler is registered and Project
        /// Settings → VRM10 → Enable VRM Import Extensions is on.
        /// </summary>
        public static bool ImportHooksAvailable
        {
            get
            {
                // Require successful registration — registry type alone is not enough.
                if (!TryRegister())
                {
                    return false;
                }

                var registryType = Type.GetType(RegistryTypeName, throwOnError: false);
                if (registryType == null)
                {
                    return false;
                }

                return ReadIsEnabled(registryType);
            }
        }

        public static bool TryRegister()
        {
            if (s_registered)
            {
                return true;
            }

            var registryType = Type.GetType(RegistryTypeName, throwOnError: false);
            if (registryType == null)
            {
                return false;
            }

            var register = registryType.GetMethod(
                "RegisterHandler",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Action<object>) },
                modifiers: null);
            if (register == null)
            {
                Debug.LogWarning(
                    "UniVRMXT: Vrm10ImportExtensionRegistry found but RegisterHandler(Action<object>) missing.");
                return false;
            }

            register.Invoke(null, new object[] { Handler });
            s_registered = true;
            return true;
        }

        private static bool ReadIsEnabled(Type registryType)
        {
            // Extended-UniVRM with project-setting gate. Older builds without IsEnabled → assume on.
            var prop = registryType.GetProperty(
                "IsEnabled",
                BindingFlags.Public | BindingFlags.Static);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                return true;
            }

            try
            {
                return (bool)prop.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        private static void OnVrmImported(object contextObj)
        {
            if (contextObj == null || !ImportHooksAvailable)
            {
                return;
            }

            try
            {
                ApplyMaterialsOverride(contextObj);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void ApplyMaterialsOverride(object contextObj)
        {
            var type = contextObj.GetType();
            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            var json = type.GetProperty("Json")?.GetValue(contextObj) as string;
            var assetPath = type.GetProperty("AssetPath")?.GetValue(contextObj) as string;
            var addObject = type.GetMethod(
                "AddObjectToAsset",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(UnityEngine.Object) },
                modifiers: null);

            if (root == null || string.IsNullOrEmpty(json))
            {
                return;
            }

            // Always attach MaterialsOverrideInstance (authoring shell). Apply is a no-op
            // when no unity overrides are present / selectable.
            if (!VrmxtMaterialsOverrideRuntime.TryAttachFromGltfJson(root, json, out var store))
            {
                return;
            }

            var pipeline = VrmxtMaterialsOverrideApplier.DetectActivePipeline();
            Func<int, Texture> resolveTexture = null;
            VrmxtVfxGlbTextures glbTextures = null;

            try
            {
                if (!string.IsNullOrEmpty(assetPath) &&
                    TryLoadGlbTextures(assetPath, out glbTextures))
                {
                    store.ClearImportedTextures();
                    // Decode into Instance first. Apply must not use glbTextures.AsResolver()
                    // after ReleaseOwnership — Get() would re-decode, then Dispose() would
                    // DestroyImmediate those live SetTexture refs (missing on reimport).
                    store.RememberTexturesFromPairs(glbTextures.AsResolver());
                    PersistImportedTextures(store, contextObj, addObject);
                    glbTextures.ReleaseOwnership();
                    resolveTexture = index =>
                        store.TryGetImportedTexture(index, out var texture) ? texture : null;
                }

                VrmxtMaterialsOverrideApplier.Apply(root, store, json, pipeline, resolveTexture);
            }
            finally
            {
                glbTextures?.Dispose();
            }
        }

        private static bool TryLoadGlbTextures(string assetPath, out VrmxtVfxGlbTextures glbTextures)
        {
            glbTextures = null;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(assetPath);
            }
            catch (IOException)
            {
                return false;
            }

            return VrmxtVfxGlbTextures.TryCreate(bytes, out glbTextures);
        }

        private static void PersistImportedTextures(
            VrmxtMaterialsOverrideInstance store,
            object contextObj,
            MethodInfo addObject)
        {
            if (store == null || addObject == null)
            {
                return;
            }

            var textures = store.ImportedTextures;
            for (var i = 0; i < textures.Count; i++)
            {
                var entry = textures[i];
                if (entry?.Texture == null)
                {
                    continue;
                }

                entry.Texture.name = "VRMXT_mo_tex_" + entry.GltfIndex;
                addObject.Invoke(
                    contextObj,
                    new object[] { "vrmxt_mo_tex_" + entry.GltfIndex, entry.Texture });
            }
        }
    }
}
