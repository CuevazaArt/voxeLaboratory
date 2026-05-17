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
        public void ChunkCoord_AndLocalCoord_AreConsistent_ForNegativePositions()
        {
            var w = new VoxelWorld(16);

            var cc = w.ChunkCoord(-1, -16, -17);
            var lc = w.LocalCoord(-1, -16, -17);

            Assert.AreEqual(new Vector3Int(-1, -1, -2), cc);
            Assert.AreEqual(new Vector3Int(15, 0, 15), lc);
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
        public void CarveSphere_AcrossChunkBoundary_RaisesDirtyOnMultipleChunks()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(new Vector3(15.5f, 0f, 0f), 3f, (byte)MaterialId.Rock);

            var dirtyChunks = new System.Collections.Generic.HashSet<Vector3Int>();
            w.OnChunkDirty += c => dirtyChunks.Add(c.origin);

            int carved = w.CarveSphere(new Vector3(15.5f, 0f, 0f), 2.5f, 1f);

            Assert.Greater(carved, 0);
            Assert.GreaterOrEqual(dirtyChunks.Count, 2, "Debe afectar chunks a ambos lados del limite X=16.");
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

        [Test]
        public void ToolInputSanitizer_ClampsNumericRanges()
        {
            var t = System.Type.GetType("VoxelLab.Tools.ToolInputSanitizer, VoxelLab.Tools");
            Assert.IsNotNull(t, "No se encontro ToolInputSanitizer en el asmdef VoxelLab.Tools.");

            var m = t.GetMethod("Sanitize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(m, "No se encontro metodo estatico Sanitize.");

            var raw = new ToolParameters
            {
                radius = -5f,
                intensity = 2.25f,
                material = (byte)MaterialId.Rock,
                maxDistance = -40f,
                planeNormal = new Vector3(10f, 0f, 0f),
            };

            object[] args = { raw };
            var sanitized = (ToolParameters)m.Invoke(null, args);

            Assert.AreEqual(0.5f, sanitized.radius, 1e-6f);
            Assert.AreEqual(1f, sanitized.intensity, 1e-6f);
            Assert.AreEqual(1f, sanitized.maxDistance, 1e-6f);
            Assert.AreEqual(1f, sanitized.planeNormal.magnitude, 1e-6f);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), sanitized.planeNormal);
        }

        [Test]
        public void ToolInputSanitizer_ReplacesDegenerateNormalWithUp()
        {
            var t = System.Type.GetType("VoxelLab.Tools.ToolInputSanitizer, VoxelLab.Tools");
            Assert.IsNotNull(t);

            var m = t.GetMethod("Sanitize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(m);

            var raw = new ToolParameters
            {
                radius = 1f,
                intensity = 0.2f,
                material = (byte)MaterialId.Dirt,
                maxDistance = 5000f,
                planeNormal = Vector3.zero,
            };

            object[] args = { raw };
            var sanitized = (ToolParameters)m.Invoke(null, args);

            Assert.AreEqual(2048f, sanitized.maxDistance, 1e-6f);
            Assert.AreEqual(Vector3.up, sanitized.planeNormal);
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

        // =================================================================
        //  Fase 1 — Material registry
        // =================================================================

        [TearDown]
        public void RestoreMaterialDefaults()
        {
            MaterialTable.ResetToDefaults();
        }

        private static VoxelMaterialDef MakeMat(byte id, string name,
            DestructionMode mode = DestructionMode.Crumble, string debrisKey = "")
        {
            var m = ScriptableObject.CreateInstance<VoxelMaterialDef>();
            m.id = id;
            m.displayName = name;
            m.color = Color.white;
            m.densidadBase = 1f;
            m.durezaBase = 0.5f;
            m.destructionMode = mode;
            m.debrisProfileKey = debrisKey;
            return m;
        }

        [Test]
        public void MaterialTable_Apply_RejectsDuplicateIds()
        {
            var a = MakeMat(3, "A");
            var b = MakeMat(3, "B");
            Assert.Throws<System.InvalidOperationException>(() =>
                MaterialTable.Apply(new[] { a, b }));
        }

        [Test]
        public void MaterialTable_Apply_PreservesAirSlot_WhenMissing()
        {
            var rock = MakeMat(1, "Rock");
            var iron = MakeMat(3, "Iron");
            MaterialTable.Apply(new[] { rock, iron });
            Assert.AreEqual(0f, MaterialTable.Get((byte)0).color.a, 1e-6f, "Slot 0 debe permanecer Air.");
            Assert.AreEqual("Air", MaterialTable.Get((byte)0).name);
        }

        [Test]
        public void MaterialTable_GetExtended_FallsBackToAir_WhenIdOutOfRange()
        {
            MaterialTable.ResetToDefaults();
            var ext = MaterialTable.GetExtended((byte)250);
            Assert.AreEqual(0, ext.id);
        }

        [Test]
        public void MaterialTable_Apply_PropagatesDebrisProfileKey()
        {
            var glass = MakeMat(5, "Glass", DestructionMode.Shatter, "GlassShards");
            MaterialTable.Apply(new[] { glass });
            var ext = MaterialTable.GetExtended((byte)5);
            Assert.AreEqual("GlassShards", ext.debrisProfileKey);
            Assert.AreEqual(DestructionMode.Shatter, ext.destructionMode);
        }

        // =================================================================
        //  Fase 2 — API destructiva enriquecida + FillBox/Cylinder
        // =================================================================

        [Test]
        public void Explosion_ReportsRemovedByMaterial_AndSamples()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(Vector3.zero, 5f, (byte)MaterialId.Iron);

            var res = w.Explosion(Vector3.zero, 4f, 1.5f, sampleStride: 2);

            Assert.IsNotNull(res.removedByMaterial);
            Assert.AreEqual(MaterialTable.Count, res.removedByMaterial.Length);
            Assert.Greater(res.removedByMaterial[(byte)MaterialId.Iron], 0);
            Assert.AreEqual(0, res.removedByMaterial[(byte)MaterialId.Air]);
            Assert.IsNotNull(res.samples);
            Assert.LessOrEqual(res.samples.Count, res.voxelsRemoved);
            Assert.Greater(res.voxelsRemoved, 0);
        }

        [Test]
        public void Explosion_SampleStrideClamp_NeverFails()
        {
            var w = new VoxelWorld(16);
            w.FillSphere(Vector3.zero, 3f, (byte)MaterialId.Rock);
            Assert.DoesNotThrow(() => w.Explosion(Vector3.zero, 2f, 1f, sampleStride: 0));
            Assert.DoesNotThrow(() => w.Explosion(Vector3.zero, 2f, 1f, sampleStride: -5));
        }

        [Test]
        public void FillBox_PlacesExactCount_AndClampsExtent()
        {
            var w = new VoxelWorld(16);
            int placed = w.FillBox(Vector3.zero, new Vector3Int(4, 6, 8), (byte)MaterialId.Rock);
            Assert.AreEqual(4 * 6 * 8, placed);
            Assert.IsTrue(w.GetVoxel(0, 0, 0).solido);
            // Extent negativo se clampa a 1.
            int placedNeg = w.FillBox(new Vector3(50, 0, 0), new Vector3Int(-3, -1, 2), (byte)MaterialId.Dirt);
            Assert.AreEqual(1 * 1 * 2, placedNeg);
        }

        [Test]
        public void FillBox_RejectsOverflow()
        {
            var w = new VoxelWorld(16);
            int placed = w.FillBox(Vector3.zero, new Vector3Int(256, 256, 256), (byte)MaterialId.Rock);
            Assert.AreEqual(0, placed, "Volumen 256^3 debe exceder MAX_FILL_VOXELS.");
        }

        [Test]
        public void FillCylinder_PlacesSolidsAlongAxis()
        {
            var w = new VoxelWorld(16);
            int placed = w.FillCylinder(Vector3.zero, radius: 3f, height: 6f, axis: 1,
                material: (byte)MaterialId.Iron);
            Assert.Greater(placed, 0);
            Assert.IsTrue(w.GetVoxel(0, 0, 0).solido);
            Assert.IsTrue(w.GetVoxel(0, 2, 0).solido);
            Assert.IsFalse(w.GetVoxel(10, 0, 0).solido);
        }

        // =================================================================
        //  Fase 3 — Proyectiles tipados
        // =================================================================

        private static ProjectileTypeDef MakeProjectileType(ProjectileImpactMode mode)
        {
            var t = ScriptableObject.CreateInstance<ProjectileTypeDef>();
            t.displayName = mode.ToString();
            t.mass = 1f;
            t.radius = 0.3f;
            t.initialSpeed = 50f;
            t.drag = 0f;
            t.lifetimeSeconds = 5f;
            t.impactMode = mode;
            return t;
        }

        [Test]
        public void Projectile_CreateFromType_AssociatesType()
        {
            var t = MakeProjectileType(ProjectileImpactMode.Kinetic);
            var p = Projectile.Create(Vector3.zero, Vector3.forward * 10f, t);
            Assert.IsNotNull(p.type);
            Assert.AreSame(t, p.type);
            Assert.AreEqual(1f, p.body.mass, 1e-6f);
        }

        [Test]
        public void Projectile_CreateFromNullType_FallsBackToDefaults()
        {
            var p = Projectile.Create(Vector3.zero, Vector3.forward, (ProjectileTypeDef)null);
            Assert.IsNull(p.type);
            Assert.Greater(p.body.mass, 0f);
        }

        // =================================================================
        //  Fase 4 — DebrisProfile + DebrisSimulator
        // =================================================================

        private static DebrisProfileDef MakeDebrisProfile(string key, float sampleFraction = 1f)
        {
            var p = ScriptableObject.CreateInstance<DebrisProfileDef>();
            p.profileKey = key;
            p.lifetimeSeconds = 1f;
            p.initialSpeedMin = 1f;
            p.initialSpeedMax = 2f;
            p.spreadAngleDeg = 30f;
            p.sampleFraction = sampleFraction;
            p.scale = 0.5f;
            p.gravityScale = 0f;
            return p;
        }

        [Test]
        public void Debris_RegisterProfile_AssignsIndex_AndIsIdempotent()
        {
            using var sim = new DebrisSimulator(64);
            var p = MakeDebrisProfile("Default");
            int a = sim.RegisterProfile(p);
            int b = sim.RegisterProfile(p);
            Assert.GreaterOrEqual(a, 0);
            Assert.AreEqual(a, b);
            Assert.AreEqual(1, sim.Profiles.Count);
        }

        [Test]
        public void Debris_SampleFractionZero_SpawnsNothing()
        {
            using var sim = new DebrisSimulator(64);
            sim.RegisterProfile(MakeDebrisProfile("Default", sampleFraction: 0f));
            var result = new ExplosionResult
            {
                voxelsRemoved = 4,
                samples = new System.Collections.Generic.List<DestructionSample>
                {
                    new DestructionSample { position = Vector3.zero, material = 1, removedDensity = 1f },
                    new DestructionSample { position = Vector3.right, material = 1, removedDensity = 1f },
                },
            };
            int spawned = sim.SpawnFromExplosion(result, Vector3.up,
                profileKeyFallback: "Default", rngSeed: 1);
            Assert.AreEqual(0, spawned);
            Assert.AreEqual(0, sim.ActiveCount);
        }

        [Test]
        public void Debris_SpawnFromExplosion_RespectsCapacity()
        {
            const int cap = 4;
            using var sim = new DebrisSimulator(cap);
            sim.RegisterProfile(MakeDebrisProfile("Default", sampleFraction: 1f));

            var samples = new System.Collections.Generic.List<DestructionSample>();
            for (int i = 0; i < 50; i++)
                samples.Add(new DestructionSample { position = Vector3.zero, material = 1, removedDensity = 1f });
            var result = new ExplosionResult { voxelsRemoved = samples.Count, samples = samples };

            sim.SpawnFromExplosion(result, Vector3.up, "Default", rngSeed: 1);
            Assert.LessOrEqual(sim.ActiveCount, cap);
            Assert.AreEqual(cap, sim.Instances.Length);
        }

        [Test]
        public void Debris_Step_AdvancesPosition_AndExpiresInstances()
        {
            using var sim = new DebrisSimulator(8);
            var profile = MakeDebrisProfile("Default", sampleFraction: 1f);
            profile.lifetimeSeconds = 0.05f;
            profile.gravityScale = 0f;
            profile.drag = 0f;
            profile.initialSpeedMin = 5f;
            profile.initialSpeedMax = 5f;
            profile.spreadAngleDeg = 0f;
            sim.RegisterProfile(profile);

            var result = new ExplosionResult
            {
                voxelsRemoved = 1,
                samples = new System.Collections.Generic.List<DestructionSample>
                {
                    new DestructionSample { position = Vector3.zero, material = 1, removedDensity = 1f },
                },
            };
            sim.SpawnFromExplosion(result, Vector3.up, "Default", rngSeed: 42);
            Assert.AreEqual(1, sim.ActiveCount);

            sim.Step(world: null, gravity: Vector3.zero, dt: 0.01f);
            var inst = sim.Instances[0];
            Assert.AreEqual(1, (int)inst.alive);
            Assert.Greater(inst.position.y, 0f, "Debe haberse desplazado en +Y.");

            sim.Step(world: null, gravity: Vector3.zero, dt: 0.2f);
            Assert.AreEqual(0, sim.ActiveCount);
        }

        // =================================================================
        //  Fase 5 — Targets y TargetSpawner
        // =================================================================

        [Test]
        public void TargetSpawner_Sphere_PlacesSolidAtCenter()
        {
            var w = new VoxelWorld(16);
            var t = ScriptableObject.CreateInstance<VoxelLab.Planet.TargetDef>();
            t.shape = VoxelLab.Planet.TargetShape.Sphere;
            t.size = new Vector3(3f, 0f, 0f);
            t.material = (byte)MaterialId.Rock;
            t.densidad = 1f;
            t.dureza = 0.5f;
            int placed = VoxelLab.Planet.TargetSpawner.Spawn(w, t, Vector3.zero);
            Assert.Greater(placed, 0);
            Assert.IsTrue(w.GetVoxel(0, 0, 0).solido);
        }

        [Test]
        public void TargetSpawner_Box_HasExpectedCount()
        {
            var w = new VoxelWorld(16);
            var t = ScriptableObject.CreateInstance<VoxelLab.Planet.TargetDef>();
            t.shape = VoxelLab.Planet.TargetShape.Box;
            t.size = new Vector3(4f, 4f, 4f);
            t.material = (byte)MaterialId.Iron;
            int placed = VoxelLab.Planet.TargetSpawner.Spawn(w, t, new Vector3(20, 0, 0));
            Assert.AreEqual(4 * 4 * 4, placed);
        }

        [Test]
        public void TargetSpawner_Composite_AggregatesChildren()
        {
            var w = new VoxelWorld(16);
            var child1 = ScriptableObject.CreateInstance<VoxelLab.Planet.TargetDef>();
            child1.shape = VoxelLab.Planet.TargetShape.Box;
            child1.size = new Vector3(2, 2, 2);
            child1.material = (byte)MaterialId.Rock;
            var child2 = ScriptableObject.CreateInstance<VoxelLab.Planet.TargetDef>();
            child2.shape = VoxelLab.Planet.TargetShape.Box;
            child2.size = new Vector3(3, 3, 3);
            child2.material = (byte)MaterialId.Iron;
            child2.positionOffset = new Vector3(8, 0, 0);

            var composite = ScriptableObject.CreateInstance<VoxelLab.Planet.TargetDef>();
            composite.shape = VoxelLab.Planet.TargetShape.Composite;
            composite.children = new[] { child1, child2 };

            int placed = VoxelLab.Planet.TargetSpawner.Spawn(w, composite, Vector3.zero);
            Assert.AreEqual(2 * 2 * 2 + 3 * 3 * 3, placed);
        }

        [Test]
        public void TargetSpawner_NullDef_NoOp()
        {
            var w = new VoxelWorld(16);
            Assert.AreEqual(0, VoxelLab.Planet.TargetSpawner.Spawn(w, null, Vector3.zero));
            Assert.AreEqual(0, VoxelLab.Planet.TargetSpawner.Spawn(null, null, Vector3.zero));
        }
    }
}
