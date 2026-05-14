// Tests covering chunk validation, mutation versioning and registry concurrency basics.
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VoxeLaboratory.ChunkSystem;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.Tests
{
    [TestFixture]
    public class ChunkSystemTests
    {
        [Test]
        public void Chunk_Index_Is_Linear_X_Major()
        {
            Assert.That(Chunk.IndexOf(0, 0, 0), Is.EqualTo(0));
            Assert.That(Chunk.IndexOf(1, 0, 0), Is.EqualTo(1));
            Assert.That(Chunk.IndexOf(0, 1, 0), Is.EqualTo(Chunk.Size));
            Assert.That(Chunk.IndexOf(0, 0, 1), Is.EqualTo(Chunk.Size * Chunk.Size));
            Assert.That(Chunk.IndexOf(Chunk.Size - 1, Chunk.Size - 1, Chunk.Size - 1),
                Is.EqualTo(Chunk.Volume - 1));
        }

        [TestCase(-1, 0, 0)]
        [TestCase(0, Chunk.Size, 0)]
        [TestCase(0, 0, Chunk.Size + 5)]
        public void Chunk_Index_Rejects_Out_Of_Range(int x, int y, int z)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Chunk.IndexOf(x, y, z));
        }

        [Test]
        public void SetVoxel_Updates_Storage_And_Bumps_Version()
        {
            var chunk = new Chunk(new ChunkCoordinate(0, 0, 0));
            long initial = chunk.Version;
            var stone = new Voxel(materialId: 1, density: 1f, hardness: 0.8f);

            chunk.SetVoxel(2, 3, 4, stone);

            Assert.That(chunk.GetVoxel(2, 3, 4), Is.EqualTo(stone));
            Assert.That(chunk.Version, Is.EqualTo(initial + 1));
        }

        [Test]
        public void SetVoxel_Is_Idempotent_When_Value_Is_Unchanged()
        {
            var chunk = new Chunk(new ChunkCoordinate(0, 0, 0));
            var stone = new Voxel(1, 1f, 0.8f);
            chunk.SetVoxel(0, 0, 0, stone);
            long version = chunk.Version;

            chunk.SetVoxel(0, 0, 0, stone);

            Assert.That(chunk.Version, Is.EqualTo(version));
        }

        [Test]
        public void Clear_Resets_Voxels_And_Bumps_Version()
        {
            var chunk = new Chunk(new ChunkCoordinate(0, 0, 0));
            chunk.SetVoxel(1, 1, 1, new Voxel(1, 1f, 0.5f));
            long version = chunk.Version;

            chunk.Clear();

            Assert.That(chunk.GetVoxel(1, 1, 1), Is.EqualTo(default(Voxel)));
            Assert.That(chunk.Version, Is.GreaterThan(version));
        }

        [Test]
        public void CopyTo_Validates_Destination_Length()
        {
            var chunk = new Chunk(new ChunkCoordinate(0, 0, 0));
            Assert.Throws<ArgumentNullException>(() => chunk.CopyTo(null));
            Assert.Throws<ArgumentException>(() => chunk.CopyTo(new Voxel[Chunk.Volume - 1]));
        }

        [Test]
        public void Registry_GetOrCreate_Is_Stable_For_Same_Coordinate()
        {
            var registry = new ChunkRegistry();
            var coord = new ChunkCoordinate(1, 2, 3);
            var first = registry.GetOrCreate(coord);
            var second = registry.GetOrCreate(coord);
            Assert.That(second, Is.SameAs(first));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Registry_Remove_Drops_Reference()
        {
            var registry = new ChunkRegistry();
            var coord = new ChunkCoordinate(0, 0, 0);
            registry.GetOrCreate(coord);

            Assert.That(registry.Remove(coord), Is.True);
            Assert.That(registry.TryGet(coord, out _), Is.False);
            Assert.That(registry.Remove(coord), Is.False);
        }

        [Test]
        public void Registry_GetOrCreate_Is_Safe_Under_Concurrent_Callers()
        {
            var registry = new ChunkRegistry();
            var coord = new ChunkCoordinate(7, 8, 9);

            var tasks = new Task<Chunk>[16];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = Task.Run(() => registry.GetOrCreate(coord));

            Task.WaitAll(tasks);

            Chunk reference = tasks[0].Result;
            for (int i = 1; i < tasks.Length; i++)
                Assert.That(tasks[i].Result, Is.SameAs(reference));
            Assert.That(registry.Count, Is.EqualTo(1));
        }
    }
}
