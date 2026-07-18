using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// Soft-detect Extended-UniVRM <c>Vrm10ImportExtensionRegistry</c> and register VFX attach
    /// on the imported <c>.vrm</c> when hooks exist and are enabled in Project Settings/VRM10.
    /// Stock UniVRM or hooks disabled → companion prefab via <see cref="VrmxtVfxAssetPostprocessor"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class VrmxtVfxImportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ImportExtensionRegistry, VRM10.Editor";

        private static readonly Action<object> Handler = OnVrmImported;
        private static bool s_registered;

        static VrmxtVfxImportHookBootstrap()
        {
            TryRegister();
        }

        /// <summary>
        /// True when the VFX import handler is registered and Project Settings → VRM10 →
        /// Enable VRM Import Extensions is on. False → postprocessor builds
        /// <c>*.vrmxt.prefab</c>.
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
                AttachVfx(contextObj);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void AttachVfx(object contextObj)
        {
            var type = contextObj.GetType();
            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            var assetPath = type.GetProperty("AssetPath")?.GetValue(contextObj) as string;
            var nodesObj = type.GetProperty("Nodes")?.GetValue(contextObj);
            var addObject = type.GetMethod(
                "AddObjectToAsset",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(UnityEngine.Object) },
                modifiers: null);

            if (root == null || string.IsNullOrEmpty(assetPath) || nodesObj == null || addObject == null)
            {
                return;
            }

            if (!(nodesObj is IReadOnlyList<Transform> nodes))
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(assetPath);
            }
            catch (IOException)
            {
                return;
            }

            Func<int, Transform> resolveNode = index =>
                index >= 0 && index < nodes.Count ? nodes[index] : null;

            if (!VrmxtVfxRuntime.TryAttachFromGlb(
                    root,
                    bytes,
                    resolveNode,
                    out _,
                    out var glbTextures))
            {
                return;
            }

            try
            {
                VrmxtVfxImportSubAssets.Persist(
                    root,
                    glbTextures,
                    (id, obj) => addObject.Invoke(contextObj, new object[] { id, obj }));
            }
            finally
            {
                glbTextures?.Dispose();
            }
        }
    }
}
