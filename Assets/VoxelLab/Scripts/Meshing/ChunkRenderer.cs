// =====================================================================
//  ChunkRenderer.cs
//  VoxelLab :: Meshing
//
//  Componente Unity por chunk. Mantiene su Mesh, MeshFilter, MeshRenderer
//  y se redibuja cuando el chunk se marca como dirty. Decide si usar
//  GPU mesher o fallback CPU.
//
//  Aplica LOD básico decimando voxels (skip = 2^lod) cuando la cámara
//  está lejos. La distancia umbral la decide ChunkRendererPool.
//
//  Dependencias: ChunkMeshBuilder, GPUChunkMesher, VoxelChunk.
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Meshing
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        public VoxelChunk Chunk { get; private set; }
        public int LodLevel { get; private set; }
        private Mesh _mesh;
        private MeshFilter _mf;
        private MeshRenderer _mr;

        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "ChunkMesh" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
        }

        public void Bind(VoxelChunk c, Material mat)
        {
            Chunk = c;
            _mr.sharedMaterial = mat;
        }

        /// <summary>Reconstruye la malla. Intenta GPU, cae a CPU.</summary>
        public void Rebuild(VoxelWorld world, GPUChunkMesher gpuMesher, int lod = 0)
        {
            if (Chunk == null) return;
            LodLevel = lod;

            ChunkMeshData data;
            bool ok = gpuMesher != null && gpuMesher.IsAvailable && lod == 0
                && gpuMesher.TryBuild(Chunk, out data);
            if (!ok)
            {
                if (lod <= 0)
                    data = ChunkMeshBuilder.Build(world, Chunk);
                else
                    data = BuildDecimated(world, Chunk, lod);
            }

            ChunkMeshBuilder.Apply(_mesh, data);
            _mr.enabled = !data.empty;
            Chunk.dirty = false;
        }

        /// <summary>LOD CPU primitivo: muestrea un voxel cada 2^lod y lo emite escalado.</summary>
        private static ChunkMeshData BuildDecimated(VoxelWorld world, VoxelChunk chunk, int lod)
        {
            int step = 1 << lod;
            int s = chunk.size;
            // Construir un chunk virtual reducido en memoria y pasarlo por el mesher CPU.
            int rs = Mathf.Max(1, s / step);
            var virt = new VoxelChunk(rs, chunk.origin);
            for (int z = 0; z < rs; z++)
            for (int y = 0; y < rs; y++)
            for (int x = 0; x < rs; x++)
            {
                // Voto mayoritario en el bloque step^3.
                int solidCount = 0;
                byte mat = 0;
                float dens = 0f, dur = 0f;
                for (int dz = 0; dz < step; dz++)
                for (int dy = 0; dy < step; dy++)
                for (int dx = 0; dx < step; dx++)
                {
                    var v = chunk.voxels[chunk.Index(x*step+dx, y*step+dy, z*step+dz)];
                    if (v.solido) { solidCount++; mat = v.material; }
                    dens += v.densidad;
                    dur += v.dureza;
                }
                int total = step * step * step;
                if (solidCount * 2 >= total)
                    virt.SetLocal(x, y, z, new Voxel(mat, dens / total, dur / total));
            }
            // Escalar bounds
            var data = ChunkMeshBuilder.Build(world, virt);
            // Reescala vertices a tamaño original.
            for (int i = 0; i < data.vertices.Length; i++)
            {
                var p = data.vertices[i];
                p.x = chunk.origin.x + (p.x - chunk.origin.x) * step;
                p.y = chunk.origin.y + (p.y - chunk.origin.y) * step;
                p.z = chunk.origin.z + (p.z - chunk.origin.z) * step;
                data.vertices[i] = p;
            }
            data.bounds = chunk.worldBounds;
            return data;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Object.Destroy(_mesh);
        }
    }
}
