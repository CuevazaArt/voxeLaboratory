// =====================================================================
//  SparseVoxelOctree.cs
//  VoxelLab :: SVO
//
//  Octree esparso construido sobre coords de chunk. La raíz cubre un
//  AABB de tamaño rootSize x rootSize x rootSize voxels y se subdivide
//  hasta hojas de tamaño = leafSize (= chunkSize por defecto).
//
//  Uso típico:
//      - MarkDirty(chunkCoord)  -> al editar voxels
//      - Refresh(tryGetChunk)   -> agrega LOD (densidad media, material
//                                  dominante) bottom-up para visualización
//                                  y para decidir qué resolución mostrar.
//      - Query(pos)             -> nodo más profundo no-vacío que contiene
//                                  la posición.
//
//  Dependencias: SVONode, VoxelChunk, MaterialTable.
//
//  Invariantes:
//      - rootSize y leafSize son potencias de dos.
//      - leafSize <= rootSize.
//      - Query/DescendOrCreate operan en coordenadas de voxel global.
//
//  Ejemplo:
//      octree.MarkDirty(chunkCoord);
//      octree.Refresh(cc => world.GetChunk(cc, create: false));
//      var node = octree.Query(voxelPos);
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.SVO
{
    public sealed class SparseVoxelOctree
    {
        public readonly SVONode root;
        public readonly int leafSize;
        public readonly int rootSize;

        public SparseVoxelOctree(int rootSize, int leafSize)
        {
            // rootSize debe ser potencia de 2 y >= leafSize
            if ((rootSize & (rootSize - 1)) != 0)
                throw new System.ArgumentException("rootSize debe ser potencia de 2");
            if ((leafSize & (leafSize - 1)) != 0)
                throw new System.ArgumentException("leafSize debe ser potencia de 2");
            if (leafSize > rootSize)
                throw new System.ArgumentException("leafSize > rootSize");

            this.rootSize = rootSize;
            this.leafSize = leafSize;
            // Centramos la raíz en el origen para soportar coords negativas.
            int half = rootSize / 2;
            root = new SVONode(new Vector3Int(-half, -half, -half), rootSize, 0);
        }

        /// <summary>Marca dirty el nodo hoja que contiene este chunk (subdivide perezosamente).</summary>
        public void MarkDirty(Vector3Int chunkCoord)
        {
            var voxelOrigin = chunkCoord * leafSize;
            var node = DescendOrCreate(voxelOrigin);
            if (node != null) node.dirty = true;
        }

        /// <summary>Devuelve la hoja del SVO que contiene la coord de voxel, creándola si hace falta.</summary>
        public SVONode DescendOrCreate(Vector3Int voxelPos)
        {
            if (!root.Contains(voxelPos.x, voxelPos.y, voxelPos.z)) return null;
            var node = root;
            while (node.size > leafSize)
            {
                if (node.isLeaf) node.Subdivide();
                int half = node.size >> 1;
                int dx = (voxelPos.x - node.origin.x) >= half ? 1 : 0;
                int dy = (voxelPos.y - node.origin.y) >= half ? 1 : 0;
                int dz = (voxelPos.z - node.origin.z) >= half ? 1 : 0;
                node = node.children[dx + 2 * dy + 4 * dz];
            }
            return node;
        }

        /// <summary>Hoja existente que contiene la posición (sin crear). Null si no existe.</summary>
        public SVONode Query(Vector3Int voxelPos)
        {
            if (!root.Contains(voxelPos.x, voxelPos.y, voxelPos.z)) return null;
            var node = root;
            while (!node.isLeaf)
            {
                int half = node.size >> 1;
                int dx = (voxelPos.x - node.origin.x) >= half ? 1 : 0;
                int dy = (voxelPos.y - node.origin.y) >= half ? 1 : 0;
                int dz = (voxelPos.z - node.origin.z) >= half ? 1 : 0;
                var child = node.children[dx + 2 * dy + 4 * dz];
                if (child == null) return node;
                node = child;
            }
            return node;
        }

        /// <summary>Recalcula agregados LOD bottom-up para nodos dirty.</summary>
        public void Refresh(Func<Vector3Int, VoxelChunk> tryGetChunk)
        {
            if (tryGetChunk == null) throw new ArgumentNullException(nameof(tryGetChunk));
            RefreshRecursive(root, tryGetChunk);
        }

        private void RefreshRecursive(SVONode node, Func<Vector3Int, VoxelChunk> tryGetChunk)
        {
            if (node.isLeaf)
            {
                // Calcular agregados desde el chunk correspondiente (si existe).
                var cc = new Vector3Int(
                    FloorDiv(node.origin.x, leafSize),
                    FloorDiv(node.origin.y, leafSize),
                    FloorDiv(node.origin.z, leafSize));
                var chunk = tryGetChunk(cc);
                if (chunk == null || chunk.empty)
                {
                    node.anySolid = false;
                    node.averageDensidad = 0f;
                    node.dominantMaterial = 0;
                }
                else
                {
                    int[] hist = new int[MaterialTable.Count];
                    float sum = 0f;
                    bool any = false;
                    for (int i = 0; i < chunk.voxels.Length; i++)
                    {
                        var v = chunk.voxels[i];
                        sum += v.densidad;
                        if (v.material < hist.Length) hist[v.material]++;
                        if (v.solido) any = true;
                    }
                    int best = 0;
                    for (int i = 1; i < hist.Length; i++) if (hist[i] > hist[best]) best = i;
                    node.anySolid = any;
                    node.averageDensidad = sum / chunk.voxels.Length;
                    node.dominantMaterial = (byte)best;
                }
                node.dirty = false;
                return;
            }

            float acc = 0f;
            bool anyS = false;
            int[] mhist = new int[MaterialTable.Count];
            int childCount = 0;
            for (int i = 0; i < 8; i++)
            {
                var c = node.children[i];
                if (c == null) continue;
                if (c.dirty) RefreshRecursive(c, tryGetChunk);
                acc += c.averageDensidad;
                anyS |= c.anySolid;
                if (c.dominantMaterial < mhist.Length) mhist[c.dominantMaterial]++;
                childCount++;
            }
            int bestM = 0;
            for (int i = 1; i < mhist.Length; i++) if (mhist[i] > mhist[bestM]) bestM = i;
            node.averageDensidad = childCount > 0 ? acc / childCount : 0f;
            node.anySolid = anyS;
            node.dominantMaterial = (byte)bestM;
            node.dirty = false;
        }

        private static int FloorDiv(int a, int b) => (a >= 0) ? a / b : -((-a + b - 1) / b);

        /// <summary>Recolecta todos los nodos visibles dado un punto de cámara y un budget LOD.</summary>
        public void CollectVisible(Vector3 cameraPos, float lodScale, List<SVONode> outList)
        {
            outList.Clear();
            CollectVisibleRecursive(root, cameraPos, lodScale, outList);
        }

        private void CollectVisibleRecursive(SVONode node, Vector3 cam, float lodScale, List<SVONode> outList)
        {
            if (node == null) return;
            if (!node.anySolid) return;
            // Heurística simple: si el nodo es visible a su tamaño / dist, descender.
            Vector3 c = new Vector3(node.origin.x + node.size * 0.5f, node.origin.y + node.size * 0.5f, node.origin.z + node.size * 0.5f);
            float dist = Vector3.Distance(cam, c);
            float screen = node.size / Mathf.Max(1f, dist);
            if (node.isLeaf || screen < lodScale)
            {
                outList.Add(node);
                return;
            }
            for (int i = 0; i < 8; i++) CollectVisibleRecursive(node.children[i], cam, lodScale, outList);
        }
    }
}
