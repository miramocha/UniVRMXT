using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsMtoonxtStencilTests
    {
        private const string RootJson = @"{
          ""materials"": [{}, {}, {}],
          ""extensions"": {
            ""VRMXT_materials_mtoonxt"": {
              ""specVersion"": ""1.0"",
              ""stencil"": [{
                ""writers"": [1],
                ""readers"": [0, 2],
                ""comparison"": ""inside"",
                ""showWritersThroughOccluders"": true,
                ""writersSelfOcclude"": false,
                ""writersWriteColor"": false,
                ""writersWriteDepth"": false,
                ""readerDepthTest"": ""always""
              }]
            }
          }
        }";

        [Test]
        public void Parse_RetiredRootName_IsNotAnAlias()
        {
            var old = RootJson.Replace("\"stencil\"", "\"stencilRelationships\"");
            Assert.IsTrue(VrmxtMaterialsMtoonxtStencils.TryParseRoot(
                JToken.Parse(old), 3, out var stencils));
            Assert.AreEqual(0, stencils.Count);
        }

        [Test]
        public void ParseAndSerialize_RoundTripsPortableStencil()
        {
            Assert.IsTrue(
                VrmxtMaterialsMtoonxtStencils.TryParseRoot(
                    JToken.Parse(RootJson),
                    3,
                    out var stencils
                )
            );
            Assert.AreEqual(1, stencils.Count);
            var stencil = stencils[0];
            Assert.AreEqual("inside", stencil.Comparison);
            Assert.IsTrue(stencil.ShowWritersThroughOccluders);
            Assert.IsFalse(stencil.WritersSelfOcclude);
            Assert.IsFalse(stencil.WritersWriteColor);
            Assert.IsFalse(stencil.WritersWriteDepth);
            Assert.AreEqual("always", stencil.ReaderDepthTest);

            var serialized = VrmxtMaterialsMtoonxtStencils.ToJson(stencils);
            Assert.That(serialized, Does.Contain("stencil"));
            Assert.That(serialized, Does.Contain("showWritersThroughOccluders"));
            Assert.That(serialized, Does.Contain("writersWriteColor"));
            Assert.That(serialized, Does.Not.Contain("readersWriteDepth"));
        }

        [Test]
        public void Parse_InvalidEntry_IsSkippedWithoutRejectingRoot()
        {
            const string json = @"{
              ""materials"": [{}, {}],
              ""extensions"": {
                ""VRMXT_materials_mtoonxt"": {
                  ""specVersion"": ""1.0"",
                  ""stencil"": [
                    {""writers"": [0], ""readers"": [0]},
                    {""writers"": [0], ""readers"": [1]}
                  ]
                }
              }
            }";

            Assert.IsTrue(
                VrmxtMaterialsMtoonxtStencils.TryParseRoot(
                    JToken.Parse(json),
                    2,
                    out var stencils
                )
            );
            Assert.AreEqual(1, stencils.Count);
        }

        [Test]
        public void Compile_M02_ProducesDualSubjectPasses()
        {
            var stencil = Stencil(showThrough: true);
            var plan = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[] { stencil },
                1
            )[0];

            Assert.AreEqual("notEqual", plan.WriterPrimary.Comp);
            Assert.AreEqual("lessEqual", plan.WriterPrimary.ZTest);
            Assert.AreEqual("equal", plan.WriterSecondary.Comp);
            Assert.AreEqual("always", plan.WriterSecondary.ZTest);
            Assert.IsTrue(plan.ReadersStampMask);
        }

        [Test]
        public void Compile_M06_ProducesBackgroundCoveragePass()
        {
            var stencil = new VrmxtMaterialsMtoonxtStencil(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                writersOnlyOutsideReaders: true
            );
            var plan = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[] { stencil },
                1
            )[0];

            Assert.AreEqual(
                VrmxtMtoonxtCoverageMode.FullSceneWithoutWriters,
                plan.CoverageMode
            );
            Assert.AreEqual("notEqual", plan.WriterPrimary.Comp);
            Assert.IsNull(plan.Reader);
            Assert.IsNull(plan.WriterSecondary);
        }

        [Test]
        public void Compile_M08_ProducesFullReaderSilhouetteAndDualPasses()
        {
            var stencil = new VrmxtMaterialsMtoonxtStencil(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                ignoreOccludedReaderAreas: false
            );
            var plan = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[] { stencil },
                1
            )[0];

            Assert.AreEqual(
                VrmxtMtoonxtCoverageMode.FullReaderSilhouette,
                plan.CoverageMode
            );
            Assert.IsNotNull(plan.WriterSecondary);
        }

        [Test]
        public void Compile_M07_PreservesIndependentSelfOcclusionAndDepth()
        {
            var stencil = new VrmxtMaterialsMtoonxtStencil(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                writersSelfOcclude: false,
                writersWriteDepth: false
            );
            var plan = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[] { stencil },
                1
            )[0];

            Assert.IsFalse(plan.WriterPrimary.CullBack);
            Assert.IsFalse(plan.WriterPrimary.ZWrite);
            Assert.IsFalse(plan.WriterSecondary.CullBack);
            Assert.IsFalse(plan.WriterSecondary.ZWrite);
        }

        [Test]
        public void Compile_ColorlessWriter_PreservesStencilAndSuppressesWriterColor()
        {
            var stencil = new VrmxtMaterialsMtoonxtStencil(
                new[] { 1 },
                new[] { 0 },
                writersWriteColor: false
            );
            var plan = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[] { stencil },
                1
            )[0];

            Assert.IsFalse(plan.WriterPrimary.WriteColor);
            Assert.IsTrue(plan.Reader.WriteColor);
            Assert.IsTrue(plan.WritersStampMask);
        }

        [Test]
        public void Compile_EquivalentWriters_CoalescesReaderMasks()
        {
            var plans = VrmxtMaterialsMtoonxtStencilCompiler.Compile(
                new[]
                {
                    new VrmxtMaterialsMtoonxtStencil(
                        new[] { 0 },
                        new[] { 1 },
                        writersWriteColor: false
                    ),
                    new VrmxtMaterialsMtoonxtStencil(
                        new[] { 0 },
                        new[] { 2 },
                        writersWriteColor: false
                    ),
                },
                1
            );

            Assert.AreEqual(1, plans.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, plans[0].Source.Readers);
        }

        private static VrmxtMaterialsMtoonxtStencil Stencil(bool showThrough)
        {
            return new VrmxtMaterialsMtoonxtStencil(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: showThrough
            );
        }
    }
}
