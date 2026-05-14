// =====================================================================
//  NoiseHelper.cs
//  VoxelLab :: Planet
//
//  Ruido determinista (gradient noise tipo Perlin/Simplex simple) sin
//  dependencias externas. No es production-quality pero alcanza para
//  generación de planetas voxel.
//
//  Dependencias: ninguna.
// =====================================================================
using UnityEngine;

namespace VoxelLab.Planet
{
    public static class NoiseHelper
    {
        // Tabla de permutaciones clásica de Perlin (256 valores duplicados).
        private static readonly int[] Perm = new int[512];
        static NoiseHelper()
        {
            int[] p = {
                151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
                140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
                247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
                57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
                74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
                60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
                65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
                200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
                52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
                207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
                119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
                129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
                218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
                81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
                184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
                222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
            };
            for (int i = 0; i < 512; i++) Perm[i] = p[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>Perlin 3D clásico [-1..1].</summary>
        public static float Perlin3D(float x, float y, float z)
        {
            int X = Mathf.FloorToInt(x) & 255;
            int Y = Mathf.FloorToInt(y) & 255;
            int Z = Mathf.FloorToInt(z) & 255;
            x -= Mathf.Floor(x); y -= Mathf.Floor(y); z -= Mathf.Floor(z);
            float u = Fade(x), v = Fade(y), w = Fade(z);
            int A = Perm[X] + Y, AA = Perm[A] + Z, AB = Perm[A + 1] + Z;
            int B = Perm[X + 1] + Y, BA = Perm[B] + Z, BB = Perm[B + 1] + Z;
            return Lerp(
                Lerp(
                    Lerp(Grad(Perm[AA], x, y, z),     Grad(Perm[BA], x - 1, y, z),     u),
                    Lerp(Grad(Perm[AB], x, y - 1, z), Grad(Perm[BB], x - 1, y - 1, z), u),
                    v),
                Lerp(
                    Lerp(Grad(Perm[AA + 1], x, y, z - 1),     Grad(Perm[BA + 1], x - 1, y, z - 1),     u),
                    Lerp(Grad(Perm[AB + 1], x, y - 1, z - 1), Grad(Perm[BB + 1], x - 1, y - 1, z - 1), u),
                    v),
                w);
        }

        /// <summary>FBM (suma octavas).</summary>
        public static float FBM(Vector3 p, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float amp = 1f, freq = 1f, sum = 0f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Perlin3D(p.x * freq, p.y * freq, p.z * freq);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / Mathf.Max(1e-6f, norm);
        }
    }
}
