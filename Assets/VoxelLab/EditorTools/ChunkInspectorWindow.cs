// =====================================================================
//  ChunkInspectorWindow.cs
//  VoxelLab :: EditorTools
//
//  EditorWindow que lista los chunks activos del VoxelWorld en la
//  escena, mostrando coordenada, voxelCount aproximado, estado dirty
//  y bounds. Refresca on-demand. Solo editor.
//
//  Acceso: Window > VoxelLab > Chunk Inspector
//
//  Dependencias: VoxelLab.Boot.VoxeLab (para acceder al World runtime).
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VoxelLab.Boot;
using VoxelLab.Core;

namespace VoxelLab.EditorTools
{
    public sealed class ChunkInspectorWindow : EditorWindow
    {
        private Vector2 _scroll;
        private VoxeLab _selected;
        private string _filter = "";
        private bool _showEmpty = false;

        [MenuItem("Window/VoxelLab/Chunk Inspector")]
        public static void Open()
        {
            var w = GetWindow<ChunkInspectorWindow>("Chunk Inspector");
            w.minSize = new Vector2(420, 320);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VoxeLab", EditorStyles.boldLabel);
            _selected = (VoxeLab)EditorGUILayout.ObjectField("Bootstrap", _selected, typeof(VoxeLab), allowSceneObjects: true);

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Asigna la instancia VoxeLab en escena (Play Mode requerido).", MessageType.Info);
                return;
            }

            var world = _selected.World;
            if (world == null)
            {
                EditorGUILayout.HelpBox("World no inicializado. Entra a Play Mode.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Filtro coord", _filter);
            _showEmpty = EditorGUILayout.ToggleLeft("Mostrar vacios", _showEmpty, GUILayout.Width(140));
            if (GUILayout.Button("Refrescar", GUILayout.Width(80))) Repaint();
            EditorGUILayout.EndHorizontal();

            int total = 0, dirty = 0, empty = 0;
            foreach (var c in world.AllChunks())
            {
                total++;
                if (c.dirty) dirty++;
                if (c.empty) empty++;
            }
            EditorGUILayout.LabelField($"Chunks: {total}  Dirty: {dirty}  Vacios: {empty}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var kv in world.chunks)
            {
                var c = kv.Value;
                if (!_showEmpty && c.empty) continue;

                string key = $"({kv.Key.x},{kv.Key.y},{kv.Key.z})";
                if (!string.IsNullOrEmpty(_filter) && key.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                int solidCount = CountSolid(c);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(key, GUILayout.Width(110));
                EditorGUILayout.LabelField($"solidos: {solidCount}", GUILayout.Width(110));
                EditorGUILayout.LabelField(c.dirty ? "dirty" : "clean", GUILayout.Width(60));
                EditorGUILayout.LabelField(c.empty ? "empty" : "filled", GUILayout.Width(60));
                if (GUILayout.Button("Ping", GUILayout.Width(48)))
                {
                    SceneView.lastActiveSceneView?.Frame(c.worldBounds, false);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private static int CountSolid(VoxelChunk c)
        {
            int n = 0;
            var voxels = c.voxels;
            for (int i = 0; i < voxels.Length; i++)
                if (voxels[i].solido) n++;
            return n;
        }
    }
}
#endif
