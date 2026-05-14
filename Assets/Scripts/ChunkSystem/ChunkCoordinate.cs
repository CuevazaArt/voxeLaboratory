// File: Assets/Scripts/ChunkSystem/ChunkCoordinate.cs
//
// Purpose
//   Integer coordinate of a chunk in the world grid.  Chunks tile space
//   along all three axes, so a chunk coordinate (cx, cy, cz) covers the
//   world voxel range [cx*Size, (cx+1)*Size) x ... .
//
// Invariants
//   * All components are 32‑bit signed integers; world span is therefore
//     limited to ±2^31 chunks per axis (~3.4·10^10 voxels per axis).
//   * Equality and hashing are content‑based so coordinates can be used as
//     dictionary keys without boxing.
//
// Dependencies
//   None.
//
// Example
//   var origin = new ChunkCoordinate(0, 0, 0);
//   var north  = origin.Offset(0, 0, 1);
//
using System;

namespace VoxeLaboratory.ChunkSystem
{
    /// <summary>Three‑dimensional integer coordinate of a chunk.</summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ChunkCoordinate(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public ChunkCoordinate Offset(int dx, int dy, int dz) =>
            new ChunkCoordinate(X + dx, Y + dy, Z + dz);

        public bool Equals(ChunkCoordinate other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";

        public static bool operator ==(ChunkCoordinate a, ChunkCoordinate b) => a.Equals(b);
        public static bool operator !=(ChunkCoordinate a, ChunkCoordinate b) => !a.Equals(b);
    }
}
