// =====================================================================
//  VolumetricPhysics.cs
//  VoxelLab :: Physics
//
//  Físicas mínimas basadas en muestreo volumétrico:
//      - SampleDensity(pos)         densidad trilineal
//      - ResolveCollision(entity)   empuje a lo largo del gradiente si
//                                   la entidad penetra densidad >= 0.5
//      - ApplyGravity(entity, c)    gravedad local hacia centro del planeta
//      - Raymarch                   reutiliza VoxelWorld.RaySample
//
//  Las "entidades" son cualquier estructura/objeto con posición y
//  velocidad: definimos VolumetricBody como POCO; VoxelRigidbody es el
//  componente Unity que envuelve uno.
//
//  Dependencias: VoxelWorld.
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Physics
{
    /// <summary>Cuerpo simple para físicas voxel (no usa Rigidbody de Unity).</summary>
    public class VolumetricBody
    {
        public Vector3 position;
        public Vector3 velocity;
        public float radius = 0.5f;
        public float mass = 1f;
        public float drag = 0.05f;
    }

    /// <summary>Servicios estáticos de físicas volumétricas.</summary>
    public static class VolumetricPhysics
    {
        public const float SOLID_THRESHOLD = 0.5f;

        /// <summary>Densidad trilineal en pos (mundo).</summary>
        public static float SampleDensity(VoxelWorld world, Vector3 pos)
        {
            int x0 = Mathf.FloorToInt(pos.x), y0 = Mathf.FloorToInt(pos.y), z0 = Mathf.FloorToInt(pos.z);
            float fx = pos.x - x0, fy = pos.y - y0, fz = pos.z - z0;
            float c000 = world.GetVoxel(x0,   y0,   z0  ).densidad;
            float c100 = world.GetVoxel(x0+1, y0,   z0  ).densidad;
            float c010 = world.GetVoxel(x0,   y0+1, z0  ).densidad;
            float c110 = world.GetVoxel(x0+1, y0+1, z0  ).densidad;
            float c001 = world.GetVoxel(x0,   y0,   z0+1).densidad;
            float c101 = world.GetVoxel(x0+1, y0,   z0+1).densidad;
            float c011 = world.GetVoxel(x0,   y0+1, z0+1).densidad;
            float c111 = world.GetVoxel(x0+1, y0+1, z0+1).densidad;
            float c00 = Mathf.Lerp(c000, c100, fx);
            float c10 = Mathf.Lerp(c010, c110, fx);
            float c01 = Mathf.Lerp(c001, c101, fx);
            float c11 = Mathf.Lerp(c011, c111, fx);
            float c0  = Mathf.Lerp(c00, c10, fy);
            float c1  = Mathf.Lerp(c01, c11, fy);
            return Mathf.Lerp(c0, c1, fz);
        }

        /// <summary>Gradiente central de densidad (apuntando hacia mayor densidad).</summary>
        public static Vector3 SampleGradient(VoxelWorld world, Vector3 pos, float h = 0.5f)
        {
            float dx = SampleDensity(world, pos + new Vector3(h,0,0)) - SampleDensity(world, pos - new Vector3(h,0,0));
            float dy = SampleDensity(world, pos + new Vector3(0,h,0)) - SampleDensity(world, pos - new Vector3(0,h,0));
            float dz = SampleDensity(world, pos + new Vector3(0,0,h)) - SampleDensity(world, pos - new Vector3(0,0,h));
            return new Vector3(dx, dy, dz);
        }

        /// <summary>
        /// Resuelve colisión: si el cuerpo penetra densidad sólida, lo empuja por el
        /// gradiente (que apunta hacia más sólido) y frena la componente normal de la velocidad.
        /// </summary>
        public static bool ResolveCollision(VoxelWorld world, VolumetricBody body)
        {
            float d = SampleDensity(world, body.position);
            if (d < SOLID_THRESHOLD) return false;

            Vector3 grad = SampleGradient(world, body.position);
            if (grad.sqrMagnitude < 1e-6f) grad = Vector3.up;
            // Normal de la superficie apunta hacia el lado vacío (densidad menor).
            Vector3 normal = (-grad).normalized;

            // Empuje proporcional a la profundidad estimada (d - 0.5).
            float penetration = (d - SOLID_THRESHOLD) * body.radius * 2f + body.radius * 0.1f;
            body.position += normal * penetration;

            // Cancelar componente normal de la velocidad si va hacia adentro.
            float vn = Vector3.Dot(body.velocity, normal);
            if (vn < 0f)
            {
                body.velocity -= normal * vn;
                // Pequeño rebote/fricción mínima
                body.velocity *= (1f - body.drag);
            }
            return true;
        }

        /// <summary>Gravedad local hacia un centro (planeta esférico).</summary>
        public static void ApplyGravity(VolumetricBody body, Vector3 planetCenter, float g, float dt)
        {
            Vector3 toCenter = planetCenter - body.position;
            float dist = toCenter.magnitude;
            if (dist < 1e-3f) return;
            Vector3 dir = toCenter / dist;
            body.velocity += dir * g * dt;
        }

        /// <summary>Step explícito de Euler con drag, gravedad y resolución de colisión.</summary>
        public static void Step(VoxelWorld world, VolumetricBody body, Vector3 planetCenter, float gravity, float dt)
        {
            ApplyGravity(body, planetCenter, gravity, dt);
            body.velocity *= Mathf.Max(0f, 1f - body.drag * dt);
            body.position += body.velocity * dt;
            // Resolver hasta 4 iteraciones (penetraciones profundas).
            for (int i = 0; i < 4; i++)
                if (!ResolveCollision(world, body)) break;
        }

        /// <summary>
        /// Step en espacio libre (sin gravedad). Aplica un vector de gravedad
        /// arbitrario opcional (Vector3.zero para vacío real), drag exponencial
        /// y resolución de colisión voxel.
        /// </summary>
        public static void StepFreeSpace(VoxelWorld world, VolumetricBody body, Vector3 gravity, float dt)
        {
            if (gravity.sqrMagnitude > 0f)
                body.velocity += gravity * dt;
            if (body.drag > 0f)
                body.velocity *= Mathf.Max(0f, 1f - body.drag * dt);
            body.position += body.velocity * dt;
            for (int i = 0; i < 4; i++)
                if (!ResolveCollision(world, body)) break;
        }
    }
}
