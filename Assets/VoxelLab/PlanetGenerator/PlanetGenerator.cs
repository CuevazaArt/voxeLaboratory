// =====================================================================
//  PlanetGenerator.cs
//  VoxelLab :: Planet
//
//  Genera un planeta esférico voxel:
//      - Capas: roca (núcleo), tierra (corteza), minerales aleatorios
//        (vetas con FBM).
//      - Forma esférica con ruido superficial (montañas/valles).
//
//  Estrategia: itera el AABB que contiene la esfera y, por cada chunk
//  intersectado, llena los voxels correspondientes en el VoxelWorld.
//  Cumple con la subdivisión SVO porque cada SetVoxel marca dirty el
//  octree.
//
//  Dependencias: VoxelWorld, NoiseHelper, MaterialTable.
// =====================================================================
using System.Collections;
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Planet
{
    [System.Serializable]
    public struct PlanetSettings
    {
        public Vector3 center;
        public float radius;            // radio principal (voxels)
        public float surfaceNoiseAmp;   // amplitud de relieve (voxels)
        public float surfaceNoiseScale; // escala de ruido superficial
        public float crustThickness;    // espesor de tierra
        public float oreNoiseScale;     // escala de ruido para vetas
        public float oreThreshold;      // [-1..1] mayor => menos vetas
        public int seed;                // futura compatibilidad
        public bool useBiomes;          // habilita BiomeSelector para la corteza

        public static PlanetSettings Default => new PlanetSettings
        {
            center = Vector3.zero,
            radius = 32f,
            surfaceNoiseAmp = 4f,
            surfaceNoiseScale = 0.06f,
            crustThickness = 3f,
            oreNoiseScale = 0.18f,
            oreThreshold = 0.55f,
            seed = 1337,
            useBiomes = true,
        };
    }

    public static class PlanetGenerator
    {
        public static int Generate(VoxelWorld world, PlanetSettings s)
        {
            int placed = 0;
            world.BeginEdit();
            try
            {
                placed = GenerateRange(world, s, fullPass: true, sliceStart: int.MinValue, sliceEnd: int.MaxValue);
            }
            finally
            {
                world.EndEdit();
            }
            world.octree.Refresh(world);
            return placed;
        }

        /// <summary>
        /// Generacion incremental: divide el AABB en slabs sobre el eje Z y procesa
        /// <paramref name="slabsPerYield"/> por iteracion. El llamador debe consumir
        /// el iterator (StartCoroutine o while MoveNext()).
        /// </summary>
        public static IEnumerator GenerateAsync(VoxelWorld world, PlanetSettings s, int slabsPerYield = 4, System.Action<float> onProgress = null)
        {
            int r = Mathf.CeilToInt(s.radius + s.surfaceNoiseAmp + 1);
            int z0 = Mathf.FloorToInt(s.center.z) - r, z1 = Mathf.FloorToInt(s.center.z) + r;
            int totalSlabs = Mathf.Max(1, z1 - z0 + 1);

            int processed = 0;
            int batch = Mathf.Max(1, slabsPerYield);
            for (int sliceStart = z0; sliceStart <= z1; sliceStart += batch)
            {
                int sliceEnd = Mathf.Min(z1, sliceStart + batch - 1);
                world.BeginEdit();
                try
                {
                    GenerateRange(world, s, fullPass: false, sliceStart: sliceStart, sliceEnd: sliceEnd);
                }
                finally
                {
                    world.EndEdit();
                }
                processed += sliceEnd - sliceStart + 1;
                onProgress?.Invoke(Mathf.Clamp01(processed / (float)totalSlabs));
                yield return null;
            }

            world.octree.Refresh(world);
            onProgress?.Invoke(1f);
        }

        // ------------------------------------------------------------------

        private static int GenerateRange(VoxelWorld world, PlanetSettings s, bool fullPass, int sliceStart, int sliceEnd)
        {
            int placed = 0;
            int r = Mathf.CeilToInt(s.radius + s.surfaceNoiseAmp + 1);
            int x0 = Mathf.FloorToInt(s.center.x) - r, x1 = Mathf.FloorToInt(s.center.x) + r;
            int y0 = Mathf.FloorToInt(s.center.y) - r, y1 = Mathf.FloorToInt(s.center.y) + r;
            int z0 = Mathf.FloorToInt(s.center.z) - r, z1 = Mathf.FloorToInt(s.center.z) + r;

            int zStart = fullPass ? z0 : Mathf.Max(z0, sliceStart);
            int zEnd = fullPass ? z1 : Mathf.Min(z1, sliceEnd);
            if (zEnd < zStart) return 0;

            for (int z = zStart; z <= zEnd; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector3 p = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                Vector3 d = p - s.center;
                float dist = d.magnitude;
                // Relieve por ruido en la superficie (radio efectivo).
                float surfaceNoise = NoiseHelper.FBM(p * s.surfaceNoiseScale, 4) * s.surfaceNoiseAmp;
                float effRadius = s.radius + surfaceNoise;
                if (dist > effRadius) continue;

                byte mat;
                float depth = effRadius - dist;
                if (depth < s.crustThickness)
                {
                    if (s.useBiomes)
                    {
                        var biome = BiomeSelector.Pick(p, s.center, s.radius, s.seed);
                        mat = BiomeSelector.SurfaceMaterial(biome);
                    }
                    else
                    {
                        mat = (byte)MaterialId.Dirt;
                    }
                }
                else
                {
                    mat = (byte)MaterialId.Rock;
                }

                // Vetas de mineral (en la zona de roca).
                if (mat == (byte)MaterialId.Rock)
                {
                    float n = NoiseHelper.FBM(p * s.oreNoiseScale + new Vector3(s.seed, s.seed, s.seed) * 0.001f, 3);
                    if (n > s.oreThreshold)
                    {
                        // Distintos minerales por umbrales adicionales.
                        if (n > s.oreThreshold + 0.18f) mat = (byte)MaterialId.Gold;
                        else if (n > s.oreThreshold + 0.10f) mat = (byte)MaterialId.Crystal;
                        else mat = (byte)MaterialId.Iron;
                    }
                    // Núcleo lava
                    if (dist < s.radius * 0.2f) mat = (byte)MaterialId.Lava;
                }

                var def = MaterialTable.Get(mat);
                var v = new Voxel(mat, 1f, def.baseDureza);
                world.SetVoxel(x, y, z, v);
                placed++;
            }
            return placed;
        }
    }
}
