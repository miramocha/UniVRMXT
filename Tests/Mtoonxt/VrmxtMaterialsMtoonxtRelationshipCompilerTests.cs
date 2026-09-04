using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtMaterialsMtoonxtRelationshipCompilerTests
    {
        [TestCase("M01", false, false, false, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=always/replace/lessEqual/1/0;S=-;R=notEqual/keep/lessEqual/1/0;WS=1;RS=0;C=None")]
        [TestCase("M02", true, false, false, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=notEqual/keep/lessEqual/1/1;S=equal/keep/always/1/1;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=None")]
        [TestCase("M03", false, true, false, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=equal/keep/lessEqual/1/1;S=-;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=None")]
        [TestCase("M04", true, true, false, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=equal/keep/always/1/1;S=-;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=None")]
        [TestCase("M05", false, false, true, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=notEqual/keep/lessEqual/1/1;S=-;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=None")]
        [TestCase("M06", true, false, true, true, true, true, true, "outside", "lessEqual", "lessEqual", "W=notEqual/keep/lessEqual/1/1;S=-;R=-;WS=0;RS=0;C=FullSceneWithoutWriters")]
        [TestCase("M07", true, false, false, false, true, false, true, "outside", "lessEqual", "lessEqual", "W=notEqual/keep/lessEqual/0/0;S=equal/keep/always/0/0;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=None")]
        [TestCase("M08", true, false, false, true, false, true, true, "outside", "lessEqual", "lessEqual", "W=notEqual/keep/lessEqual/1/1;S=equal/keep/always/1/1;R=always/replace/lessEqual/1/0;WS=0;RS=1;C=FullReaderSilhouette")]
        [TestCase("M09", false, false, false, true, true, false, true, "outside", "lessEqual", "lessEqual", "W=always/replace/lessEqual/0/0;S=-;R=notEqual/keep/lessEqual/1/0;WS=1;RS=0;C=None")]
        [TestCase("M10", false, false, false, true, true, true, false, "outside", "lessEqual", "lessEqual", "W=always/replace/lessEqual/1/0;S=-;R=notEqual/keep/lessEqual/0/0;WS=1;RS=0;C=None")]
        [TestCase("A01", false, false, false, true, true, true, true, "inside", "lessEqual", "lessEqual", "W=always/replace/lessEqual/1/0;S=-;R=equal/keep/lessEqual/1/0;WS=1;RS=0;C=None")]
        [TestCase("A02", false, false, false, true, true, true, true, "outside", "always", "lessEqual", "W=always/replace/always/1/0;S=-;R=notEqual/keep/lessEqual/1/0;WS=1;RS=0;C=None")]
        [TestCase("A03", false, false, false, true, true, true, true, "outside", "lessEqual", "always", "W=always/replace/lessEqual/1/0;S=-;R=notEqual/keep/always/1/0;WS=1;RS=0;C=None")]
        public void Compile_ConfirmedMatrixRow_MatchesUnityPassPlan(
            string row,
            bool showThrough,
            bool insideOnly,
            bool outsideOnly,
            bool selfOcclude,
            bool ignoreOccludedReaders,
            bool writersWriteDepth,
            bool readersWriteDepth,
            string comparison,
            string writerDepth,
            string readerDepth,
            string expected
        )
        {
            var relationship = new VrmxtMaterialsMtoonxtRelationship(
                new[] { 0 },
                new[] { 1 },
                comparison,
                showThrough,
                insideOnly,
                outsideOnly,
                selfOcclude,
                ignoreOccludedReaders,
                writersWriteDepth,
                readersWriteDepth,
                writerDepth,
                readerDepth
            );

            var plans = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new List<VrmxtMaterialsMtoonxtRelationship> { relationship },
                1
            );

            Assert.AreEqual(1, plans.Count, row);
            Assert.AreEqual(expected, Snapshot(plans[0]), row);
        }

        [Test]
        public void Compile_SameReadersAndPresentation_CoalescesWriters()
        {
            var first = Relationship(new[] { 0 }, new[] { 4, 5 });
            var second = Relationship(new[] { 1 }, new[] { 4, 5 });
            var third = Relationship(new[] { 2, 3 }, new[] { 4, 5 });

            var plans = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(
                new List<VrmxtMaterialsMtoonxtRelationship> { first, second, third },
                1
            );

            Assert.AreEqual(1, plans.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, plans[0].Source.Writers);
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, plans[0].Source.Readers);
        }

        private static VrmxtMaterialsMtoonxtRelationship Relationship(
            IReadOnlyList<int> writers,
            IReadOnlyList<int> readers
        )
        {
            return new VrmxtMaterialsMtoonxtRelationship(
                writers,
                readers,
                "outside",
                showWritersThroughOccluders: true,
                writersOnlyInsideReaders: false,
                writersOnlyOutsideReaders: false,
                writersSelfOcclude: true,
                ignoreOccludedReaderAreas: true,
                writersWriteDepth: true,
                readersWriteDepth: true,
                writerDepthTest: "lessEqual",
                readerDepthTest: "lessEqual"
            );
        }

        private static string Snapshot(VrmxtMtoonxtRelationshipPlan plan)
        {
            return "W=" + Pass(plan.WriterPrimary)
                + ";S=" + Pass(plan.WriterSecondary)
                + ";R=" + Pass(plan.Reader)
                + ";WS=" + Bool(plan.WritersStampMask)
                + ";RS=" + Bool(plan.ReadersStampMask)
                + ";C=" + plan.CoverageMode;
        }

        private static string Pass(VrmxtMtoonxtRelationshipPass pass)
        {
            if (pass == null)
            {
                return "-";
            }

            return pass.Comp
                + "/" + pass.Pass
                + "/" + pass.ZTest
                + "/" + Bool(pass.ZWrite)
                + "/" + Bool(pass.CullBack);
        }

        private static string Bool(bool value)
        {
            return value ? "1" : "0";
        }
    }
}
