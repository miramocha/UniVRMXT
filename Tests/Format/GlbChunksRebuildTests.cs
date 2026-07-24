using System;
using System.Text;
using NUnit.Framework;
using UniVRMXT.Format;

namespace UniVRMXT.Tests.Format
{
    public sealed class GlbChunksRebuildTests
    {
        [Test]
        public void TryRebuild_JsonOnly_ProducesValidGlb()
        {
            const string json = "{\"asset\":{\"version\":\"2.0\"}}";
            Assert.IsTrue(GlbChunks.TryRebuild(json, null, out var glb));
            Assert.IsTrue(GlbChunks.TryExtract(glb, out var extracted, out var bin));
            Assert.AreEqual(json, extracted);
            Assert.IsNull(bin);
            AssertHeaderAndLengths(glb, expectBin: false);
        }

        [Test]
        public void TryRebuild_JsonPlusBin_PreservesBinBytes()
        {
            const string json = "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":5}]}";
            var bin = new byte[] { 1, 2, 3, 4, 5 };
            Assert.IsTrue(GlbChunks.TryRebuild(json, bin, out var glb));
            Assert.IsTrue(GlbChunks.TryExtract(glb, out var extracted, out var extractedBin));
            Assert.AreEqual(json, extracted);
            Assert.IsNotNull(extractedBin);
            // Extract returns padded BIN chunk length; payload prefix must match.
            Assert.That(extractedBin.Length, Is.GreaterThanOrEqualTo(bin.Length));
            for (var i = 0; i < bin.Length; i++)
            {
                Assert.AreEqual(bin[i], extractedBin[i]);
            }

            AssertHeaderAndLengths(glb, expectBin: true);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TryRebuild_JsonPadding_UsesSpaces(int extraBytes)
        {
            var json = "{\"asset\":{\"version\":\"2.0\"}" + new string(' ', extraBytes) + "}";
            // Force specific UTF-8 length modulo by appending ASCII.
            json = "{\"k\":\"" + new string('a', extraBytes) + "\"}";
            Assert.IsTrue(GlbChunks.TryRebuild(json, null, out var glb));
            var jsonUtf8Len = Encoding.UTF8.GetByteCount(json);
            var padded = (jsonUtf8Len + 3) & ~3;
            var padCount = padded - jsonUtf8Len;
            var chunkStart = 12 + 8;
            for (var i = 0; i < padCount; i++)
            {
                Assert.AreEqual((byte)' ', glb[chunkStart + jsonUtf8Len + i]);
            }

            Assert.AreEqual(0, padded % 4);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TryRebuild_BinPadding_UsesZeros(int binLenMod)
        {
            var bin = new byte[binLenMod == 0 ? 4 : binLenMod];
            for (var i = 0; i < bin.Length; i++)
            {
                bin[i] = (byte)(i + 1);
            }

            Assert.IsTrue(GlbChunks.TryRebuild("{}", bin, out var glb));
            var jsonPad = (Encoding.UTF8.GetByteCount("{}") + 3) & ~3;
            var binChunkStart = 12 + 8 + jsonPad + 8;
            var binPad = (bin.Length + 3) & ~3;
            for (var i = bin.Length; i < binPad; i++)
            {
                Assert.AreEqual(0, glb[binChunkStart + i]);
            }
        }

        [Test]
        public void TryRebuild_ExtractRebuildExtract_PreservesBinExactly()
        {
            var originalBin = new byte[] { 9, 8, 7, 6, 5, 4, 3 };
            Assert.IsTrue(GlbChunks.TryRebuild("{\"a\":1}", originalBin, out var glb1));
            Assert.IsTrue(GlbChunks.TryExtract(glb1, out var json, out var bin1));
            Assert.IsTrue(GlbChunks.TryRebuild(json, bin1, out var glb2));
            Assert.IsTrue(GlbChunks.TryExtract(glb2, out _, out var bin2));
            Assert.AreEqual(bin1.Length, bin2.Length);
            for (var i = 0; i < bin1.Length; i++)
            {
                Assert.AreEqual(bin1[i], bin2[i]);
            }
        }

        [Test]
        public void TryRebuild_NullOrEmptyJson_ReturnsFalse()
        {
            Assert.IsFalse(GlbChunks.TryRebuild(null, null, out var glb1));
            Assert.IsNull(glb1);
            Assert.IsFalse(GlbChunks.TryRebuild("", null, out var glb2));
            Assert.IsNull(glb2);
        }

        [Test]
        public void TryRebuild_EmptyBinArray_TreatedAsJsonOnly()
        {
            Assert.IsTrue(GlbChunks.TryRebuild("{}", Array.Empty<byte>(), out var glb));
            Assert.IsTrue(GlbChunks.TryExtract(glb, out _, out var bin));
            Assert.IsNull(bin);
            AssertHeaderAndLengths(glb, expectBin: false);
        }

        [Test]
        public void TryRebuild_DoesNotMutateInputBin()
        {
            var bin = new byte[] { 1, 2, 3 };
            var copy = (byte[])bin.Clone();
            Assert.IsTrue(GlbChunks.TryRebuild("{}", bin, out _));
            CollectionAssert.AreEqual(copy, bin);
        }

        private static void AssertHeaderAndLengths(byte[] glb, bool expectBin)
        {
            Assert.AreEqual((byte)'g', glb[0]);
            Assert.AreEqual((byte)'l', glb[1]);
            Assert.AreEqual((byte)'T', glb[2]);
            Assert.AreEqual((byte)'F', glb[3]);
            Assert.AreEqual(2, ReadUInt32Le(glb, 4));
            Assert.AreEqual((uint)glb.Length, ReadUInt32Le(glb, 8));

            var jsonLen = ReadUInt32Le(glb, 12);
            Assert.AreEqual(0, jsonLen % 4);
            Assert.AreEqual((byte)'J', glb[16]);
            Assert.AreEqual((byte)'S', glb[17]);
            Assert.AreEqual((byte)'O', glb[18]);
            Assert.AreEqual((byte)'N', glb[19]);

            var afterJson = 20 + (int)jsonLen;
            if (!expectBin)
            {
                Assert.AreEqual(glb.Length, afterJson);
                return;
            }

            var binLen = ReadUInt32Le(glb, afterJson);
            Assert.AreEqual(0, binLen % 4);
            Assert.AreEqual((byte)'B', glb[afterJson + 4]);
            Assert.AreEqual((byte)'I', glb[afterJson + 5]);
            Assert.AreEqual((byte)'N', glb[afterJson + 6]);
            Assert.AreEqual(0, glb[afterJson + 7]);
            Assert.AreEqual(glb.Length, afterJson + 8 + (int)binLen);
        }

        private static uint ReadUInt32Le(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }
    }
}
