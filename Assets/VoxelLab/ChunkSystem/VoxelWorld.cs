// =====================================================================
//  VoxelWorld.cs
//  VoxelLab :: VoxelCore
//
//  Fachada de alto nivel del volumen. Combina:
//      - un diccionario de chunks (clave = coordenada de chunk)
//      - un Sparse Voxel Octree opcional para LOD/queries amplias
//      - operaciones declaradas en el problema:
//          GetVoxel / SetVoxel
//          CarveSphere / FillSphere
//          Explosion (carve esférico + impulso)
//          RaySample (raymarch por densidad)
//
//  Diseño: no depende de Unity para la lógica de voxels (solo Vector3
//  como tipo de datos). Esto permite testear en EditMode/headless.
//
//  Dependencias: Voxel, VoxelChunk, MaterialTable, SparseVoxelOctree (opcional).
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.SVO;

namespace VoxelLab.Core
{
    /// <summary>Resultado de un raycast volumétrico.</summary>
    public struct RaySampleHit
    {
        public bool hit;
        public Vector3 position;
        public Vector3 normal;
        public float distance;
        public Voxel voxel;
    }

    /// <summary>Muestra de un voxel destruido (para spawnear debris/efectos).</summary>
    public struct DestructionSample
    {
        /// <summary>Centro del voxel en espacio mundo.</summary>
        public Vector3 position;
        /// <summary>Material previo a la destrucción.</summary>
        public byte material;
        /// <summary>Densidad eliminada en esta operación (0..1].</summary>
        public float removedDensity;
    }

    /// <summary>Resultado de un Explosion: lista de voxels afectados (para físicas/efectos).</summary>
    public struct ExplosionResult
    {
        public int voxelsRemoved;
        public Vector3 center;
        public float radius;
        public float force;
        /// <summary>Conteo de voxels destruidos por id de material (longitud = MaterialTable.Count).</summary>
        public int[] removedByMaterial;
        /// <summary>Muestras sub-sampleadas para alimentar sistemas de partículas (puede ser null).</summary>
        public System.Collections.Generic.List<DestructionSample> samples;
    }

    /// <summary>Mundo voxel. Coordenadas en unidades de voxel (1 voxel = 1 unidad mundo).</summary>
    public class VoxelWorld
    {
        public readonly int chunkSize;
        public readonly Dictionary<Vector3Int, VoxelChunk> chunks = new Dictionary<Vector3Int, VoxelChunk>();
        public readonly SparseVoxelOctree octree;

        // Eventos para que el meshing/UI reaccione.
        public event Action<VoxelChunk> OnChunkDirty;

        // Transacción de edición activa (Undo/Redo + batching de eventos dirty).
        private EditTransaction _currentTx;
        private int _txDepth;

        /// <summary>True si hay una transacción de edición abierta.</summary>
        public bool IsEditing => _txDepth > 0;

        /// <summary>Transacción activa (null si no hay).</summary>
        public EditTransaction CurrentTransaction => _currentTx;

        public VoxelWorld(int chunkSize = 16, int octreeSizePow2 = 9 /* 512 */)
        {
            this.chunkSize = chunkSize;
            int s = 1 << octreeSizePow2;
            octree = new SparseVoxelOctree(s, chunkSize);
        }

        // -----------------------------------------------------------------
        //  Coordenadas
        // -----------------------------------------------------------------

        /// <summary>Coordenada de chunk que contiene la coord global de voxel.</summary>
        public Vector3Int ChunkCoord(int x, int y, int z) => new Vector3Int(
            FloorDiv(x, chunkSize), FloorDiv(y, chunkSize), FloorDiv(z, chunkSize));

        /// <summary>Coords locales [0..chunkSize) dentro del chunk.</summary>
        public Vector3Int LocalCoord(int x, int y, int z) => new Vector3Int(
            Mod(x, chunkSize), Mod(y, chunkSize), Mod(z, chunkSize));

        private static int FloorDiv(int a, int b) => (a >= 0) ? a / b : -((-a + b - 1) / b);
        private static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

