// =====================================================================
//  ProjectileLauncher.cs
//  VoxelLab :: Physics
//
//  Propósito: componente Unity que gestiona el disparo y simulación de
//  proyectiles voxel en un mundo dado. Usa StepFreeSpace por defecto
//  (espacio sin gravedad). Ideal para el laboratorio balístico.
//
//  Invariantes:
//      - World debe inyectarse antes del primer Fire().
//      - Magnitudes (mass, radius, speed, drag) se sanean en Fire().
//      - No mantiene referencias a GameObjects por proyectil; los
//        renderiza con Graphics.DrawMesh para evitar GO por proyectil.
//      - Limita el número de proyectiles activos por seguridad.
//
//  Dependencias: VoxelWorld, VolumetricPhysics, Projectile.
//
//  Uso:
//      var launcher = gameObject.AddComponent<ProjectileLauncher>();
//      launcher.World = voxelWorld;
//      launcher.Fire(origin, direction);
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Physics
{
    public class ProjectileLauncher : MonoBehaviour
    {
        [Header("Magnitudes del proyectil")]
        public float mass = 1f;             // kg (unidad arbitraria)
        public float radius = 0.5f;         // voxels
        public float initialSpeed = 50f;    // voxels/s
        public float drag = 0f;             // 0 = vacío

        [Header("Entorno")]
        public Vector3 gravity = Vector3.zero;  // Vector3.zero = sin gravedad
        public float lifetimeSeconds = 10f;
        public int maxActive = 64;

        [Header("Impacto en voxels")]
        public bool applyDestructionOnImpact = true;
        public float baseImpactRadius = 0.2f;
        public float impactRadiusPerSqrtEnergy = 0.08f;
        public float maxImpactRadius = 8f;
        public float impactForceScale = 0.05f;
        public float maxImpactForce = 10f;

        [Header("Render")]
        public Mesh projectileMesh;
        public Material projectileMaterial;

        public VoxelWorld World { get; set; }

        private struct Tracked
        {
            public Projectile projectile;
            public float bornAt;
            public bool impacted;
            public Vector3 impactPosition;
        }

        private readonly List<Tracked> _alive = new List<Tracked>();

        public int ActiveCount => _alive.Count;
        public int TotalShots { get; private set; }
        public int TotalImpacts { get; private set; }
        public int LastRemovedVoxels { get; private set; }
        public float LastImpactEnergy { get; private set; }
        public float LastImpactRadius { get; private set; }
        public Vector3 LastImpactPosition { get; private set; }

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
            OnImpact += HandleImpact;
        }

        private void OnDestroy()
        {
            OnImpact -= HandleImpact;
        }

        /// <summary>Dispara un proyectil desde origin en la dirección dada.</summary>
        public Projectile Fire(Vector3 origin, Vector3 direction)
        {
            if (World == null) return null;
            if (_alive.Count >= Mathf.Max(1, maxActive)) return null;

            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            float speed = Mathf.Max(0f, initialSpeed);
            var p = Projectile.Create(origin, dir * speed, mass, radius, drag);
            _alive.Add(new Tracked { projectile = p, bornAt = Time.time, impacted = false });
            TotalShots++;
            return p;
        }

        public static float ComputeImpactRadius(float kineticEnergy, float baseRadius, float radiusPerSqrtEnergy, float maxRadius)
        {
            float e = Mathf.Max(0f, kineticEnergy);
            float r = Mathf.Max(0.01f, baseRadius) + Mathf.Max(0f, radiusPerSqrtEnergy) * Mathf.Sqrt(e);
            return Mathf.Min(r, Mathf.Max(0.01f, maxRadius));
        }

        private void HandleImpact(Projectile projectile, Vector3 previousPosition, Vector3 impactPosition)
        {
            TotalImpacts++;
            LastImpactPosition = impactPosition;
            LastImpactEnergy = projectile != null ? projectile.KineticEnergy : 0f;
            LastImpactRadius = ComputeImpactRadius(
                LastImpactEnergy,
                baseImpactRadius,
                impactRadiusPerSqrtEnergy,
                maxImpactRadius);

            LastRemovedVoxels = 0;
            if (!applyDestructionOnImpact || World == null) return;

            float force = Mathf.Clamp(LastImpactEnergy * Mathf.Max(0f, impactForceScale), 0f, Mathf.Max(0.01f, maxImpactForce));
            var result = World.Explosion(impactPosition, LastImpactRadius, force);
            LastRemovedVoxels = Mathf.Max(0, result.voxelsRemoved);
        }

        private void FixedUpdate()
        {
            if (World == null || _alive.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            float now = Time.time;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var t = _alive[i];
                Vector3 prevPos = t.projectile.body.position;
                VolumetricPhysics.StepFreeSpace(World, t.projectile.body, gravity, dt);

                bool justImpacted = !t.impacted &&
                    VolumetricPhysics.SampleDensity(World, t.projectile.body.position) >= VolumetricPhysics.SOLID_THRESHOLD;
                if (justImpacted)
                {
                    t.impacted = true;
                    t.impactPosition = t.projectile.body.position;
                    OnImpact?.Invoke(t.projectile, prevPos, t.impactPosition);
                }

                bool dead = (now - t.bornAt) > Mathf.Max(0.1f, lifetimeSeconds);
                if (dead) { _alive.RemoveAt(i); continue; }
                _alive[i] = t;
            }
        }

        private void Update()
        {
            if (projectileMesh == null || projectileMaterial == null || _alive.Count == 0) return;
            for (int i = 0; i < _alive.Count; i++)
            {
                var t = _alive[i];
                Matrix4x4 m = Matrix4x4.TRS(t.projectile.body.position, Quaternion.identity,
                    Vector3.one * (t.projectile.body.radius * 2f));
                Graphics.DrawMesh(projectileMesh, m, projectileMaterial, 0);
            }
        }

        /// <summary>Evento opcional para reaccionar a impactos (analítica, carve, etc.).</summary>
        public System.Action<Projectile, Vector3, Vector3> OnImpact;

        /// <summary>Limpia todos los proyectiles activos.</summary>
        public void Clear() => _alive.Clear();

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
