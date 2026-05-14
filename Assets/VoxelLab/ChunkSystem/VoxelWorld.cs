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

    /// <summary>Resultado de un Explosion: lista de voxels afectados (para físicas/efectos).</summary>
    public struct ExplosionResult
    {
        public int voxelsRemoved;
        public Vector3 center;
        public float radius;
        public float force;
    }

    /// <summary>Mundo voxel. Coordenadas en unidades de voxel (1 voxel = 1 unidad mundo).</summary>
    public class VoxelWorld
    {
        public readonly int chunkSize;
        public readonly Dictionary<Vector3Int, VoxelChunk> chunks = new Dictionary<Vector3Int, VoxelChunk>();
        public readonly SparseVoxelOctree octree;

        // Eventos para que el meshing/UI reaccione.
        public event Action<VoxelChunk> OnChunkDirty;

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
            chunk.SetLocal(lc.x, lc.y, lc.z, v);
            octree.MarkDirty(cc);
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
        /// La fuerza se interpreta por la capa de físicas.
        /// </summary>
        public ExplosionResult Explosion(Vector3 center, float radius, float force)
        {
            int removed = ApplySphere(center, radius, (v, t, dist) =>
            {
                if (!v.solido) return v;
                float remove = (1f - dist * dist) * (1f - v.dureza * 0.5f) * Mathf.Clamp01(force * 0.5f);
                v.densidad = Mathf.Max(0f, v.densidad - remove);
                if (v.densidad < 0.5f) { v.material = 0; }
                v.Recompute();
                return v;
            }, out int affected);

            return new ExplosionResult
            {
                voxelsRemoved = removed,
                center = center,
                radius = radius,
                force = force,
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
                    affected++;
                }
            }

            foreach (var c in dirtyChunks)
            {
                if (!c.empty) c.RecomputeEmpty();
                octree.MarkDirty(ChunkCoord(c.origin.x, c.origin.y, c.origin.z));
                OnChunkDirty?.Invoke(c);
            }
            return affected;
        }
    }
}
