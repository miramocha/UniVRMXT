using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Editor.Mtoonxt
{
    /// <summary>
    /// Write attached <c>VRMXT_materials_mtoonxt</c> JSON on VRM 1.0 export.
    /// </summary>
    [InitializeOnLoad]
    public static class VrmxtMaterialsMtoonxtExportHookBootstrap
    {
        private const string RegistryTypeName = "UniVRM10.Vrm10ExportExtensionRegistry, VRM10";

        private static readonly Action<object> Handler = OnVrmExport;
        private static bool s_registered;
        private static bool s_loggedMissingAddRootExtension;

        static VrmxtMaterialsMtoonxtExportHookBootstrap()
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
                modifiers: null
            );
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
                BindingFlags.Public | BindingFlags.Static
            );
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
            if (phaseObj == null)
            {
                return;
            }

            var root = type.GetProperty("Root")?.GetValue(contextObj) as GameObject;
            if (root == null)
            {
                return;
            }

            var phase = phaseObj.ToString();
            if (phase == "PreHierarchy")
            {
                foreach (var auxiliary in root.GetComponentsInChildren<VrmxtMaterialsMtoonxtAuxiliaryRenderer>(true))
                {
                    auxiliary.PrepareExportCopy();
                }
                VrmxtMaterialsMtoonxtAuthoring.ClearExportStockCopies();
                RemapMtoonxtShadersToStockMtoon(root);
                return;
            }

            if (phase != "WriteExtensions")
            {
                return;
            }

            try
            {
                WriteMtoonxtExtensions(contextObj, type, root);
            }
            finally
            {
                VrmxtMaterialsMtoonxtAuthoring.ClearExportStockCopies();
            }
        }

        private static void WriteMtoonxtExtensions(object contextObj, Type type, GameObject root)
        {
            var store = root.GetComponent<VrmxtMaterialsMtoonxtInstance>();
            if (store == null)
            {
                return;
            }

            var tryGetMaterialIndex = type.GetMethod(
                "TryGetMaterialIndex",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Material) },
                modifiers: null
            );

            WriteRootStencilRelationships(
                contextObj,
                type,
                store,
                tryGetMaterialIndex
            );

        }

        private static void WriteRootStencilRelationships(
            object contextObj,
            Type type,
            VrmxtMaterialsMtoonxtInstance store,
            MethodInfo tryGetMaterialIndex
        )
        {
            if (store.StencilRelationships.Count == 0 || tryGetMaterialIndex == null)
            {
                return;
            }

            var addRootExtension = type.GetMethod(
                "AddRootExtension",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(byte[]) },
                modifiers: null
            );
            if (addRootExtension == null)
            {
                if (!s_loggedMissingAddRootExtension)
                {
                    s_loggedMissingAddRootExtension = true;
                    Debug.LogWarning(
                        "UniVRMXT: Vrm10ExportExtensionContext.AddRootExtension is missing — "
                            + "VRMXT_materials_mtoonxt stencil cannot be exported."
                    );
                }

                return;
            }

            var relationships = VrmxtMaterialsMtoonxtAuthoring.ToExportRelationships(
                store,
                material => ResolveMaterialIndex(
                    contextObj,
                    type,
                    tryGetMaterialIndex,
                    material
                )
            );
            if (relationships.Count == 0)
            {
                return;
            }

            addRootExtension.Invoke(
                contextObj,
                new object[]
                {
                    VrmxtMaterialsMtoonxt.ExtensionName,
                    Encoding.UTF8.GetBytes(
                        VrmxtMaterialsMtoonxtRelationships.ToJson(relationships)
                    ),
                }
            );
        }

        private static int? ResolveMaterialIndex(
            object contextObj,
            Type type,
            MethodInfo tryGetMaterialIndex,
            Material material
        )
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

        /// <summary>
        /// Export copy only: UniVRM MToon export matches stock <c>VRM10/MToon10</c> shader
        /// identity. Swap MToonXT forks to stock so <c>VRMC_materials_mtoon</c> is written.
        /// New material instances so shared assets are not mutated.
        /// </summary>
        private static void RemapMtoonxtShadersToStockMtoon(GameObject root)
        {
            var stockBirp = Shader.Find("VRM10/MToon10");
            var stockUrp = Shader.Find("VRM10/Universal Render Pipeline/MToon10");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var shared = renderer.sharedMaterials;
                Material[] next = null;
                for (var j = 0; j < shared.Length; j++)
                {
                    var src = shared[j];
                    var stock = StockMtoonShaderFor(src, stockBirp, stockUrp);
                    if (stock == null)
                    {
                        continue;
                    }

                    if (next == null)
                    {
                        next = (Material[])shared.Clone();
                    }

                    var copy = new Material(src);
                    copy.shader = stock;
                    copy.name = src.name;
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    VrmxtMaterialsMtoonxtAuthoring.RegisterExportStockCopy(src, copy);
                    next[j] = copy;
                }

                if (next != null)
                {
                    renderer.sharedMaterials = next;
                }
            }
        }

        private static Shader StockMtoonShaderFor(Material src, Shader stockBirp, Shader stockUrp)
        {
            if (src == null || src.shader == null)
            {
                return null;
            }

            var name = src.shader.name;
            if (name == VrmxtMaterialsMtoonxt.BuiltinShaderName)
            {
                return stockBirp;
            }

            if (name == VrmxtMaterialsMtoonxt.UrpShaderName)
            {
                return stockUrp;
            }

            return null;
        }
    }
}
