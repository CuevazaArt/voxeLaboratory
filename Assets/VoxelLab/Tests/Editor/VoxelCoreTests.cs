// =====================================================================
//  VoxelCoreTests.cs
//  VoxelLab :: Tests (EditMode)
//
//  Tests headless (sin Unity Player) que validan la lógica de
//  VoxelWorld y SVO. Se pueden correr con `Unity Test Runner`.
//
//  Cobertura:
//      - SetVoxel / GetVoxel cross-chunk (coords negativas).
//      - CarveSphere reduce densidad y limpia material.
//      - FillSphere produce sólidos.
//      - Explosion afecta voxels y reporta count.
//      - RaySample golpea desde fuera de la esfera.
//      - SVO subdivide y refleja contenido.
//
//  Dependencias: NUnit, VoxelLab.Runtime.
//
//  Invariantes:
//      - ninguna prueba depende de Play Mode.
//      - las pruebas validan coordenadas positivas y negativas.
//
//  Ejemplo de ejecución:
//      Unity Test Runner -> EditMode -> Run All.
// =====================================================================
using NUnit.Framework;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Physics;
using VoxelLab.Planet;
using VoxelLab.Tools;

namespace VoxelLab.Tests
{
    public class VoxelCoreTests
    {
        [Test]
        public void SetGetVoxel_RoundTrip_NegativeCoords()
        {
            var w = new VoxelWorld(16);
            var v = new Voxel((byte)MaterialId.Iron, 1f, 0.5f);
            w.SetVoxel(-3, 100, -42, v);
            var r = w.GetVoxel(-3, 100, -42);
            Assert.AreEqual(v.material, r.material);
            Assert.IsTrue(r.solido);
        }

        [Test]
        public void GetVoxel_ReturnsEmpty_WhenChunkMissing()
        {
            var w = new VoxelWorld(16);
            var r = w.GetVoxel(9999, 9999, 9999);
            Assert.AreEqual(0, r.material);
            Assert.IsFalse(r.solido);
        }

        [Test]
        public void FillThenCarveSphere_RemovesSolids()
        {
            var w = new VoxelWorld(16);
            int filled = w.FillSphere(Vector3.zero, 4f, (byte)MaterialId.Rock);
            Assert.Greater(filled, 10);
            int carved = w.CarveSphere(Vector3.zero, 4f, 1f);
            Assert.Greater(carved, 0);
            // Centro debe estar vacío
            var c = w.GetVoxel(0, 0, 0);
            Assert.IsFalse(c.solido);
        }

        [Test]
        public void Explosion_ReportsRemovedCount()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(Vector3.zero, 5f, (byte)MaterialId.Dirt);
            var res = w.Explosion(Vector3.zero, 4f, 1f);
            Assert.Greater(res.voxelsRemoved, 0);
        }