        // -----------------------------------------------------------------
        //  Chunk lookup / creación bajo demanda
        // -----------------------------------------------------------------

        public VoxelChunk GetChunk(Vector3Int chunkCoord, bool create)
        {
            if (chunks.TryGetValue(chunkCoord, out var c)) return c;
            if (!create) return null;
            var origin = chunkCoord * chunkSize;
            c = new VoxelChunk(chunkSize, origin);
            chunks.Add(chunkCoord, c);
            return c;
        }

        public IEnumerable<VoxelChunk> AllChunks() => chunks.Values;

        // -----------------------------------------------------------------
        //  API solicitada en la spec
        // -----------------------------------------------------------------

        /// <summary>Lee un voxel. Devuelve Voxel.Empty si el chunk no existe.</summary>
        public Voxel GetVoxel(int x, int y, int z)
        {
            var cc = ChunkCoord(x, y, z);
            var chunk = GetChunk(cc, create: false);
            if (chunk == null) return Voxel.Empty;
            var lc = LocalCoord(x, y, z);
            return chunk.voxels[chunk.Index(lc.x, lc.y, lc.z)];
        }

        /// <summary>Escribe un voxel (crea el chunk si hace falta).</summary>
        public void SetVoxel(int x, int y, int z, Voxel v)
        {
            var cc = ChunkCoord(x, y, z);
            var chunk = GetChunk(cc, create: true);
            var lc = LocalCoord(x, y, z);
            int idx = chunk.Index(lc.x, lc.y, lc.z);
            Voxel prev = chunk.voxels[idx];
            chunk.SetLocal(lc.x, lc.y, lc.z, v);
            NotifyEdit(x, y, z, cc, chunk, prev, v);
        }

        // -----------------------------------------------------------------
        //  Transacciones de edición (Undo/Redo + batching)
        // -----------------------------------------------------------------

        /// <summary>Inicia o anida una transacción. Devuelve la activa.</summary>
        public EditTransaction BeginEdit()
        {
            if (_currentTx == null) _currentTx = new EditTransaction();
            _txDepth++;
            return _currentTx;
        }

        /// <summary>
        /// Cierra la transacción más interna. Cuando se cierra la externa,
        /// emite un <see cref="OnChunkDirty"/> por chunk afectado y devuelve
        /// la transacción completa (consumible por un <c>UndoStack</c>).
        /// </summary>
        public EditTransaction EndEdit()
        {
            if (_txDepth <= 0) return null;
            _txDepth--;
            if (_txDepth > 0) return _currentTx;
            var tx = _currentTx;
            _currentTx = null;
            if (tx != null)
            {
                foreach (var c in tx.AffectedChunks)
                {
                    if (!c.empty) c.RecomputeEmpty();
                    OnChunkDirty?.Invoke(c);
                }
            }
            return tx;
        }

        /// <summary>
        /// Si hay transacción activa, registra la edición y difiere el evento dirty.
        /// Si no, marca octree y dispara OnChunkDirty inmediatamente.
        /// </summary>
        private void NotifyEdit(int x, int y, int z, Vector3Int cc, VoxelChunk chunk, Voxel prev, Voxel curr)
        {
            octree.MarkDirty(cc);
            if (_currentTx != null)
                _currentTx.Record(x, y, z, prev, curr, chunk);
            else
                OnChunkDirty?.Invoke(chunk);
        }

        /// <summary>Carve (vacía) una esfera de radio dado en coords mundo.</summary>
        public int CarveSphere(Vector3 center, float radius, float intensity = 1f)
        {
            return ApplySphere(center, radius, (v, t, dist) =>
            {
                if (!v.solido) return v;
                float resist = Mathf.Lerp(1f, 0.1f, intensity); // intensidad alta -> menos resistencia
                float remove = (1f - dist) * intensity * (1f - v.dureza * resist);
                v.densidad = Mathf.Max(0f, v.densidad - remove);
                if (v.densidad < 0.5f) { v.material = 0; v.solido = false; }
                v.Recompute();
                return v;
            }, out int affected);
        }

