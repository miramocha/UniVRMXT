using System;
using System.Collections.Generic;
using System.Reflection;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Vfx
{
    /// <summary>
    /// Soft-detect Extended-UniVRM <c>Vrm10ExportExtensionRegistry</c> and write
    /// <c>VRMXT_vfx</c> on VRM 1.0 export from <see cref="VrmxtVfxInstance"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class VrmxtVfxExportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ExportExtensionRegistry, VRM10";

        private static readonly Action<object> Handler = OnVrmExport;
        private static bool s_registered;

        static VrmxtVfxExportHookBootstrap()
        {
            TryRegister();
        }

        public static bool ExportHooksAvailable
        {
            get
            {
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
                    "UniVRMXT: Vrm10ExportExtensionRegistry found but RegisterHandler(Action<object>) missing.");
                return false;
            }

            register.Invoke(null, new object[] { Handler });
            s_registered = true;
            return true;
        }

        private static bool ReadIsEnabled(Type registryType)
        {
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

        private static void OnVrmExport(object contextObj)
        {
            if (contextObj == null || !ExportHooksAvailable)
            {
                return;
            }

            try
            {
                Handle(contextObj);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void Handle(object contextObj)
        {
            var type = contextObj.GetType();
            var phaseObj = type.GetProperty("Phase")?.GetValue(contextObj);
            if (phaseObj == null)
            {
                return;
            }

            var phaseName = phaseObj.ToString();
            switch (phaseName)
            {
                case "PreHierarchy":
                    OnPreHierarchy(contextObj, type);
                    break;
                case "PrepareTextures":
                    OnPrepareTextures(contextObj, type);
                    break;
                case "WriteExtensions":
                    OnWriteExtensions(contextObj, type);
                    break;
            }
        }

        private static void OnPreHierarchy(object contextObj, Type type)
        {
            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var pending = VrmxtVfxExporter.CaptureAndClearParticleSystems(root);
            if (pending.Count == 0)
            {
                return;
            }

            if (!TryGetUserData(contextObj, type, out var userData))
            {
                return;
            }

            userData[VrmxtVfxExporter.PendingUserDataKey] = pending;
        }

        private static void OnPrepareTextures(object contextObj, Type type)
        {
            if (!TryGetPending(contextObj, type, out var pending) || pending.Count == 0)
            {
                return;
            }

            var register = type.GetMethod(
                "RegisterSRgbTexture",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Texture), typeof(bool) },
                modifiers: null);
            if (register == null)
            {
                return;
            }

            VrmxtVfxExporter.RegisterTextures(
                pending,
                texture => (int)register.Invoke(contextObj, new object[] { texture, true }));
        }

        private static void OnWriteExtensions(object contextObj, Type type)
        {
            if (!TryGetPending(contextObj, type, out var pending) || pending.Count == 0)
            {
                return;
            }

            var tryGetNodeIndex = type.GetMethod(
                "TryGetNodeIndex",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Transform) },
                modifiers: null);
            var addRootExtension = type.GetMethod(
                "AddRootExtension",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(byte[]) },
                modifiers: null);
            if (tryGetNodeIndex == null || addRootExtension == null)
            {
                return;
            }

            if (!VrmxtVfxExporter.TryBuildUtf8Json(
                    pending,
                    transform =>
                    {
                        var result = tryGetNodeIndex.Invoke(contextObj, new object[] { transform });
                        return result as int?;
                    },
                    out var utf8Json))
            {
                return;
            }

            addRootExtension.Invoke(
                contextObj,
                new object[] { VrmxtVfx.ExtensionName, utf8Json });
        }

        private static bool TryGetPending(
            object contextObj,
            Type type,
            out List<VrmxtVfxPendingEmitter> pending)
        {
            pending = null;
            if (!TryGetUserData(contextObj, type, out var userData))
            {
                return false;
            }

            if (!userData.TryGetValue(VrmxtVfxExporter.PendingUserDataKey, out var boxed) ||
                boxed is not List<VrmxtVfxPendingEmitter> list)
            {
                return false;
            }

            pending = list;
            return true;
        }

        private static bool TryGetUserData(
            object contextObj,
            Type type,
            out Dictionary<string, object> userData)
        {
            userData = null;
            var boxed = type.GetProperty("UserData")?.GetValue(contextObj);
            if (boxed is Dictionary<string, object> dict)
            {
                userData = dict;
                return true;
            }

            return false;
        }
    }
}
