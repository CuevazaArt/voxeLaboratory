// =====================================================================
//  GPUChunkMesher.cs
//  VoxelLab :: Meshing
//
//  Wrapper alrededor del compute shader VoxelMeshing.compute. Sube los
//  voxels del chunk a una StructuredBuffer<uint> compactando los campos
//  (mat:8, dens:8, dur:8, solid:1, _:7), dispatcha y lee los AppendBuffers
//  resultantes (vertices/indices). Si la GPU no está disponible o el
//  compute falla, retorna false y el llamador usa el fallback CPU.
//
//  Layout vértice GPU (SoA en buffer): float3 pos, float3 normal, uint color
//
//  Dependencias: ChunkMeshBuilder (para fallback), VoxelChunk, MaterialTable.
//
//  Invariantes:
//      - chunk.size debe ser potencia de dos y <= 32.
//      - no se crean buffers si superan límites de seguridad.
//      - si falla GPU, el llamador debe usar fallback CPU.
//
//  Ejemplo:
//      if (!gpuMesher.TryBuild(chunk, out var data))
//          data = ChunkMeshBuilder.Build(world, chunk);
// =====================================================================
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelLab.Core;

namespace VoxelLab.Meshing
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public uint color; // 0xAABBGGRR
    }

    /// <summary>Wrapper del compute shader. No retiene estado entre chunks (alloc por dispatch).</summary>
    public class GPUChunkMesher : System.IDisposable
    {
        public const int VERTEX_STRIDE = 3 * 4 + 3 * 4 + 4; // 28 bytes
        public const int MAX_QUADS_PER_VOXEL = 6;
        public const int MAX_CHUNK_SIZE = 32;
        public const long MAX_BUFFER_ELEMENTS = 8_000_000;

        private readonly ComputeShader _shader;
        private readonly int _kernel;

        public bool IsAvailable => _shader != null && _kernel >= 0 && SystemInfo.supportsComputeShaders;

        public GPUChunkMesher(ComputeShader shader)
        {
            _shader = shader;
            if (_shader != null)
                _kernel = _shader.FindKernel("CSMeshChunk");
            else
                _kernel = -1;
        }

        public void Dispose() { /* nada propio que liberar */ }

        /// <summary>
        /// Mesh GPU. Devuelve true si se generó (incluye el caso "vacío" sin verts).
        /// En caso de error, devuelve false para que el llamador use CPU.
        /// </summary>
        public bool TryBuild(VoxelChunk chunk, out ChunkMeshData data)
        {
            data = default;
            if (!IsAvailable || chunk == null) return false;

            int s = chunk.size;
            if (s <= 0 || s > MAX_CHUNK_SIZE || (s & (s - 1)) != 0)
            {
                Debug.LogWarning($"[GPUChunkMesher] chunkSize inválido ({s}). Se usa fallback CPU.");
                return false;
            }

            int volume = s * s * s;
            long maxVerts64 = (long)volume * MAX_QUADS_PER_VOXEL * 4L;
            long maxInds64 = (long)volume * MAX_QUADS_PER_VOXEL * 6L;
            if (maxVerts64 <= 0 || maxInds64 <= 0 || maxVerts64 > MAX_BUFFER_ELEMENTS || maxInds64 > MAX_BUFFER_ELEMENTS)
            {
                Debug.LogWarning($"[GPUChunkMesher] buffers fuera de límite (v={maxVerts64}, i={maxInds64}). Se usa fallback CPU.");
                return false;
            }

            int maxVerts = (int)maxVerts64;
            int maxInds = (int)maxInds64;

            // Empaquetar voxels -> uint
            uint[] packed = new uint[volume];
            for (int i = 0; i < volume; i++)
            {
                var v = chunk.voxels[i];
                uint mat = v.material;
                uint dens = (uint)Mathf.Clamp(Mathf.RoundToInt(v.densidad * 255f), 0, 255);
                uint dur  = (uint)Mathf.Clamp(Mathf.RoundToInt(v.dureza   * 255f), 0, 255);
                uint sol  = v.solido ? 1u : 0u;
                packed[i] = (mat & 0xFFu) | ((dens & 0xFFu) << 8) | ((dur & 0xFFu) << 16) | ((sol & 0x1u) << 24);
            }

            ComputeBuffer voxBuf = null;
            ComputeBuffer vertBuf = null;
            ComputeBuffer indBuf = null;
            ComputeBuffer counter = null;
            ComputeBuffer palette = null;
            try
            {
                voxBuf = new ComputeBuffer(volume, sizeof(uint), ComputeBufferType.Structured);
                voxBuf.SetData(packed);

                vertBuf = new ComputeBuffer(maxVerts, VERTEX_STRIDE, ComputeBufferType.Counter);
                vertBuf.SetCounterValue(0);
                indBuf = new ComputeBuffer(maxInds, sizeof(uint), ComputeBufferType.Counter);
                indBuf.SetCounterValue(0);

                // Paleta de materiales empaquetados como uint RGBA
                int matCount = MaterialTable.Count;
                uint[] paletteData = new uint[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    var c = MaterialTable.Get((byte)i).color;
                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
                    paletteData[i] = (uint)(r) | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
                }
                palette = new ComputeBuffer(matCount, sizeof(uint));
                palette.SetData(paletteData);

                _shader.SetInt("_ChunkSize", s);
                _shader.SetVector("_ChunkOrigin", new Vector4(chunk.origin.x, chunk.origin.y, chunk.origin.z, 0));
                _shader.SetInt("_PaletteCount", matCount);
                _shader.SetBuffer(_kernel, "_Voxels", voxBuf);
                _shader.SetBuffer(_kernel, "_OutVertices", vertBuf);
                _shader.SetBuffer(_kernel, "_OutIndices", indBuf);
                _shader.SetBuffer(_kernel, "_Palette", palette);

                int groups = Mathf.Max(1, (s + 7) / 8);
                if (groups > 65535)
                {
                    Debug.LogWarning($"[GPUChunkMesher] dispatch groups fuera de límite: {groups}.");
                    return false;
                }
                _shader.Dispatch(_kernel, groups, groups, groups);

                // Leer contadores
                counter = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                ComputeBuffer.CopyCount(vertBuf, counter, 0);
                int[] vCount = new int[1];
                counter.GetData(vCount);
                int vertexCount = vCount[0];

                ComputeBuffer.CopyCount(indBuf, counter, 0);
                counter.GetData(vCount);
                int indexCount = vCount[0];

                if (vertexCount == 0 || indexCount == 0)
                {
                    data = new ChunkMeshData { empty = true, bounds = chunk.worldBounds };
                    return true;
                }

                var gpuVerts = new GpuVertex[vertexCount];
                vertBuf.GetData(gpuVerts, 0, 0, vertexCount);
                var indices = new int[indexCount];
                indBuf.GetData(indices, 0, 0, indexCount);

                var verts = new Vector3[vertexCount];
                var norms = new Vector3[vertexCount];
                var cols  = new Color32[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    verts[i] = gpuVerts[i].position;
                    norms[i] = gpuVerts[i].normal;
                    uint c = gpuVerts[i].color;
                    cols[i] = new Color32((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF), (byte)((c >> 24) & 0xFF));
                }

                data = new ChunkMeshData
                {
                    vertices = verts,
                    normals = norms,
                    colors = cols,
                    indices = indices,
                    bounds = chunk.worldBounds,
                    empty = false,
                };
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GPUChunkMesher] fallo, fallback CPU: {e.Message}");
                return false;
            }
            finally
            {
                voxBuf?.Release();
                vertBuf?.Release();
                indBuf?.Release();
                counter?.Release();
                palette?.Release();
            }
        }
    }
}
