// =====================================================================
//  OrbitalCamera.cs
//  VoxelLab :: Cameras
//
//  Orbita alrededor de un target (centro del planeta por defecto).
//  Botón derecho = rotar, scroll = zoom.
// =====================================================================
using UnityEngine;

namespace VoxelLab.Cameras
{
    public class OrbitalCamera : VoxelLabCameraBase
    {
        public Transform target;
        public float distance = 80f;
        public float minDistance = 5f;
        public float maxDistance = 500f;
        public float yawSpeed = 120f;
        public float pitchSpeed = 120f;
        public float zoomSpeed = 30f;

        private float _yaw = 0f, _pitch = 20f;

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * yawSpeed * Time.deltaTime;
                _pitch -= Input.GetAxis("Mouse Y") * pitchSpeed * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            }
            distance -= Input.mouseScrollDelta.y * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            Vector3 t = target != null ? target.position : Vector3.zero;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0);
            transform.position = t + rot * new Vector3(0, 0, -distance);
            transform.LookAt(t);
        }
    }
}
