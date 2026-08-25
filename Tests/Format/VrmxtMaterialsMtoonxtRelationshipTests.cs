using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsMtoonxtRelationshipTests
    {
        private const string RootJson = @"{
          ""materials"": [{}, {}, {}],
          ""extensions"": {
            ""VRMXT_materials_mtoonxt"": {
              ""specVersion"": ""1.0"",
              ""stencilRelationships"": [{
                ""writers"": [1],
                ""readers"": [0, 2],
                ""comparison"": ""inside"",
                ""showWritersThroughOccluders"": true,
                ""writersSelfOcclude"": false,
                ""writersWriteDepth"": false,
                ""readerDepthTest"": ""always""
              }]
            }
          }
        }";

        [Test]
        public void ParseAndSerialize_RoundTripsPortableRelationship()
        {
            Assert.IsTrue(
                VrmxtMaterialsMtoonxtRelationships.TryParseRoot(
                    JToken.Parse(RootJson),
                    3,
                    out var relationships
                )
            );
            Assert.AreEqual(1, relationships.Count);
            var relationship = relationships[0];
            Assert.AreEqual("inside", relationship.Comparison);
            Assert.IsTrue(relationship.ShowWritersThroughOccluders);
            Assert.IsFalse(relationship.WritersSelfOcclude);
            Assert.IsFalse(relationship.WritersWriteDepth);
            Assert.AreEqual("always", relationship.ReaderDepthTest);

            var serialized = VrmxtMaterialsMtoonxtRelationships.ToJson(relationships);
            Assert.That(serialized, Does.Contain("stencilRelationships"));
            Assert.That(serialized, Does.Contain("showWritersThroughOccluders"));
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
                  ""stencilRelationships"": [
                    {""writers"": [0], ""readers"": [0]},
                    {""writers"": [0], ""readers"": [1]}
                  ]
                }
              }
            }";

            Assert.IsTrue(
                VrmxtMaterialsMtoonxtRelationships.TryParseRoot(
                    JToken.Parse(json),
                    2,
                    out var relationships
                )
            );
            Assert.AreEqual(1, relationships.Count);
        }

        [Test]
        public void Compile_M02_ProducesDualSubjectPasses()
        {
            var relationship = Relationship(showThrough: true);
            var plan = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new[] { relationship },
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
            var relationship = new VrmxtMaterialsMtoonxtRelationship(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                writersOnlyOutsideReaders: true
            );
            var plan = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new[] { relationship },
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
            var relationship = new VrmxtMaterialsMtoonxtRelationship(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                ignoreOccludedReaderAreas: false
            );
            var plan = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new[] { relationship },
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
            var relationship = new VrmxtMaterialsMtoonxtRelationship(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: true,
                writersSelfOcclude: false,
                writersWriteDepth: false
            );
            var plan = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new[] { relationship },
                1
            )[0];

            Assert.IsFalse(plan.WriterPrimary.CullBack);
            Assert.IsFalse(plan.WriterPrimary.ZWrite);
            Assert.IsFalse(plan.WriterSecondary.CullBack);
            Assert.IsFalse(plan.WriterSecondary.ZWrite);
        }

        private static VrmxtMaterialsMtoonxtRelationship Relationship(bool showThrough)
        {
            return new VrmxtMaterialsMtoonxtRelationship(
                new[] { 1 },
                new[] { 0 },
                showWritersThroughOccluders: showThrough
            );
        }
    }
}
