// File: Assets/Scripts/VoxelCore/Voxel.cs
//
// Purpose
//   Defines the canonical voxel value object used everywhere in the engine.
//   A voxel is a small, blittable struct so it can be packed in arrays,
//   uploaded to a GraphicsBuffer and consumed by compute shaders without
//   boxing or marshalling.
//
// Invariants
//   * `Density` is normalised in [0, 1]. Values outside the range are clamped
//     by the constructor; the static factory `Empty` produces density 0 and
//     `Solid` produces density 1.
//   * `Hardness` is normalised in [0, 1] and represents how resistant the
//     voxel is to volumetric tools (drill, explosion, erosion).
//   * `MaterialId` references an entry in `VoxelMaterialRegistry`.
//     `MaterialId == VoxelMaterial.AirId (0)` always means empty space and
//     forces `Density == 0`.
//   * The struct is exactly 8 bytes (ushort + byte + byte + float) so the
//     memory layout matches the HLSL `Voxel` definition used by
//     `MeshingGPU`.
//
// Dependencies
//   None.  This file intentionally does not reference UnityEngine so the
//   type can be unit‑tested on any .NET runtime.
//
// Example
//   var stone = new Voxel(materialId: 2, density: 1f, hardness: 0.8f);
//   if (!stone.IsEmpty) { /* meshable */ }
//
using System;
using System.Runtime.InteropServices;

namespace VoxeLaboratory.VoxelCore
{
    /// <summary>
    /// Immutable voxel sample: material identifier plus normalised density
    /// and hardness.  Layout matches the HLSL counterpart used by the GPU
    /// meshing kernels.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    public readonly struct Voxel : IEquatable<Voxel>
    {
        /// <summary>Material identifier, indexes <see cref="VoxelMaterialRegistry"/>.</summary>
        public readonly ushort MaterialId;

        /// <summary>Normalised hardness in [0, 1] mapped to byte for compactness.</summary>
        private readonly byte _hardness;

        /// <summary>Reserved for future flags; kept zero today to preserve layout.</summary>
        private readonly byte _flags;

        /// <summary>Normalised density in [0, 1].  0 ⇒ empty, 1 ⇒ fully solid.</summary>
        public readonly float Density;

        /// <summary>Sentinel "air" voxel.</summary>
        public static readonly Voxel Empty = new Voxel(VoxelMaterial.AirId, 0f, 0f);

        public Voxel(ushort materialId, float density, float hardness)
        {
            // Air must always be empty; reject inconsistent combinations early
            // rather than silently corrupting GPU buffers.
            if (materialId == VoxelMaterial.AirId && density > 0f)
            {
                throw new ArgumentException(
                    "Air voxels (materialId == 0) must have density == 0.",
                    nameof(density));
            }

            MaterialId = materialId;
            Density = Clamp01(density);
            _hardness = (byte)(Clamp01(hardness) * 255f + 0.5f);
            _flags = 0;
        }

        /// <summary>Decoded hardness in [0, 1].</summary>
        public float Hardness => _hardness / 255f;

        /// <summary>True when the voxel does not contribute to the surface mesh.</summary>
        public bool IsEmpty => MaterialId == VoxelMaterial.AirId || Density <= 0f;

        public bool Equals(Voxel other) =>
            MaterialId == other.MaterialId
            && _hardness == other._hardness
            && _flags == other._flags
            && Density.Equals(other.Density);

        public override bool Equals(object obj) => obj is Voxel other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(MaterialId, _hardness, _flags, Density);

        public static bool operator ==(Voxel a, Voxel b) => a.Equals(b);
        public static bool operator !=(Voxel a, Voxel b) => !a.Equals(b);

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
