// File: Assets/Scripts/ChunkSystem/Chunk.cs
//
// Purpose
//   Fixed‑size cubical block of voxel data.  A chunk owns a contiguous
//   `Voxel[]` of length `Size^3` so it can be passed to a `GraphicsBuffer`
//   in a single upload and iterated cache‑friendly on the CPU.
//
// Invariants
//   * `Size` is a compile‑time constant (16) and must remain a power of two:
//     compute kernels rely on bit‑mask addressing.
//   * Local voxel coordinates passed to public APIs are validated; out‑of
//     range accesses throw `ArgumentOutOfRangeException`.
//   * `Coordinate` is immutable for the lifetime of the chunk; the chunk
//     never moves in the world grid.
//   * Mutations bump `Version`, which downstream systems (meshing, physics)
//     observe to know whether a regeneration is required.
//
// Dependencies
//   `VoxeLaboratory.VoxelCore`.
//
// Example
//   var chunk = new Chunk(new ChunkCoordinate(0, 0, 0));
//   chunk.SetVoxel(1, 2, 3, new Voxel(stoneId, 1f, 0.8f));
//   bool dirty = chunk.Version != lastSeenVersion;
//
using System;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.ChunkSystem
{
    /// <summary>
    /// Dense voxel grid of side length <see cref="Size"/>.  Designed to be
    /// owned by exactly one writer at a time; concurrent writers must be
    /// coordinated by <see cref="ChunkRegistry"/>.
    /// </summary>
    public sealed class Chunk
    {
        /// <summary>Side length of a chunk in voxels.</summary>
        public const int Size = 16;

        /// <summary>Total number of voxels in a chunk (Size^3).</summary>
        public const int Volume = Size * Size * Size;

        private readonly Voxel[] _voxels;
        private long _version;

        public ChunkCoordinate Coordinate { get; }

        /// <summary>
        /// Monotonically increasing counter incremented on every successful
        /// mutation.  Wraps after ~9.2·10^18 edits, which is unreachable in
        /// practice.
        /// </summary>
        public long Version => _version;

        public Chunk(ChunkCoordinate coordinate)
        {
            Coordinate = coordinate;
            _voxels = new Voxel[Volume];
            // Default state is air, matching `default(Voxel)` already.
            _version = 0;
        }

        /// <summary>
        /// Linearise local coordinates `(x, y, z)` into the underlying
        /// flat array.  Throws if any component is outside `[0, Size)`.
        /// </summary>
        public static int IndexOf(int x, int y, int z)
        {
            ValidateLocal(x, y, z);
            return UncheckedIndexOf(x, y, z);
        }

        /// <summary>Read a voxel by local coordinate.</summary>
        public Voxel GetVoxel(int x, int y, int z)
        {
            ValidateLocal(x, y, z);
            return _voxels[UncheckedIndexOf(x, y, z)];
        }

        /// <summary>
        /// Replace a voxel by local coordinate.  Increments <see cref="Version"/>
        /// only when the new value actually differs from the stored one,
        /// which keeps idle chunks out of the meshing queue.
        /// </summary>
        public void SetVoxel(int x, int y, int z, Voxel voxel)
        {
            ValidateLocal(x, y, z);
            int index = UncheckedIndexOf(x, y, z);
            if (_voxels[index] == voxel)
                return;
            _voxels[index] = voxel;
            _version++;
        }

        /// <summary>
        /// Copy the entire voxel buffer into <paramref name="destination"/>.
        /// Used by the meshing pipeline to upload to a GraphicsBuffer.
        /// </summary>
        public void CopyTo(Voxel[] destination)
        {
            if (destination is null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < Volume)
                throw new ArgumentException($"Destination must have at least {Volume} elements.", nameof(destination));
            Array.Copy(_voxels, destination, Volume);
        }

        /// <summary>
        /// Reset every voxel to <see cref="Voxel.Empty"/> in a single pass.
        /// Always bumps <see cref="Version"/> because callers explicitly
        /// requested invalidation.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_voxels, 0, _voxels.Length);
            _version++;
        }

        private static int UncheckedIndexOf(int x, int y, int z) =>
            x + (y * Size) + (z * Size * Size);

        private static void ValidateLocal(int x, int y, int z)
        {
            if ((uint)x >= Size) throw new ArgumentOutOfRangeException(nameof(x), x, $"Must be in [0, {Size}).");
            if ((uint)y >= Size) throw new ArgumentOutOfRangeException(nameof(y), y, $"Must be in [0, {Size}).");
            if ((uint)z >= Size) throw new ArgumentOutOfRangeException(nameof(z), z, $"Must be in [0, {Size}).");
        }
    }
}
