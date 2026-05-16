// =====================================================================
//  PerfOverlay.cs
//  VoxelLab :: EditorTools
//
//  Ventana de runtime que muestra metricas basicas del frame mientras
//  el editor esta en Play Mode: FPS estimado, ms de Update, chunks y
//  dirty count, debris activos, proyectiles activos.
//
//  Acceso: Window > VoxelLab > Perf Overlay.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VoxelLab.Boot;

namespace VoxelLab.EditorTools
{
    public sealed class PerfOverlay : EditorWindow
    {
        private VoxeLab _selected;
        private double _lastTime;
        private float _smoothedFps;

        [MenuItem("Window/VoxelLab/Perf Overlay")]
        public static void Open()
        {
            var w = GetWindow<PerfOverlay>("Perf Overlay");
            w.minSize = new Vector2(280, 200);
            w.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            _selected = (VoxeLab)EditorGUILayout.ObjectField("Bootstrap", _selected, typeof(VoxeLab), allowSceneObjects: true);
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Asigna VoxeLab y entra a Play Mode.", MessageType.Info);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double dt = now - _lastTime;
            _lastTime = now;
            if (dt > 0.0)
            {
                float fps = (float)(1.0 / dt);
                _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.1f);
            }

            EditorGUILayout.LabelField($"Editor FPS ~ {_smoothedFps:0.0}");

            var world = _selected.World;
            if (world != null)
            {
                int total = 0, dirty = 0, empty = 0;
                foreach (var c in world.AllChunks())
                {
                    total++;
                    if (c.dirty) dirty++;
                    if (c.empty) empty++;
                }
                EditorGUILayout.LabelField($"Chunks: {total} (dirty {dirty}, empty {empty})");
            }
            else
            {
                EditorGUILayout.LabelField("World: -");
            }

            var debris = _selected.GetComponentInChildren<VoxelLab.Physics.DebrisSimulator>();
            if (debris != null)
                EditorGUILayout.LabelField($"Debris: {debris.ActiveCount} / {debris.Capacity}");

            var launcher = _selected.GetComponentInChildren<VoxelLab.Physics.ProjectileLauncher>();
            if (launcher != null)
                EditorGUILayout.LabelField($"Proyectiles: {launcher.ActiveCount}  Shots:{launcher.TotalShots}  Hits:{launcher.TotalImpacts}");
        }
    }
}
#endif