        /// <summary>Rellena una esfera con el material indicado.</summary>
        public int FillSphere(Vector3 center, float radius, byte material, float densidad = 1f, float dureza = 0.5f)
        {
            return ApplySphere(center, radius, (v, t, dist) =>
            {
                if (v.solido && v.material == material) return v;
                v.material = material;
                v.densidad = Mathf.Max(v.densidad, densidad * (1f - dist * 0.25f));
                v.dureza = dureza;
                v.Recompute();
                return v;
            }, out int affected);
        }

        /// <summary>
        /// Explosión: carve esférico + reporta resultado para empujar entidades.
        /// La fuerza se interpreta por la capa de físicas. Retorna muestras
        /// sub-sampleadas con paso <paramref name="sampleStride"/> (>=1).
        /// </summary>
        public ExplosionResult Explosion(Vector3 center, float radius, float force, int sampleStride = 2)
        {
            sampleStride = Mathf.Max(1, sampleStride);
            int matCount = MaterialTable.Count;
            var removedByMat = new int[matCount];
            var samples = new System.Collections.Generic.List<DestructionSample>(64);

            int removed = ApplySphereSampled(center, radius, sampleStride,
                (Voxel v, float t, float dist, int x, int y, int z) =>
            {
                if (!v.solido) return v;
                byte prevMat = v.material;
                float prevDens = v.densidad;
                float remove = (1f - dist * dist) * (1f - v.dureza * 0.5f) * Mathf.Clamp01(force * 0.5f);
                v.densidad = Mathf.Max(0f, v.densidad - remove);
                bool wasRemoved = v.densidad < 0.5f;
                if (wasRemoved) v.material = 0;
                v.Recompute();
                if (wasRemoved && prevMat < removedByMat.Length) removedByMat[prevMat]++;
                return v;
            },
            (DestructionSample s) => samples.Add(s),
            out int affected);

            return new ExplosionResult
            {
                voxelsRemoved = removed,
                center = center,
                radius = radius,
                force = force,
                removedByMaterial = removedByMat,
                samples = samples,
            };
        }

        /// <summary>Raymarch por densidad. dt=0.5 voxel (Nyquist conservador).</summary>
        public RaySampleHit RaySample(Vector3 origin, Vector3 dir, float maxDist, float dt = 0.5f)
        {
            var hit = new RaySampleHit();
            if (maxDist <= 0f) return hit;

            dir = dir.normalized;
            dt = Mathf.Max(1e-3f, dt);
            float t = 0f;
            Vector3 prev = origin;
            while (t < maxDist)
            {
                var p = origin + dir * t;
                int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
                var v = GetVoxel(xi, yi, zi);
                if (v.solido)
                {
                    hit.hit = true;
                    hit.position = p;
                    hit.distance = t;
                    hit.voxel = v;
                    hit.normal = EstimateNormal(xi, yi, zi);
                    return hit;
                }
                prev = p;
                t += dt;
            }
            return hit;
        }

        /// <summary>Normal central por gradiente de densidad (1 voxel).</summary>
        public Vector3 EstimateNormal(int x, int y, int z)
        {
            float dx = GetVoxel(x + 1, y, z).densidad - GetVoxel(x - 1, y, z).densidad;
            float dy = GetVoxel(x, y + 1, z).densidad - GetVoxel(x, y - 1, z).densidad;
            float dz = GetVoxel(x, y, z + 1).densidad - GetVoxel(x, y, z - 1).densidad;
            var n = new Vector3(-dx, -dy, -dz);
            if (n.sqrMagnitude < 1e-6f) return Vector3.up;
            return n.normalized;
        }

        // -----------------------------------------------------------------
        //  Helpers internos
        // -----------------------------------------------------------------

