// =====================================================================
//  UndoRedoTests.cs
//  VoxelLab :: Tests
//
//  Cubre EditTransaction + UndoStack: round-trip, batching de
//  OnChunkDirty y cap de la pila.
// =====================================================================
#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Tools;

namespace VoxelLab.Tests
{
    public class UndoRedoTests
    {
        [Test]
        public void EditTransaction_RecordsPrevAndCurr_OnSetVoxel()
        {
            var w = new VoxelWorld(16, 6);
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.BeginEdit();
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Iron, 1f, 0.5f));
            var tx = w.EndEdit();

            Assert.IsNotNull(tx);
            Assert.AreEqual(1, tx.Count);
            Assert.AreEqual((byte)MaterialId.Rock, tx.Edits[0].prev.material);
            Assert.AreEqual((byte)MaterialId.Iron, tx.Edits[0].curr.material);
        }

        [Test]
        public void EditTransaction_DefersChunkDirty_UntilEndEdit()
        {
            var w = new VoxelWorld(16, 6);
            int dirtyCount = 0;
            w.OnChunkDirty += _ => dirtyCount++;

            w.BeginEdit();
            // 64 ediciones en el mismo chunk.
            for (int i = 0; i < 64; i++)
                w.SetVoxel(i % 16, (i / 16) % 16, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            Assert.AreEqual(0, dirtyCount, "OnChunkDirty no debe dispararse durante la transaccion");
            w.EndEdit();
            Assert.AreEqual(1, dirtyCount, "OnChunkDirty debe dispararse una vez por chunk afectado al cerrar");
        }

        [Test]
        public void UndoStack_Push_ThenUndo_RestoresOriginalVoxels()
        {
            var w = new VoxelWorld(16, 6);
            var stack = new UndoStack();

            // Estado inicial: roca solida en (1,2,3).
            w.SetVoxel(1, 2, 3, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));

            // Carve dentro de transaccion.
            w.BeginEdit();
            w.CarveSphere(new Vector3(1.5f, 2.5f, 3.5f), 1.5f, 1f);
            var tx = w.EndEdit();
            stack.Push(tx);

            Assert.IsTrue(w.GetVoxel(1, 2, 3).densidad < 0.5f, "Carve debe haber vaciado la celda");
            Assert.IsTrue(stack.Undo(w));
            Assert.AreEqual((byte)MaterialId.Rock, w.GetVoxel(1, 2, 3).material);
            Assert.IsTrue(w.GetVoxel(1, 2, 3).densidad >= 0.5f);
        }

        [Test]
        public void UndoStack_RedoAfterUndo_ReappliesEdits()
        {
            var w = new VoxelWorld(16, 6);
            var stack = new UndoStack();

            w.SetVoxel(5, 5, 5, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));

            w.BeginEdit();
            w.SetVoxel(5, 5, 5, Voxel.Empty);
            stack.Push(w.EndEdit());

            stack.Undo(w);
            Assert.AreEqual((byte)MaterialId.Rock, w.GetVoxel(5, 5, 5).material);
            Assert.IsTrue(stack.Redo(w));
            Assert.AreEqual(0, w.GetVoxel(5, 5, 5).material);
        }

        [Test]
        public void UndoStack_Push_ClearsRedoStack()
        {
            var w = new VoxelWorld(16, 6);
            var stack = new UndoStack();

            w.BeginEdit();
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            stack.Push(w.EndEdit());

            stack.Undo(w);
            Assert.AreEqual(1, stack.RedoCount);

            w.BeginEdit();
            w.SetVoxel(1, 0, 0, new Voxel((byte)MaterialId.Iron, 1f, 0.5f));
            stack.Push(w.EndEdit());

            Assert.AreEqual(0, stack.RedoCount, "Push tras Undo debe limpiar la pila Redo");
        }

        [Test]
        public void UndoStack_Capacity_DropsOldestEntries()
        {
            var w = new VoxelWorld(16, 6);
            var stack = new UndoStack(capacity: 3);

            for (int i = 0; i < 5; i++)
            {
                w.BeginEdit();
                w.SetVoxel(i, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
                stack.Push(w.EndEdit());
            }
            Assert.AreEqual(3, stack.UndoCount);
        }

        [Test]
        public void UndoStack_Undo_DoesNothing_WhenEmpty()
        {
            var w = new VoxelWorld(16, 6);
            var stack = new UndoStack();
            Assert.IsFalse(stack.Undo(w));
            Assert.IsFalse(stack.Redo(w));
        }

        [Test]
        public void EditTransaction_BatchesMultipleChunks()
        {
            var w = new VoxelWorld(16, 6);
            var dirtyChunks = new System.Collections.Generic.HashSet<VoxelChunk>();
            w.OnChunkDirty += c => dirtyChunks.Add(c);

            w.BeginEdit();
            // Tres chunks distintos.
            w.SetVoxel(0, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.SetVoxel(20, 0, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            w.SetVoxel(0, 20, 0, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            Assert.AreEqual(0, dirtyChunks.Count);
            w.EndEdit();
            Assert.AreEqual(3, dirtyChunks.Count);
        }
    }
}
#endif
