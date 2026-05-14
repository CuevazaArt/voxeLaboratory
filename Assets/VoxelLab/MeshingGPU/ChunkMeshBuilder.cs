// =====================================================================
//  ChunkMeshBuilder.cs
//  VoxelLab :: Meshing
//
//  CPU fallback de meshing por caras (greedy-light): para cada voxel
//  sólido emite las caras hacia vecinos no sólidos. Suficiente para
//  validar lógica y como respaldo si la GPU no está disponible.
//
//  Salida: arrays Vector3[]/int[] listos para Mesh.SetVertices/Indices.
//
//  Dependencias: VoxelChunk, VoxelWorld (para vecinos en bordes), MaterialTable.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Meshing
{
    /// <summary>Resultado de construir la malla de un chunk.</summary>
    public struct ChunkMeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Color32[] colors;
        public int[] indices;
        public Bounds bounds;
        public bool empty;
    }

    /// <summary>Mesher CPU sencillo. Emite quads orientados por cara visible.</summary>
    public static class ChunkMeshBuilder
    {
        // Offsets de las 6 caras (dx,dy,dz) y sus 4 vértices base.
        private static readonly Vector3Int[] FaceDir =
        {
            new Vector3Int( 1, 0, 0), // +X
            new Vector3Int(-1, 0, 0), // -X
            new Vector3Int( 0, 1, 0), // +Y
            new Vector3Int( 0,-1, 0), // -Y
            new Vector3Int( 0, 0, 1), // +Z
            new Vector3Int( 0, 0,-1), // -Z
        };

        private static readonly Vector3[][] FaceQuad =
        {
            new [] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) }, // +X
            new [] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) }, // -X
            new [] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) }, // +Y
            new [] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) }, // -Y
            new [] { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) }, // +Z
            new [] { new Vector3(1,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0) }, // -Z
        };

        private static readonly Vector3[] FaceNormal =
        {
            Vector3.right, Vector3.left,
            Vector3.up,    Vector3.down,
            Vector3.forward, Vector3.back,
        };

        /// <summary>Construye la malla del chunk consultando vecinos (cross-chunk) cuando se piden bordes.</summary>
        public static ChunkMeshData Build(VoxelWorld world, VoxelChunk chunk)
        {
            var verts = new List<Vector3>(chunk.size * chunk.size * 6);
            var norms = new List<Vector3>();
            var cols = new List<Color32>();
            var inds = new List<int>();

            int s = chunk.size;
            for (int z = 0; z < s; z++)
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var v = chunk.voxels[chunk.Index(x, y, z)];
                if (!v.solido) continue;

                int gx = chunk.origin.x + x, gy = chunk.origin.y + y, gz = chunk.origin.z + z;
                Color32 col = MaterialTable.Get(v.material).color;

                for (int f = 0; f < 6; f++)
                {
                    var d = FaceDir[f];
                    Voxel n;
                    if (x + d.x >= 0 && x + d.x < s && y + d.y >= 0 && y + d.y < s && z + d.z >= 0 && z + d.z < s)
                        n = chunk.voxels[chunk.Index(x + d.x, y + d.y, z + d.z)];
                    else
                        n = world != null ? world.GetVoxel(gx + d.x, gy + d.y, gz + d.z) : Voxel.Empty;

                    if (n.solido) continue;

                    var quad = FaceQuad[f];
                    int baseIdx = verts.Count;
                    Vector3 origin = new Vector3(gx, gy, gz);
                    for (int k = 0; k < 4; k++)
                    {
                        verts.Add(origin + quad[k]);
                        norms.Add(FaceNormal[f]);
                        cols.Add(col);
                    }
                    inds.Add(baseIdx + 0); inds.Add(baseIdx + 1); inds.Add(baseIdx + 2);
                    inds.Add(baseIdx + 0); inds.Add(baseIdx + 2); inds.Add(baseIdx + 3);
                }
            }

            return new ChunkMeshData
            {
                vertices = verts.ToArray(),
                normals = norms.ToArray(),
                colors = cols.ToArray(),
                indices = inds.ToArray(),
                bounds = chunk.worldBounds,
                empty = verts.Count == 0,
            };
        }

        /// <summary>Aplica un ChunkMeshData a un Unity Mesh (limpia y vuelve a llenar).</summary>
        public static void Apply(Mesh mesh, in ChunkMeshData data)
        {
            mesh.Clear();
            if (data.empty) return;
            mesh.indexFormat = data.vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(data.vertices);
            mesh.SetNormals(data.normals);
            mesh.SetColors(data.colors);
            mesh.SetIndices(data.indices, MeshTopology.Triangles, 0, calculateBounds: true);
        }
    }
}
