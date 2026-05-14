// Tests covering SVO subdivision, sampling and bottom‑up compaction.
using System;
using NUnit.Framework;
using VoxeLaboratory.SVO;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.Tests
{
    [TestFixture]
    public class SvoTests
    {
        [TestCase(0)]
        [TestCase(SparseVoxelOctree.AbsoluteMaxDepth + 1)]
        public void Constructor_Rejects_Out_Of_Range_Depth(int depth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SparseVoxelOctree(depth));
        }

        [Test]
        public void Empty_Tree_Reports_Empty_Voxels()
        {
            var tree = new SparseVoxelOctree(maxDepth: 4);
            Assert.That(tree.Extent, Is.EqualTo(16));
            Assert.That(tree.GetVoxel(new VoxelCoordinate(0, 0, 0)), Is.EqualTo(Voxel.Empty));
            Assert.That(tree.GetVoxel(new VoxelCoordinate(15, 15, 15)), Is.EqualTo(Voxel.Empty));
        }

        [Test]
        public void GetVoxel_Rejects_Coordinates_Outside_Extent()
        {
            var tree = new SparseVoxelOctree(maxDepth: 3); // extent = 8
            Assert.Throws<ArgumentOutOfRangeException>(() => tree.GetVoxel(new VoxelCoordinate(8, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => tree.GetVoxel(new VoxelCoordinate(0, -1, 0)));
        }

        [Test]
        public void SetVoxel_Round_Trips_Through_Subdivision()
        {
            var tree = new SparseVoxelOctree(maxDepth: 6);
            var stone = new Voxel(materialId: 1, density: 1f, hardness: 0.8f);
            var coord = new VoxelCoordinate(5, 17, 42);

            tree.SetVoxel(coord, stone);

            Assert.That(tree.GetVoxel(coord), Is.EqualTo(stone));
            // Voxels outside the path are left untouched (still empty).
            Assert.That(tree.GetVoxel(new VoxelCoordinate(0, 0, 0)), Is.EqualTo(Voxel.Empty));
            Assert.That(tree.GetVoxel(new VoxelCoordinate(63, 63, 63)), Is.EqualTo(Voxel.Empty));
        }

        [Test]
        public void Subdividing_A_Leaf_Allocates_Eight_Children()
        {
            var leaf = new SvoNode(depth: 0, sample: Voxel.Empty);
            Assert.That(leaf.IsLeaf, Is.True);

            // Forced through SetVoxel which is the only public path.
            var tree = new SparseVoxelOctree(maxDepth: 1);
            tree.SetVoxel(new VoxelCoordinate(1, 1, 1), new Voxel(1, 1f, 0.5f));

            Assert.That(tree.Root.IsLeaf, Is.False);
            for (int i = 0; i < SvoOctants.Count; i++)
                Assert.That(tree.Root.GetChild(i), Is.Not.Null);
        }

        [Test]
        public void Compact_Collapses_Uniform_Subtrees()
        {
            var tree = new SparseVoxelOctree(maxDepth: 3); // extent = 8
            var stone = new Voxel(1, 1f, 0.8f);

            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    for (int z = 0; z < 8; z++)
                        tree.SetVoxel(new VoxelCoordinate(x, y, z), stone);

            // Every node was subdivided to depth 3; the tree currently has
            // 1 + 8 + 64 + 512 nodes.  Compaction should collapse all the
            // way back to the root.
            int freed = tree.Compact();

            Assert.That(freed, Is.GreaterThan(0));
            Assert.That(tree.Root.IsLeaf, Is.True);
            Assert.That(tree.Root.Sample, Is.EqualTo(stone));
            Assert.That(tree.GetVoxel(new VoxelCoordinate(3, 5, 7)), Is.EqualTo(stone));
        }

        [Test]
        public void Compact_Leaves_Heterogeneous_Subtrees_Untouched()
        {
            var tree = new SparseVoxelOctree(maxDepth: 2); // extent = 4
            tree.SetVoxel(new VoxelCoordinate(0, 0, 0), new Voxel(1, 1f, 0.5f));
            tree.SetVoxel(new VoxelCoordinate(3, 3, 3), new Voxel(2, 1f, 0.5f));

            tree.Compact();

            Assert.That(tree.Root.IsLeaf, Is.False);
            Assert.That(tree.GetVoxel(new VoxelCoordinate(0, 0, 0)).MaterialId, Is.EqualTo(1));
            Assert.That(tree.GetVoxel(new VoxelCoordinate(3, 3, 3)).MaterialId, Is.EqualTo(2));
        }

        [Test]
        public void OctantIndex_Is_The_Canonical_Bit_Packing()
        {
            Assert.That(SvoOctants.OctantIndex(0, 0, 0), Is.EqualTo(0));
            Assert.That(SvoOctants.OctantIndex(1, 0, 0), Is.EqualTo(1));
            Assert.That(SvoOctants.OctantIndex(0, 1, 0), Is.EqualTo(2));
            Assert.That(SvoOctants.OctantIndex(0, 0, 1), Is.EqualTo(4));
            Assert.That(SvoOctants.OctantIndex(1, 1, 1), Is.EqualTo(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => SvoOctants.OctantIndex(2, 0, 0));
        }
    }
}
