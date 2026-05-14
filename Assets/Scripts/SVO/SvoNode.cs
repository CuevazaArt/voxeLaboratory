// File: Assets/Scripts/SVO/SvoNode.cs
//
// Purpose
//   Single node of a Sparse Voxel Octree.  A node either references eight
//   child nodes (interior) or holds a single voxel sample (leaf).  Nodes
//   are mutable but only through the public API of `SparseVoxelOctree`,
//   which keeps the tree invariants intact.
//
// Invariants
//   * `Children` is either null (leaf) or an array of length 8 (interior).
//   * Children are ordered using the canonical octant index returned by
//     <see cref="OctantIndex"/>.
//   * `Depth` is the absolute depth from the root and is set at
//     construction time; it never changes for the lifetime of the node.
//   * Leaf nodes carry a `Sample` voxel; interior nodes ignore it.
//
// Dependencies
//   `VoxeLaboratory.VoxelCore`.
//
// Example
//   var root = new SvoNode(depth: 0, sample: Voxel.Empty);
//   root.Subdivide(Voxel.Empty);
//
using System;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.SVO
{
    /// <summary>Octant addressing helpers shared by the tree and its tests.</summary>
    public static class SvoOctants
    {
        /// <summary>Number of children per interior node.</summary>
        public const int Count = 8;

        /// <summary>
        /// Map a 3‑bit coordinate (each component 0 or 1) to the canonical
        /// child index used everywhere in the engine.
        /// </summary>
        public static int OctantIndex(int x, int y, int z)
        {
            if ((uint)x > 1) throw new ArgumentOutOfRangeException(nameof(x), x, "Must be 0 or 1.");
            if ((uint)y > 1) throw new ArgumentOutOfRangeException(nameof(y), y, "Must be 0 or 1.");
            if ((uint)z > 1) throw new ArgumentOutOfRangeException(nameof(z), z, "Must be 0 or 1.");
            return x | (y << 1) | (z << 2);
        }
    }

    /// <summary>Single node of the SVO; leaf or interior.</summary>
    public sealed class SvoNode
    {
        private SvoNode[] _children;

        /// <summary>Absolute depth from the root (root is depth 0).</summary>
        public int Depth { get; }

        /// <summary>Sample stored at this node when it is a leaf.</summary>
        public Voxel Sample { get; private set; }

        /// <summary>True when this node has no children.</summary>
        public bool IsLeaf => _children is null;

        public SvoNode(int depth, Voxel sample)
        {
            if (depth < 0) throw new ArgumentOutOfRangeException(nameof(depth), depth, "Must be ≥ 0.");
            Depth = depth;
            Sample = sample;
        }

        /// <summary>
        /// Replace the sample carried by a leaf node.  Interior nodes reject
        /// the call so callers must not silently lose the children.
        /// </summary>
        public void SetSample(Voxel sample)
        {
            if (!IsLeaf)
                throw new InvalidOperationException("Cannot set a sample on an interior SvoNode.");
            Sample = sample;
        }

        /// <summary>Read‑only access to the eight children of an interior node.</summary>
        public SvoNode GetChild(int index)
        {
            if (IsLeaf)
                throw new InvalidOperationException("Leaf nodes have no children.");
            if ((uint)index >= SvoOctants.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be in [0, {SvoOctants.Count}).");
            return _children[index];
        }

        /// <summary>
        /// Convert a leaf into an interior node by allocating eight children
        /// initialised to <paramref name="fill"/>.  No‑op on interior nodes.
        /// Callers must verify that <see cref="Depth"/> + 1 is within the
        /// tree's maximum depth.
        /// </summary>
        internal void Subdivide(Voxel fill)
        {
            if (!IsLeaf)
                return;
            var children = new SvoNode[SvoOctants.Count];
            int childDepth = Depth + 1;
            for (int i = 0; i < SvoOctants.Count; i++)
                children[i] = new SvoNode(childDepth, fill);
            _children = children;
            // Sample is meaningless on interior nodes, leave the stored value
            // untouched so it can be inspected for debugging.
        }

        /// <summary>
        /// Collapse an interior node back to a leaf.  All children are
        /// discarded; callers must guarantee they are uniform first.
        /// </summary>
        internal void CollapseTo(Voxel sample)
        {
            _children = null;
            Sample = sample;
        }
    }
}
