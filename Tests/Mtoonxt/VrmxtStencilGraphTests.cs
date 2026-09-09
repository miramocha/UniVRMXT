using System.Collections.Generic;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Mtoonxt;

namespace UniVRMXT.Tests.Mtoonxt
{
    public sealed class VrmxtStencilGraphTests
    {
        [Test]
        public void SharedReaders_UnionOnlyAuthoredEdges_IndependentOfOrder()
        {
            var rows = new List<VrmxtMaterialsMtoonxtRelationship>
            {
                new VrmxtMaterialsMtoonxtRelationship(new[] { 0 }, new[] { 2, 4 }),
                new VrmxtMaterialsMtoonxtRelationship(new[] { 1 }, new[] { 2, 3 }),
                new VrmxtMaterialsMtoonxtRelationship(new[] { 3 }, new[] { 4 }),
            };
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var plans = VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(rows, 1);
                Assert.IsTrue(VrmxtStencilGraph.NeedsCoverage(plans));
                var readers = VrmxtStencilGraph.Readers(plans);
                CollectionAssert.AreEquivalent(new[] { 0, 1 }, readers[2]);
                CollectionAssert.AreEquivalent(new[] { 1 }, readers[3]);
                CollectionAssert.AreEquivalent(new[] { 0, 3 }, readers[4]);
                Assert.IsFalse(readers.ContainsKey(0));
                Assert.IsFalse(readers.ContainsKey(1));
                rows.Reverse();
            }
        }

        [Test]
        public void CycleDepthStage_IncludesUpstreamWriters_ButNotOtherSide()
        {
            var rows = new[]
            {
                new VrmxtMaterialsMtoonxtRelationship(new[] { 0 }, new[] { 1, 3 }),
                new VrmxtMaterialsMtoonxtRelationship(new[] { 1 }, new[] { 2, 3 }),
                new VrmxtMaterialsMtoonxtRelationship(new[] { 2 }, new[] { 1, 3 }),
                new VrmxtMaterialsMtoonxtRelationship(new[] { 4 }, new[] { 5 }),
            };
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, VrmxtStencilGraph.DepthPeers(
                VrmxtMaterialsMtoonxtRelationshipCompiler.Compile(rows, 1)));
        }

        [Test]
        public void Render_SharedAndDualRoleGraphs_UsesActualPixels()
        {
            Assert.IsNotNull(VrmxtStencilGraphRenderProbe.Run());
        }
    }
}
