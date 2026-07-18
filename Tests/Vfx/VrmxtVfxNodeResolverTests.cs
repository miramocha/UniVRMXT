using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UniVRMXT.Format;
using UniVRMXT.Vfx;
using UnityEngine;

namespace UniVRMXT.Tests.Vfx
{
    public sealed class VrmxtVfxNodeResolverTests
    {
        [Test]
        public void TryReadNodeNames_AppliesUniVrmEmptyNameFallback()
        {
            const string json = @"
            {
              ""nodes"": [
                { ""name"": ""hips"" },
                { },
                { ""name"": ""a/b"" }
              ]
            }";

            Assert.IsTrue(VrmxtVfxNodeResolver.TryReadNodeNames(json, out var names));
            Assert.AreEqual(3, names.Count);
            Assert.AreEqual("hips", names[0]);
            Assert.AreEqual("nodeIndex_1", names[1]);
            Assert.AreEqual("a_b", names[2]);
        }

        [Test]
        public void ResolveByName_FindsDescendant()
        {
            var root = new GameObject("root");
            var head = new GameObject("head");
            head.transform.SetParent(root.transform, false);

            try
            {
                var names = new List<string> { "root", "head" };
                Assert.AreSame(
                    head.transform,
                    VrmxtVfxNodeResolver.ResolveByName(root.transform, names, 1));
                Assert.IsNull(
                    VrmxtVfxNodeResolver.ResolveByName(root.transform, names, 99));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GlbJson_TryExtract_ReadsJsonChunk()
        {
            const string payload =
                "{\"asset\":{\"version\":\"2.0\"},\"nodes\":[{\"name\":\"head\"}]}";
            var glb = BuildMinimalGlb(payload);

            Assert.IsTrue(GlbJson.TryExtract(glb, out var json));
            Assert.IsTrue(json.Contains("\"head\""));
            Assert.IsTrue(VrmxtVfxNodeResolver.TryReadNodeNames(json, out var names));
            Assert.AreEqual("head", names[0]);
        }

        private static byte[] BuildMinimalGlb(string json)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var paddedLength = (jsonBytes.Length + 3) & ~3;
            var chunk = new byte[paddedLength];
            System.Array.Copy(jsonBytes, chunk, jsonBytes.Length);
            for (var i = jsonBytes.Length; i < paddedLength; i++)
            {
                chunk[i] = (byte)' ';
            }

            var totalLength = 12 + 8 + paddedLength;
            var glb = new byte[totalLength];
            glb[0] = (byte)'g';
            glb[1] = (byte)'l';
            glb[2] = (byte)'T';
            glb[3] = (byte)'F';
            WriteUInt32(glb, 4, 2);
            WriteUInt32(glb, 8, (uint)totalLength);
            WriteUInt32(glb, 12, (uint)paddedLength);
            glb[16] = (byte)'J';
            glb[17] = (byte)'S';
            glb[18] = (byte)'O';
            glb[19] = (byte)'N';
            System.Array.Copy(chunk, 0, glb, 20, paddedLength);
            return glb;
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)((value >> 8) & 0xff);
            data[offset + 2] = (byte)((value >> 16) & 0xff);
            data[offset + 3] = (byte)((value >> 24) & 0xff);
        }
    }
}
