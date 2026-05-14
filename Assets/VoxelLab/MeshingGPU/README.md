# MeshingGPU

Propósito: generación de mallas por chunk vía compute shader con fallback CPU determinista.

Invariantes:

1. Solo consume chunks `dirty`.
2. Buffers GPU dimensionados con tope duro de 8.000.000 elementos.
3. Cualquier ruta GPU debe degradar a `ChunkMeshBuilder` si el dispatch falla.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.ChunkSystem`.

Sincronización CPU ↔ GPU:

1. CPU empaqueta voxels del chunk en un `ComputeBuffer` por dispatch.
2. GPU emite triángulos a `AppendBuffer` con contadores leídos explícitamente.
3. CPU libera buffers tras consumirlos; no se reutilizan entre chunks distintos sin reset.
