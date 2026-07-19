using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;
using UnityEngine;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideAuthoringTests
    {
        [Test]
        public void SyncUnityOverrideFromMaterial_UpsertsActiveSlot_KeepsSiblingVariant()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                overrideMat.SetFloat("_Metallic", 0.3f);

                const string initialJson =
                    @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""Old/Shader"",""variant"":""urp""},""bindings"":[{""source"":""shadeColorFactor"",""target"":""_Color"",""targetType"":""vector""}],""properties"":[]},{""engine"":""unreal"",""material"":{""idType"":""resourcePath"",""id"":""/Game/M"",""variant"":""opaque""}}]}";

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(initialJson, out _),
                    "fixture JSON must parse before Sync");

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson);

                pair.OverrideMaterial = overrideMat;
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension));

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline());

                Assert.IsTrue(VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var unitySlots));
                Assert.GreaterOrEqual(unitySlots.Count, 1);

                UnityMaterialOverride activeUnity = null;
                UnityMaterialOverride urpSibling = null;
                foreach (var slot in unitySlots)
                {
                    var unity = slot.Material as UnityMaterialOverride;
                    Assert.IsNotNull(unity);
                    if (string.Equals(unity.Variant, activeVariant, System.StringComparison.Ordinal))
                    {
                        activeUnity = unity;
                    }

                    if (string.Equals(unity.Variant, "urp", System.StringComparison.Ordinal))
                    {
                        urpSibling = unity;
                    }
                }

                Assert.IsNotNull(activeUnity);
                Assert.AreEqual("Standard", activeUnity.ShaderName);

                if (string.Equals(activeVariant, "urp", System.StringComparison.Ordinal))
                {
                    // Sync on URP updates the existing urp slot; bindings survive.
                    Assert.AreEqual(1, unitySlots.Count);
                    Assert.AreEqual(1, unitySlots[0].Bindings.Count);
                }
                else
                {
                    // Sync on BIRP/HDRP creates active slot; urp sibling + bindings stay.
                    Assert.IsNotNull(urpSibling);
                    Assert.AreEqual("Old/Shader", urpSibling.ShaderName);
                    foreach (var slot in unitySlots)
                    {
                        if (string.Equals(
                                ((UnityMaterialOverride)slot.Material).Variant,
                                "urp",
                                System.StringComparison.Ordinal))
                        {
                            Assert.AreEqual(1, slot.Bindings.Count);
                        }
                    }
                }

                var hasUnreal = false;
                foreach (var entry in extension.Overrides)
                {
                    if (entry.Engine == "unreal")
                    {
                        hasUnreal = true;
                    }
                }

                Assert.IsTrue(hasUnreal);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

        [Test]
        public void SyncUnityOverrideFromMaterial_WritesShaderNameAndPreservesVariant()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                overrideMat.SetFloat("_Metallic", 0.3f);

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline());

                var initialJson =
                    "{\"specVersion\":\"1.0\",\"overrides\":[{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Old/Shader\",\"variant\":\"" +
                    activeVariant +
                    "\"},\"bindings\":[{\"source\":\"shadeColorFactor\",\"target\":\"_Color\",\"targetType\":\"vector\"}],\"properties\":[]},{\"engine\":\"unreal\",\"material\":{\"idType\":\"resourcePath\",\"id\":\"/Game/M\",\"variant\":\"opaque\"}}]}";

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(initialJson, out _),
                    "fixture JSON must parse before Sync");

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson);

                pair.OverrideMaterial = overrideMat;
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension));
                Assert.IsTrue(UnityOverrideSelector.TrySelectUnityOverride(
                    extension,
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline(),
                    out var unity));
                Assert.AreEqual("Standard", unity.ShaderName);
                Assert.AreEqual(activeVariant, unity.Variant);

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
                Assert.AreEqual(2, extension.Overrides.Count);
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
                new VrmxtMaterialsOverridePair("Hair", null)
                {
                    SourceMaterial = stock,
                    OverrideMaterial = overrideMat,
                },
            });

            try
            {
                VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(root, instance);
                var live = mesh.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreEqual(0.77f, live.GetFloat("_Metallic"), 1e-4f);
                // Stock asset must stay untouched (scene uses a clone).
                Assert.AreNotSame(stock, live);
                Assert.AreNotEqual(0.77f, stock.GetFloat("_Metallic"));
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ClearOverrides_RestoresSourceAndClearsOverrideFields()
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
                new VrmxtMaterialsOverridePair("Hair", @"{""specVersion"":""1.0"",""overrides"":[]}")
                {
                    SourceMaterial = stock,
                    OverrideMaterial = overrideMat,
                },
            });

            try
            {
                VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(root, instance);
                Assert.AreNotSame(stock, mesh.GetComponent<MeshRenderer>().sharedMaterial);

                instance.ClearOverrides();

                Assert.AreSame(stock, mesh.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.IsNull(instance.Pairs[0].OverrideMaterial);
                Assert.IsTrue(string.IsNullOrEmpty(instance.Pairs[0].ExtensionJson));
                Assert.AreSame(stock, instance.Pairs[0].SourceMaterial);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ClearOverrideAt_ClearsOnlyTargetPair()
        {
            var root = new GameObject("root");
            var meshA = new GameObject("meshA");
            var meshB = new GameObject("meshB");
            meshA.transform.SetParent(root.transform, false);
            meshB.transform.SetParent(root.transform, false);

            var stockA = new Material(Shader.Find("Standard")) { name = "Hair" };
            var stockB = new Material(Shader.Find("Standard")) { name = "Face" };
            var overrideA = new Material(Shader.Find("Standard")) { name = "OverrideA" };
            overrideA.SetFloat("_Metallic", 0.5f);
            meshA.AddComponent<MeshRenderer>().sharedMaterial = stockA;
            meshB.AddComponent<MeshRenderer>().sharedMaterial = stockB;

            const string faceJson =
                @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""X""}}]}";

            var instance = root.AddComponent<VrmxtMaterialsOverrideInstance>();
            instance.SetPairs(new[]
            {
                new VrmxtMaterialsOverridePair("Hair", null)
                {
                    SourceMaterial = stockA,
                    OverrideMaterial = overrideA,
                },
                new VrmxtMaterialsOverridePair("Face", faceJson)
                {
                    SourceMaterial = stockB,
                },
            });

            try
            {
                VrmxtMaterialsOverrideAuthoring.ApplyOverrideMaterialsToRenderers(root, instance);
                Assert.IsTrue(instance.ClearOverrideAt(0));

                Assert.AreSame(stockA, meshA.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.IsNull(instance.Pairs[0].OverrideMaterial);
                Assert.IsTrue(string.IsNullOrEmpty(instance.Pairs[0].ExtensionJson));
                Assert.AreEqual(faceJson, instance.Pairs[1].ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(overrideA);
                Object.DestroyImmediate(stockA);
                Object.DestroyImmediate(stockB);
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
