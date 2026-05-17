// =====================================================================
//  VoxeLab.cs
//  VoxelLab :: Bootstrapper
//
//  Componente principal. En Start():
//      1. Crea el VoxelWorld + SVO.
//      2. Genera el planeta inicial.
//      3. Crea un ChunkRenderer por chunk.
//      4. Conecta cámaras, overlays, herramientas y UI.
//
//  Mantiene una cola de chunks dirty y los remesha por frame con un
//  budget configurable (chunksPerFrame).
//
//  Dependencias: todos los módulos.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Cameras;
using VoxelLab.Meshing;
using VoxelLab.Overlays;
using VoxelLab.Planet;
using VoxelLab.Physics;
using VoxelLab.Tools;
using VoxelLab.UI;

namespace VoxelLab.Boot
{
    public enum WorldMode
    {
        Planet = 0,
        CubeSandbox = 1,
    }

    public class VoxeLab : MonoBehaviour
    {
        [Header("Mundo")]
        public int chunkSize = 16;
        public int octreeSizePow2 = 9;          // 2^9 = 512 voxels lado raíz
        public WorldMode worldMode = WorldMode.Planet;
        public PlanetSettings planet = PlanetSettings.Default;
        public CubeSettings cube = CubeSettings.Default;

        [Header("Render")]
        public Material defaultMaterial;
        public Material wireframeMaterial;
        public Material densityMaterial;
        public Material materialOverlayMaterial;
        public ComputeShader meshingCompute;
        public int chunksPerFrame = 4;
        public float lodScale = 1.0f;

        [Header("Refs UI/Cámaras (opcional, se autocrean si null)")]
        public CameraSwitcher cameraSwitcher;
        public OverlayController overlay;
        public ToolManager toolManager;
        public LabUI ui;

        public VoxelWorld World { get; private set; }
        public GPUChunkMesher Mesher { get; private set; }

        private readonly Queue<VoxelChunk> _dirtyQueue = new Queue<VoxelChunk>();
        private readonly HashSet<VoxelChunk> _dirtySet = new HashSet<VoxelChunk>();
        private readonly Dictionary<VoxelChunk, ChunkRenderer> _renderers = new Dictionary<VoxelChunk, ChunkRenderer>();
        private Transform _chunksRoot;

        public int ChunkCount => _renderers.Count;
        public int SolidVoxelEstimate { get; private set; }

        private void Start()
        {
            BuildWorld();
            HookOverlays();
            HookTools();
            HookUI();
        }

        private void BuildWorld()
        {
            World = new VoxelWorld(chunkSize, octreeSizePow2);
            World.OnChunkDirty += OnChunkDirty;
            Mesher = new GPUChunkMesher(meshingCompute);
            _chunksRoot = new GameObject("Chunks").transform;
            _chunksRoot.SetParent(transform, false);

            int placed = GenerateWorldContent();
            SolidVoxelEstimate = placed;

            // Crear renderers iniciales
            foreach (var c in World.AllChunks())
                EnsureRenderer(c);
            // Encolar todos
            foreach (var c in World.AllChunks())
                if (!c.empty) Enqueue(c);
        }

        private void HookOverlays()
        {
            if (overlay == null)
            {
                var go = new GameObject("Overlays");
                go.transform.SetParent(transform, false);
                overlay = go.AddComponent<OverlayController>();
            }
            overlay.World = World;
            overlay.defaultMat = defaultMaterial;
            overlay.wireframeMat = wireframeMaterial;
            overlay.densityMat = densityMaterial;
            overlay.materialOverlayMat = materialOverlayMaterial;
            RefreshOverlayRendererList();
        }

        private void HookTools()
        {
            if (toolManager == null)
            {
                var go = new GameObject("Tools");
                go.transform.SetParent(transform, false);
                toolManager = go.AddComponent<ToolManager>();
            }
            toolManager.World = World;
        }

        private void HookUI()
        {
            if (ui == null)
            {
                var go = new GameObject("UI");
                go.transform.SetParent(transform, false);
                ui = go.AddComponent<LabUI>();
            }
            ui.lab = this;
            ui.toolManager = toolManager;
            ui.overlays = overlay;
            ui.cameras = cameraSwitcher;
            if (ui.launcher == null)
                ui.launcher = Object.FindFirstObjectByType<ProjectileLauncher>();
        }

        private int GenerateWorldContent()
        {
            if (worldMode == WorldMode.CubeSandbox)
                return CubeGenerator.Generate(World, cube);
            return PlanetGenerator.Generate(World, planet);
        }

        public void RegeneratePlanet()
        {
            RegenerateWorld();
        }

        public void RegenerateWorld()
        {
            // Limpia mundo y rehace.
            foreach (var kv in _renderers) Destroy(kv.Value.gameObject);
            _renderers.Clear();
            _dirtyQueue.Clear();
            _dirtySet.Clear();
            World.chunks.Clear();
            int placed = GenerateWorldContent();
            SolidVoxelEstimate = placed;
            foreach (var c in World.AllChunks())
            {
                EnsureRenderer(c);
                if (!c.empty) Enqueue(c);
            }
            RefreshOverlayRendererList();
        }

        private void RefreshOverlayRendererList()
        {
            var list = new List<ChunkRenderer>(_renderers.Values);
            overlay.Renderers = list.ToArray();
            overlay.ApplyMaterials();
        }

        private ChunkRenderer EnsureRenderer(VoxelChunk c)
        {
            if (_renderers.TryGetValue(c, out var r)) return r;
            var go = new GameObject($"Chunk_{c.origin.x}_{c.origin.y}_{c.origin.z}");
            go.transform.SetParent(_chunksRoot, false);
            r = go.AddComponent<ChunkRenderer>();
            r.Bind(c, defaultMaterial);
            _renderers.Add(c, r);
            return r;
        }

        private void OnChunkDirty(VoxelChunk c)
        {
            EnsureRenderer(c);
            Enqueue(c);
        }

        private void Enqueue(VoxelChunk c)
        {
            if (_dirtySet.Add(c)) _dirtyQueue.Enqueue(c);
        }

        private void Update()
        {
            int budget = Mathf.Max(1, chunksPerFrame);
            while (budget-- > 0 && _dirtyQueue.Count > 0)
            {
                var c = _dirtyQueue.Dequeue();
                _dirtySet.Remove(c);
                if (!_renderers.TryGetValue(c, out var r)) r = EnsureRenderer(c);

                // LOD basado en distancia a la cámara activa.
                int lod = 0;
                if (cameraSwitcher != null && cameraSwitcher.slots != null && cameraSwitcher.slots.Length > 0)
                {
                    var cam = cameraSwitcher.slots[cameraSwitcher.active].camera;
                    if (cam != null)
                    {
                        float dist = Vector3.Distance(cam.transform.position, c.worldBounds.center);
                        lod = Mathf.Clamp(Mathf.FloorToInt(dist / (chunkSize * 4f * lodScale)), 0, 2);
                    }
                }
                bool completed = r.Rebuild(World, Mesher, lod);
                if (!completed)
                    Enqueue(c);
            }

            // Refresh esporádico del LOD del SVO.
            if (Time.frameCount % 60 == 0) World.octree.Refresh(World);
        }

        private void OnDestroy()
        {
            Mesher?.Dispose();
        }
    }
}
