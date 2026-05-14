# PlanetGenerator

Propósito: generación procedural inicial del planeta voxel (capas, vetas, núcleo).

Invariantes:

1. Determinista para una misma seed.
2. No depende de Unity Physics ni de meshing.
3. Escribe el mundo solo a través de `VoxelWorld.SetVoxel`.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.ChunkSystem`.
