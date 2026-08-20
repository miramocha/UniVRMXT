using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class VrmcMaterialsMtoonxtStencilRefsTests
    {
        [SetUp]
        public void SetUp()
        {
            VrmcMaterialsMtoonxtStencilRefs.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VrmcMaterialsMtoonxtStencilRefs.Reset();
        }

        [Test]
        public void Acquire_FirstSpan_StartsAtBand()
        {
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.GpuRef(1, 32));
        }

        [Test]
        public void Acquire_SecondInstance_Advances()
        {
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 2));
            Assert.AreEqual(34, VrmcMaterialsMtoonxtStencilRefs.Acquire(2, 1));
        }

        [Test]
        public void Release_RecyclesBand()
        {
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(33, VrmcMaterialsMtoonxtStencilRefs.Acquire(2, 1));
            VrmcMaterialsMtoonxtStencilRefs.Release(1);
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(3, 1));
        }

        [Test]
        public void Acquire_SameInstance_ReplacesLease()
        {
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 2));
            Assert.AreEqual(32, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 1));
            Assert.AreEqual(33, VrmcMaterialsMtoonxtStencilRefs.Acquire(2, 1));
        }

        [Test]
        public void Acquire_SkipsPoiyomiFakeShadow51()
        {
            Assert.AreEqual(52, VrmcMaterialsMtoonxtStencilRefs.Acquire(1, 20));
        }

        [Test]
        public void GpuRef_WithoutBase_KeepsLocal()
        {
            Assert.AreEqual(1, VrmcMaterialsMtoonxtStencilRefs.GpuRef(1, 0));
        }
    }
}
