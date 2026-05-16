// =====================================================================
//  Biome.cs
//  VoxelLab :: Planet
//
//  Selector de material por bioma. Se basa en parametros geometricos
//  ya disponibles en PlanetGenerator (latitud derivada del eje Y,
//  altitud relativa al radio nominal y temperatura/humedad de noise).
//
//  Mantiene el mismo seed-flow que NoiseHelper.FBM para reproducibilidad.
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Planet
{
    public enum BiomeId : byte
    {
        Plains = 0,
        Desert = 1,
        Tundra = 2,
        Highlands = 3,
    }

    public static class BiomeSelector
    {
        /// <summary>
        /// Decide el bioma de la superficie a partir de la posicion en el planeta.
        /// </summary>
        public static BiomeId Pick(Vector3 surfacePos, Vector3 center, float radius, int seed)
        {
            if (radius <= 0f) return BiomeId.Plains;
            Vector3 r = surfacePos - center;
            float latitude01 = Mathf.Clamp01(Mathf.Abs(r.y) / Mathf.Max(0.001f, radius)); // 0=ecuador, 1=polos
            float altitude01 = Mathf.Clamp01((r.magnitude - radius * 0.95f) / Mathf.Max(0.001f, radius * 0.15f));

            // Humedad derivada de FBM determinista por seed.
            Vector3 sample = surfacePos * 0.07f + new Vector3(seed * 0.013f, seed * 0.017f, seed * 0.019f);
            float humidity = NoiseHelper.FBM(sample, 3); // 0..1 aprox

            if (latitude01 > 0.78f) return BiomeId.Tundra;
            if (altitude01 > 0.6f) return BiomeId.Highlands;
            if (humidity < 0.35f && latitude01 < 0.55f) return BiomeId.Desert;
            return BiomeId.Plains;
        }

        /// <summary>Material de superficie segun bioma.</summary>
        public static byte SurfaceMaterial(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Desert: return (byte)MaterialId.Sand;
                case BiomeId.Tundra: return (byte)MaterialId.Ice;
                case BiomeId.Highlands: return (byte)MaterialId.Rock;
                default: return (byte)MaterialId.Dirt;
            }
        }
    }
}
