// =====================================================================
//  EditTransaction.cs
//  VoxelLab :: ChunkSystem
//
//  Agrupa una serie de ediciones de voxel para soportar:
//    1. Notificación diferida de chunks dirty (un solo OnChunkDirty
//       por chunk al cerrar la transacción).
//    2. Reproducción inversa para Undo/Redo.
//
//  Dependencias: Voxel, VoxelChunk.
//
//  Invariantes:
//    - Las entradas almacenan (x,y,z) en coords globales de voxel.
//    - prev = estado anterior, curr = estado tras la edición.
//    - Una misma celda puede aparecer varias veces; el undo aplica en
//      orden inverso para restituir correctamente.
//
//  Uso:
//      using (var tx = world.BeginEditScope()) {
//          world.CarveSphere(...);
//      }
//
//  O bien:
//      world.BeginEdit();
//      world.SetVoxel(...);
//      var tx = world.EndEdit();
//      undoStack.Push(tx);
// =====================================================================
using System;
using System.Collections.Generic;

namespace VoxelLab.Core
{
    /// <summary>Edición individual de un voxel (par previo/actual).</summary>
    public readonly struct VoxelEdit : IEquatable<VoxelEdit>
    {
        public readonly int x, y, z;
        public readonly Voxel prev;
        public readonly Voxel curr;

        public VoxelEdit(int x, int y, int z, Voxel prev, Voxel curr)
        {
            this.x = x; this.y = y; this.z = z;
            this.prev = prev; this.curr = curr;
        }

        public bool Equals(VoxelEdit other) =>
            x == other.x && y == other.y && z == other.z &&
            prev.Equals(other.prev) && curr.Equals(other.curr);
    }

    /// <summary>
    /// Conjunto cerrado de ediciones de voxel. Producido por
    /// <see cref="VoxelWorld.EndEdit"/>; consumido por una pila Undo/Redo.
    /// </summary>
    public sealed class EditTransaction
    {
        private readonly List<VoxelEdit> _edits;
        private readonly HashSet<VoxelChunk> _chunks;

        public IReadOnlyList<VoxelEdit> Edits => _edits;
        public IReadOnlyCollection<VoxelChunk> AffectedChunks => _chunks;
        public int Count => _edits.Count;
        public bool IsEmpty => _edits.Count == 0;

        public EditTransaction(int capacity = 32)
        {
            _edits = new List<VoxelEdit>(capacity);
            _chunks = new HashSet<VoxelChunk>();
        }

        /// <summary>Registra una edición y el chunk afectado.</summary>
        public void Record(int x, int y, int z, Voxel prev, Voxel curr, VoxelChunk chunk)
        {
            _edits.Add(new VoxelEdit(x, y, z, prev, curr));
            if (chunk != null) _chunks.Add(chunk);
        }

        /// <summary>Añade un chunk afectado sin registrar una edición concreta.</summary>
        public void AddChunk(VoxelChunk chunk)
        {
            if (chunk != null) _chunks.Add(chunk);
        }

        /// <summary>Libera la lista interna (uso opcional tras drop del stack).</summary>
        public void Clear()
        {
            _edits.Clear();
            _chunks.Clear();
        }
    }
}
