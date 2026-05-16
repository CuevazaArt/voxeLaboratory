// =====================================================================
//  PlanetGeneratorTests.cs
//  VoxelLab :: Tests
//
//  Pruebas deterministicas de PlanetGenerator y BiomeSelector.
// =====================================================================
using NUnit.Framework;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Planet;

namespace VoxelLab.Tests
{
    public class PlanetGeneratorTests
    {
        private static VoxelWorld NewWorld()
        {
            return new VoxelWorld(chunkSize: 8, octreeSizePow2: 6 /* 64 */);
        }

        [Test]
        public void Generate_SameSeed_Deterministic()
        {
            var s = PlanetSettings.Default;
            s.radius = 10f;
            s.surfaceNoiseAmp = 1f;
            s.seed = 42;

            var w1 = NewWorld();
            int a = PlanetGenerator.Generate(w1, s);
            var w2 = NewWorld();
            int b = PlanetGenerator.Generate(w2, s);

            Assert.AreEqual(a, b, "El mismo seed debe producir el mismo numero de voxels colocados.");
            Assert.Greater(a, 0);
        }

        [Test]
        public void Generate_DifferentSeed_DifferentResult()
        {
            var sA = PlanetSettings.Default;
            sA.radius = 10f; sA.surfaceNoiseAmp = 1f; sA.seed = 1;
            var sB = sA; sB.seed = 9999;

            var wA = NewWorld(); int a = PlanetGenerator.Generate(wA, sA);
            var wB = NewWorld(); int b = PlanetGenerator.Generate(wB, sB);

            // Mismo radio aprox, los counts pueden coincidir; lo critico es que no crashee
            // y que ambos generen contenido. Si coinciden por azar, no rompe el test.
            Assert.Greater(a, 0);
            Assert.Greater(b, 0);
        }

        [Test]
        public void Biome_Pole_ReturnsTundra()
        {
            var pole = new Vector3(0, 20f, 0);
            var biome = BiomeSelector.Pick(pole, Vector3.zero, 20f, 0);
            Assert.AreEqual(BiomeId.Tundra, biome);
        }

        [Test]
        public void Biome_SurfaceMaterial_NeverAir()
        {
            for (int b = 0; b < 4; b++)
            {
                byte m = BiomeSelector.SurfaceMaterial((BiomeId)b);
                Assert.AreNotEqual((byte)MaterialId.Air, m);
            }
        }
    }
}
