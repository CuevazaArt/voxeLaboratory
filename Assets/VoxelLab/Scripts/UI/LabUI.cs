// =====================================================================
//  LabUI.cs
//  VoxelLab :: UI
//
//  Panel IMGUI minimalista (no requiere prefabs ni Canvas) con:
//      - Selector de material
//      - Sliders: tamaño herramienta, intensidad, LOD
//      - Selector de herramienta
//      - Toggles de overlays
//      - Selector de cámara
//      - Botón "Regenerar planeta"
//
//  IMGUI = OnGUI: deliberadamente espartano para que el laboratorio
//  funcione fuera de la caja sin depender de UGUI/UIToolkit.
//
//  Dependencias: ToolManager, OverlayController, CameraSwitcher, VoxeLab.
// =====================================================================
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Cameras;
using VoxelLab.Overlays;
using VoxelLab.Tools;

namespace VoxelLab.UI
{
    public class LabUI : MonoBehaviour
    {
        public ToolManager toolManager;
        public OverlayController overlays;
        public CameraSwitcher cameras;
        public VoxelLab.Boot.VoxeLab lab;

        private Rect _windowRect = new Rect(10, 10, 320, 480);
        private string[] _materialNames;
        private string[] _toolNames;
        private bool _show = true;

        private void Start()
        {
            int mc = MaterialTable.Count;
            _materialNames = new string[mc];
            for (int i = 0; i < mc; i++) _materialNames[i] = MaterialTable.Get((byte)i).name;
            if (toolManager != null)
            {
                _toolNames = new string[toolManager.tools.Length];
                for (int i = 0; i < _toolNames.Length; i++) _toolNames[i] = toolManager.tools[i].Name;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _show = !_show;
            if (toolManager != null)
                toolManager.ConsumePointerOverUI = _show && _windowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        }

        private void OnGUI()
        {
            if (!_show) return;
            _windowRect = GUILayout.Window(987654, _windowRect, DrawWindow, "VoxelLab — Tab para ocultar");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Herramienta");
            if (toolManager != null && _toolNames != null)
            {
                int newTool = GUILayout.SelectionGrid(toolManager.activeIndex, _toolNames, 3);
                toolManager.activeIndex = newTool;

                GUILayout.Space(4);
                GUILayout.Label($"Radio: {toolManager.parameters.radius:0.0}");
                toolManager.parameters.radius = GUILayout.HorizontalSlider(toolManager.parameters.radius, 0.5f, 20f);
                GUILayout.Label($"Intensidad: {toolManager.parameters.intensity:0.00}");
                toolManager.parameters.intensity = GUILayout.HorizontalSlider(toolManager.parameters.intensity, 0.05f, 2f);

                GUILayout.Space(8);
                GUILayout.Label("Material");
                int newMat = GUILayout.SelectionGrid(toolManager.parameters.material, _materialNames, 4);
                toolManager.parameters.material = (byte)Mathf.Clamp(newMat, 0, _materialNames.Length - 1);
            }

            GUILayout.Space(8);
            GUILayout.Label("Overlays");
            if (overlays != null)
            {
                DrawOverlay("Wireframe", OverlayFlags.Wireframe);
                DrawOverlay("Densidad",  OverlayFlags.Density);
                DrawOverlay("Material",  OverlayFlags.Material);
                DrawOverlay("Chunks",    OverlayFlags.ChunkBounds);
                DrawOverlay("Octree",    OverlayFlags.OctreeNodes);
            }

            GUILayout.Space(8);
            GUILayout.Label("Cámaras (F1-F4)");
            if (cameras != null && cameras.slots != null)
            {
                for (int i = 0; i < cameras.slots.Length; i++)
                {
                    if (GUILayout.Button((cameras.active == i ? "● " : "○ ") + cameras.slots[i].label))
                    {
                        cameras.active = i;
                        cameras.Apply();
                    }
                }
            }

            GUILayout.Space(8);
            if (lab != null)
            {
                GUILayout.Label($"LOD scale: {lab.lodScale:0.00}");
                lab.lodScale = GUILayout.HorizontalSlider(lab.lodScale, 0.1f, 4f);
                if (GUILayout.Button("Regenerar planeta")) lab.RegeneratePlanet();
                GUILayout.Label($"Chunks: {lab.ChunkCount}  /  Voxels sólidos: {lab.SolidVoxelEstimate}");
            }

            GUI.DragWindow();
        }

        private void DrawOverlay(string label, OverlayFlags f)
        {
            bool on = (overlays.flags & f) != 0;
            bool nw = GUILayout.Toggle(on, label);
            if (nw != on) overlays.Set(f, nw);
        }
    }
}
