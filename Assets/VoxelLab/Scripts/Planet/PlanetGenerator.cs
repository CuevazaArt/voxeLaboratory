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
        };
    }

    public static class PlanetGenerator
    {
        public static int Generate(VoxelWorld world, PlanetSettings s)
        {
            int placed = 0;
            int r = Mathf.CeilToInt(s.radius + s.surfaceNoiseAmp + 1);
            int x0 = Mathf.FloorToInt(s.center.x) - r, x1 = Mathf.FloorToInt(s.center.x) + r;
            int y0 = Mathf.FloorToInt(s.center.y) - r, y1 = Mathf.FloorToInt(s.center.y) + r;
            int z0 = Mathf.FloorToInt(s.center.z) - r, z1 = Mathf.FloorToInt(s.center.z) + r;

            for (int z = z0; z <= z1; z++)
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
                    mat = (byte)MaterialId.Dirt;
                else
                    mat = (byte)MaterialId.Rock;

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

            // Tras generación masiva, refrescamos LOD del SVO.
            world.octree.Refresh(world);
            return placed;
        }
    }
}