        /// <summary>Itera todos los voxels en un AABB esférico aplicando un transform.</summary>
        private int ApplySphere(Vector3 center, float radius,
            Func<Voxel, float /*t [0..1]*/, float /*distNorm*/, Voxel> mutator,
            out int affected)
        {
            affected = 0;
            if (radius <= 0f)
                return 0;

            int r = Mathf.CeilToInt(radius);
            int x0 = Mathf.FloorToInt(center.x) - r, x1 = Mathf.FloorToInt(center.x) + r;
            int y0 = Mathf.FloorToInt(center.y) - r, y1 = Mathf.FloorToInt(center.y) + r;
            int z0 = Mathf.FloorToInt(center.z) - r, z1 = Mathf.FloorToInt(center.z) + r;
            float r2 = radius * radius;
            var dirtyChunks = new HashSet<VoxelChunk>();

            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector3 p = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                float d2 = (p - center).sqrMagnitude;
                if (d2 > r2) continue;
                float distNorm = Mathf.Sqrt(d2) / radius;

                var cc = ChunkCoord(x, y, z);
                var chunk = GetChunk(cc, create: true);
                var lc = LocalCoord(x, y, z);
                int idx = chunk.Index(lc.x, lc.y, lc.z);
                var v = chunk.voxels[idx];
                var nv = mutator(v, 1f - distNorm, distNorm);
                if (!v.Equals(nv))
                {
                    chunk.voxels[idx] = nv;
                    chunk.dirty = true;
                    if (nv.solido) chunk.empty = false;
                    dirtyChunks.Add(chunk);
                    if (_currentTx != null) _currentTx.Record(x, y, z, v, nv, chunk);
                    affected++;
                }
            }

            foreach (var c in dirtyChunks)
            {
                if (!c.empty) c.RecomputeEmpty();
                octree.MarkDirty(ChunkCoord(c.origin.x, c.origin.y, c.origin.z));
                if (_currentTx != null) _currentTx.AddChunk(c);
                else OnChunkDirty?.Invoke(c);
            }
            return affected;
        }

        /// <summary>
        /// Variante de <see cref="ApplySphere"/> que reporta una muestra por cada
        /// voxel destruido (densidad cae a 0). El sub-sampleado se controla por
        /// <paramref name="sampleStride"/>: 1 = todos, 2 = uno de cada 2^3, etc.
        /// </summary>
        private int ApplySphereSampled(
            Vector3 center, float radius, int sampleStride,
            System.Func<Voxel, float, float, int, int, int, Voxel> mutator,
            System.Action<DestructionSample> onSample,
            out int affected)
        {
            affected = 0;
            if (radius <= 0f) return 0;

            int r = Mathf.CeilToInt(radius);
            int x0 = Mathf.FloorToInt(center.x) - r, x1 = Mathf.FloorToInt(center.x) + r;
            int y0 = Mathf.FloorToInt(center.y) - r, y1 = Mathf.FloorToInt(center.y) + r;
            int z0 = Mathf.FloorToInt(center.z) - r, z1 = Mathf.FloorToInt(center.z) + r;
            float r2 = radius * radius;
            var dirtyChunks = new HashSet<VoxelChunk>();
            int stride = Mathf.Max(1, sampleStride);

            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector3 p = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                float d2 = (p - center).sqrMagnitude;
                if (d2 > r2) continue;
                float distNorm = Mathf.Sqrt(d2) / radius;

                var cc = ChunkCoord(x, y, z);
                var chunk = GetChunk(cc, create: true);
                var lc = LocalCoord(x, y, z);
                int idx = chunk.Index(lc.x, lc.y, lc.z);
                var v = chunk.voxels[idx];
                byte prevMat = v.material;
                float prevDens = v.densidad;
                var nv = mutator(v, 1f - distNorm, distNorm, x, y, z);
                if (!v.Equals(nv))
                {
                    chunk.voxels[idx] = nv;
                    chunk.dirty = true;
                    if (nv.solido) chunk.empty = false;
                    dirtyChunks.Add(chunk);
                    if (_currentTx != null) _currentTx.Record(x, y, z, v, nv, chunk);
                    affected++;

                    bool destroyed = prevDens >= 0.5f && nv.densidad < 0.5f;
                    if (destroyed && onSample != null &&
                        (((x % stride) + stride) % stride == 0) &&
                        (((y % stride) + stride) % stride == 0) &&
                        (((z % stride) + stride) % stride == 0))
                    {
                        onSample(new DestructionSample
                        {
                            position = p,
                            material = prevMat,
                            removedDensity = prevDens - nv.densidad,
                        });
                    }
                }
            }

