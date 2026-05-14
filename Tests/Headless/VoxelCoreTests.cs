// Tests covering the Voxel struct and material registry invariants.
using System;
using NUnit.Framework;
using VoxeLaboratory.VoxelCore;

namespace VoxeLaboratory.Tests
{
    [TestFixture]
    public class VoxelCoreTests
    {
        [Test]
        public void Empty_Voxel_Is_Air_With_Zero_Density()
        {
            var v = Voxel.Empty;
            Assert.That(v.IsEmpty, Is.True);
            Assert.That(v.MaterialId, Is.EqualTo(VoxelMaterial.AirId));
            Assert.That(v.Density, Is.EqualTo(0f));
        }

        [Test]
        public void Density_And_Hardness_Are_Clamped_To_Unit_Range()
        {
            var below = new Voxel(materialId: 1, density: -10f, hardness: -1f);
            Assert.That(below.Density, Is.EqualTo(0f));
            Assert.That(below.Hardness, Is.EqualTo(0f));

            var above = new Voxel(materialId: 1, density: 5f, hardness: 5f);
            Assert.That(above.Density, Is.EqualTo(1f));
            Assert.That(above.Hardness, Is.EqualTo(1f).Within(1f / 255f));
        }

        [Test]
        public void Constructing_Air_With_Density_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Voxel(VoxelMaterial.AirId, 0.5f, 0f));
        }

        [Test]
        public void Registry_Always_Contains_Air_At_Id_Zero()
        {
            var registry = new VoxelMaterialRegistry();
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.Get(VoxelMaterial.AirId).Name, Is.EqualTo("air"));
        }

        [Test]
        public void Registering_Duplicates_Throws()
        {
            var registry = new VoxelMaterialRegistry();
            registry.Register("stone", 1f, 0.8f);
            Assert.Throws<ArgumentException>(() => registry.Register("stone", 0.5f, 0.5f));
        }

        [Test]
        public void Registry_Lookup_By_Name_Round_Trips()
        {
            var registry = new VoxelMaterialRegistry();
            ushort id = registry.Register("dirt", 0.7f, 0.2f);
            Assert.That(registry.TryGetId("dirt", out var found), Is.True);
            Assert.That(found, Is.EqualTo(id));
            Assert.That(registry.Get(id).BaseDensity, Is.EqualTo(0.7f));
        }
    }
}
