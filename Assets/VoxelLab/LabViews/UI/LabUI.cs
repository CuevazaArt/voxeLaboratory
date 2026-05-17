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
using UnityEngine.InputSystem;
using VoxelLab.Core;
using VoxelLab.Cameras;
using VoxelLab.Overlays;
using VoxelLab.Physics;
using VoxelLab.Planet;
using VoxelLab.Tools;

namespace VoxelLab.UI
{
    public class LabUI : MonoBehaviour
    {
        public ToolManager toolManager;
        public OverlayController overlays;
        public CameraSwitcher cameras;
        public VoxelLab.Boot.VoxeLab lab;
        public ProjectileLauncher launcher;

        [Header("Sandbox extendido")]
        public TargetDef[] availableTargets;
        public Vector3 targetSpawnOrigin = Vector3.zero;
        public DebrisSimulator debrisSimulator;

        private Rect _windowRect = new Rect(10, 10, 380, 760);
        private string[] _materialNames;
        private string[] _toolNames;
        private string[] _projectileNames;
        private string[] _targetNames;
        private int _activeTargetIndex = 0;
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
            RefreshProjectileNames();
            RefreshTargetNames();
        }

        private void RefreshProjectileNames()
        {
            if (launcher == null || launcher.availableTypes == null) { _projectileNames = null; return; }
            _projectileNames = new string[launcher.availableTypes.Length];
            for (int i = 0; i < _projectileNames.Length; i++)
            {
                var t = launcher.availableTypes[i];
                _projectileNames[i] = t != null ? (string.IsNullOrEmpty(t.displayName) ? t.name : t.displayName) : "-";
            }
        }

        private void RefreshTargetNames()
        {
            if (availableTargets == null) { _targetNames = null; return; }
            _targetNames = new string[availableTargets.Length];
            for (int i = 0; i < _targetNames.Length; i++)
            {
                var t = availableTargets[i];
                _targetNames[i] = t != null ? (string.IsNullOrEmpty(t.displayName) ? t.name : t.displayName) : "-";
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) _show = !_show;
            if (toolManager != null)
            {
                Vector2 mPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                toolManager.ConsumePointerOverUI = _show && _windowRect.Contains(new Vector2(mPos.x, Screen.height - mPos.y));
            }
        }

        private void OnGUI()
        {
            if (!_show) return;
            _windowRect = GUILayout.Window(987654, _windowRect, DrawWindow, "VoxelLab — Tab para ocultar");
        }

        private void DrawWindow(int id)
        {
            DrawUndoRedo();
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
                if (GUILayout.Button("Regenerar mundo")) lab.RegenerateWorld();
                GUILayout.Label($"Chunks: {lab.ChunkCount}  /  Voxels sólidos: {lab.SolidVoxelEstimate}");
            }

            DrawBallistics();
            DrawTargets();
            DrawDebris();

