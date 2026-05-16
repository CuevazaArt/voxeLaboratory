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
using System.Collections.Generic;
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
        public enum BuildStatus
        {
            Pending = 0,
            Ready = 1,
            Failed = 2,
        }

        private sealed class PendingBuild
        {
            public VoxelChunk chunk;
            public int lodStride;
            public ComputeBuffer voxBuf;
            public ComputeBuffer vertBuf;
            public ComputeBuffer indBuf;
            public ComputeBuffer paletteBuf;
            public ComputeBuffer guardCounterBuf;
            public ComputeBuffer vertCounterBuf;
            public ComputeBuffer indCounterBuf;

            public AsyncGPUReadbackRequest vertCounterRequest;
            public AsyncGPUReadbackRequest indCounterRequest;
            public AsyncGPUReadbackRequest vertRequest;
            public AsyncGPUReadbackRequest indRequest;

            public bool hasDataRequests;
            public int vertexCount;
            public int indexCount;
        }

        public const int VERTEX_STRIDE = 3 * 4 + 3 * 4 + 4; // 28 bytes
        public const int MAX_QUADS_PER_VOXEL = 6;
        public const int MAX_CHUNK_SIZE = 32;
        public const long MAX_BUFFER_ELEMENTS = 8_000_000;

        private readonly ComputeShader _shader;
        private readonly int _kernel;
        private readonly Dictionary<VoxelChunk, PendingBuild> _pending = new Dictionary<VoxelChunk, PendingBuild>();

        public bool IsAvailable => _shader != null && _kernel >= 0 && SystemInfo.supportsComputeShaders;

        public GPUChunkMesher(ComputeShader shader)
        {
            _shader = shader;
            if (_shader != null)
                _kernel = _shader.FindKernel("CSMeshChunk");
            else
                _kernel = -1;
        }

        public void Dispose()
        {
            foreach (var it in _pending.Values)
                ReleasePending(it);
            _pending.Clear();
        }

        /// <summary>
        /// Flujo no bloqueante: dispatcha en el primer llamado y va devolviendo Pending
        /// hasta que los readbacks terminan. Cuando termina, devuelve Ready.
        /// Si falla, devuelve Failed para usar fallback CPU.
        /// </summary>
        public BuildStatus TryBuildNonBlocking(VoxelChunk chunk, out ChunkMeshData data)
            => TryBuildNonBlocking(chunk, 1, out data);

        /// <summary>
        /// Variante con stride LOD. <paramref name="lodStride"/> debe ser potencia
        /// de dos &gt;= 1 y &lt;= chunk.size. Hebras no alineadas al stride se
        /// descartan en el kernel; las caras emitidas escalan por stride.
        /// </summary>
        public BuildStatus TryBuildNonBlocking(VoxelChunk chunk, int lodStride, out ChunkMeshData data)
        {
            data = default;
            if (!IsAvailable || chunk == null) return BuildStatus.Failed;

            // Sanitizar stride.
            if (lodStride < 1) lodStride = 1;
            if ((lodStride & (lodStride - 1)) != 0)
            {
                Debug.LogWarning($"[GPUChunkMesher] lodStride debe ser potencia de 2 (recibido {lodStride}). Fallback CPU.");
                return BuildStatus.Failed;
            }
            if (lodStride > chunk.size)
            {
                Debug.LogWarning($"[GPUChunkMesher] lodStride {lodStride} > chunkSize {chunk.size}. Fallback CPU.");
                return BuildStatus.Failed;
            }

            if (!_pending.TryGetValue(chunk, out var pending))
            {
                if (!TryCreatePendingBuild(chunk, lodStride, out pending))
                    return BuildStatus.Failed;
                _pending.Add(chunk, pending);
                return BuildStatus.Pending;
            }

            if (!pending.hasDataRequests)
            {
                if (!pending.vertCounterRequest.done || !pending.indCounterRequest.done)
                    return BuildStatus.Pending;

                if (pending.vertCounterRequest.hasError || pending.indCounterRequest.hasError)
                {
                    CleanupPending(chunk, pending);
                    return BuildStatus.Failed;
                }

                var vertCountData = pending.vertCounterRequest.GetData<int>();
                var indCountData = pending.indCounterRequest.GetData<int>();
                if (vertCountData.Length < 1 || indCountData.Length < 1)
                {
                    CleanupPending(chunk, pending);
                    return BuildStatus.Failed;
                }

                pending.vertexCount = vertCountData[0];
                pending.indexCount = indCountData[0];

                if (pending.vertexCount <= 0 || pending.indexCount <= 0)
                {
                    data = new ChunkMeshData { empty = true, bounds = chunk.worldBounds };
                    CleanupPending(chunk, pending);
                    return BuildStatus.Ready;
                }

                int vertBytes = pending.vertexCount * VERTEX_STRIDE;
                int indBytes = pending.indexCount * sizeof(uint);
                if (vertBytes <= 0 || indBytes <= 0)
                {
                    CleanupPending(chunk, pending);
                    return BuildStatus.Failed;
                }

                pending.vertRequest = AsyncGPUReadback.Request(pending.vertBuf, vertBytes, 0);
                pending.indRequest = AsyncGPUReadback.Request(pending.indBuf, indBytes, 0);
                pending.hasDataRequests = true;
                return BuildStatus.Pending;
            }

            if (!pending.vertRequest.done || !pending.indRequest.done)
                return BuildStatus.Pending;

            if (pending.vertRequest.hasError || pending.indRequest.hasError)
            {
                CleanupPending(chunk, pending);
                return BuildStatus.Failed;
            }

            var gpuVerts = pending.vertRequest.GetData<GpuVertex>();
            var gpuInds = pending.indRequest.GetData<uint>();
            if (gpuVerts.Length != pending.vertexCount || gpuInds.Length != pending.indexCount)
            {
                CleanupPending(chunk, pending);
                return BuildStatus.Failed;
            }

            var verts = new Vector3[pending.vertexCount];
            var norms = new Vector3[pending.vertexCount];
            var cols = new Color32[pending.vertexCount];
            for (int i = 0; i < pending.vertexCount; i++)
            {
                verts[i] = gpuVerts[i].position;
                norms[i] = gpuVerts[i].normal;
                uint c = gpuVerts[i].color;
                cols[i] = new Color32((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF), (byte)((c >> 24) & 0xFF));
            }

            var indices = new int[pending.indexCount];
            for (int i = 0; i < pending.indexCount; i++)
                indices[i] = (int)gpuInds[i];

            data = new ChunkMeshData
            {
                vertices = verts,
                normals = norms,
                colors = cols,
                indices = indices,
                bounds = chunk.worldBounds,
                empty = false,
            };

            CleanupPending(chunk, pending);
            return BuildStatus.Ready;
        }

        private bool TryCreatePendingBuild(VoxelChunk chunk, int lodStride, out PendingBuild pending)
        {
            pending = null;

            int s = chunk.size;
            if (s <= 0 || s > MAX_CHUNK_SIZE || (s & (s - 1)) != 0)
            {
                Debug.LogWarning($"[GPUChunkMesher] chunkSize invalido ({s}). Se usa fallback CPU.");
                return false;
            }

            int volume = s * s * s;
            long maxVerts64 = (long)volume * MAX_QUADS_PER_VOXEL * 4L;
            long maxInds64 = (long)volume * MAX_QUADS_PER_VOXEL * 6L;
            if (maxVerts64 <= 0 || maxInds64 <= 0 || maxVerts64 > MAX_BUFFER_ELEMENTS || maxInds64 > MAX_BUFFER_ELEMENTS)
            {
                Debug.LogWarning($"[GPUChunkMesher] buffers fuera de limite (v={maxVerts64}, i={maxInds64}). Se usa fallback CPU.");
                return false;
            }

            int groups = Mathf.Max(1, (s + 7) / 8);
            if (groups > 65535)
            {
                Debug.LogWarning($"[GPUChunkMesher] dispatch groups fuera de limite: {groups}.");
                return false;
            }

            int maxVerts = (int)maxVerts64;
            int maxInds = (int)maxInds64;

            uint[] packed = new uint[volume];
            for (int i = 0; i < volume; i++)
            {
                var v = chunk.voxels[i];
                uint mat = v.material;
                uint dens = (uint)Mathf.Clamp(Mathf.RoundToInt(v.densidad * 255f), 0, 255);
                uint dur = (uint)Mathf.Clamp(Mathf.RoundToInt(v.dureza * 255f), 0, 255);
                uint sol = v.solido ? 1u : 0u;
                packed[i] = (mat & 0xFFu) | ((dens & 0xFFu) << 8) | ((dur & 0xFFu) << 16) | ((sol & 0x1u) << 24);
            }

            ComputeBuffer voxBuf = null;
            ComputeBuffer vertBuf = null;
            ComputeBuffer indBuf = null;
            ComputeBuffer paletteBuf = null;
            ComputeBuffer guardCounterBuf = null;
            ComputeBuffer vertCounterBuf = null;
            ComputeBuffer indCounterBuf = null;

            try
            {
                voxBuf = new ComputeBuffer(volume, sizeof(uint), ComputeBufferType.Structured);
                voxBuf.SetData(packed);

                vertBuf = new ComputeBuffer(maxVerts, VERTEX_STRIDE, ComputeBufferType.Counter);
                vertBuf.SetCounterValue(0);
                indBuf = new ComputeBuffer(maxInds, sizeof(uint), ComputeBufferType.Counter);
                indBuf.SetCounterValue(0);

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

                paletteBuf = new ComputeBuffer(matCount, sizeof(uint));
                paletteBuf.SetData(paletteData);

                guardCounterBuf = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
                guardCounterBuf.SetData(new uint[2]);

                _shader.SetInt("_ChunkSize", s);
                _shader.SetVector("_ChunkOrigin", new Vector4(chunk.origin.x, chunk.origin.y, chunk.origin.z, 0));
                _shader.SetInt("_PaletteCount", matCount);
                _shader.SetInt("_MaxVertices", maxVerts);
                _shader.SetInt("_MaxIndices", maxInds);
                _shader.SetInt("_LodStride", lodStride);
                _shader.SetBuffer(_kernel, "_Voxels", voxBuf);
                _shader.SetBuffer(_kernel, "_OutVertices", vertBuf);
                _shader.SetBuffer(_kernel, "_OutIndices", indBuf);
                _shader.SetBuffer(_kernel, "_Palette", paletteBuf);
                _shader.SetBuffer(_kernel, "_GuardCounters", guardCounterBuf);

                _shader.Dispatch(_kernel, groups, groups, groups);

                vertCounterBuf = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                indCounterBuf = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                ComputeBuffer.CopyCount(vertBuf, vertCounterBuf, 0);
                ComputeBuffer.CopyCount(indBuf, indCounterBuf, 0);

                pending = new PendingBuild
                {
                    chunk = chunk,
                    lodStride = lodStride,
                    voxBuf = voxBuf,
                    vertBuf = vertBuf,
                    indBuf = indBuf,
                    paletteBuf = paletteBuf,
                    guardCounterBuf = guardCounterBuf,
                    vertCounterBuf = vertCounterBuf,
                    indCounterBuf = indCounterBuf,
                    vertCounterRequest = AsyncGPUReadback.Request(vertCounterBuf),
                    indCounterRequest = AsyncGPUReadback.Request(indCounterBuf),
                    hasDataRequests = false,
                };

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GPUChunkMesher] fallo, fallback CPU: {e.Message}");
                voxBuf?.Release();
                vertBuf?.Release();
                indBuf?.Release();
                paletteBuf?.Release();
                guardCounterBuf?.Release();
                vertCounterBuf?.Release();
                indCounterBuf?.Release();
                return false;
            }
        }

        private void CleanupPending(VoxelChunk chunk, PendingBuild pending)
        {
            _pending.Remove(chunk);
            ReleasePending(pending);
        }

        private static void ReleasePending(PendingBuild pending)
        {
            pending.voxBuf?.Release();
            pending.vertBuf?.Release();
            pending.indBuf?.Release();
            pending.paletteBuf?.Release();
            pending.guardCounterBuf?.Release();
            pending.vertCounterBuf?.Release();
            pending.indCounterBuf?.Release();
        }

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
            ComputeBuffer guardCounterBuf = null;
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

                guardCounterBuf = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
                guardCounterBuf.SetData(new uint[2]);

                _shader.SetInt("_ChunkSize", s);
                _shader.SetVector("_ChunkOrigin", new Vector4(chunk.origin.x, chunk.origin.y, chunk.origin.z, 0));
                _shader.SetInt("_PaletteCount", matCount);
                _shader.SetInt("_MaxVertices", maxVerts);
                _shader.SetInt("_MaxIndices", maxInds);
                _shader.SetInt("_LodStride", 1);
                _shader.SetBuffer(_kernel, "_Voxels", voxBuf);
                _shader.SetBuffer(_kernel, "_OutVertices", vertBuf);
                _shader.SetBuffer(_kernel, "_OutIndices", indBuf);
                _shader.SetBuffer(_kernel, "_Palette", palette);
                _shader.SetBuffer(_kernel, "_GuardCounters", guardCounterBuf);

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
                guardCounterBuf?.Release();
            }
        }
    }
}
