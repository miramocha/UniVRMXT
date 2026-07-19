using System;
using System.Reflection;
using UniVRMXT.MaterialsOverride;
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

            // resolveTexture is null for the MVP: texture-typed overrides/bindings are
            // skipped rather than decoding the source GLB a second time during import.
            VrmxtMaterialsOverrideApplier.Apply(root, store, json, pipeline, resolveTexture: null);
        }
    }
}
