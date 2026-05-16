// =====================================================================
//  UndoStack.cs
//  VoxelLab :: Tools
//
//  Pila Undo/Redo para EditTransaction. Cap configurable; al
//  desbordar descarta la entrada m\u00e1s antigua.
//
//  Dependencias: VoxelWorld, EditTransaction.
//
//  Invariantes:
//    - Push limpia la pila Redo (rama nueva tras editar).
//    - Undo aplica las ediciones en orden inverso restituyendo
//      <c>prev</c> en cada celda; mueve la tx a la pila Redo.
//    - Redo reaplica <c>curr</c> en orden directo; mueve a la pila Undo.
//    - Las re-aplicaciones se hacen dentro de BeginEdit/EndEdit para
//      emitir un solo OnChunkDirty por chunk afectado.
// =====================================================================
using System.Collections.Generic;
using VoxelLab.Core;

namespace VoxelLab.Tools
{
    /// <summary>Pila Undo/Redo de transacciones de edici\u00f3n del mundo.</summary>
    public sealed class UndoStack
    {
        public const int DEFAULT_CAPACITY = 64;

        private readonly LinkedList<EditTransaction> _undo = new LinkedList<EditTransaction>();
        private readonly LinkedList<EditTransaction> _redo = new LinkedList<EditTransaction>();
        private readonly int _capacity;

        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public int Capacity => _capacity;

        public UndoStack(int capacity = DEFAULT_CAPACITY)
        {
            _capacity = System.Math.Max(1, capacity);
        }

        /// <summary>Empuja una transacci\u00f3n cerrada. Limpia el redo.</summary>
        public void Push(EditTransaction tx)
        {
            if (tx == null || tx.IsEmpty) return;
            _undo.AddLast(tx);
            while (_undo.Count > _capacity) _undo.RemoveFirst();
            _redo.Clear();
        }

        /// <summary>Aplica el ultimo Undo. Devuelve true si hizo algo.</summary>
        public bool Undo(VoxelWorld world)
        {
            if (world == null || _undo.Count == 0) return false;
            var tx = _undo.Last.Value;
            _undo.RemoveLast();
            ApplyReverse(world, tx);
            _redo.AddLast(tx);
            return true;
        }

        /// <summary>Reaplica el \u00fcltimo Redo. Devuelve true si hizo algo.</summary>
        public bool Redo(VoxelWorld world)
        {
            if (world == null || _redo.Count == 0) return false;
            var tx = _redo.Last.Value;
            _redo.RemoveLast();
            ApplyForward(world, tx);
            _undo.AddLast(tx);
            return true;
        }

        /// <summary>Limpia ambas pilas.</summary>
        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        // ------------------------------------------------------------------

        private static void ApplyReverse(VoxelWorld world, EditTransaction tx)
        {
            world.BeginEdit();
            try
            {
                var edits = tx.Edits;
                for (int i = edits.Count - 1; i >= 0; i--)
                {
                    var e = edits[i];
                    world.SetVoxel(e.x, e.y, e.z, e.prev);
                }
            }
            finally
            {
                // Descartamos la transacci\u00f3n generada: la operaci\u00f3n inversa
                // no debe entrar en la pila (la propia Undo gestiona el estado).
                world.EndEdit();
            }
        }

        private static void ApplyForward(VoxelWorld world, EditTransaction tx)
        {
            world.BeginEdit();
            try
            {
                var edits = tx.Edits;
                for (int i = 0; i < edits.Count; i++)
                {
                    var e = edits[i];
                    world.SetVoxel(e.x, e.y, e.z, e.curr);
                }
            }
            finally
            {
                world.EndEdit();
            }
        }
    }
}
