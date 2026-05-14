# Arquitectura — VoxelLab

```text
Assets/VoxelLab/
├── VoxelCore/        (1) Voxel, VoxelChunk, MaterialId, MaterialTable
├── SVO/              (2) SparseVoxelOctree, SVONode
├── Scripts/
│   ├── Core/         Fachada de mundo (`VoxelWorld`) y operaciones volumétricas
│   ├── Meshing/      (3) ChunkMeshBuilder (CPU), GPUChunkMesher, ChunkRenderer
│   ├── Physics/      (4) VolumetricPhysics, VoxelRigidbody
│   ├── Planet/       (5) NoiseHelper, PlanetGenerator
│   ├── Cameras/      (6a) Fly / Orbital / FirstPerson + CameraSwitcher
│   ├── Overlays/     (6b) OverlayController (wireframe/densidad/material/chunk/octree)
│   ├── Tools/        (7) DrillTool, ExplosionTool, BrushTool, ErosionTool, CutTool, ToolManager
│   ├── UI/           (8) LabUI (IMGUI)
│   ├── VoxeLab.cs            (namespace VoxelLab.Boot) Bootstrapper principal
│   └── VoxeLabBootstrap.cs   (namespace VoxelLab.Scene) Crea la escena por código
├── Shaders/          VoxelMeshing.compute, VoxelDefault/Wireframe/Density/Material.shader
├── Scenes/           README de cómo abrir el laboratorio
└── Tests/Editor/     EditMode tests con NUnit (VoxelCoreTests)
```

## Dependencias entre módulos

```text
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

* `VoxelCore` no depende de módulos de más alto nivel.
* `SVO` depende de `VoxelCore` y consume chunks vía delegado (`Refresh`).
* `Meshing` depende de `Core`. La capa GPU usa el compute shader de
  `Shaders/VoxelMeshing.compute` y degrada a CPU si no está disponible.
* `Physics` depende sólo de `Core`. Funciona sin Unity Physics.
* `Tools` depende de `Core` y `Physics` (para empuje en explosiones).
* `UI`, `Overlays`, `Cameras` son capa de presentación.

## Reglas de módulos (sin dependencias circulares)

1. `VoxelCore` no depende de otros módulos del laboratorio.
2. `ChunkSystem`, `SVO`, `MeshingGPU`, `PhysicsVolumetric`, `PlanetGenerator` dependen de `VoxelCore`.
3. `LabViews` y `Tools` solo dependen de capas de dominio (`VoxelCore`, `ChunkSystem`, `PhysicsVolumetric`) y nunca al revés.
4. `EditorTools` solo referencia módulos de runtime para inspección, no para alterar su API pública.
5. Cualquier dependencia nueva debe respetar un grafo dirigido acíclico.

## Sincronización CPU ↔ GPU (contrato)

1. Fuente de verdad: CPU (`VoxelWorld` + `VoxelChunk`).
2. Meshing GPU: empaquetado CPU a `ComputeBuffer` por chunk; dispatch; lectura explícita de contadores y buffers de salida.
3. Fallback obligatorio: si compute no está disponible o falla, usar `ChunkMeshBuilder` en CPU.
4. Actualización de chunks: edición marca `dirty`; el pipeline de malla sólo consume chunks `dirty`.
5. No se escriben buffers GPU sin límites calculados previamente.

## Límites operativos y seguridad

1. Chunk size soportado por pipeline GPU actual: 16 o 32 (potencias de dos).
2. Límite duro de elementos por buffer GPU: `8,000,000` para prevenir asignaciones peligrosas.
3. Todas las herramientas sanitizan entrada (`radius`, `intensity`, `maxDistance`, `planeNormal`).
4. `SVO.Refresh` usa división piso para coordenadas negativas y evita sesgo de agregados con promedio por hijos existentes.
5. Los accesos de voxel/chunk fuera de rango retornan seguro (`Voxel.Empty`) o no-op controlado.

## Evolución hacia ECS 1.0

1. Estado actual: implementación orientada a datos en C# clásico, sin GameObject por voxel.
2. Paso siguiente: mover almacenamiento y scheduling de chunks/meshing a `Entities 1.0 + Jobs + Burst`.
3. Restricción: no usar corutinas para cargas masivas de chunks; usar jobs y colas de trabajo explícitas.

## Ejecutar tests

`Window → General → Test Runner → EditMode → Run All`. Los tests no
requieren Unity Player (usan `VoxelWorld` puro).
