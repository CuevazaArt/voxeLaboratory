# AGENTS

Repository execution policy for coding agents.

## Mission

Build and evolve a secure, modular, experimental planetary voxel engine for Unity with ECS, compute shaders, SVO and volumetric physics.

## Mandatory constraints

1. Follow docs/ENGINE_DIRECTIVES.md.
2. Keep module boundaries acyclic.
3. Prefer minimal diffs that preserve existing public APIs unless explicitly changed.
4. Add or update tests with behavior changes.
5. Document CPU <-> GPU contracts for meshing or buffer updates.

## Architecture boundaries

1. VoxelCore contains voxel primitives and chunk-level data contracts.
2. ChunkSystem owns chunk lifecycle and synchronization.
3. SVO owns sparse hierarchy and LOD traversal.
4. MeshingGPU owns compute meshing pipeline and fallback contract.
5. PhysicsVolumetric owns sampling collision and local gravity behavior.
6. PlanetGenerator owns procedural data generation.
7. LabViews owns runtime technical visualization.
8. Tools owns volumetric editing actions.
9. EditorTools owns editor-only diagnostics and tooling.

## Safety checklist before merge

1. Bounds checks for voxel/chunk/svo addressing.
2. Compute kernels enforce capacity and index guards.
3. No unbounded GPU allocations.
4. No concurrent writes to same chunk region.
5. Tool input sanitized (radius, intensity, distance, normals).

## Delivery checklist

1. Code
2. Brief explanation
3. Dependencies touched
4. Integration instructions
5. Technical warnings
