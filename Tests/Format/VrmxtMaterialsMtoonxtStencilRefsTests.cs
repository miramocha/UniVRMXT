using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmxtMaterialsMtoonxtStencilRefsTests
    {
        [SetUp]
        public void SetUp()
        {
            VrmxtMaterialsMtoonxtStencilRefs.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VrmxtMaterialsMtoonxtStencilRefs.Reset();
        }

        [Test]
        public void Acquire_FirstSpan_StartsAtBand()
        {
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.GpuRef(1, 32));
        }

        [Test]
        public void Acquire_SecondInstance_Advances()
        {
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 2));
            Assert.AreEqual(34, VrmxtMaterialsMtoonxtStencilRefs.Acquire(2, 1));
        }

        [Test]
        public void Release_RecyclesBand()
        {
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(33, VrmxtMaterialsMtoonxtStencilRefs.Acquire(2, 1));
            VrmxtMaterialsMtoonxtStencilRefs.Release(1);
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(3, 1));
        }

        [Test]
        public void Acquire_SameInstance_ReplacesLease()
        {
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 2));
            Assert.AreEqual(32, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(33, VrmxtMaterialsMtoonxtStencilRefs.Acquire(2, 1));
        }

        [Test]
        public void Acquire_SkipsPoiyomiFakeShadow51()
        {
            Assert.AreEqual(52, VrmxtMaterialsMtoonxtStencilRefs.Acquire(1, 20));
        }

        [Test]
        public void GpuRef_WithoutBase_KeepsLocal()
        {
            Assert.AreEqual(1, VrmxtMaterialsMtoonxtStencilRefs.GpuRef(1, 0));
        }
    }
}
