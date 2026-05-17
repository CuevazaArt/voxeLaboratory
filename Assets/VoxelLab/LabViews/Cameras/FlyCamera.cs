// =====================================================================
//  FlyCamera.cs
//  VoxelLab :: Cameras
//
//  Cámara libre estilo "fly through". WASD + Q/E vertical, ratón con
//  botón derecho para rotar, shift para acelerar.
// =====================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelLab.Cameras
{
    public class FlyCamera : VoxelLabCameraBase
    {
        public float speed = 10f;
        public float boost = 4f;
        public float lookSensitivity = 0.2f;

        private float _yaw, _pitch;

        protected override void OnEnableCamera()
        {
            var e = transform.eulerAngles;
            _yaw = e.y; _pitch = e.x;
        }

        private void Update()
        {
            if (!enabled) return;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                _yaw   += Mouse.current.delta.x.ReadValue() * lookSensitivity;
                _pitch -= Mouse.current.delta.y.ReadValue() * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            }

            float s = speed;
            Vector3 m = Vector3.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftShiftKey.isPressed) s *= boost;
                if (Keyboard.current.wKey.isPressed) m += transform.forward;
                if (Keyboard.current.sKey.isPressed) m -= transform.forward;
                if (Keyboard.current.dKey.isPressed) m += transform.right;
                if (Keyboard.current.aKey.isPressed) m -= transform.right;
                if (Keyboard.current.eKey.isPressed) m += transform.up;
                if (Keyboard.current.qKey.isPressed) m -= transform.up;
            }
            transform.position += m * s * Time.deltaTime;
        }
    }
}
