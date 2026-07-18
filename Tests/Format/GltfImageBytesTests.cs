using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Format
{
    public sealed class GltfImageBytesTests
    {
        // 1x1 RGB PNG (red).
        private static readonly byte[] TinyPng =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0xC9, 0xFE, 0x92, 0xEF, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82,
        };

        [Test]
        public void TryGetTextureImage_ReadsEmbeddedPngFromBinChunk()
        {
            var glb = BuildGlbWithEmbeddedPng(TinyPng);
            Assert.IsTrue(GlbChunks.TryExtract(glb, out var json, out var bin));
            Assert.IsTrue(
                GltfImageBytes.TryGetTextureImage(json, bin, 0, out var imageBytes, out var mime));
            Assert.AreEqual("image/png", mime);
            Assert.AreEqual(TinyPng.Length, imageBytes.Length);
            Assert.AreEqual(0x89, imageBytes[0]);
            Assert.AreEqual(0x50, imageBytes[1]);
        }

        [Test]
        public void VrmxtVfxGlbTextures_DecodesTexture2D()
        {
            var glb = BuildGlbWithEmbeddedPng(TinyPng);
            Assert.IsTrue(VrmxtVfxGlbTextures.TryCreate(glb, out var textures));
            try
            {
                var texture = textures.Get(0) as Texture2D;
                Assert.IsNotNull(texture);
                Assert.AreEqual(1, texture.width);
                Assert.AreEqual(1, texture.height);
                Assert.IsNull(textures.Get(99));
            }
            finally
            {
                textures.Dispose();
            }
        }

        [Test]
        public void TryGetTextureImage_OutOfRange_ReturnsFalse()
        {
            var glb = BuildGlbWithEmbeddedPng(TinyPng);
            Assert.IsTrue(GlbChunks.TryExtract(glb, out var json, out var bin));
            Assert.IsFalse(GltfImageBytes.TryGetTextureImage(json, bin, 1, out _, out _));
        }

        private static byte[] BuildGlbWithEmbeddedPng(byte[] png)
        {
            var json =
                "{" +
                "\"asset\":{\"version\":\"2.0\"}," +
                "\"buffers\":[{\"byteLength\":" + png.Length + "}]," +
                "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + png.Length + "}]," +
                "\"images\":[{\"bufferView\":0,\"mimeType\":\"image/png\",\"name\":\"dot\"}]," +
                "\"textures\":[{\"sampler\":0,\"source\":0}]," +
                "\"samplers\":[{}]" +
                "}";

            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var jsonPad = (jsonBytes.Length + 3) & ~3;
            var jsonChunk = new byte[jsonPad];
            Buffer.BlockCopy(jsonBytes, 0, jsonChunk, 0, jsonBytes.Length);
            for (var i = jsonBytes.Length; i < jsonPad; i++)
            {
                jsonChunk[i] = (byte)' ';
            }

            var binPad = (png.Length + 3) & ~3;
            var binChunk = new byte[binPad];
            Buffer.BlockCopy(png, 0, binChunk, 0, png.Length);

            var total = 12 + 8 + jsonPad + 8 + binPad;
            using var ms = new MemoryStream(total);
            using var bw = new BinaryWriter(ms);
            bw.Write(Encoding.ASCII.GetBytes("glTF"));
            bw.Write(2);
            bw.Write(total);
            bw.Write(jsonPad);
            bw.Write(Encoding.ASCII.GetBytes("JSON"));
            bw.Write(jsonChunk);
            bw.Write(binPad);
            bw.Write(new byte[] { (byte)'B', (byte)'I', (byte)'N', 0 });
            bw.Write(binChunk);
            return ms.ToArray();
        }
    }
}
