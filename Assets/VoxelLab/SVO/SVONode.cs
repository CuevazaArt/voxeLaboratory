// =====================================================================
//  SVONode.cs
//  VoxelLab :: SVO
//
//  Nodo del Sparse Voxel Octree. Solo se subdivide cuando se marca
//  dirty contenido en su rango. Los hijos son lazy (null hasta que
//  se necesiten) para mantener el árbol esparso.
//
//  Dependencias: ninguna.
// =====================================================================
using UnityEngine;

namespace VoxelLab.SVO
{
    /// <summary>Nodo de un SVO con bounds en coords de voxel.</summary>
    public sealed class SVONode
    {
        public Vector3Int origin;       // esquina mínima del nodo en coords de voxel
        public int size;                // longitud del lado del nodo (voxels)
        public int depth;               // 0 = raíz
        public bool isLeaf;             // true si no se ha subdividido
        public bool dirty;              // contenido cambió desde el último rebuild de LOD
        public SVONode[] children;      // 8 hijos en orden (x + 2y + 4z)

        // LOD/agregados (rellenados por el motor):
        public byte dominantMaterial;
        public float averageDensidad;
        public bool anySolid;

        public SVONode(Vector3Int origin, int size, int depth)
        {
            this.origin = origin;
            this.size = size;
            this.depth = depth;
            isLeaf = true;
            dirty = true;
        }

        /// <summary>True si el nodo contiene la coord de voxel dada.</summary>
        public bool Contains(int x, int y, int z) =>
            x >= origin.x && x < origin.x + size &&
            y >= origin.y && y < origin.y + size &&
            z >= origin.z && z < origin.z + size;

        /// <summary>Crea los 8 hijos vacíos. size se divide entre 2.</summary>
        public void Subdivide()
        {
            if (!isLeaf) return;
            int half = size >> 1;
            children = new SVONode[8];
            for (int i = 0; i < 8; i++)
            {
                int dx = (i & 1);
                int dy = (i >> 1) & 1;
                int dz = (i >> 2) & 1;
                children[i] = new SVONode(
                    new Vector3Int(origin.x + dx * half, origin.y + dy * half, origin.z + dz * half),
                    half, depth + 1);
            }
            isLeaf = false;
        }
    }
}
