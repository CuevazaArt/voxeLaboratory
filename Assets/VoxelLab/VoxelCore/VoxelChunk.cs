// =====================================================================
//  VoxelChunk.cs
//  VoxelLab :: VoxelCore
//
//  Bloque cúbico de SIZE^3 voxels (16 o 32) almacenados en un array
//  plano. Es la unidad de trabajo para meshing, edición y streaming.
//
//  Layout: index = x + SIZE * (y + SIZE * z)  (X-major).
//
//  Mantiene metadatos: dirty flag (para regenerar malla), empty flag
//  (early-out de meshing/colisión) y bounds en espacio mundo.
//
//  Dependencias: Voxel.
// =====================================================================
using System;
using UnityEngine;

namespace VoxelLab.Core
{
    /// <summary>Chunk denso de voxels. Tamaño parametrizable en construcción.</summary>
    public sealed class VoxelChunk
    {
        public readonly int size;
        public readonly Vector3Int origin;          // origen en coords de voxel global
        public readonly Voxel[] voxels;             // array plano size^3
        public bool dirty;                          // requiere remesh
        public bool empty;                          // ningún voxel sólido
        public Bounds worldBounds;                  // AABB en espacio mundo (voxelSize=1)

        public VoxelChunk(int size, Vector3Int origin)
        {
            if (size <= 0 || (size & (size - 1)) != 0)
                throw new ArgumentException("size debe ser potencia de dos > 0", nameof(size));
            this.size = size;
            this.origin = origin;
            voxels = new Voxel[size * size * size];
            dirty = true;
            empty = true;
            worldBounds = new Bounds(
                new Vector3(origin.x + size * 0.5f, origin.y + size * 0.5f, origin.z + size * 0.5f),
                new Vector3(size, size, size));
        }

        /// <summary>Total de voxels (size^3).</summary>
        public int Volume => voxels.Length;

        /// <summary>Convierte coords locales (0..size-1) a índice plano.</summary>
        public int Index(int lx, int ly, int lz) => lx + size * (ly + size * lz);

        /// <summary>True si las coords locales caen dentro del chunk.</summary>
        public bool InBounds(int lx, int ly, int lz) =>
            (uint)lx < (uint)size && (uint)ly < (uint)size && (uint)lz < (uint)size;

        /// <summary>Lee voxel local con clamp seguro -> Empty si fuera de rango.</summary>
        public Voxel GetLocal(int lx, int ly, int lz)
        {
            if (!InBounds(lx, ly, lz)) return Voxel.Empty;
            return voxels[Index(lx, ly, lz)];
        }

        /// <summary>Escribe voxel local. Marca dirty y actualiza empty.</summary>
        public void SetLocal(int lx, int ly, int lz, Voxel v)
        {
            if (!InBounds(lx, ly, lz)) return;
            v.Recompute();
            voxels[Index(lx, ly, lz)] = v;
            dirty = true;
            if (v.solido) empty = false;
            // empty a true se recalcula bajo demanda (RecomputeEmpty) para evitar O(n).
        }

        /// <summary>Recorre todo el chunk recalculando el flag empty (O(n)).</summary>
        public void RecomputeEmpty()
        {
            for (int i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].solido) { empty = false; return; }
            }
            empty = true;
        }

        /// <summary>Llena todo el chunk con un mismo voxel (útil para tests/seed).</summary>
        public void Fill(Voxel v)
        {
            v.Recompute();
            for (int i = 0; i < voxels.Length; i++) voxels[i] = v;
            dirty = true;
            empty = !v.solido;
        }
    }
}
