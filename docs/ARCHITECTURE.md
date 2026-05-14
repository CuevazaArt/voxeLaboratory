# Arquitectura — VoxelLab

## Layout

```text
Assets/VoxelLab/
├── VoxelCore/            (1) Voxel, VoxelChunk, MaterialId
├── SVO/                  (2) SparseVoxelOctree, SVONode
├── ChunkSystem/          (3) VoxelWorld (fachada de mundo y operaciones volumétricas)
├── MeshingGPU/           (4) ChunkMeshBuilder (CPU), GPUChunkMesher, ChunkRenderer
├── PhysicsVolumetric/    (5) VolumetricPhysics, VoxelRigidbody
├── PlanetGenerator/      (6) NoiseHelper, PlanetGenerator
├── Tools/                (7) ToolManager, VoxelTools (Drill, Explosion, Brush, Erosion, Cut)
├── LabViews/             (8) Cameras, Overlays, UI (capa de presentación)
├── EditorTools/          (9) ventanas de editor (placeholder)
├── Bootstrap/            (R) VoxeLab, VoxeLabBootstrap (composición de escena)
├── Shaders/              VoxelMeshing.compute, VoxelDefault/Wireframe/Density/Material.shader
├── Scenes/               README de cómo abrir el laboratorio
└── Tests/Editor/         EditMode tests (NUnit)
```

## Assemblies (asmdef)

| Assembly                       | Carpeta                | Depende de                                                                                          |
| ------------------------------ | ---------------------- | --------------------------------------------------------------------------------------------------- |
| `VoxelLab.VoxelCore`           | `VoxelCore/`           | —                                                                                                   |
| `VoxelLab.SVO`                 | `SVO/`                 | VoxelCore                                                                                           |
| `VoxelLab.ChunkSystem`         | `ChunkSystem/`         | VoxelCore, SVO                                                                                      |
| `VoxelLab.MeshingGPU`          | `MeshingGPU/`          | VoxelCore, ChunkSystem                                                                              |
| `VoxelLab.PhysicsVolumetric`   | `PhysicsVolumetric/`   | VoxelCore, ChunkSystem                                                                              |
| `VoxelLab.PlanetGenerator`     | `PlanetGenerator/`     | VoxelCore, ChunkSystem                                                                              |
| `VoxelLab.Tools`               | `Tools/`               | VoxelCore, ChunkSystem, PhysicsVolumetric                                                           |
| `VoxelLab.LabViews`            | `LabViews/`            | VoxelCore, SVO, ChunkSystem, MeshingGPU, PhysicsVolumetric, Tools                                   |
| `VoxelLab.Bootstrap`           | `Bootstrap/`           | todos los runtime                                                                                   |
| `VoxelLab.EditorTools` (Editor)| `EditorTools/`         | todos los runtime                                                                                   |
| `VoxelLab.Tests` (Editor)      | `Tests/Editor/`        | VoxelCore, SVO, ChunkSystem, PhysicsVolumetric, Tools                                               |

## Grafo de dependencias

```text
VoxelCore ──► SVO ──► ChunkSystem ──┬─► MeshingGPU
                                    ├─► PhysicsVolumetric ──► Tools
                                    └─► PlanetGenerator

LabViews   ◄── { ChunkSystem, MeshingGPU, PhysicsVolumetric, Tools, SVO }
Bootstrap  ◄── { todos los runtime }
EditorTools◄── { todos los runtime }   (solo Editor)
```

Reglas:

1. `VoxelCore` no depende de ningún módulo del laboratorio.
2. `ChunkSystem`, `SVO`, `MeshingGPU`, `PhysicsVolumetric`, `PlanetGenerator` solo dependen de `VoxelCore` (y `SVO` cuando aplica).
3. `LabViews`, `Tools` solo dependen de capas de dominio y nunca al revés.
4. `EditorTools` solo referencia runtime para inspección.
5. `Bootstrap` es el único módulo autorizado a componer todos los runtime.
6. Toda dependencia nueva debe respetar un grafo dirigido acíclico.

## Sincronización CPU ↔ GPU (contrato)

1. Fuente de verdad: CPU (`VoxelWorld` + `VoxelChunk`).
2. Meshing GPU: empaquetado CPU a `ComputeBuffer` por chunk; dispatch; lectura explícita de contadores y buffers de salida.
3. Fallback obligatorio: si compute no está disponible o falla, usar `ChunkMeshBuilder` en CPU.
4. Actualización de chunks: edición marca `dirty`; el pipeline solo consume chunks `dirty`.
5. No se escriben buffers GPU sin capacidad calculada previamente.

## Límites operativos y seguridad

1. Chunk size soportado por el pipeline GPU actual: 16 o 32 (potencias de dos).
2. Límite duro de elementos por buffer GPU: `8.000.000` para prevenir asignaciones peligrosas.
3. Toda herramienta sanitiza entrada (`radius`, `intensity`, `maxDistance`, `planeNormal`).
4. `SVO.Refresh` usa división piso para coordenadas negativas.
5. Accesos de voxel/chunk fuera de rango devuelven seguro (`Voxel.Empty`) o no-op controlado.

## Evolución hacia ECS 1.0

1. Estado actual: implementación orientada a datos en C# clásico, sin GameObject por voxel.
2. Paso siguiente: mover almacenamiento y scheduling de chunks/meshing a `Entities 1.0 + Jobs + Burst`.
3. Restricción: no usar corutinas para cargas masivas de chunks; usar jobs y colas explícitas.

## Tests

`Window → General → Test Runner → EditMode → Run All`. Los tests usan
`VoxelLab.VoxelCore`, `VoxelLab.ChunkSystem`, `VoxelLab.SVO`,
`VoxelLab.PhysicsVolumetric` y `VoxelLab.Tools` directamente, sin `Bootstrap`.
