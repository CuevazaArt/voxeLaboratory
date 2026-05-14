// File: Assets/Scripts/ChunkSystem/ChunkRegistry.cs
//
// Purpose
//   In‑memory lookup of all loaded chunks indexed by `ChunkCoordinate`.
//   The registry is the single point of synchronisation for chunk
//   creation and removal so race conditions during streaming and editing
//   stay contained.
//
// Invariants
//   * Each `ChunkCoordinate` maps to at most one `Chunk` instance for the
//     lifetime of the registry.
//   * Public APIs are safe to call from multiple threads; reads acquire a
//     read lock, writes acquire a write lock.
//   * Removed chunks are garbage‑collected by callers; the registry only
//     drops the reference.
//
// Dependencies
//   `VoxeLaboratory.VoxelCore`, sibling files in this module.
//
// Example
//   var registry = new ChunkRegistry();
//   var chunk = registry.GetOrCreate(new ChunkCoordinate(0, 0, 0));
//
using System;
using System.Collections.Generic;
using System.Threading;

namespace VoxeLaboratory.ChunkSystem
{
    /// <summary>Thread‑safe map from <see cref="ChunkCoordinate"/> to <see cref="Chunk"/>.</summary>
    public sealed class ChunkRegistry
    {
        private readonly Dictionary<ChunkCoordinate, Chunk> _chunks = new Dictionary<ChunkCoordinate, Chunk>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>Number of chunks currently held by the registry.</summary>
        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try { return _chunks.Count; }
                finally { _lock.ExitReadLock(); }
            }
        }

        /// <summary>Try to retrieve an existing chunk without creating it.</summary>
        public bool TryGet(ChunkCoordinate coordinate, out Chunk chunk)
        {
            _lock.EnterReadLock();
            try { return _chunks.TryGetValue(coordinate, out chunk); }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// Return the chunk at <paramref name="coordinate"/>, creating an
        /// empty one if it does not yet exist.  Atomic with respect to other
        /// registry operations.
        /// </summary>
        public Chunk GetOrCreate(ChunkCoordinate coordinate)
        {
            // Fast path: read lock only.
            _lock.EnterReadLock();
            try
            {
                if (_chunks.TryGetValue(coordinate, out var existing))
                    return existing;
            }
            finally { _lock.ExitReadLock(); }

            // Slow path: upgrade to write lock and re‑check.
            _lock.EnterWriteLock();
            try
            {
                if (_chunks.TryGetValue(coordinate, out var existing))
                    return existing;
                var created = new Chunk(coordinate);
                _chunks.Add(coordinate, created);
                return created;
            }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// Remove the chunk at <paramref name="coordinate"/> from the registry.
        /// Returns true if a chunk was removed.
        /// </summary>
        public bool Remove(ChunkCoordinate coordinate)
        {
            _lock.EnterWriteLock();
            try { return _chunks.Remove(coordinate); }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// Snapshot the current chunks into a new array.  Useful for editor
        /// debug visualisers; cost is `O(n)`.
        /// </summary>
        public Chunk[] Snapshot()
        {
            _lock.EnterReadLock();
            try
            {
                var snapshot = new Chunk[_chunks.Count];
                int i = 0;
                foreach (var kv in _chunks)
                    snapshot[i++] = kv.Value;
                return snapshot;
            }
            finally { _lock.ExitReadLock(); }
        }
    }
}
