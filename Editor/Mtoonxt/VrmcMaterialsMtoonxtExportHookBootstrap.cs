using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Write attached <c>VRMC_materials_mtoonxt</c> JSON on VRM 1.0 export.
    /// </summary>
    [InitializeOnLoad]
    public static class VrmcMaterialsMtoonxtExportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ExportExtensionRegistry, VRM10";

        private static readonly Action<object> Handler = OnVrmExport;
        private static bool s_registered;
        private static bool s_loggedMissingAddMaterialExtension;

        static VrmcMaterialsMtoonxtExportHookBootstrap()
        {
            TryRegister();
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
            if (contextObj == null || !TryRegister())
            {
                return;
            }

            var registryType = Type.GetType(RegistryTypeName, throwOnError: false);
            if (registryType != null && !ReadIsEnabled(registryType))
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
            if (phaseObj == null || phaseObj.ToString() != "WriteExtensions")
            {
                return;
            }

            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var store = root.GetComponent<VrmcMaterialsMtoonxtInstance>();
            if (store == null || store.Pairs.Count == 0)
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
                        "UniVRMXT: Vrm10ExportExtensionContext.AddMaterialExtension is missing — " +
                        "VRMC_materials_mtoonxt cannot be written per-material on stock UniVRM.");
                }

                return;
            }

            var tryGetMaterialIndex = type.GetMethod(
                "TryGetMaterialIndex",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Material) },
                modifiers: null);

            for (var i = 0; i < store.Pairs.Count; i++)
            {
                var pair = store.Pairs[i];
                if (pair == null || string.IsNullOrEmpty(pair.ExtensionJson))
                {
                    continue;
                }

                if (!VrmcMaterialsMtoonxt.TryParse(pair.ExtensionJson, out var xt))
                {
                    continue;
                }

                var payload = pair.ExtensionJson;
                if (NeedsClipIndexRewrite(xt))
                {
                    payload = RewriteClipIndices(
                        xt,
                        root,
                        store,
                        contextObj,
                        type,
                        tryGetMaterialIndex);
                }

                var utf8 = Encoding.UTF8.GetBytes(payload);
                var written = new HashSet<int>();
                var matchedAny = false;

                foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root,
                             pair.MaterialName))
                {
                    matchedAny = true;
                    var materialIndex = ResolveMaterialIndex(
                        contextObj,
                        type,
                        tryGetMaterialIndex,
                        material);
                    if (!materialIndex.HasValue || !written.Add(materialIndex.Value))
                    {
                        continue;
                    }

                    addMaterialExtension.Invoke(
                        contextObj,
                        new object[]
                        {
                            materialIndex.Value,
                            VrmcMaterialsMtoonxt.ExtensionName,
                            utf8,
                        });
                }

                if (!matchedAny && pair.GltfMaterialIndex >= 0)
                {
                    addMaterialExtension.Invoke(
                        contextObj,
                        new object[]
                        {
                            pair.GltfMaterialIndex,
                            VrmcMaterialsMtoonxt.ExtensionName,
                            utf8,
                        });
                }
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
                var boxed = tryGetMaterialIndex.Invoke(contextObj, new object[] { material });
                if (boxed is int index)
                {
                    return index;
                }
            }

            return null;
        }

        private static bool NeedsClipIndexRewrite(VrmcMaterialsMtoonxtExtension xt)
        {
            return xt != null &&
                ((xt.Stencil != null &&
                  xt.Stencil.HasOp &&
                  VrmcMaterialsMtoonxt.UsesMaterialsList(xt.Stencil.Op)) ||
                 (xt.OutlineStencil != null &&
                  xt.OutlineStencil.HasOp &&
                  VrmcMaterialsMtoonxt.UsesMaterialsList(xt.OutlineStencil.Op)));
        }

        private static string RewriteClipIndices(
            VrmcMaterialsMtoonxtExtension xt,
            GameObject root,
            VrmcMaterialsMtoonxtInstance store,
            object contextObj,
            Type type,
            MethodInfo tryGetMaterialIndex)
        {
            var body = RewriteStencil(xt.Stencil, root, store, contextObj, type, tryGetMaterialIndex);
            var outline = RewriteStencil(xt.OutlineStencil, root, store, contextObj, type, tryGetMaterialIndex);
            var next = new VrmcMaterialsMtoonxtExtension(body, outline, xt.ZTest, xt.ZWrite);
            return VrmcMaterialsMtoonxt.ToJson(next);
        }

        private static VrmcMaterialsMtoonxtStencil RewriteStencil(
            VrmcMaterialsMtoonxtStencil stencil,
            GameObject root,
            VrmcMaterialsMtoonxtInstance store,
            object contextObj,
            Type type,
            MethodInfo tryGetMaterialIndex)
        {
            if (stencil == null ||
                !stencil.HasOp ||
                !VrmcMaterialsMtoonxt.UsesMaterialsList(stencil.Op) ||
                stencil.Materials == null)
            {
                return stencil;
            }

            var mapped = new List<int>();
            for (var i = 0; i < stencil.Materials.Count; i++)
            {
                var material = FindMaterialForGltfIndex(root, store, stencil.Materials[i]);
                var index = ResolveMaterialIndex(contextObj, type, tryGetMaterialIndex, material);
                if (index.HasValue && !mapped.Contains(index.Value))
                {
                    mapped.Add(index.Value);
                }
            }

            return VrmcMaterialsMtoonxtStencil.FromOp(stencil.Op, mapped);
        }

        private static Material FindMaterialForGltfIndex(
            GameObject root,
            VrmcMaterialsMtoonxtInstance store,
            int gltfIndex)
        {
            for (var i = 0; i < store.Pairs.Count; i++)
            {
                var pair = store.Pairs[i];
                if (pair == null || pair.GltfMaterialIndex != gltfIndex)
                {
                    continue;
                }

                foreach (var material in VrmxtMaterialsOverrideRuntime.FindMaterialsForStoreKey(
                             root,
                             pair.MaterialName))
                {
                    if (material != null)
                    {
                        return material;
                    }
                }
            }

            return null;
        }
    }
}
