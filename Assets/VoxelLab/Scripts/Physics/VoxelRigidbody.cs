// =====================================================================
//  VoxelRigidbody.cs
//  VoxelLab :: Physics
//
//  MonoBehaviour que envuelve un VolumetricBody y lo sincroniza con
//  el transform. Necesita una referencia a un VoxeLab (para el mundo
//  y centro de planeta).
//
//  Dependencias: VolumetricPhysics, VoxeLab (bootstrapper).
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Physics
{
    public class VoxelRigidbody : MonoBehaviour
    {
        public float radius = 0.5f;
        public float mass = 1f;
        public float drag = 0.05f;
        public float gravity = 9.8f;
        public Transform planetCenter;

        [HideInInspector] public VolumetricBody body;

        // Inyectado por el bootstrapper.
        public VoxelWorld World { get; set; }

        private void Awake()
        {
            body = new VolumetricBody
            {
                position = transform.position,
                velocity = Vector3.zero,
                radius = radius,
                mass = mass,
                drag = drag,
            };
        }

        private void FixedUpdate()
        {
            if (World == null) return;
            Vector3 c = planetCenter != null ? planetCenter.position : Vector3.zero;
            VolumetricPhysics.Step(World, body, c, gravity, Time.fixedDeltaTime);
            transform.position = body.position;
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (body != null) body.velocity += impulse / Mathf.Max(0.001f, body.mass);
        }
    }
}
