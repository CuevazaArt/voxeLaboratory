// =====================================================================
//  ProjectileLauncher.cs
//  VoxelLab :: Physics
//
//  Propósito: gestionar disparo y simulación de proyectiles polimórficos
//  en un VoxelWorld. Soporta múltiples tipos vía ProjectileTypeDef y
//  ramifica el impacto según ProjectileImpactMode.
//
//  Invariantes:
//      - World debe inyectarse antes de Fire().
//      - Si availableTypes está vacío, se sintetiza un tipo implícito
//        a partir de los campos legacy (compat).
//      - Cluster respeta maxRecursionDepth para evitar explosión combinatoria.
//      - Plasma sites se actualizan en FixedUpdate y mueren al expirar.
//      - Render por Graphics.DrawMesh (sin GO por proyectil).
//
//  Dependencias: VoxelWorld, VolumetricPhysics, Projectile, ProjectileTypeDef.
//
//  Uso:
//      var launcher = gameObject.AddComponent<ProjectileLauncher>();
//      launcher.World = voxelWorld;
//      launcher.availableTypes = new[] { bullet, heRound };
//      launcher.Fire(origin, direction);
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Physics
{
    public class ProjectileLauncher : MonoBehaviour
    {
        // ------------------------------------------------------------------
        //  Tipos disponibles (autoría) y selección
        // ------------------------------------------------------------------

        [Header("Tipos disponibles (ScriptableObjects)")]
        public ProjectileTypeDef[] availableTypes;
        [Tooltip("Índice del tipo activo. 0..availableTypes.Length-1.")]
        public int activeIndex = 0;
        [Tooltip("Hotkeys numéricos (Alpha1..Alpha9) para cambiar el tipo activo.")]
        public bool enableHotkeys = true;

        // ------------------------------------------------------------------
        //  Magnitudes legacy (usadas si availableTypes está vacío)
        // ------------------------------------------------------------------

        [Header("Magnitudes legacy (compat si no hay tipos)")]
        public float mass = 1f;
        public float radius = 0.5f;
        public float initialSpeed = 50f;
        public float drag = 0f;

        [Header("Entorno")]
        public Vector3 gravity = Vector3.zero;
        public float lifetimeSeconds = 10f;
        public int maxActive = 64;

        [Header("Kinetic legacy (fallback si tipo == null)")]
        public bool applyDestructionOnImpact = true;
        public float baseImpactRadius = 0.2f;
        public float impactRadiusPerSqrtEnergy = 0.08f;
        public float maxImpactRadius = 8f;
        public float impactForceScale = 0.05f;
        public float maxImpactForce = 10f;

        [Header("Cluster")]
        [Min(0)] public int maxRecursionDepth = 2;

        [Header("Render")]
        public Mesh projectileMesh;
        public Material projectileMaterial;

        public VoxelWorld World { get; set; }

        // ------------------------------------------------------------------
        //  Tracking interno
        // ------------------------------------------------------------------

        private struct Tracked
        {
            public Projectile projectile;
            public float bornAt;
            public bool impacted;
            public Vector3 impactPosition;
            public Vector3 impactVelocity;
            public ProjectileTypeDef type;
            public int recursionDepth;
        }

        private struct PlasmaSite
        {
            public Vector3 center;
            public float radius;
            public float drainPerSec;
            public float bornAt;
            public float linger;
        }

        private readonly List<Tracked> _alive = new List<Tracked>();
        private readonly List<PlasmaSite> _plasma = new List<PlasmaSite>();

        // ------------------------------------------------------------------
        //  Telemetría
        // ------------------------------------------------------------------

        public int ActiveCount => _alive.Count;
        public int TotalShots { get; private set; }
        public int TotalImpacts { get; private set; }
        public int LastRemovedVoxels { get; private set; }
        public float LastImpactEnergy { get; private set; }
        public float LastImpactRadius { get; private set; }
        public Vector3 LastImpactPosition { get; private set; }
        public string LastImpactMode { get; private set; } = "-";

        public ProjectileTypeDef ActiveType
        {
            get
            {
                if (availableTypes == null || availableTypes.Length == 0) return null;
                int i = Mathf.Clamp(activeIndex, 0, availableTypes.Length - 1);
                return availableTypes[i];
            }
        }

        // ------------------------------------------------------------------
        //  Eventos
        // ------------------------------------------------------------------

        /// <summary>Disparado tras cada impacto (después de la destrucción).</summary>
        public System.Action<Projectile, Vector3, Vector3> OnImpact;
        /// <summary>Muestras de destrucción para alimentar al sistema de debris.</summary>
        public System.Action<ExplosionResult, Vector3 /*impactNormalEstimate*/> OnDestructionSamples;

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (projectileMesh == null) projectileMesh = BuildDefaultSphereMesh();
            if (projectileMaterial == null)
            {
                var sh = Shader.Find("Unlit/Color");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh != null)
                {
                    projectileMaterial = new Material(sh) { name = "VL_Projectile" };
                    if (projectileMaterial.HasProperty("_Color"))
                        projectileMaterial.color = new Color(1f, 0.85f, 0.2f, 1f);
                }
            }
        }

        private void Update()
        {
            if (enableHotkeys && availableTypes != null && availableTypes.Length > 0)
            {
                int max = Mathf.Min(availableTypes.Length, 9);
                for (int i = 0; i < max; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        activeIndex = i;
                        break;
                    }
                }
            }

            if (projectileMesh == null || projectileMaterial == null || _alive.Count == 0) return;
            for (int i = 0; i < _alive.Count; i++)
            {
                var t = _alive[i];
                Mesh m = t.type != null && t.type.mesh != null ? t.type.mesh : projectileMesh;
                Material mat = t.type != null && t.type.material != null ? t.type.material : projectileMaterial;
                Matrix4x4 trs = Matrix4x4.TRS(t.projectile.body.position, Quaternion.identity,
                    Vector3.one * (t.projectile.body.radius * 2f));
                Graphics.DrawMesh(m, trs, mat, 0);
            }
        }

        private void FixedUpdate()
        {
            if (World == null) return;
            float dt = Time.fixedDeltaTime;
            float now = Time.time;

            StepProjectiles(dt, now);
            StepPlasmaSites(dt, now);
        }

        // ------------------------------------------------------------------
        //  Disparo
        // ------------------------------------------------------------------

        /// <summary>Dispara un proyectil del tipo activo (o tipo implícito legacy).</summary>
        public Projectile Fire(Vector3 origin, Vector3 direction)
        {
            return FireOfType(origin, direction, ActiveType, recursionDepth: 0);
        }

        /// <summary>Dispara un proyectil de un tipo concreto.</summary>
        public Projectile FireOfType(Vector3 origin, Vector3 direction, ProjectileTypeDef type, int recursionDepth = 0)
        {
            if (World == null) return null;
            if (_alive.Count >= Mathf.Max(1, maxActive)) return null;

            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Projectile p;
            if (type != null)
                p = Projectile.Create(origin, dir * Mathf.Max(0f, type.initialSpeed), type);
            else
                p = Projectile.Create(origin, dir * Mathf.Max(0f, initialSpeed), mass, radius, drag);

            _alive.Add(new Tracked
            {
                projectile = p,
                bornAt = Time.time,
                impacted = false,
                type = type,
                recursionDepth = recursionDepth,
            });
            TotalShots++;
            return p;
        }

        public void Clear()
        {
            _alive.Clear();
            _plasma.Clear();
        }

        // ------------------------------------------------------------------
        //  Step proyectiles
        // ------------------------------------------------------------------

        private void StepProjectiles(float dt, float now)
        {
            if (_alive.Count == 0) return;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var t = _alive[i];
                Vector3 prevPos = t.projectile.body.position;
                Vector3 prevVel = t.projectile.body.velocity;
                VolumetricPhysics.StepFreeSpace(World, t.projectile.body, gravity, dt);

                bool justImpacted = !t.impacted &&
                    VolumetricPhysics.SampleDensity(World, t.projectile.body.position) >= VolumetricPhysics.SOLID_THRESHOLD;
                if (justImpacted)
                {
                    t.impacted = true;
                    t.impactPosition = t.projectile.body.position;
                    t.impactVelocity = prevVel;
                    HandleImpact(t.projectile, prevPos, t.impactPosition, t.impactVelocity, t.type, t.recursionDepth);
                    OnImpact?.Invoke(t.projectile, prevPos, t.impactPosition);
                }

                float life = (t.type != null ? t.type.lifetimeSeconds : lifetimeSeconds);
                bool dead = (now - t.bornAt) > Mathf.Max(0.1f, life);
                if (dead) { _alive.RemoveAt(i); continue; }
                _alive[i] = t;
            }
        }

        // ------------------------------------------------------------------
        //  Dispatch de impacto
        // ------------------------------------------------------------------

        private void HandleImpact(Projectile projectile, Vector3 prevPos, Vector3 impactPos, Vector3 impactVel,
            ProjectileTypeDef type, int recursionDepth)
        {
            TotalImpacts++;
            LastImpactPosition = impactPos;
            LastImpactEnergy = projectile != null ? projectile.KineticEnergy : 0f;

            if (!applyDestructionOnImpact || World == null) return;

            ProjectileImpactMode mode = type != null ? type.impactMode : ProjectileImpactMode.Kinetic;
            LastImpactMode = mode.ToString();

            switch (mode)
            {
                case ProjectileImpactMode.Kinetic:    ApplyKinetic(impactPos, type); break;
                case ProjectileImpactMode.Explosive:  ApplyExplosive(impactPos, type); break;
                case ProjectileImpactMode.Cluster:    ApplyCluster(impactPos, impactVel, type, recursionDepth); break;
                case ProjectileImpactMode.Plasma:     ApplyPlasma(impactPos, type); break;
                case ProjectileImpactMode.Penetrator: ApplyPenetrator(impactPos, impactVel, type); break;
            }
        }

        private void ApplyKinetic(Vector3 pos, ProjectileTypeDef type)
        {
            float baseR, perSqrt, fScale, maxR, maxF;
            if (type != null)
            {
                baseR = type.kinetic.craterRadiusBase;
                perSqrt = type.kinetic.radiusPerSqrtEnergy;
                fScale = type.kinetic.forceScale;
                maxR = type.kinetic.maxRadius;
                maxF = maxImpactForce;
            }
            else
            {
                baseR = baseImpactRadius;
                perSqrt = impactRadiusPerSqrtEnergy;
                fScale = impactForceScale;
                maxR = maxImpactRadius;
                maxF = maxImpactForce;
            }

            LastImpactRadius = ComputeImpactRadius(LastImpactEnergy, baseR, perSqrt, maxR);
            float force = Mathf.Clamp(LastImpactEnergy * Mathf.Max(0f, fScale), 0f, Mathf.Max(0.01f, maxF));
            var result = World.Explosion(pos, LastImpactRadius, force);
            LastRemovedVoxels = Mathf.Max(0, result.voxelsRemoved);
            EmitDestructionSamples(result, EstimateImpactNormal(pos));
        }

        private void ApplyExplosive(Vector3 pos, ProjectileTypeDef type)
        {
            var p = type != null ? type.explosive : ExplosiveImpactParams.Default;
            LastImpactRadius = Mathf.Max(0.1f, p.blastRadius);
            float force = Mathf.Max(0.01f, p.blastForce);
            var result = World.Explosion(pos, LastImpactRadius, force);
            LastRemovedVoxels = Mathf.Max(0, result.voxelsRemoved);
            EmitDestructionSamples(result, EstimateImpactNormal(pos));
        }

        private void ApplyCluster(Vector3 pos, Vector3 velocity, ProjectileTypeDef type, int depth)
        {
            if (type == null) return;
            if (depth >= Mathf.Max(0, maxRecursionDepth)) return;

            var p = type.cluster;
            var sub = p.subProjectileType;
            if (sub == null || p.count <= 0) return;

            Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            // Spawnea fuera de la superficie golpeada.
            Vector3 spawn = pos - dir * 0.5f;
            float spread = Mathf.Clamp(p.spreadAngleDeg, 0f, 180f);
            float speedScale = Mathf.Max(0f, p.subSpeedScale);
            int count = Mathf.Min(p.count, 16);

            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = Random.insideUnitSphere;
                Vector3 outDir = Vector3.Slerp(dir, jitter.sqrMagnitude > 1e-6f ? jitter.normalized : dir,
                    Random.value * spread / 180f);
                if (outDir.sqrMagnitude < 1e-6f) outDir = dir;
                outDir.Normalize();

                float subSpeed = (sub.initialSpeed > 0f ? sub.initialSpeed : velocity.magnitude) * speedScale;
                if (subSpeed <= 0f) subSpeed = velocity.magnitude * 0.5f;

                FireOfType(spawn, outDir, sub, depth + 1);
                if (_alive.Count > 0)
                {
                    var last = _alive[_alive.Count - 1];
                    last.projectile.body.velocity = outDir * subSpeed;
                    _alive[_alive.Count - 1] = last;
                }
            }
        }

        private void ApplyPlasma(Vector3 pos, ProjectileTypeDef type)
        {
            var p = type != null ? type.plasma : PlasmaImpactParams.Default;
            float r = Mathf.Max(0.5f, p.meltRadius);
            float drain = Mathf.Max(0f, p.densityDrainPerSec);
            float linger = Mathf.Max(0f, p.lingerSeconds);

            var result = World.Explosion(pos, r, drain * 0.5f);
            LastImpactRadius = r;
            LastRemovedVoxels = Mathf.Max(0, result.voxelsRemoved);
            EmitDestructionSamples(result, EstimateImpactNormal(pos));

            if (linger > 0f && drain > 0f)
            {
                _plasma.Add(new PlasmaSite
                {
                    center = pos,
                    radius = r,
                    drainPerSec = drain,
                    bornAt = Time.time,
                    linger = linger,
                });
            }
        }

        private void StepPlasmaSites(float dt, float now)
        {
            if (_plasma.Count == 0) return;
            for (int i = _plasma.Count - 1; i >= 0; i--)
            {
                var s = _plasma[i];
                if (now - s.bornAt > s.linger) { _plasma.RemoveAt(i); continue; }
                World.CarveSphere(s.center, s.radius, Mathf.Clamp01(s.drainPerSec * dt));
            }
        }

        private void ApplyPenetrator(Vector3 pos, Vector3 velocity, ProjectileTypeDef type)
        {
            var p = type != null ? type.penetrator : PenetratorImpactParams.Default;
            float depth = Mathf.Clamp(p.tunnelDepth, 0.5f, 64f);
            float r = Mathf.Clamp(p.tunnelRadius, 0.1f, 8f);

            Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            int steps = Mathf.CeilToInt(depth / 0.5f);
            int totalRemoved = 0;
            int matCount = MaterialTable.Count;
            var aggregateByMat = new int[matCount];
            var aggregateSamples = new System.Collections.Generic.List<DestructionSample>(steps * 4);

            for (int i = 0; i < steps; i++)
            {
                Vector3 q = pos + dir * (i * 0.5f);
                var res = World.Explosion(q, r, Mathf.Max(0.1f, p.secondaryForce));
                totalRemoved += res.voxelsRemoved;
                if (res.removedByMaterial != null)
                    for (int m = 0; m < matCount && m < res.removedByMaterial.Length; m++)
                        aggregateByMat[m] += res.removedByMaterial[m];
                if (res.samples != null) aggregateSamples.AddRange(res.samples);
            }

            LastImpactRadius = r;
            LastRemovedVoxels = totalRemoved;
            var aggregate = new ExplosionResult
            {
                voxelsRemoved = totalRemoved,
                center = pos,
                radius = r,
                force = p.secondaryForce,
                removedByMaterial = aggregateByMat,
                samples = aggregateSamples,
            };
            EmitDestructionSamples(aggregate, -dir);
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        public static float ComputeImpactRadius(float kineticEnergy, float baseRadius, float radiusPerSqrtEnergy, float maxRadius)
        {
            float e = Mathf.Max(0f, kineticEnergy);
            float r = Mathf.Max(0.01f, baseRadius) + Mathf.Max(0f, radiusPerSqrtEnergy) * Mathf.Sqrt(e);
            return Mathf.Min(r, Mathf.Max(0.01f, maxRadius));
        }

        private Vector3 EstimateImpactNormal(Vector3 pos)
        {
            int xi = Mathf.FloorToInt(pos.x), yi = Mathf.FloorToInt(pos.y), zi = Mathf.FloorToInt(pos.z);
            return World != null ? World.EstimateNormal(xi, yi, zi) : Vector3.up;
        }

        private void EmitDestructionSamples(ExplosionResult result, Vector3 normal)
        {
            if (result.voxelsRemoved <= 0) return;
            OnDestructionSamples?.Invoke(result, normal);
        }

        private static Mesh BuildDefaultSphereMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var mf = temp.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }
            finally
            {
                Destroy(temp);
            }
        }
    }
}
