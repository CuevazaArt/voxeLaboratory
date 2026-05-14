// =====================================================================
//  FlyCamera.cs
//  VoxelLab :: Cameras
//
//  Cámara libre estilo "fly through". WASD + Q/E vertical, ratón con
//  botón derecho para rotar, shift para acelerar.
// =====================================================================
using UnityEngine;

namespace VoxelLab.Cameras
{
    public class FlyCamera : VoxelLabCameraBase
    {
        public float speed = 10f;
        public float boost = 4f;
        public float lookSensitivity = 2f;

        private float _yaw, _pitch;

        protected override void OnEnableCamera()
        {
            var e = transform.eulerAngles;
            _yaw = e.y; _pitch = e.x;
        }

        private void Update()
        {
            if (!enabled) return;
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            }

            float s = speed * (Input.GetKey(KeyCode.LeftShift) ? boost : 1f);
            Vector3 m = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) m += transform.forward;
            if (Input.GetKey(KeyCode.S)) m -= transform.forward;
            if (Input.GetKey(KeyCode.D)) m += transform.right;
            if (Input.GetKey(KeyCode.A)) m -= transform.right;
            if (Input.GetKey(KeyCode.E)) m += transform.up;
            if (Input.GetKey(KeyCode.Q)) m -= transform.up;
            transform.position += m * s * Time.deltaTime;
        }
    }
}
