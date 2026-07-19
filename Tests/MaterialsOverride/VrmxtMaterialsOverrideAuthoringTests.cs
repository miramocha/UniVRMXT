using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UnityEngine;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideAuthoringTests
    {
        [Test]
        public void SyncUnityOverrideFromMaterial_WritesShaderNameAndPreservesVariant()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                overrideMat.SetFloat("_Metallic", 0.3f);

                var pair = new VrmxtMaterialsOverridePair(
                    "Hair",
                    @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""Old/Shader"",""variant"":""urp""},""bindings"":[{""source"":""shadeColorFactor"",""target"":""_Color"",""targetType"":""vector""}],""properties"":[]},{""engine"":""unreal"",""material"":{""idType"":""materialSet"",""variants"":{""default"":""/Game/M""}}]}");

                pair.OverrideMaterial = overrideMat;
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension));
                Assert.IsTrue(VrmxtMaterialsOverride.TryGetUnityOverride(extension, out var unity));
                Assert.AreEqual("Standard", unity.ShaderName);
                Assert.AreEqual("urp", unity.Variant);

                var hasUnreal = false;
                var hasBinding = false;
                foreach (var entry in extension.Overrides)
                {
                    if (entry.Engine == "unreal")
                    {
                        hasUnreal = true;
                    }

                    if (entry.Engine == "unity" && entry.Bindings.Count > 0)
                    {
                        hasBinding = true;
                    }
                }

                Assert.IsTrue(hasUnreal);
                Assert.IsTrue(hasBinding);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

        [Test]
        public void ApplyOverrideMaterialsToRenderers_CopiesShaderOntoNamedMaterial()
        {
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);

            var stock = new Material(Shader.Find("Standard")) { name = "Hair" };
            var overrideMat = new Material(Shader.Find("Standard")) { name = "Override" };
            overrideMat.SetFloat("_Metallic", 0.77f);
            mesh.AddComponent<MeshRenderer>().sharedMaterial = stock;

            var instance = root.AddComponent<VrmxtMaterialsOverrideInstance>();
            instance.SetPairs(new[]
            {
                new VrmxtMaterialsOverridePair("Hair", null) { OverrideMaterial = overrideMat },
            });

            try
            {
                VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(root, instance);
                Assert.AreEqual(0.77f, stock.GetFloat("_Metallic"), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VrmxtInstance_EnsureOn_WiresMaterialsOverride()
        {
            var root = new GameObject("root");
            try
            {
                var mats = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                var facade = VrmxtInstance.EnsureOn(root);
                Assert.IsNotNull(facade);
                Assert.AreSame(mats, facade.MaterialsOverride);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