        [Test]
        public void RaySample_HitsFilledSphere()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(new Vector3(0, 0, 10), 3f, (byte)MaterialId.Rock);
            var hit = w.RaySample(new Vector3(0, 0, -5), Vector3.forward, 50f);
            Assert.IsTrue(hit.hit);
            Assert.Less(hit.distance, 50f);
        }

        [Test]
        public void RaySample_Misses_WhenEmpty()
        {
            var w = new VoxelWorld(16);
            var hit = w.RaySample(Vector3.zero, Vector3.up, 32f);
            Assert.IsFalse(hit.hit);
        }

        [Test]
        public void RaySample_ClampsInvalidStep_AndDoesNotHang()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(new Vector3(0, 0, 8), 3f, (byte)MaterialId.Rock);

            var hit = w.RaySample(Vector3.zero, Vector3.forward, 32f, dt: 0f);
            Assert.IsTrue(hit.hit);
        }

        [Test]
        public void SphereOps_ReturnZero_WhenRadiusIsNonPositive()
        {
            var w = new VoxelWorld(16);

            Assert.AreEqual(0, w.FillSphere(Vector3.zero, 0f, (byte)MaterialId.Dirt));
            Assert.AreEqual(0, w.CarveSphere(Vector3.zero, -1f, 1f));

            var explosion = w.Explosion(Vector3.zero, 0f, 1f);
            Assert.AreEqual(0, explosion.voxelsRemoved);
        }

        [Test]
        public void Octree_Subdivides_OnSetVoxel()
        {
            var w = new VoxelWorld(16, 7); // root 128
            w.SetVoxel(40, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.octree.Refresh(cc => w.GetChunk(cc, create: false));
            var n = w.octree.Query(new Vector3Int(40, 0, 0));
            Assert.IsNotNull(n);
            Assert.AreEqual(16, n.size); // hoja = leafSize
            Assert.IsTrue(n.anySolid);
        }

        [Test]
        public void Octree_Subdivides_OnNegativeCoords()
        {
            var w = new VoxelWorld(16, 7); // root 128
            w.SetVoxel(-40, -1, -8, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.octree.Refresh(cc => w.GetChunk(cc, create: false));

            var n = w.octree.Query(new Vector3Int(-40, -1, -8));
            Assert.IsNotNull(n);
            Assert.AreEqual(16, n.size);
            Assert.IsTrue(n.anySolid);
        }

        [Test]
        public void Tools_DoNotThrow_OnInvalidParameters()
        {
            var w = new VoxelWorld(16);
            var p = new ToolParameters
            {
                radius = -10f,
                intensity = 99f,
                material = (byte)MaterialId.Rock,
                maxDistance = -1f,
                planeNormal = Vector3.zero,
            };

            var ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.DoesNotThrow(() => new DrillTool().Apply(w, ray, p, null));
            Assert.DoesNotThrow(() => new ExplosionTool().Apply(w, ray, p, null));
            Assert.DoesNotThrow(() => new BrushTool().Apply(w, ray, p, null));
            Assert.DoesNotThrow(() => new ErosionTool().Apply(w, ray, p, null));
            Assert.DoesNotThrow(() => new CutTool().Apply(w, ray, p, null));
        }

        // -----------------------------------------------------------------
        //  Ballistics sandbox
        // -----------------------------------------------------------------

        [Test]
        public void CubeGenerator_FillsExactExtent_AndIsSolidAtCenter()
        {
            var w = new VoxelWorld(16);
            var s = CubeSettings.Default;
            s.size = 8;
            s.center = Vector3Int.zero;
            int placed = CubeGenerator.Generate(w, s);
            Assert.AreEqual(8 * 8 * 8, placed);
            Assert.IsTrue(w.GetVoxel(0, 0, 0).solido);
            // Esquina justo dentro
            Assert.IsTrue(w.GetVoxel(-4, -4, -4).solido);
            // Esquina justo fuera
            Assert.IsFalse(w.GetVoxel(4, 4, 4).solido);
        }

        [Test]
        public void StepFreeSpace_PreservesMomentum_InVacuum()
        {
            var w = new VoxelWorld(16); // mundo vacío
            var p = Projectile.Create(Vector3.zero, new Vector3(10f, 0f, 0f), mass: 2f, radius: 0.25f, drag: 0f);
            float dt = 1f / 60f;
            for (int i = 0; i < 30; i++)
                VolumetricPhysics.StepFreeSpace(w, p.body, Vector3.zero, dt);
            Assert.AreEqual(10f, p.body.velocity.x, 1e-4f);
            Assert.AreEqual(10f * 30f * dt, p.body.position.x, 1e-3f);
            Assert.AreEqual(0f, p.body.velocity.y, 1e-6f);
            Assert.AreEqual(0f, p.body.velocity.z, 1e-6f);
        }

        [Test]
        public void StepFreeSpace_ResolvesCollision_AgainstCube()
        {
            var w = new VoxelWorld(16);
            var s = CubeSettings.Default;
            s.size = 8;
            s.center = new Vector3Int(10, 0, 0); // cubo a la derecha del origen
            CubeGenerator.Generate(w, s);

            var p = Projectile.Create(Vector3.zero, new Vector3(50f, 0f, 0f), mass: 1f, radius: 0.25f, drag: 0f);
            float dt = 1f / 240f;
            // Simular hasta 0.5s; debe quedar fuera del sólido (densidad < SOLID_THRESHOLD).
            for (int i = 0; i < 120; i++)
                VolumetricPhysics.StepFreeSpace(w, p.body, Vector3.zero, dt);

            float density = VolumetricPhysics.SampleDensity(w, p.body.position);
            Assert.Less(density, VolumetricPhysics.SOLID_THRESHOLD,
                "El proyectil no debería penetrar persistentemente el cubo sólido.");
        }

        [Test]
        public void Projectile_KineticEnergy_Matches_HalfMV2()
        {
            var p = Projectile.Create(Vector3.zero, new Vector3(0f, 0f, 20f), mass: 3f, radius: 0.5f, drag: 0f);
            float expected = 0.5f * 3f * 20f * 20f;
            Assert.AreEqual(expected, p.KineticEnergy, 1e-3f);
        }

        [Test]
        public void ImpactRadius_IncreasesWithEnergy_AndRespectsClamp()
        {
            float baseR = 0.2f;
            float k = 0.1f;
            float maxR = 2.5f;

            float r0 = ProjectileLauncher.ComputeImpactRadius(0f, baseR, k, maxR);
            float r1 = ProjectileLauncher.ComputeImpactRadius(100f, baseR, k, maxR);
            float r2 = ProjectileLauncher.ComputeImpactRadius(10000f, baseR, k, maxR);

            Assert.GreaterOrEqual(r0, baseR);
            Assert.Greater(r1, r0);
            Assert.LessOrEqual(r2, maxR + 1e-6f);
        }
    }
}
