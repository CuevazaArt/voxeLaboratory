# LabViews

Propósito: capa de presentación técnica (cámaras, overlays y UI IMGUI).

Submódulos:

1. `Cameras/` — Fly, Orbital, FirstPerson y CameraSwitcher.
2. `Overlays/` — wireframe, densidad, material, chunk bounds, octree.
3. `UI/` — `LabUI` IMGUI sin dependencias externas.

Invariantes:

1. No muta el estado del mundo voxel directamente, solo lo visualiza o invoca `Tools`.
2. No es referenciado por módulos de dominio.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.SVO`, `VoxelLab.ChunkSystem`, `VoxelLab.MeshingGPU`, `VoxelLab.PhysicsVolumetric`, `VoxelLab.Tools`.
