// =====================================================================
//  VoxelLabCameraBase.cs / CameraSwitcher.cs
//  VoxelLab :: Cameras
//
//  Base común y switcher entre tipos de cámaras.
//  Teclas por defecto: F1 fly, F2 orbital, F3 fps, F4 superficie.
// =====================================================================
using UnityEngine;

namespace VoxelLab.Cameras
{
    public abstract class VoxelLabCameraBase : MonoBehaviour
    {
        protected virtual void OnEnableCamera() { }
        public void Activate() { enabled = true; OnEnableCamera(); }
        public void Deactivate() { enabled = false; }
    }

    /// <summary>Mantiene una lista de cámaras y solo deja activa la seleccionada.</summary>
    public class CameraSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public string label;
            public Camera camera;
            public VoxelLabCameraBase controller;
            public KeyCode hotkey = KeyCode.F1;
        }

        public Slot[] slots;
        public int active = 0;

        private void Start()
        {
            Apply();
        }

        private void Update()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (Input.GetKeyDown(slots[i].hotkey)) { active = i; Apply(); }
            }
        }

        public void Apply()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                bool on = i == active;
                if (slots[i].camera != null) slots[i].camera.enabled = on;
                if (slots[i].controller != null)
                {
                    if (on) slots[i].controller.Activate(); else slots[i].controller.Deactivate();
                }
            }
        }
    }
}
