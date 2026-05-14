// File: Assets/Scripts/VoxelCore/VoxelMaterial.cs
//
// Purpose
//   Describes a kind of matter that can fill a voxel and the registry that
//   maps small integer ids to those descriptions.  Materials are
//   shared across CPU and GPU so the registry is the single source of truth
//   for ids, names and physical parameters.
//
// Invariants
//   * Id 0 is reserved for `Air` and always present.
//   * Ids are dense and assigned monotonically by `Register`; once assigned
//     they never change for the lifetime of the registry.
//   * Material names are case‑sensitive and unique.
//   * `BaseHardness` and `BaseDensity` are normalised in [0, 1].
//
// Dependencies
//   None besides BCL.
//
// Example
//   var registry = new VoxelMaterialRegistry();
//   ushort stoneId = registry.Register("stone", baseDensity: 1f, baseHardness: 0.8f);
//   var stoneVoxel = new Voxel(stoneId, density: 1f, hardness: 0.8f);
//
using System;
using System.Collections.Generic;

namespace VoxeLaboratory.VoxelCore
{
    /// <summary>
    /// Immutable description of a voxel material.
    /// </summary>
    public readonly struct VoxelMaterial : IEquatable<VoxelMaterial>
    {
        /// <summary>Reserved id for empty space.</summary>
        public const ushort AirId = 0;

        public readonly ushort Id;
        public readonly string Name;
        public readonly float BaseDensity;
        public readonly float BaseHardness;

        public VoxelMaterial(ushort id, string name, float baseDensity, float baseHardness)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Material name must not be empty.", nameof(name));
            if (baseDensity < 0f || baseDensity > 1f)
                throw new ArgumentOutOfRangeException(nameof(baseDensity), "Must be in [0, 1].");
            if (baseHardness < 0f || baseHardness > 1f)
                throw new ArgumentOutOfRangeException(nameof(baseHardness), "Must be in [0, 1].");

            Id = id;
            Name = name;
            BaseDensity = baseDensity;
            BaseHardness = baseHardness;
        }

        public bool Equals(VoxelMaterial other) =>
            Id == other.Id
            && Name == other.Name
            && BaseDensity.Equals(other.BaseDensity)
            && BaseHardness.Equals(other.BaseHardness);

        public override bool Equals(object obj) => obj is VoxelMaterial other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Name, BaseDensity, BaseHardness);
    }

    /// <summary>
    /// Append‑only registry of voxel materials.  Not thread‑safe for writes;
    /// callers that mutate the registry concurrently must serialise access.
    /// Reads are safe once the registry has been fully populated.
    /// </summary>
    public sealed class VoxelMaterialRegistry
    {
        private readonly List<VoxelMaterial> _materials = new List<VoxelMaterial>();
        private readonly Dictionary<string, ushort> _byName = new Dictionary<string, ushort>(StringComparer.Ordinal);

        public VoxelMaterialRegistry()
        {
            // Air is always present and always at id 0.
            var air = new VoxelMaterial(VoxelMaterial.AirId, "air", 0f, 0f);
            _materials.Add(air);
            _byName.Add(air.Name, air.Id);
        }

        /// <summary>Number of registered materials, including air.</summary>
        public int Count => _materials.Count;

        /// <summary>
        /// Register a new material.  Throws if <paramref name="name"/> is
        /// already used or if the registry would exceed <see cref="ushort.MaxValue"/>.
        /// </summary>
        public ushort Register(string name, float baseDensity, float baseHardness)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Material name must not be empty.", nameof(name));
            if (_byName.ContainsKey(name))
                throw new ArgumentException($"Material '{name}' is already registered.", nameof(name));
            if (_materials.Count >= ushort.MaxValue)
                throw new InvalidOperationException("VoxelMaterialRegistry is full (65535 entries).");

            ushort id = (ushort)_materials.Count;
            var material = new VoxelMaterial(id, name, baseDensity, baseHardness);
            _materials.Add(material);
            _byName.Add(name, id);
            return id;
        }

        /// <summary>Retrieve a material by id.  Throws on unknown ids.</summary>
        public VoxelMaterial Get(ushort id)
        {
            if (id >= _materials.Count)
                throw new ArgumentOutOfRangeException(nameof(id), id, $"Unknown material id; registry has {_materials.Count} entries.");
            return _materials[id];
        }

        /// <summary>Try to look up a material id by name.</summary>
        public bool TryGetId(string name, out ushort id) => _byName.TryGetValue(name ?? string.Empty, out id);
    }
}
