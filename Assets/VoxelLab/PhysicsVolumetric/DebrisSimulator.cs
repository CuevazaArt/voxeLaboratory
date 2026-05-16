// =====================================================================
//  DebrisSimulator.cs
//  VoxelLab :: Physics
//
//  Propósito: simular partículas de debris derivadas de la destrucción
//  voxel. Mantiene un buffer plano de instancias (NativeArray) y avanza
//  posición/velocidad con un IJobParallelFor Burst-compilable.
//
//  Invariantes:
//      - capacity es fijo en construcción; nuevos spawns sobre lleno
//        siguen política FIFO (sobrescriben el más viejo vivo).
//      - Step requiere haber sido inicializado (Initialize llamado).
//      - La colisión voxel se resuelve main-thread (post-job) para no
//        mover VoxelWorld a NativeContainers en esta jornada.
//      - Los perfiles se registran por profileKey y se buscan con
//        ResolveByKey o ResolveByMaterial.
//      - secondaryEffectRadius del material no se aplica aquí (reservado).
//
//  Dependencias: VoxelWorld, VolumetricPhysics, VoxelMaterialDescriptor,
//      DebrisProfileDef, Unity.Burst, Unity.Collections, Unity.Mathematics.
//
//  Uso:
//      var sim = new DebrisSimulator(4096);
//      sim.RegisterProfile(profile);
//      sim.SpawnFromExplosion(world, explosion, normal, gravity);
//      sim.Step(world, gravity, Time.fixedDeltaTime);
//      sim.Dispose();
// =====================================================================
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Physics
{
    /// <summary>Instancia plana de un debris. Compatible con Burst (POD blittable).</summary>
    public struct DebrisInstance
    {
        public float3 position;
        public float3 velocity;
        public float ageSec;
        public float lifetimeSec;
        public float scale;
        public float restitution;
        public float drag;
        public float gravityScale;
        public byte materialId;
        public byte profileIndex;
        public byte alive;     // 0 = libre, 1 = vivo
        public byte _pad;
    }

    /// <summary>Simulador POCO. Suelo envolverlo desde un MonoBehaviour si hace falta.</summary>
    public class DebrisSimulator : System.IDisposable
    {
        public const int DEFAULT_CAPACITY = 4096;

        private NativeArray<DebrisInstance> _instances;
        private int _capacity;
        private int _activeCount;
        private int _writeCursor;     // FIFO ring-buffer cursor
        private bool _initialized;

        private readonly List<DebrisProfileDef> _profiles = new List<DebrisProfileDef>(8);
        private readonly Dictionary<string, int> _profileByKey = new Dictionary<string, int>();
        private readonly Dictionary<byte, int> _profileByMaterial = new Dictionary<byte, int>();

        public int Capacity => _capacity;
        public int ActiveCount => _activeCount;
        public IReadOnlyList<DebrisProfileDef> Profiles => _profiles;
        public NativeArray<DebrisInstance> Instances => _instances;

        public DebrisSimulator(int capacity = DEFAULT_CAPACITY)
        {
            _capacity = Mathf.Max(1, capacity);
            _instances = new NativeArray<DebrisInstance>(_capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _initialized = true;
        }

        public void Dispose()
        {
            if (_initialized && _instances.IsCreated) _instances.Dispose();
            _initialized = false;
        }

        // ------------------------------------------------------------------
        //  Profile registry
        // ------------------------------------------------------------------

        public int RegisterProfile(DebrisProfileDef profile)
        {
            if (profile == null) return -1;
            if (_profileByKey.TryGetValue(profile.profileKey ?? string.Empty, out int existing))
                return existing;
            int idx = _profiles.Count;
            if (idx >= 255)
            {
                Debug.LogWarning("DebrisSimulator: cap de perfiles (255) alcanzado.");
                return -1;
            }
            _profiles.Add(profile);
            _profileByKey[profile.profileKey ?? string.Empty] = idx;
            return idx;
        }

        /// <summary>Mapea un material id al índice de perfil (precomputado para spawn rápido).</summary>
        public void MapMaterialToProfile(byte materialId, string profileKey)
        {
            if (string.IsNullOrEmpty(profileKey)) return;
            if (_profileByKey.TryGetValue(profileKey, out int idx))
                _profileByMaterial[materialId] = idx;
        }

        public int ResolveByMaterial(byte materialId)
        {
            return _profileByMaterial.TryGetValue(materialId, out int idx) ? idx : -1;
        }

        public int ResolveByKey(string key)
        {
            return _profileByKey.TryGetValue(key ?? string.Empty, out int idx) ? idx : -1;
        }

        // ------------------------------------------------------------------
        //  Spawn
        // ------------------------------------------------------------------

        /// <summary>
        /// Genera debris a partir de las muestras de un ExplosionResult, usando
        /// el perfil mapeado por material (o el default si profileKeyFallback != null).
        /// Aplica sampleFraction de cada perfil para decidir cuántas instancias spawnear.
        /// </summary>
        public int SpawnFromExplosion(ExplosionResult result, Vector3 normal,
            string profileKeyFallback = null, int? rngSeed = null)
        {
            if (result.samples == null || result.samples.Count == 0) return 0;

            int fallbackIdx = -1;
            if (!string.IsNullOrEmpty(profileKeyFallback))
                fallbackIdx = ResolveByKey(profileKeyFallback);

            var rng = new System.Random(rngSeed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            int spawned = 0;
            float3 normal3 = new float3(normal.x, normal.y, normal.z);
            if (math.lengthsq(normal3) < 1e-6f) normal3 = new float3(0, 1, 0);
            normal3 = math.normalize(normal3);

            for (int i = 0; i < result.samples.Count; i++)
            {
                var s = result.samples[i];
                int profIdx = ResolveByMaterial(s.material);
                if (profIdx < 0) profIdx = fallbackIdx;
                if (profIdx < 0) continue;

                var profile = _profiles[profIdx];
                if (profile == null) continue;

                // sampleFraction estocástico.
                if (rng.NextDouble() > profile.sampleFraction) continue;

                if (!TrySpawnOne(s.position, normal3, s.material, (byte)profIdx, profile, rng))
                    break; // capacidad agotada (FIFO sobreescribirá)
                spawned++;
            }
            return spawned;
        }

        private bool TrySpawnOne(Vector3 pos, float3 normal, byte materialId, byte profileIndex,
            DebrisProfileDef profile, System.Random rng)
        {
            int slot = AllocateSlot();
            if (slot < 0) return false;

            float speedMin = Mathf.Min(profile.initialSpeedMin, profile.initialSpeedMax);
            float speedMax = Mathf.Max(profile.initialSpeedMin, profile.initialSpeedMax);
            float speed = Mathf.Lerp(speedMin, speedMax, (float)rng.NextDouble());

            float spreadRad = math.radians(Mathf.Clamp(profile.spreadAngleDeg, 0f, 180f) * 0.5f);
            float3 dir = SampleConeDirection(normal, spreadRad, rng);

            var inst = new DebrisInstance
            {
                position = new float3(pos.x, pos.y, pos.z),
                velocity = dir * speed,
                ageSec = 0f,
                lifetimeSec = Mathf.Max(0.05f, profile.lifetimeSeconds),
                scale = profile.scale,
                restitution = Mathf.Clamp01(profile.restitution),
                drag = Mathf.Max(0f, profile.drag),
                gravityScale = profile.gravityScale,
                materialId = materialId,
                profileIndex = profileIndex,
                alive = 1,
                _pad = 0,
            };
            _instances[slot] = inst;
            return true;
        }

        private int AllocateSlot()
        {
            // 1) buscar slot muerto a partir del cursor (rápido si hay huecos).
            int n = _capacity;
            for (int i = 0; i < n; i++)
            {
                int idx = (_writeCursor + i) % n;
                if (_instances[idx].alive == 0)
                {
                    _writeCursor = (idx + 1) % n;
                    _activeCount = Mathf.Min(_activeCount + 1, _capacity);
                    return idx;
                }
            }
            // 2) FIFO: reusar el más viejo (cursor actual).
            int slot = _writeCursor;
            _writeCursor = (_writeCursor + 1) % n;
            return slot;
        }

        private static float3 SampleConeDirection(float3 axis, float halfAngleRad, System.Random rng)
        {
            // Muestra dirección uniforme en cono alrededor de 'axis' con semi-ángulo halfAngleRad.
            float cosTheta = Mathf.Lerp(Mathf.Cos(halfAngleRad), 1f, (float)rng.NextDouble());
            float sinTheta = math.sqrt(math.max(0f, 1f - cosTheta * cosTheta));
            float phi = (float)(rng.NextDouble() * math.PI * 2.0);
            float3 local = new float3(math.cos(phi) * sinTheta, math.sin(phi) * sinTheta, cosTheta);

            // Construye base ortonormal con axis como eje Z.
            float3 z = math.normalize(axis);
            float3 up = math.abs(z.y) < 0.99f ? new float3(0, 1, 0) : new float3(1, 0, 0);
            float3 x = math.normalize(math.cross(up, z));
            float3 y = math.cross(z, x);
            return math.normalize(local.x * x + local.y * y + local.z * z);
        }

        // ------------------------------------------------------------------
        //  Step
        // ------------------------------------------------------------------

        /// <summary>
        /// Avanza un paso de simulación. Ejecuta job Burst para integración y
        /// resuelve colisiones voxel main-thread (cap por <paramref name="maxCollisionResolves"/>).
        /// </summary>
        public void Step(VoxelWorld world, Vector3 gravity, float dt, int maxCollisionResolves = 256)
        {
            if (!_initialized || dt <= 0f) return;

            var job = new DebrisStepJob
            {
                instances = _instances,
                gravity = new float3(gravity.x, gravity.y, gravity.z),
                dt = dt,
            };
            JobHandle handle = job.Schedule(_capacity, 64);
            handle.Complete();

            // Recompute active count + collisions main-thread.
            int active = 0;
            int collisionsLeft = Mathf.Max(0, maxCollisionResolves);
            if (world != null)
            {
                for (int i = 0; i < _capacity; i++)
                {
                    var inst = _instances[i];
                    if (inst.alive == 0) continue;

                    if (collisionsLeft > 0)
                    {
                        var body = new VolumetricBody
                        {
                            position = new Vector3(inst.position.x, inst.position.y, inst.position.z),
                            velocity = new Vector3(inst.velocity.x, inst.velocity.y, inst.velocity.z),
                            radius = Mathf.Max(0.05f, inst.scale * 0.5f),
                            mass = 0.1f,
                            drag = 0f,
                        };
                        if (VolumetricPhysics.ResolveCollision(world, body))
                        {
                            // Aplicar restitution simple sobre la componente de velocidad recién corregida.
                            inst.position = new float3(body.position.x, body.position.y, body.position.z);
                            float bounce = inst.restitution;
                            inst.velocity = new float3(
                                body.velocity.x * bounce,
                                body.velocity.y * bounce,
                                body.velocity.z * bounce);
                            collisionsLeft--;
                        }
                    }

                    if (inst.ageSec >= inst.lifetimeSec)
                    {
                        inst.alive = 0;
                    }
                    else
                    {
                        active++;
                    }
                    _instances[i] = inst;
                }
            }
            else
            {
                for (int i = 0; i < _capacity; i++)
                {
                    var inst = _instances[i];
                    if (inst.alive == 0) continue;
                    if (inst.ageSec >= inst.lifetimeSec) { inst.alive = 0; _instances[i] = inst; continue; }
                    active++;
                }
            }
            _activeCount = active;
        }

        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _instances[i];
                inst.alive = 0;
                _instances[i] = inst;
            }
            _activeCount = 0;
            _writeCursor = 0;
        }
    }

    /// <summary>Job Burst-compilable para integrar posición/velocidad/edad.</summary>
    [BurstCompile]
    public struct DebrisStepJob : IJobParallelFor
    {
        public NativeArray<DebrisInstance> instances;
        public float3 gravity;
        public float dt;

        public void Execute(int index)
        {
            var inst = instances[index];
            if (inst.alive == 0) return;

            // Gravedad escalada por perfil.
            inst.velocity += gravity * (inst.gravityScale * dt);
            // Drag exponencial.
            float decay = math.max(0f, 1f - inst.drag * dt);
            inst.velocity *= decay;
            // Integración de posición.
            inst.position += inst.velocity * dt;
            // Edad.
            inst.ageSec += dt;

            instances[index] = inst;
        }
    }
}
