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
    /// on the imported <c>.vrm</c> when hooks exist and are enabled in Preferences/VRM10.
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
        /// True when Extended hooks exist and Preferences → VRM10 → Enable VRM import
        /// extensions is on. False → postprocessor builds <c>*.vrmxt.prefab</c>.
        /// </summary>
        public static bool ImportHooksAvailable
        {
            get
            {
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
            // Extended-UniVRM with preference gate. Older builds without IsEnabled → assume on.
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

            // Materials created with `new Material()` during ScriptedImporter are destroyed
            // unless sub-asseted → missing refs → pink particles after import.
            PersistOwnedParticleMaterials(root, addObject, contextObj);

            try
            {
                if (glbTextures == null)
                {
                    return;
                }

                var i = 0;
                foreach (var texture in glbTextures.CachedTextures)
                {
                    if (texture == null)
                    {
                        continue;
                    }

                    addObject.Invoke(contextObj, new object[] { "VRMXT_vfx_tex_" + i, texture });
                    i++;
                }

                glbTextures.ReleaseOwnership();
            }
            finally
            {
                glbTextures?.Dispose();
            }
        }

        private static void PersistOwnedParticleMaterials(
            GameObject root,
            MethodInfo addObject,
            object contextObj)
        {
            if (root == null || addObject == null || contextObj == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            var i = 0;
            for (var r = 0; r < renderers.Length; r++)
            {
                var material = renderers[r] != null ? renderers[r].sharedMaterial : null;
                if (!VrmxtVfxParticleSystemMapper.IsOwnedParticleMaterial(material))
                {
                    continue;
                }

                addObject.Invoke(contextObj, new object[] { "VRMXT_vfx_mat_" + i, material });
                i++;
            }
        }
    }
}