            GUI.DragWindow();
        }

        private void DrawUndoRedo()
        {
            if (toolManager == null || toolManager.Undo == null) return;
            var undo = toolManager.Undo;
            GUILayout.BeginHorizontal();
            GUI.enabled = undo.UndoCount > 0;
            if (GUILayout.Button($"\u21B6 Undo ({undo.UndoCount})") && lab != null && lab.World != null)
                undo.Undo(lab.World);
            GUI.enabled = undo.RedoCount > 0;
            if (GUILayout.Button($"Redo ({undo.RedoCount}) \u21B7") && lab != null && lab.World != null)
                undo.Redo(lab.World);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            DrawSaveLoad();
            GUILayout.Space(4);
        }

        private void DrawSaveLoad()
        {
            if (lab == null || lab.World == null) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Guardar"))
            {
                try
                {
                    string path = System.IO.Path.Combine(Application.persistentDataPath, "voxellab_world.vlab");
                    WorldSerializer.SaveToFile(lab.World, path);
                    Debug.Log($"[LabUI] Mundo guardado en {path}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LabUI] Save fallo: {e.Message}");
                }
            }
            if (GUILayout.Button("Cargar"))
            {
                try
                {
                    string path = System.IO.Path.Combine(Application.persistentDataPath, "voxellab_world.vlab");
                    WorldSerializer.LoadFromFile(lab.World, path);
                    if (toolManager != null && toolManager.Undo != null) toolManager.Undo.Clear();
                    Debug.Log($"[LabUI] Mundo cargado desde {path}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LabUI] Load fallo: {e.Message}");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTargets()
        {
            if (availableTargets == null || availableTargets.Length == 0) return;
            if (_targetNames == null || _targetNames.Length != availableTargets.Length) RefreshTargetNames();

            GUILayout.Space(8);
            GUILayout.Label("Targets (T = spawn)");
            int newIdx = GUILayout.SelectionGrid(_activeTargetIndex, _targetNames, 3);
            _activeTargetIndex = Mathf.Clamp(newIdx, 0, availableTargets.Length - 1);
            if (GUILayout.Button("Spawn target en origen"))
            {
                if (lab != null && lab.World != null)
                {
                    int filled = TargetSpawner.Spawn(lab.World, availableTargets[_activeTargetIndex], targetSpawnOrigin);
                    Debug.Log($"TargetSpawner: {filled} voxels");
                }
            }
        }

        private void DrawDebris()
        {
            if (debrisSimulator == null) return;
            GUILayout.Space(8);
            GUILayout.Label("Debris");
            GUILayout.Label($"Activos: {debrisSimulator.ActiveCount} / {debrisSimulator.Capacity}");
            if (GUILayout.Button("Limpiar debris")) debrisSimulator.Clear();
        }

        private void DrawBallistics()
        {
            if (launcher == null) return;

            GUILayout.Space(8);
            GUILayout.Label("Balística (sandbox)");

            if (launcher.availableTypes != null && launcher.availableTypes.Length > 0)
            {
                if (_projectileNames == null || _projectileNames.Length != launcher.availableTypes.Length)
                    RefreshProjectileNames();
                GUILayout.Label($"Tipo activo (1-9): {(launcher.ActiveType != null ? launcher.ActiveType.displayName : "-")}");
                int newType = GUILayout.SelectionGrid(launcher.activeIndex, _projectileNames, 3);
                launcher.activeIndex = Mathf.Clamp(newType, 0, launcher.availableTypes.Length - 1);
                GUILayout.Label($"Modo: {launcher.LastImpactMode}");
            }

            GUILayout.Label($"Masa: {launcher.mass:0.00}");
            launcher.mass = GUILayout.HorizontalSlider(launcher.mass, 0.05f, 50f);

            GUILayout.Label($"Radio: {launcher.radius:0.00}");
            launcher.radius = GUILayout.HorizontalSlider(launcher.radius, 0.05f, 2f);

            GUILayout.Label($"Velocidad: {launcher.initialSpeed:0.0}");
            launcher.initialSpeed = GUILayout.HorizontalSlider(launcher.initialSpeed, 1f, 300f);

            GUILayout.Label($"Drag: {launcher.drag:0.000}");
            launcher.drag = GUILayout.HorizontalSlider(launcher.drag, 0f, 1f);

            launcher.applyDestructionOnImpact = GUILayout.Toggle(launcher.applyDestructionOnImpact, "Destruir voxels al impactar");
            GUILayout.Label($"R impacto base: {launcher.baseImpactRadius:0.00}");
            launcher.baseImpactRadius = GUILayout.HorizontalSlider(launcher.baseImpactRadius, 0.05f, 4f);
            GUILayout.Label($"Escala radio / sqrt(E): {launcher.impactRadiusPerSqrtEnergy:0.000}");
            launcher.impactRadiusPerSqrtEnergy = GUILayout.HorizontalSlider(launcher.impactRadiusPerSqrtEnergy, 0f, 0.5f);
            GUILayout.Label($"Radio impacto max: {launcher.maxImpactRadius:0.00}");
            launcher.maxImpactRadius = GUILayout.HorizontalSlider(launcher.maxImpactRadius, 0.2f, 20f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Disparar")) FireFromActiveCamera();
            if (GUILayout.Button("Limpiar proyectiles")) launcher.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Label($"Activos: {launcher.ActiveCount}  Disparos: {launcher.TotalShots}  Impactos: {launcher.TotalImpacts}");
            GUILayout.Label($"Ultimo impacto E: {launcher.LastImpactEnergy:0.00}  R: {launcher.LastImpactRadius:0.00}  Vx removidos: {launcher.LastRemovedVoxels}");
        }

        private void FireFromActiveCamera()
        {
            if (launcher == null) return;

            Camera cam = null;
            if (cameras != null && cameras.slots != null && cameras.slots.Length > 0)
            {
                int idx = Mathf.Clamp(cameras.active, 0, cameras.slots.Length - 1);
                cam = cameras.slots[idx].camera;
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 origin = cam.transform.position + cam.transform.forward * 1.5f;
            launcher.Fire(origin, cam.transform.forward);
        }

        private void DrawOverlay(string label, OverlayFlags f)
        {
            bool on = (overlays.flags & f) != 0;
            bool nw = GUILayout.Toggle(on, label);
            if (nw != on) overlays.Set(f, nw);
        }
    }
}
