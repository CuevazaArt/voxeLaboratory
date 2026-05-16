// =====================================================================
//  WorldSerializerTests.cs
//  VoxelLab :: Tests
//
//  Round-trip y validaciones del codec RLE de VoxelWorld.
// =====================================================================
#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Tests
{
    public class WorldSerializerTests
    {
        private static VoxelWorld BuildSampleWorld()
        {
            var w = new VoxelWorld(16, 6);
            // Algunos voxels en chunks diferentes.
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.SetVoxel(1, 0, 0, new Voxel((byte)MaterialId.Iron, 1f, 0.6f));
            w.SetVoxel(20, 5, 5, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.SetVoxel(0, 0, 30, new Voxel((byte)MaterialId.Rock, 0.9f, 0.5f));
            return w;
        }

        [Test]
        public void RoundTrip_PreservesVoxels()
        {
            var src = BuildSampleWorld();
            byte[] bytes = WorldSerializer.SaveToBytes(src);

            var dst = new VoxelWorld(16, 6);
            WorldSerializer.LoadFromBytes(dst, bytes);

            // Comparar voxels muestreados.
            Assert.AreEqual(src.GetVoxel(0, 0, 0).material, dst.GetVoxel(0, 0, 0).material);
            Assert.AreEqual(src.GetVoxel(1, 0, 0).material, dst.GetVoxel(1, 0, 0).material);
            Assert.AreEqual(src.GetVoxel(20, 5, 5).material, dst.GetVoxel(20, 5, 5).material);
            Assert.AreEqual(src.GetVoxel(0, 0, 30).material, dst.GetVoxel(0, 0, 30).material);
            Assert.AreEqual(src.GetVoxel(15, 15, 15).material, dst.GetVoxel(15, 15, 15).material);
        }

        [Test]
        public void RoundTrip_PreservesChunkCount()
        {
            var src = BuildSampleWorld();
            int countSrc = 0; foreach (var _ in src.AllChunks()) countSrc++;

            byte[] bytes = WorldSerializer.SaveToBytes(src);

            var dst = new VoxelWorld(16, 6);
            WorldSerializer.LoadFromBytes(dst, bytes);

            int countDst = 0; foreach (var _ in dst.AllChunks()) countDst++;
            Assert.AreEqual(countSrc, countDst);
        }

        [Test]
        public void Load_RaisesOnChunkDirty_PerChunk()
        {
            var src = BuildSampleWorld();
            byte[] bytes = WorldSerializer.SaveToBytes(src);

            var dst = new VoxelWorld(16, 6);
            int dirty = 0;
            dst.OnChunkDirty += _ => dirty++;
            WorldSerializer.LoadFromBytes(dst, bytes);

            int expected = 0; foreach (var _ in dst.AllChunks()) expected++;
            Assert.AreEqual(expected, dirty);
        }

        [Test]
        public void Load_RejectsBadMagic()
        {
            var w = new VoxelWorld(16, 6);
            byte[] junk = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x00 };
            Assert.Throws<InvalidDataException>(() => WorldSerializer.LoadFromBytes(w, junk));
        }

        [Test]
        public void Load_RejectsMismatchedChunkSize()
        {
            var src = new VoxelWorld(16, 6);
            src.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            byte[] bytes = WorldSerializer.SaveToBytes(src);

            var dst = new VoxelWorld(32, 6);
            Assert.Throws<InvalidDataException>(() => WorldSerializer.LoadFromBytes(dst, bytes));
        }

        [Test]
        public void Save_ProducesValidHeader()
        {
            var w = new VoxelWorld(16, 6);
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            byte[] bytes = WorldSerializer.SaveToBytes(w);

            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                Assert.AreEqual(WorldSerializer.MAGIC, br.ReadUInt32());
                Assert.AreEqual(WorldSerializer.VERSION, br.ReadUInt16());
                Assert.AreEqual((ushort)16, br.ReadUInt16());
            }
        }
    }
}
#endif
