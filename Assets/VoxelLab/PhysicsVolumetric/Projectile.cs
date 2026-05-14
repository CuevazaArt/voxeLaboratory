// =====================================================================
//  Projectile.cs
//  VoxelLab :: Physics
//
//  Propósito: descripción de un proyectil balístico para el laboratorio
//  voxel. Es un POCO con magnitudes seteables (masa, radio, velocidad,
//  drag) que se evalúa en espacio libre o con gravedad arbitraria.
//
//  Invariantes:
//      - mass, radius son > 0 (sanitizados al construir).
//      - drag >= 0.
//      - velocity en unidades de voxels por segundo.
//      - No depende de Unity más allá de Vector3.
//
//  Dependencias: VolumetricBody (lo extiende lógicamente), no requiere
//      ChunkSystem en construcción.
//
//  Uso:
//      var p = Projectile.Create(pos, dir.normalized * speed, mass, radius, drag);
//      VolumetricPhysics.StepFreeSpace(world, p.body, Vector3.zero, dt);
// =====================================================================
using UnityEngine;

namespace VoxelLab.Physics
{
    /// <summary>Proyectil simple: contenedor de magnitudes y cuerpo volumétrico.</summary>
    public class Projectile
    {
        public readonly VolumetricBody body;

        /// <summary>Energía cinética actual: 0.5 * m * |v|^2.</summary>
        public float KineticEnergy
        {
            get
            {
                float v2 = body.velocity.sqrMagnitude;
                return 0.5f * body.mass * v2;
            }
        }

        /// <summary>Velocidad escalar (módulo del vector velocidad).</summary>
        public float Speed => body.velocity.magnitude;

        private Projectile(VolumetricBody body) { this.body = body; }

        /// <summary>Crea un proyectil con magnitudes saneadas.</summary>
        public static Projectile Create(Vector3 position, Vector3 velocity, float mass, float radius, float drag)
        {
            var b = new VolumetricBody
            {
                position = position,
                velocity = velocity,
                mass = Mathf.Max(0.0001f, mass),
                radius = Mathf.Max(0.01f, radius),
                drag = Mathf.Max(0f, drag),
            };
            return new Projectile(b);
        }
    }
}
