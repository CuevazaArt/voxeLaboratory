// =====================================================================
//  ToolManager.cs
//  VoxelLab :: Tools
//
//  MonoBehaviour que conecta el input de mouse + UI con la herramienta
//  activa. Conserva ToolParameters y expone API para la UI.
//
//  Dependencias: VoxelTools, VoxeLab (para mundo + cámara activa).
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Physics;

namespace VoxelLab.Tools
{
    public class ToolManager : MonoBehaviour
    {
        public Camera cameraOverride;
        public List<VoxelRigidbody> bodies = new List<VoxelRigidbody>();
        public ToolParameters parameters = new ToolParameters
        {
            radius = 3f,
            intensity = 1f,
            material = (byte)MaterialId.Rock,
            maxDistance = 200f,
            planeNormal = Vector3.up,
        };

        public IVoxelTool[] tools = {
            new DrillTool(),
            new ExplosionTool(),
            new BrushTool(),
            new ErosionTool(),
            new CutTool(),
        };
        public int activeIndex = 0;

        public VoxelWorld World { get; set; }
        public IVoxelTool Active => tools[Mathf.Clamp(activeIndex, 0, tools.Length - 1)];

        public bool ConsumePointerOverUI;     // true cuando el cursor está sobre UI

        // Undo/Redo stack para operaciones destructivas de tools.
        private readonly UndoStack _undo = new UndoStack();
        public UndoStack Undo => _undo;

        /// <summary>Atajos de teclado Undo/Redo. Modificable para tests/headless.</summary>
        public KeyCode undoKey = KeyCode.Z;
        public KeyCode redoKey = KeyCode.Y;

        private void Update()
        {
            if (World == null) return;

            // Ctrl-Z / Ctrl-Y antes de cualquier otra interacción.
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl)
            {
                if (Input.GetKeyDown(undoKey)) { _undo.Undo(World); return; }
                if (Input.GetKeyDown(redoKey)) { _undo.Redo(World); return; }
            }

            if (ConsumePointerOverUI) return;
            if (!Input.GetMouseButton(0)) return;
            var cam = cameraOverride != null ? cameraOverride : Camera.main;
            if (cam == null) return;
            Ray r = cam.ScreenPointToRay(Input.mousePosition);

            // Envolver la operación en una transacción para Undo y para
            // batchear los OnChunkDirty.
            World.BeginEdit();
            try
            {
                Active.Apply(World, r, parameters, bodies);
            }
            finally
            {
                var tx = World.EndEdit();
                if (tx != null && !tx.IsEmpty) _undo.Push(tx);
            }
        }
    }
}
