// =====================================================================
//  FirstPersonCamera.cs
//  VoxelLab :: Cameras
//
//  Cámara FPS adherida a la superficie del planeta. Usa físicas
//  voxel: gravedad local, salto, alineación al gradiente.
// =====================================================================
using UnityEngine;
using VoxelLab.Physics;

namespace VoxelLab.Cameras
{
    [RequireComponent(typeof(VoxelRigidbody))]
    public class FirstPersonCamera : VoxelLabCameraBase
    {
        public float walkSpeed = 6f;
        public float jumpImpulse = 6f;
        public float lookSensitivity = 2f;

        private float _yaw, _pitch;
        private VoxelRigidbody _rb;

        private void Awake() { _rb = GetComponent<VoxelRigidbody>(); }

        protected override void OnEnableCamera()
        {
            var e = transform.eulerAngles;
            _yaw = e.y; _pitch = 0f;
        }

        private void Update()
        {
            // Mirar
            _yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);

            // Alinear "up" al gradiente del planeta (radial).
            Vector3 up = (transform.position - (_rb.planetCenter ? _rb.planetCenter.position : Vector3.zero)).normalized;
            if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
            Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, up);
            transform.rotation = baseRot * Quaternion.Euler(_pitch, _yaw, 0f);

            // Movimiento tangencial
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, fwd);
            Vector3 m = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) m += fwd;
            if (Input.GetKey(KeyCode.S)) m -= fwd;
            if (Input.GetKey(KeyCode.D)) m += right;
            if (Input.GetKey(KeyCode.A)) m -= right;
            if (m.sqrMagnitude > 0.001f)
            {
                m = m.normalized * walkSpeed;
                // Replace planar component with desired velocity, keep radial component.
                _rb.body.velocity = m + Vector3.Project(_rb.body.velocity, up);
            }
            if (Input.GetKeyDown(KeyCode.Space))
                _rb.AddImpulse(up * jumpImpulse);
        }
    }
}
