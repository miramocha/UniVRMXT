using System;
using System.Collections.Generic;
using System.Reflection;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Soft-detect Extended-UniVRM <c>Vrm10ExportExtensionRegistry</c> and write
    /// <c>VRMXT_materials_override</c> per material on VRM 1.0 export from a
    /// <see cref="VrmxtMaterialsOverrideInstance"/> found on the export root.
    /// </summary>
    [InitializeOnLoad]
    public static class VrmxtMaterialsOverrideExportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ExportExtensionRegistry, VRM10";

        // Cross-phase bag key (PrepareTextures → WriteExtensions), same convention as
        // VrmxtVfxExporter.PendingUserDataKey.
        private const string PendingUserDataKey = "UniVRMXT.MaterialsOverride.PendingEntries";

        private static readonly Action<object> Handler = OnVrmExport;
        private static bool s_registered;
        private static bool s_loggedMissingAddMaterialExtension;

        static VrmxtMaterialsOverrideExportHookBootstrap()
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

        /// <summary>
        /// Put stock SourceMaterial back on the export root (usually a throwaway copy)
        /// so UniVRM mesh export / validation does not see VRMXT preview override shaders.
        /// Override JSON is still written later from the Instance.
        /// </summary>
        private static void OnPreHierarchy(object contextObj, Type type)
        {
            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var instance = VrmxtInstance.FindMaterialsOverride(root);
            if (instance == null)
            {
                return;
            }

            // Refresh JSON from OverrideMaterial before restoring slots.
            VrmxtMaterialsOverrideAuthoring.SyncAllFromOverrideMaterials(instance);
            VrmxtMaterialsOverrideAuthoring.RestoreSourceMaterialsToRenderers(root, instance);
        }

        private static void OnPrepareTextures(object contextObj, Type type)
        {
            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var pending = VrmxtMaterialsOverrideExporter.BuildPending(root);
            if (pending.Count == 0)
            {
                return;
            }

            if (!TryGetUserData(contextObj, type, out var userData))
            {
                return;
            }

            // Stash pending even if RegisterSRgbTexture is missing below — scalar/vector/
            // shaderFeature overrides still need to reach WriteExtensions.
            userData[PendingUserDataKey] = pending;

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

            VrmxtMaterialsOverrideExporter.PrepareTextures(
                pending,
                root,
                (texture, needsAlpha) => (int)register.Invoke(contextObj, new object[] { texture, needsAlpha }));
        }

        private static void OnWriteExtensions(object contextObj, Type type)
        {
            if (!TryGetPending(contextObj, type, out var pending) || pending.Count == 0)
            {
                return;
            }

            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var addMaterialExtension = type.GetMethod(
                "AddMaterialExtension",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(int), typeof(string), typeof(byte[]) },
                modifiers: null);
            if (addMaterialExtension == null)
            {
                if (!s_loggedMissingAddMaterialExtension)
                {
                    s_loggedMissingAddMaterialExtension = true;
                    Debug.LogWarning(
                        "UniVRMXT: Vrm10ExportExtensionContext.AddMaterialExtension is missing on this host — " +
                        "VRMXT_materials_override cannot be written per-material on stock UniVRM.");
                }

                return;
            }

            // NEW on Extended-UniVRM; older hosts fall back to reflecting
            // ModelExporter.Materials below.
            var tryGetMaterialIndex = type.GetMethod(
                "TryGetMaterialIndex",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Material) },
                modifiers: null);

            var extensions = VrmxtMaterialsOverrideExporter.BuildAllUtf8Extensions(pending);
            foreach (var pair in extensions)
            {
                WriteMaterialExtension(
                    contextObj,
                    type,
                    root,
                    tryGetMaterialIndex,
                    addMaterialExtension,
                    pair.Key,
                    pair.Value);
            }
        }

        private static void WriteMaterialExtension(
            object contextObj,
            Type type,
            GameObject root,
            MethodInfo tryGetMaterialIndex,
            MethodInfo addMaterialExtension,
            string materialName,
            byte[] utf8Json)
        {
            var written = new HashSet<int>();
            var matchedAny = false;

            // Same lookup the applier uses for import (renderer search + " (Instance)" trim,
            // plus Name#N disambiguation for duplicate glTF material names) so export
            // round-trips the material(s) that actually received the override.
            foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(root, materialName))
            {
                matchedAny = true;
                var materialIndex = ResolveMaterialIndex(contextObj, type, tryGetMaterialIndex, material);
                if (!materialIndex.HasValue || !written.Add(materialIndex.Value))
                {
                    continue;
                }

                // material.variant is never touched here — the exporter's write-through
                // path (BuildAllUtf8Extensions) preserves the stored JSON verbatim.
                addMaterialExtension.Invoke(
                    contextObj,
                    new object[] { materialIndex.Value, VrmxtMaterialsOverride.ExtensionName, utf8Json });
            }

            if (!matchedAny)
            {
                Debug.LogWarning(
                    $"UniVRMXT: VRMXT_materials_override pending entry for material '{materialName}' matched no " +
                    "live Unity materials on the export root; the override was not written for this material.");
            }
        }

        private static int? ResolveMaterialIndex(
            object contextObj,
            Type type,
            MethodInfo tryGetMaterialIndex,
            Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (tryGetMaterialIndex != null)
            {
                return tryGetMaterialIndex.Invoke(contextObj, new object[] { material }) as int?;
            }

            return TryGetMaterialIndexFromConverter(contextObj, type, material);
        }

        private static int? TryGetMaterialIndexFromConverter(object contextObj, Type type, Material material)
        {
            var converter = type.GetProperty("Converter", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(contextObj);
            if (converter == null)
            {
                return null;
            }

            // ModelExporter.Materials is a public field (List<Material>) as of the current
            // Extended-UniVRM ModelExporter; check the property too in case a future host
            // refactors it.
            var converterType = converter.GetType();
            var materialsObj =
                converterType.GetField("Materials", BindingFlags.Public | BindingFlags.Instance)?.GetValue(converter) ??
                converterType.GetProperty("Materials", BindingFlags.Public | BindingFlags.Instance)?.GetValue(converter);

            if (materialsObj is IList<Material> typedList)
            {
                var index = typedList.IndexOf(material);
                return index >= 0 ? index : (int?)null;
            }

            if (materialsObj is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], material))
                    {
                        return i;
                    }
                }
            }

            return null;
        }

        private static bool TryGetPending(
            object contextObj,
            Type type,
            out List<VrmxtMaterialsOverridePendingEntry> pending)
        {
            pending = null;
            if (!TryGetUserData(contextObj, type, out var userData))
            {
                return false;
            }

            if (!userData.TryGetValue(PendingUserDataKey, out var boxed) ||
                boxed is not List<VrmxtMaterialsOverridePendingEntry> list)
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
