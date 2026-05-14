// File: Assets/Scripts/SVO/SparseVoxelOctree.cs
//
// Purpose
//   Top‑level Sparse Voxel Octree used for planetary LOD.  The tree covers
//   a cubic region of edge length `2^MaxDepth` voxels and only allocates
//   children where the volume contains heterogeneous samples.
//
// Invariants
//   * `MaxDepth` is in [1, 20].  20 is the practical upper bound: a single
//     axis would otherwise exceed 2^31.
//   * The world volume is [0, 2^MaxDepth) on each axis.  Voxel coordinates
//     outside the volume are rejected.
//   * `Subdivide` and `Sample` are deterministic: identical input always
//     yields the same tree shape regardless of insertion order.
//   * The tree is single‑threaded.  Callers that need concurrent access
//     must serialise externally.
//
// Dependencies
//   `VoxeLaboratory.VoxelCore` (Voxel), `VoxeLaboratory.SVO.SvoNode`.
//
// Example
//   var tree = new SparseVoxelOctree(maxDepth: 8);
//   tree.SetVoxel(new VoxelCoordinate(10, 20, 30), stoneVoxel);
//   var v = tree.GetVoxel(new VoxelCoordinate(10, 20, 30));
//
using System;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.SVO
{
    /// <summary>Absolute integer voxel coordinate inside the SVO volume.</summary>
    public readonly struct VoxelCoordinate : IEquatable<VoxelCoordinate>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public VoxelCoordinate(int x, int y, int z) { X = x; Y = y; Z = z; }

        public bool Equals(VoxelCoordinate other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is VoxelCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>Sparse Voxel Octree; thin façade over a <see cref="SvoNode"/> root.</summary>
    public sealed class SparseVoxelOctree
    {
        /// <summary>Hard upper bound on tree depth so 2^MaxDepth fits in int.</summary>
        public const int AbsoluteMaxDepth = 20;

        public int MaxDepth { get; }
        public SvoNode Root { get; }

        /// <summary>Edge length of the volume covered by the tree, in voxels.</summary>
        public int Extent => 1 << MaxDepth;

        public SparseVoxelOctree(int maxDepth)
        {
            if (maxDepth < 1 || maxDepth > AbsoluteMaxDepth)
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth,
                    $"Must be in [1, {AbsoluteMaxDepth}].");
            MaxDepth = maxDepth;
            Root = new SvoNode(depth: 0, sample: Voxel.Empty);
        }

        /// <summary>Read the voxel that contains <paramref name="coordinate"/>.</summary>
        public Voxel GetVoxel(VoxelCoordinate coordinate)
        {
            ValidateCoordinate(coordinate);

            SvoNode node = Root;
            int size = Extent;
            int x = coordinate.X, y = coordinate.Y, z = coordinate.Z;

            while (!node.IsLeaf)
            {
                size >>= 1;
                int ox = x >= size ? 1 : 0;
                int oy = y >= size ? 1 : 0;
                int oz = z >= size ? 1 : 0;
                node = node.GetChild(SvoOctants.OctantIndex(ox, oy, oz));
                if (ox == 1) x -= size;
                if (oy == 1) y -= size;
                if (oz == 1) z -= size;
            }

            return node.Sample;
        }

        /// <summary>
        /// Set a single voxel, subdividing nodes along the path until a
        /// leaf at <see cref="MaxDepth"/> is reached.  No collapsing is
        /// performed automatically; call <see cref="Compact"/> for that.
        /// </summary>
        public void SetVoxel(VoxelCoordinate coordinate, Voxel value)
        {
            ValidateCoordinate(coordinate);

            SvoNode node = Root;
            int size = Extent;
            int x = coordinate.X, y = coordinate.Y, z = coordinate.Z;

            for (int depth = 0; depth < MaxDepth; depth++)
            {
                if (node.IsLeaf)
                {
                    // Subdividing fills the new children with the parent's
                    // current sample, preserving the value at every voxel
                    // outside the path we are walking.
                    node.Subdivide(node.Sample);
                }

                size >>= 1;
                int ox = x >= size ? 1 : 0;
                int oy = y >= size ? 1 : 0;
                int oz = z >= size ? 1 : 0;
                node = node.GetChild(SvoOctants.OctantIndex(ox, oy, oz));
                if (ox == 1) x -= size;
                if (oy == 1) y -= size;
                if (oz == 1) z -= size;
            }

            node.SetSample(value);
        }

        /// <summary>
        /// Walk the tree bottom‑up and collapse any interior node whose
        /// eight children are leaves carrying the same sample.  Returns the
        /// number of nodes that were freed by the pass.
        /// </summary>
        public int Compact() => CompactRecursive(Root);

        private static int CompactRecursive(SvoNode node)
        {
            if (node.IsLeaf) return 0;

            int freed = 0;
            for (int i = 0; i < SvoOctants.Count; i++)
                freed += CompactRecursive(node.GetChild(i));

            // Re‑evaluate after children may have collapsed themselves.
            var first = node.GetChild(0);
            if (!first.IsLeaf) return freed;
            var sample = first.Sample;
            for (int i = 1; i < SvoOctants.Count; i++)
            {
                var child = node.GetChild(i);
                if (!child.IsLeaf || child.Sample != sample)
                    return freed;
            }

            node.CollapseTo(sample);
            return freed + SvoOctants.Count;
        }

        private void ValidateCoordinate(VoxelCoordinate coordinate)
        {
            int extent = Extent;
            if ((uint)coordinate.X >= (uint)extent)
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate.X, $"X must be in [0, {extent}).");
            if ((uint)coordinate.Y >= (uint)extent)
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate.Y, $"Y must be in [0, {extent}).");
            if ((uint)coordinate.Z >= (uint)extent)
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate.Z, $"Z must be in [0, {extent}).");
        }
    }
}
