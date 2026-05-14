# voxeLaboratory

Experimental **planetary voxel engine** for Unity. Built with C#, ECS (Entities 1.0),
Compute Shaders (HLSL) and the Universal Render Pipeline (URP).

The repository is organised as independent, self-contained modules with explicit
dependencies. Circular dependencies between modules are forbidden and are enforced
by Unity Assembly Definitions (`.asmdef`) and by the headless build.

## Module map

| Module              | Path                                  | Depends on                        | Responsibility                                            |
|---------------------|---------------------------------------|-----------------------------------|-----------------------------------------------------------|
| `VoxelCore`         | `Assets/Scripts/VoxelCore`            | —                                 | Voxel struct, materials, density, hardness, validation.   |
| `ChunkSystem`       | `Assets/Scripts/ChunkSystem`          | `VoxelCore`                       | Fixed-size chunks (16³), coordinates, in-memory registry. |
| `SVO`               | `Assets/Scripts/SVO`                  | `VoxelCore`, `ChunkSystem`        | Sparse Voxel Octree for planetary LOD.                    |
| `MeshingGPU`        | `Assets/Scripts/MeshingGPU`           | `VoxelCore`, `ChunkSystem`        | HLSL compute shaders for dynamic meshing (planned).       |
| `PhysicsVolumetric` | `Assets/Scripts/PhysicsVolumetric`    | `VoxelCore`, `ChunkSystem`, `SVO` | Sample-based collision, local gravity, raymarching (planned). |
| `PlanetGenerator`   | `Assets/Scripts/PlanetGenerator`      | `VoxelCore`, `ChunkSystem`, `SVO` | 3D noise, layered terrain, spherical topology (planned).  |
| `LabViews`          | `Assets/Scripts/LabViews`             | `VoxelCore`, `ChunkSystem`        | Free / orbital / FPV cameras, technical overlays (planned). |
| `Tools`             | `Assets/Scripts/Tools`                | `VoxelCore`, `ChunkSystem`, `SVO` | Drill, explosion, brush, erosion (planned).               |
| `EditorTools`       | `Assets/Editor/EditorTools`           | all above                         | Inspector windows, chunk/SVO debug visualisers (planned). |

Modules tagged *planned* are intentionally not yet present in the repository: per
directive §3 we only ship code that is complete, documented and tested. New
modules are added in dedicated PRs.

## Hard invariants

* **Voxel addressing.** Local coordinates inside a chunk are integers in
  `[0, ChunkSize)` on every axis. All public APIs validate indices.
* **Chunk size.** `Chunk.Size = 16`, total volume `4096` voxels. The size is a
  compile-time constant so meshing kernels can rely on it.
* **SVO depth.** `SparseVoxelOctree.MaxDepth ≤ 20` (≈ 1 048 576³ leaf resolution).
  Higher depths are rejected at construction time.
* **CPU ↔ GPU sync.** Any data passed to compute shaders must be uploaded through
  `GraphicsBuffer` (never `ComputeBuffer.SetData` from a worker thread).
  Voxels are never represented as `GameObject`s.
* **Concurrency.** Chunk mutations go through `ChunkRegistry`, which serialises
  writes per-chunk. Reads are lock-free snapshots.

## Building and testing without Unity

The Unity Editor is heavy; for fast iteration in CI we compile the pure-C# parts
of the engine against a small `UnityEngine` stub and run NUnit tests with the
.NET SDK. See `Tests/Headless/README.md`.

```bash
dotnet test Tests/Headless/VoxeLaboratory.Headless.Tests.csproj -c Release
```

The same workflow runs on every pull request and on every push to `main`
(see `.github/workflows/ci.yml`).
