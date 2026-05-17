// =====================================================================
//  CubeGenerator.cs
//  VoxelLab :: Planet
//
//  Propósito: rellenar un cubo NxNxN sólido de un material homogéneo en
//  el VoxelWorld para el laboratorio balístico (sandbox sin gravedad).
//
//  Invariantes:
//      - Tamaño del cubo se interpreta en voxels (entero, >= 1).
//      - Centro en coordenadas mundo (voxel-space).
//      - No depende de generación procedural ni de SVO; solo SetVoxel.
//      - Determinista: misma entrada => misma salida.
//
//  Dependencias: VoxelWorld, MaterialTable, Voxel.
//
//  Uso:
//      CubeGenerator.Generate(world, CubeSettings.Default);
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Planet
{
    [System.Serializable]
    public struct CubeSettings
    {
        public Vector3Int center;   // centro del cubo en voxel-space
        public int size;            // longitud de arista en voxels (>= 1)
        public byte material;       // id de material (MaterialId)
        public float densidad;      // [0..1]
        public float dureza;        // [0..1]

        public static CubeSettings Default => new CubeSettings
        {
            center = Vector3Int.zero,
            size = 16,
            material = (byte)MaterialId.Rock,
            densidad = 1f,
            dureza = 0.5f,
        };
    }

    public static class CubeGenerator
    {
        /// <summary>Llena un cubo sólido de tamaño <c>s.size</c> centrado en <c>s.center</c>.</summary>
        public static int Generate(VoxelWorld world, CubeSettings s)
        {
            if (world == null) return 0;
            int size = Mathf.Max(1, s.size);
            int half = size / 2;
            int x0 = s.center.x - half;
            int y0 = s.center.y - half;
            int z0 = s.center.z - half;
            int x1 = x0 + size;
            int y1 = y0 + size;
            int z1 = z0 + size;

            float densidad = Mathf.Clamp01(s.densidad);
            float dureza = Mathf.Clamp01(s.dureza);
            byte material = s.material;

            int placed = 0;
            for (int z = z0; z < z1; z++)
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                world.SetVoxel(x, y, z, new Voxel(material, densidad, dureza));
                placed++;
            }

            world.octree.Refresh(cc => world.GetChunk(cc, false));
            return placed;
        }
    }
}
