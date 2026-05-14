# PhysicsVolumetric

Propósito: colisión y dinámica por muestreo trilineal de densidad y gradiente local.

Invariantes:

1. Muestreo siempre acotado a `Voxel.Empty` fuera de rango.
2. La normal proviene del gradiente central de densidad (1 voxel).
3. La gravedad local se evalúa contra el campo de densidad, no contra mallas.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.ChunkSystem`.
