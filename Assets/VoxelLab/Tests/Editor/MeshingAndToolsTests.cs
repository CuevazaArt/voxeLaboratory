using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelLab.Core;
using VoxelLab.Meshing;
using VoxelLab.Tools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VoxelLab.Tests
{
    public class MeshingAndToolsTests
    {
        [Test]
        public void GpuChunkMesher_NullShader_IsUnavailable()
        {
            using var mesher = new GPUChunkMesher(null);
            Assert.IsFalse(mesher.IsAvailable);
        }

        [Test]
        public void GpuChunkMesher_TryBuild_ReturnsFalse_WhenUnavailable()
        {
            using var mesher = new GPUChunkMesher(null);
            var chunk = new VoxelChunk(16, Vector3Int.zero);
            bool ok = mesher.TryBuild(chunk, out var data);

            Assert.IsFalse(ok);
            Assert.AreEqual(default(ChunkMeshData), data);
        }

        [Test]
        public void GpuChunkMesher_TryBuildNonBlocking_ReturnsFailed_WhenUnavailable()
        {
            using var mesher = new GPUChunkMesher(null);
            var chunk = new VoxelChunk(16, Vector3Int.zero);
            var status = mesher.TryBuildNonBlocking(chunk, out var data);

            Assert.AreEqual(GPUChunkMesher.BuildStatus.Failed, status);
            Assert.AreEqual(default(ChunkMeshData), data);
        }

        [Test]
        public void GpuChunkMesher_Dispose_IsIdempotent_WhenNoPendingJobs()
        {
            var mesher = new GPUChunkMesher(null);
            Assert.DoesNotThrow(() => mesher.Dispose());
            Assert.DoesNotThrow(() => mesher.Dispose());
        }

        [Test]
        public void GpuChunkMesher_TryBuild_WithComputeShader_ProducesMesh_WhenSupported()
        {
#if UNITY_EDITOR
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VoxelLab/Shaders/VoxelMeshing.compute");
            if (shader == null)
                Assert.Ignore("No se encontro VoxelMeshing.compute en la ruta esperada.");

            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("El entorno no soporta compute shaders.");

            using var mesher = new GPUChunkMesher(shader);
            if (!mesher.IsAvailable)
                Assert.Ignore("GPUChunkMesher no esta disponible en este entorno de test.");

            var chunk = new VoxelChunk(16, Vector3Int.zero);
            chunk.SetLocal(1, 1, 1, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));

            bool ok = mesher.TryBuild(chunk, out var data);
            Assert.IsTrue(ok, "TryBuild deberia completar cuando compute esta disponible.");
            Assert.IsFalse(data.empty, "Un chunk con un voxel solido no deberia quedar vacio.");
            Assert.Greater(data.vertices.Length, 0);
            Assert.Greater(data.indices.Length, 0);
#else
            Assert.Ignore("Test solo valido en Editor.");
#endif
        }

        [Test]
        public void GpuChunkMesher_TryBuildNonBlocking_Completes_WhenSupported()
        {
#if UNITY_EDITOR
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VoxelLab/Shaders/VoxelMeshing.compute");
            if (shader == null)
                Assert.Ignore("No se encontro VoxelMeshing.compute en la ruta esperada.");

            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("El entorno no soporta compute shaders.");

            using var mesher = new GPUChunkMesher(shader);
            if (!mesher.IsAvailable)
                Assert.Ignore("GPUChunkMesher no esta disponible en este entorno de test.");

            var chunk = new VoxelChunk(16, new Vector3Int(16, 0, 0));
            chunk.SetLocal(2, 2, 2, new Voxel((byte)MaterialId.Dirt, 1f, 0.5f));

            var first = mesher.TryBuildNonBlocking(chunk, out var _);
            Assert.AreEqual(GPUChunkMesher.BuildStatus.Pending, first,
                "El primer llamado debe iniciar dispatch y quedar pendiente.");

            AsyncGPUReadback.WaitAllRequests();

            GPUChunkMesher.BuildStatus status = GPUChunkMesher.BuildStatus.Pending;
            ChunkMeshData data = default;
            for (int i = 0; i < 8 && status == GPUChunkMesher.BuildStatus.Pending; i++)
            {
                status = mesher.TryBuildNonBlocking(chunk, out data);
            }

            Assert.AreEqual(GPUChunkMesher.BuildStatus.Ready, status,
                "El flujo non-blocking deberia completar tras resolver readbacks.");
            Assert.IsFalse(data.empty);
            Assert.Greater(data.vertices.Length, 0);
            Assert.Greater(data.indices.Length, 0);
#else
            Assert.Ignore("Test solo valido en Editor.");
#endif
        }

        [Test]
        public void ToolManager_Active_ClampsToFirstTool_WhenIndexIsNegative()
        {
            var go = new GameObject("ToolManager_Test");
            try
            {
                var tm = go.AddComponent<ToolManager>();
                tm.activeIndex = -10;
                Assert.AreEqual("Drill", tm.Active.Name);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ToolManager_Active_ClampsToLastTool_WhenIndexIsTooHigh()
        {
            var go = new GameObject("ToolManager_Test");
            try
            {
                var tm = go.AddComponent<ToolManager>();
                tm.activeIndex = 999;
                Assert.AreEqual("Cut", tm.Active.Name);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ToolManager_Active_ReturnsCurrentTool_WhenIndexIsInRange()
        {
            var go = new GameObject("ToolManager_Test");
            try
            {
                var tm = go.AddComponent<ToolManager>();
                tm.activeIndex = 1;
                Assert.AreEqual("Explosion", tm.Active.Name);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ToolManager_DefaultParameters_AreWithinSanitizedRange()
        {
            var go = new GameObject("ToolManager_Test");
            try
            {
                var tm = go.AddComponent<ToolManager>();
                Assert.GreaterOrEqual(tm.parameters.radius, 0.5f);
                Assert.GreaterOrEqual(tm.parameters.intensity, 0f);
                Assert.LessOrEqual(tm.parameters.intensity, 1f);
                Assert.GreaterOrEqual(tm.parameters.maxDistance, 1f);
                Assert.LessOrEqual(tm.parameters.maxDistance, 2048f);
                Assert.Greater(tm.parameters.planeNormal.sqrMagnitude, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ChunkRenderer_Rebuild_UsesCpuFallback_WhenGpuMesherIsNull()
        {
            var go = new GameObject("ChunkRenderer_Test");
            try
            {
                var renderer = go.AddComponent<ChunkRenderer>();
                var world = new VoxelWorld(16);
                var chunk = world.GetChunk(Vector3Int.zero, create: true);
                chunk.SetLocal(1, 1, 1, new Voxel((byte)MaterialId.Rock, 1f, 0.5f));

                renderer.Bind(chunk, null);
                bool completed = renderer.Rebuild(world, null, lod: 0);

                Assert.IsTrue(completed);
                Assert.AreEqual(0, renderer.LodLevel);
                Assert.IsFalse(chunk.dirty);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ChunkRenderer_Rebuild_SetsLodLevel_ForDecimatedPath()
        {
            var go = new GameObject("ChunkRenderer_Test");
            try
            {
                var renderer = go.AddComponent<ChunkRenderer>();
                var world = new VoxelWorld(16);
                var chunk = world.GetChunk(new Vector3Int(1, 0, 0), create: true);
                chunk.Fill(new Voxel((byte)MaterialId.Dirt, 1f, 0.5f));

                renderer.Bind(chunk, null);
                bool completed = renderer.Rebuild(world, null, lod: 1);

                Assert.IsTrue(completed);
                Assert.AreEqual(1, renderer.LodLevel);
                Assert.IsFalse(chunk.dirty);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // -----------------------------------------------------------------
        //  VoxelChunk
        // -----------------------------------------------------------------

        [Test]
        public void VoxelChunk_Constructor_RejectsNonPowerOfTwoSize()
        {
            Assert.Throws<System.ArgumentException>(() => new VoxelChunk(7, Vector3Int.zero));
            Assert.Throws<System.ArgumentException>(() => new VoxelChunk(0, Vector3Int.zero));
            Assert.Throws<System.ArgumentException>(() => new VoxelChunk(-4, Vector3Int.zero));
        }

        [Test]
        public void VoxelChunk_Fill_MarksDirty_AndUpdatesEmptyFlag()
        {
            var chunk = new VoxelChunk(16, Vector3Int.zero);

            chunk.Fill(Voxel.Empty);
            Assert.IsTrue(chunk.dirty);
            Assert.IsTrue(chunk.empty);

            chunk.Fill(new Voxel((byte)MaterialId.Rock, 1f, 0.5f));
            Assert.IsFalse(chunk.empty);
        }

        [Test]
        public void VoxelChunk_SetLocal_OutOfBounds_IsNoOp()
        {
            var chunk = new VoxelChunk(16, Vector3Int.zero);
            chunk.SetLocal(-1, 0, 0, new Voxel((byte)MaterialId.Iron, 1f, 0.5f));
            chunk.SetLocal(16, 0, 0, new Voxel((byte)MaterialId.Iron, 1f, 0.5f));
            Assert.IsTrue(chunk.empty);
        }

        [Test]
        public void VoxelChunk_RecomputeEmpty_DetectsBothStates()
        {
            var chunk = new VoxelChunk(16, Vector3Int.zero);
            chunk.SetLocal(3, 4, 5, new Voxel((byte)MaterialId.Dirt, 1f, 0.5f));
            chunk.RecomputeEmpty();
            Assert.IsFalse(chunk.empty);

            chunk.SetLocal(3, 4, 5, Voxel.Empty);
            chunk.RecomputeEmpty();
            Assert.IsTrue(chunk.empty);
        }

        [Test]
        public void VoxelChunk_InBounds_RespectsSize()
        {
            var chunk = new VoxelChunk(16, Vector3Int.zero);
            Assert.IsTrue(chunk.InBounds(0, 0, 0));
            Assert.IsTrue(chunk.InBounds(15, 15, 15));
            Assert.IsFalse(chunk.InBounds(16, 0, 0));
            Assert.IsFalse(chunk.InBounds(0, -1, 0));
        }
    }
}