            foreach (var c in dirtyChunks)
            {
                if (!c.empty) c.RecomputeEmpty();
                octree.MarkDirty(ChunkCoord(c.origin.x, c.origin.y, c.origin.z));
                if (_currentTx != null) _currentTx.AddChunk(c);
                else OnChunkDirty?.Invoke(c);
            }
            return affected;
        }

        // -----------------------------------------------------------------
        //  Primitivos de relleno (targets)
        // -----------------------------------------------------------------

        /// <summary>Límite duro de voxels por operación de relleno (alineado con cap GPU).</summary>
        public const int MAX_FILL_VOXELS = 8_000_000;

        /// <summary>
        /// Llena un AABB con un material homogéneo. <paramref name="size"/> se clampa a [1..256] por eje
        /// y el volumen total se valida contra <see cref="MAX_FILL_VOXELS"/>.
        /// </summary>
        public int FillBox(Vector3 center, Vector3Int size, byte material, float densidad = 1f, float dureza = 0.5f)
        {
            int sx = Mathf.Clamp(size.x, 1, 256);
            int sy = Mathf.Clamp(size.y, 1, 256);
            int sz = Mathf.Clamp(size.z, 1, 256);
            long volume = (long)sx * sy * sz;
            if (volume > MAX_FILL_VOXELS) return 0;

            int hx = sx / 2, hy = sy / 2, hz = sz / 2;
            int cx = Mathf.FloorToInt(center.x), cy = Mathf.FloorToInt(center.y), cz = Mathf.FloorToInt(center.z);
            int x0 = cx - hx, y0 = cy - hy, z0 = cz - hz;
            int x1 = x0 + sx, y1 = y0 + sy, z1 = z0 + sz;

            float d = Mathf.Clamp01(densidad);
            float h = Mathf.Clamp01(dureza);
            int placed = 0;
            for (int z = z0; z < z1; z++)
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                SetVoxel(x, y, z, new Voxel(material, d, h));
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Llena un cilindro alineado a uno de los tres ejes principales.
        /// <paramref name="axis"/>: 0=X, 1=Y, 2=Z.
        /// </summary>
        public int FillCylinder(Vector3 center, float radius, float height, int axis,
            byte material, float densidad = 1f, float dureza = 0.5f)
        {
            radius = Mathf.Clamp(radius, 0.5f, 128f);
            height = Mathf.Clamp(height, 1f, 256f);
            axis = Mathf.Clamp(axis, 0, 2);

            int r = Mathf.CeilToInt(radius);
            int hh = Mathf.CeilToInt(height * 0.5f);
            long bbox = (long)(2 * r + 1) * (2 * r + 1) * (2 * hh + 1);
            if (bbox > MAX_FILL_VOXELS) return 0;

            int cx = Mathf.FloorToInt(center.x), cy = Mathf.FloorToInt(center.y), cz = Mathf.FloorToInt(center.z);
            float r2 = radius * radius;
            float d = Mathf.Clamp01(densidad);
            float hd = Mathf.Clamp01(dureza);
            int placed = 0;

            // Recorremos: dos ejes ortogonales (a,b) forman el círculo; tercer eje (h) la altura.
            for (int a = -r; a <= r; a++)
            for (int b = -r; b <= r; b++)
            {
                if (a * a + b * b > r2) continue;
                for (int h = -hh; h <= hh; h++)
                {
                    int x, y, z;
                    if (axis == 0)      { x = cx + h; y = cy + a; z = cz + b; }
                    else if (axis == 1) { x = cx + a; y = cy + h; z = cz + b; }
                    else                { x = cx + a; y = cy + b; z = cz + h; }
                    SetVoxel(x, y, z, new Voxel(material, d, hd));
                    placed++;
                }
            }
            return placed;
        }
    }
}
