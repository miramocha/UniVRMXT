using NUnit.Framework;
using UnityEngine;
using UniVRMXT.Format;
using UniVRMXT.MaterialsOverride;

namespace UniVRMXT.Tests.MaterialsOverride
{
    public sealed class VrmxtMaterialsOverrideAuthoringTests
    {
        [Test]
        public void SyncUnityOverrideFromMaterial_StockMtoon_ClearsActiveUnity_KeepsSiblings()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "StockMtoonProxy" };
            try
            {
                // Portable name is stock MToon even when the live shader is unavailable.
                overrideMat.SetOverrideTag(
                    VrmxtMaterialsOverrideApplier.OriginalShaderTag,
                    "VRM10/MToon10"
                );

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );
                var siblingVariant = string.Equals(activeVariant, "urp", System.StringComparison.Ordinal)
                    ? "builtin"
                    : "urp";

                var initialJson =
                    @"{""specVersion"":""1.0"",""overrides"":["
                    + @"{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""Old/Active"",""variant"":"""
                    + activeVariant
                    + @"""},""properties"":[]},"
                    + @"{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""Old/Sibling"",""variant"":"""
                    + siblingVariant
                    + @"""},""properties"":[]},"
                    + @"{""engine"":""unreal"",""material"":{""idType"":""resourcePath"",""id"":""/Game/M"",""variant"":""opaque""}}"
                    + "]}";

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(initialJson, out _),
                    "fixture JSON must parse before Sync"
                );

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson)
                {
                    OverrideMaterial = overrideMat,
                };
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension),
                    pair.ExtensionJson
                );
                Assert.AreEqual(2, extension.Overrides.Count, pair.ExtensionJson);

                var hasSiblingUnity = false;
                var hasUnreal = false;
                var hasActiveUnity = false;
                for (var i = 0; i < extension.Overrides.Count; i++)
                {
                    var entry = extension.Overrides[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    if (entry.Engine == "unreal")
                    {
                        hasUnreal = true;
                        continue;
                    }

                    if (
                        entry.Engine == "unity"
                        && entry.Material is UnityMaterialOverride unity
                    )
                    {
                        if (unity.Variant == activeVariant)
                        {
                            hasActiveUnity = true;
                        }

                        if (unity.Variant == siblingVariant)
                        {
                            hasSiblingUnity = true;
                        }
                    }
                }

                Assert.IsFalse(hasActiveUnity, "active RP unity slot must be cleared");
                Assert.IsTrue(hasSiblingUnity, pair.ExtensionJson);
                Assert.IsTrue(hasUnreal, pair.ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

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
                    "fixture JSON must parse before Sync"
                );

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson);

                pair.OverrideMaterial = overrideMat;
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension)
                );

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var unitySlots)
                );
                Assert.GreaterOrEqual(unitySlots.Count, 1);

                UnityMaterialOverride activeUnity = null;
                UnityMaterialOverride urpSibling = null;
                foreach (var slot in unitySlots)
                {
                    var unity = slot.Material as UnityMaterialOverride;
                    Assert.IsNotNull(unity);
                    if (
                        string.Equals(unity.Variant, activeVariant, System.StringComparison.Ordinal)
                    )
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
                        if (
                            string.Equals(
                                ((UnityMaterialOverride)slot.Material).Variant,
                                "urp",
                                System.StringComparison.Ordinal
                            )
                        )
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
        public void SyncUnityOverrideFromMaterial_DoesNotStampEmptyToOccupiedBuiltin()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                // Typed builtin sibling + empty sibling. Sync on urp/hdrp must not stamp
                // empty→builtin (duplicate selection key → TryParse reject).
                const string initialJson =
                    "{\"specVersion\":\"1.0\",\"overrides\":["
                    + "{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Builtin/Shader\",\"variant\":\"builtin\"},\"properties\":[]},"
                    + "{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Empty/Shader\"},\"properties\":["
                    + "{\"name\":\"_Color\",\"type\":\"vector\",\"value\":[0,1,0,1]}]}"
                    + "]}";

                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(initialJson, out _), initialJson);

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson)
                {
                    OverrideMaterial = overrideMat,
                };
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension),
                    pair.ExtensionJson
                );
                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var slots)
                );
                Assert.GreaterOrEqual(slots.Count, 2, pair.ExtensionJson);

                var builtinCount = 0;
                var hasEmptyContent = false;
                foreach (var slot in slots)
                {
                    var unity = (UnityMaterialOverride)slot.Material;
                    if (string.Equals(unity.Variant, "builtin", System.StringComparison.Ordinal))
                    {
                        builtinCount++;
                    }

                    if (unity.ShaderName == "Empty/Shader")
                    {
                        hasEmptyContent = true;
                    }
                }

                Assert.AreEqual(1, builtinCount, pair.ExtensionJson);
                Assert.IsTrue(hasEmptyContent, pair.ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

        [Test]
        public void SyncUnityOverrideFromMaterial_KeepsEmptySibling_WhenActiveTypedSlotMatched()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );

                // Typed active slot + empty-variant sibling — Sync must keep the empty one.
                var initialJson =
                    "{\"specVersion\":\"1.0\",\"overrides\":["
                    + "{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Active/Shader\",\"variant\":\""
                    + activeVariant
                    + "\"},\"properties\":[]},"
                    + "{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Empty/Shader\"},\"properties\":["
                    + "{\"name\":\"_Color\",\"type\":\"vector\",\"value\":[0,1,0,1]}]}"
                    + "]}";

                // Duplicate (engine, empty variant) + typed is valid selection-key-wise.
                Assert.IsTrue(VrmxtMaterialsOverride.TryParse(initialJson, out _), initialJson);

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson)
                {
                    OverrideMaterial = overrideMat,
                };
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension)
                );
                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var slots)
                );
                Assert.GreaterOrEqual(slots.Count, 2, pair.ExtensionJson);

                var hasActive = false;
                var hasPreservedEmptyContent = false;
                foreach (var slot in slots)
                {
                    var unity = (UnityMaterialOverride)slot.Material;
                    if (
                        string.Equals(unity.Variant, activeVariant, System.StringComparison.Ordinal)
                        && unity.ShaderName == "Standard"
                    )
                    {
                        hasActive = true;
                    }

                    // Empty may be stamped to builtin when active is urp/hdrp.
                    if (unity.ShaderName == "Empty/Shader")
                    {
                        hasPreservedEmptyContent = true;
                    }
                }

                Assert.IsTrue(hasActive, pair.ExtensionJson);
                Assert.IsTrue(hasPreservedEmptyContent, pair.ExtensionJson);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

        [Test]
        public void SyncUnityOverrideFromMaterial_KeepsBuiltinSibling_WhenShaderDiffers()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                const string initialJson =
                    @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""VRMXT/Samples/TestOverrideBuiltin"",""variant"":""builtin""},""properties"":[{""name"":""_Color"",""type"":""vector"",""value"":[0,1,0,1]}]}]}";

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson)
                {
                    OverrideMaterial = overrideMat,
                };
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension)
                );
                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var slots)
                );

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );

                UnityMaterialOverride builtin = null;
                UnityMaterialOverride active = null;
                foreach (var slot in slots)
                {
                    var unity = (UnityMaterialOverride)slot.Material;
                    if (string.Equals(unity.Variant, "builtin", System.StringComparison.Ordinal))
                    {
                        builtin = unity;
                    }

                    if (
                        string.Equals(unity.Variant, activeVariant, System.StringComparison.Ordinal)
                    )
                    {
                        active = unity;
                    }
                }

                Assert.IsNotNull(builtin);

                if (string.Equals(activeVariant, "builtin", System.StringComparison.Ordinal))
                {
                    // Active slot is builtin — Sync upserts Standard in place (single slot).
                    Assert.AreEqual(1, slots.Count);
                    Assert.AreEqual("Standard", builtin.ShaderName);
                }
                else
                {
                    Assert.GreaterOrEqual(slots.Count, 2);
                    Assert.IsNotNull(active);
                    Assert.AreEqual("Standard", active.ShaderName);
                    Assert.AreEqual("VRMXT/Samples/TestOverrideBuiltin", builtin.ShaderName);
                }
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
            }
        }

        [Test]
        public void SyncUnityOverrideFromMaterial_KeepsEmptySibling_WhenShaderDiffers()
        {
            var shader = Shader.Find("Standard");
            Assert.IsNotNull(shader);

            var overrideMat = new Material(shader) { name = "Override" };
            try
            {
                // Unlabeled single unity slot (any-pipeline) with a different shader than the
                // Override Material — must not be folded into the active RP slot.
                const string initialJson =
                    @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""VRMXT/Samples/TestOverrideBuiltin""},""properties"":[{""name"":""_Color"",""type"":""vector"",""value"":[0,1,0,1]}]}]}";

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson)
                {
                    OverrideMaterial = overrideMat,
                };
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension)
                );
                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryGetUnityOverrides(extension, out var slots)
                );

                var activeVariant = UnityOverrideSelector.RenderPipelineVariantToVariantString(
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );

                if (string.Equals(activeVariant, "builtin", System.StringComparison.Ordinal))
                {
                    // Same-RP edit of unlabeled slot may fold in place when shaders differ
                    // still creates active builtin and stamps/keeps prior content.
                    Assert.GreaterOrEqual(slots.Count, 1);
                }
                else
                {
                    Assert.GreaterOrEqual(slots.Count, 2);
                    var hasBuiltin = false;
                    var hasActive = false;
                    foreach (var slot in slots)
                    {
                        var unity = (UnityMaterialOverride)slot.Material;
                        if (
                            string.Equals(unity.Variant, "builtin", System.StringComparison.Ordinal)
                            && unity.ShaderName == "VRMXT/Samples/TestOverrideBuiltin"
                        )
                        {
                            hasBuiltin = true;
                        }

                        if (
                            string.Equals(
                                unity.Variant,
                                activeVariant,
                                System.StringComparison.Ordinal
                            )
                            && unity.ShaderName == "Standard"
                        )
                        {
                            hasActive = true;
                        }
                    }

                    Assert.IsTrue(hasBuiltin, pair.ExtensionJson);
                    Assert.IsTrue(hasActive, pair.ExtensionJson);
                }
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
                    VrmxtMaterialsOverrideApplier.DetectActivePipeline()
                );

                var initialJson =
                    "{\"specVersion\":\"1.0\",\"overrides\":[{\"engine\":\"unity\",\"material\":{\"idType\":\"shaderName\",\"id\":\"Old/Shader\",\"variant\":\""
                    + activeVariant
                    + "\"},\"bindings\":[{\"source\":\"shadeColorFactor\",\"target\":\"_Color\",\"targetType\":\"vector\"}],\"properties\":[]},{\"engine\":\"unreal\",\"material\":{\"idType\":\"resourcePath\",\"id\":\"/Game/M\",\"variant\":\"opaque\"}}]}";

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(initialJson, out _),
                    "fixture JSON must parse before Sync"
                );

                var pair = new VrmxtMaterialsOverridePair("Hair", initialJson);

                pair.OverrideMaterial = overrideMat;
                VrmxtMaterialsOverrideAuthoring.SyncUnityOverrideFromMaterial(pair);

                Assert.IsTrue(
                    VrmxtMaterialsOverride.TryParse(pair.ExtensionJson, out var extension)
                );
                Assert.IsTrue(
                    UnityOverrideSelector.TrySelectUnityOverride(
                        extension,
                        VrmxtMaterialsOverrideApplier.DetectActivePipeline(),
                        out var unity
                    )
                );
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
            instance.SetPairs(
                new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", null)
                    {
                        SourceMaterial = stock,
                        OverrideMaterial = overrideMat,
                    },
                }
            );

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
            instance.SetPairs(
                new[]
                {
                    new VrmxtMaterialsOverridePair(
                        "Hair",
                        @"{""specVersion"":""1.0"",""overrides"":[]}"
                    )
                    {
                        SourceMaterial = stock,
                        OverrideMaterial = overrideMat,
                    },
                }
            );

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
            instance.SetPairs(
                new[]
                {
                    new VrmxtMaterialsOverridePair("Hair", null)
                    {
                        SourceMaterial = stockA,
                        OverrideMaterial = overrideA,
                    },
                    new VrmxtMaterialsOverridePair("Face", faceJson) { SourceMaterial = stockB },
                }
            );

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
        public void PopulatePairsFromRenderers_DoesNotDuplicateExistingPlainName()
        {
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);

            var stock = new Material(Shader.Find("Standard")) { name = "Hair" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = stock;

            var instance = root.AddComponent<VrmxtMaterialsOverrideInstance>();
            instance.SetPairs(
                new[]
                {
                    new VrmxtMaterialsOverridePair(
                        "Hair",
                        @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""X""}}]}"
                    )
                    {
                        SourceMaterial = stock,
                    },
                }
            );

            try
            {
                instance.PopulatePairsFromRenderers();
                Assert.AreEqual(1, instance.Pairs.Count);
                Assert.AreEqual("Hair", instance.Pairs[0].MaterialName);
            }
            finally
            {
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulatePairsFromRenderers_DoesNotDuplicateDisambiguatedImportKey()
        {
            var root = new GameObject("root");
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(root.transform, false);

            // Live mat keeps plain glTF name; store key is Name#N from import disambiguation.
            var stock = new Material(Shader.Find("Standard")) { name = "Hair" };
            mesh.AddComponent<MeshRenderer>().sharedMaterial = stock;

            var instance = root.AddComponent<VrmxtMaterialsOverrideInstance>();
            instance.SetPairs(
                new[]
                {
                    new VrmxtMaterialsOverridePair(
                        "Hair#1",
                        @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""X""}}]}"
                    )
                    {
                        SourceMaterial = stock,
                    },
                }
            );

            try
            {
                instance.PopulatePairsFromRenderers();
                Assert.AreEqual(1, instance.Pairs.Count, "plain Hair must not duplicate Hair#1");
                Assert.AreEqual("Hair#1", instance.Pairs[0].MaterialName);
            }
            finally
            {
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CaptureProperties_IncludesHideInInspectorScalarsAndVectors()
        {
            var shader = Shader.Find("VRMXT/Samples/TestOverrideBuiltin");
            Assert.IsNotNull(shader, "TestOverrideBuiltin sample shader must be in project");

            var material = new Material(shader) { name = "CaptureHidden" };
            try
            {
                material.SetFloat("_FeatureEnable", 1f);
                material.SetVector("_MainTexPan", new Vector4(0.25f, -0.5f, 0f, 0f));

                var props = VrmxtMaterialsOverrideAuthoring.CaptureProperties(material);

                VrmxtMaterialProperty enable = null;
                VrmxtMaterialProperty pan = null;
                for (var i = 0; i < props.Count; i++)
                {
                    var p = props[i];
                    if (p == null)
                    {
                        continue;
                    }

                    if (p.Name == "_FeatureEnable")
                    {
                        enable = p;
                    }
                    else if (p.Name == "_MainTexPan")
                    {
                        pan = p;
                    }
                }

                Assert.IsNotNull(enable, "HideInInspector toggle must be captured");
                Assert.AreEqual(VrmxtMaterialsOverride.TargetTypeScalar, enable.Type);
                Assert.AreEqual(1f, enable.ScalarValue.Value, 1e-5f);

                Assert.IsNotNull(pan, "HideInInspector UV pan must be captured");
                Assert.AreEqual(VrmxtMaterialsOverride.TargetTypeVector, pan.Type);
                Assert.AreEqual(4, pan.VectorValue.Count);
                Assert.AreEqual(0.25f, pan.VectorValue[0], 1e-5f);
                Assert.AreEqual(-0.5f, pan.VectorValue[1], 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void CaptureProperties_IncludesAssignedTextures_ByDefault()
        {
            var shader = Shader.Find("VRMXT/Samples/TestOverrideBuiltin");
            Assert.IsNotNull(shader, "TestOverrideBuiltin sample shader must be in project");

            var material = new Material(shader) { name = "CaptureTex" };
            var texture = new Texture2D(2, 2) { name = "GlitterMap" };
            try
            {
                material.SetTexture("_MainTex", texture);

                var withTextures = VrmxtMaterialsOverrideAuthoring.CaptureProperties(material);
                VrmxtMaterialProperty mainTex = null;
                for (var i = 0; i < withTextures.Count; i++)
                {
                    if (withTextures[i]?.Name == "_MainTex")
                    {
                        mainTex = withTextures[i];
                        break;
                    }
                }

                Assert.IsNotNull(mainTex, "assigned texture must be captured for full export");
                Assert.AreEqual(VrmxtMaterialsOverride.TargetTypeTexture, mainTex.Type);
                Assert.AreEqual(0, mainTex.TextureIndex.Value);
                Assert.IsNull(mainTex.VectorValue, "identity ST omitted");

                material.SetTextureScale("_MainTex", new Vector2(3f, 12f));
                material.SetTextureOffset("_MainTex", new Vector2(0.25f, 0.5f));
                var withSt = VrmxtMaterialsOverrideAuthoring.CaptureProperties(material);
                VrmxtMaterialProperty scaled = null;
                for (var i = 0; i < withSt.Count; i++)
                {
                    if (withSt[i]?.Name == "_MainTex")
                    {
                        scaled = withSt[i];
                        break;
                    }
                }

                Assert.IsNotNull(scaled);
                Assert.AreEqual(4, scaled.VectorValue.Count);
                Assert.AreEqual(3f, scaled.VectorValue[0]);
                Assert.AreEqual(12f, scaled.VectorValue[1]);
                Assert.AreEqual(0.25f, scaled.VectorValue[2]);
                Assert.AreEqual(0.5f, scaled.VectorValue[3]);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void FilterTexturesToPackedOnly_KeepsImported_OmitsUnpackaged()
        {
            var shader = Shader.Find("VRMXT/Samples/TestOverrideBuiltin");
            Assert.IsNotNull(shader);

            var material = new Material(shader) { name = "Hair" };
            var packed = new Texture2D(2, 2) { name = "Packed" };
            var unpackaged = new Texture2D(2, 2) { name = "WarudoImages" };
            var root = new GameObject("filter-root");
            try
            {
                var store = root.AddComponent<VrmxtMaterialsOverrideInstance>();
                store.RememberImportedTexture(3, packed);

                material.SetTexture("_MainTex", packed);
                material.SetTexture("_ShadeTex", unpackaged);

                const string json =
                    @"{""specVersion"":""1.0"",""overrides"":[{""engine"":""unity"",""material"":{""idType"":""shaderName"",""id"":""VRMXT/Samples/TestOverrideBuiltin"",""variant"":""builtin""},""properties"":[{""name"":""_MainTex"",""type"":""texture"",""texture"":3}]}]}";
                var pair = new VrmxtMaterialsOverridePair("Hair", json);

                var filtered = VrmxtMaterialsOverrideAuthoring.FilterTexturesToPackedOnly(
                    VrmxtMaterialsOverrideAuthoring.CaptureProperties(material),
                    material,
                    pair,
                    store
                );

                VrmxtMaterialProperty main = null;
                VrmxtMaterialProperty shade = null;
                for (var i = 0; i < filtered.Count; i++)
                {
                    var p = filtered[i];
                    if (p?.Name == "_MainTex")
                    {
                        main = p;
                    }
                    else if (p?.Name == "_ShadeTex")
                    {
                        shade = p;
                    }
                }

                Assert.IsNotNull(main, "packed _MainTex must survive");
                Assert.AreEqual(VrmxtMaterialsOverride.TargetTypeTexture, main.Type);
                Assert.AreEqual(3, main.TextureIndex.Value);
                Assert.IsNull(shade, "unpackaged Warudo Images map must be omitted");
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(packed);
                Object.DestroyImmediate(unpackaged);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CaptureProperties_NullMainTex_FillsFromFallback_AndFixesBlackColor()
        {
            var shader = Shader.Find("VRMXT/Samples/TestOverrideBuiltin");
            Assert.IsNotNull(shader, "TestOverrideBuiltin sample shader must be in project");

            var overrideMat = new Material(shader) { name = "PoiTemplate" };
            var stock = new Material(shader) { name = "StockHair" };
            var hairTex = new Texture2D(2, 2) { name = "HairAlbedo" };
            var lut = new Texture2D(2, 2) { name = "ToonRamp" };
            try
            {
                overrideMat.SetColor("_Color", Color.black);
                overrideMat.SetTexture("_MainTex", null);
                overrideMat.SetTexture("_ShadeTex", lut);

                stock.SetColor("_Color", Color.white);
                stock.SetTexture("_MainTex", hairTex);

                var props = VrmxtMaterialsOverrideAuthoring.CaptureProperties(
                    overrideMat,
                    stock
                );

                VrmxtMaterialProperty mainTex = null;
                VrmxtMaterialProperty shade = null;
                VrmxtMaterialProperty color = null;
                for (var i = 0; i < props.Count; i++)
                {
                    var p = props[i];
                    if (p == null)
                    {
                        continue;
                    }

                    if (p.Name == "_MainTex")
                    {
                        mainTex = p;
                    }
                    else if (p.Name == "_ShadeTex")
                    {
                        shade = p;
                    }
                    else if (p.Name == "_Color")
                    {
                        color = p;
                    }
                }

                Assert.IsNotNull(mainTex, "stock albedo must fill null override _MainTex");
                Assert.AreEqual(VrmxtMaterialsOverride.TargetTypeTexture, mainTex.Type);
                Assert.IsNotNull(shade, "override LUT/extra maps must stay when albedo filled");
                Assert.IsNotNull(color);
                Assert.AreEqual(4, color.VectorValue.Count);
                Assert.AreEqual(1f, color.VectorValue[0], 1e-5f);
                Assert.AreEqual(1f, color.VectorValue[1], 1e-5f);
                Assert.AreEqual(1f, color.VectorValue[2], 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(stock);
                Object.DestroyImmediate(hairTex);
                Object.DestroyImmediate(lut);
            }
        }

        [Test]
        public void CaptureProperties_NullMainTex_NoFallback_DropsTextureRows_AndWhitensColor()
        {
            var shader = Shader.Find("VRMXT/Samples/TestOverrideBuiltin");
            Assert.IsNotNull(shader, "TestOverrideBuiltin sample shader must be in project");

            var overrideMat = new Material(shader) { name = "PoiTemplate" };
            var lut = new Texture2D(2, 2) { name = "ToonRamp" };
            try
            {
                overrideMat.SetColor("_Color", Color.black);
                overrideMat.SetTexture("_MainTex", null);
                overrideMat.SetTexture("_ShadeTex", lut);

                var props = VrmxtMaterialsOverrideAuthoring.CaptureProperties(overrideMat);

                var texCount = 0;
                VrmxtMaterialProperty color = null;
                for (var i = 0; i < props.Count; i++)
                {
                    var p = props[i];
                    if (p == null)
                    {
                        continue;
                    }

                    if (
                        string.Equals(
                            p.Type,
                            VrmxtMaterialsOverride.TargetTypeTexture,
                            System.StringComparison.Ordinal
                        )
                    )
                    {
                        texCount++;
                    }
                    else if (p.Name == "_Color")
                    {
                        color = p;
                    }
                }

                Assert.AreEqual(
                    0,
                    texCount,
                    "defaults-only textures must drop so ClearUnlisted will not wipe stock"
                );
                Assert.IsNotNull(color);
                Assert.AreEqual(1f, color.VectorValue[0], 1e-5f);
                Assert.AreEqual(1f, color.VectorValue[1], 1e-5f);
                Assert.AreEqual(1f, color.VectorValue[2], 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(overrideMat);
                Object.DestroyImmediate(lut);
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
