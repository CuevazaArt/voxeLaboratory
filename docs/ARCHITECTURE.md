# Arquitectura — VoxelLab

```
Assets/VoxelLab/
├── Scripts/
│   ├── Core/         (1) VoxelCore       — Voxel, VoxelChunk, VoxelWorld, MaterialId
│   ├── SVO/          (2) Sparse Voxel Octree
│   ├── Meshing/      (3) ChunkMeshBuilder (CPU), GPUChunkMesher, ChunkRenderer
│   ├── Physics/      (4) VolumetricPhysics, VoxelRigidbody
│   ├── Planet/       (5) NoiseHelper, PlanetGenerator
│   ├── Cameras/      (6a) Fly / Orbital / FirstPerson + CameraSwitcher
│   ├── Overlays/     (6b) OverlayController (wireframe/densidad/material/chunk/octree)
│   ├── Tools/        (7) DrillTool, ExplosionTool, BrushTool, ErosionTool, CutTool, ToolManager
│   ├── UI/           (8) LabUI (IMGUI)
│   ├── VoxeLab.cs    Bootstrapper principal
│   └── VoxeLabBootstrap.cs  Crea la escena por código
├── Shaders/          VoxelMeshing.compute, VoxelDefault/Wireframe/Density/Material.shader
├── Scenes/           README de cómo abrir el laboratorio
└── Tests/Editor/     EditMode tests con NUnit (VoxelCoreTests)
```

## Dependencias entre módulos

```
   Core ─────────────┐
     │               │
     ▼               ▼
    SVO          Meshing  ──────┐
     │               │           │
     ▼               ▼           │
   Planet         Overlays       │
                                ▼
                          Physics ◄── Tools ◄── UI ◄── Bootstrapper
```

* `Core` no depende de Unity (excepto tipos de datos como `Vector3`).
* `SVO` depende de `Core`.
* `Meshing` depende de `Core`. La capa GPU usa el compute shader de
  `Shaders/VoxelMeshing.compute` y degrada a CPU si no está disponible.
* `Physics` depende sólo de `Core`. Funciona sin Unity Physics.
* `Tools` depende de `Core` y `Physics` (para empuje en explosiones).
* `UI`, `Overlays`, `Cameras` son capa de presentación.

## Ejecutar tests

`Window → General → Test Runner → EditMode → Run All`. Los tests no
requieren Unity Player (usan `VoxelWorld` puro).
