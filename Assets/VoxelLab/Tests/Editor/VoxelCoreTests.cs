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
    }
}
