# Tools

Propósito: edición volumétrica destructiva y constructiva (taladro, explosión, pincel, erosión, corte).

Invariantes:

1. Toda herramienta sanea entrada (`radius`, `intensity`, `maxDistance`, `planeNormal`).
2. Operaciones esféricas con radio no positivo son no-op.
3. Cada herramienta opera sobre `VoxelWorld`; el empuje físico se delega a `PhysicsVolumetric`.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.ChunkSystem`, `VoxelLab.PhysicsVolumetric`.
