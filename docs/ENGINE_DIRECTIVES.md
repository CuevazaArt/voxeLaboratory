# Engine Directives (Consolidated)

This document defines mandatory engineering rules for VoxelLab.
Scope: architecture, Unity implementation, safety, testing and delivery.

## 1) Mandatory Architecture

Modules are required and must remain acyclic:

1. VoxelCore
2. ChunkSystem
3. SVO
4. MeshingGPU
5. PhysicsVolumetric
6. PlanetGenerator
7. LabViews
8. Tools
9. EditorTools

Rules:

1. Each module is self-contained and documented.
2. No circular assembly references.
3. Domain modules never depend on presentation/editor modules.
4. Runtime and editor code stay separated.

## 2) Unity Implementation Rules

1. Use Entities 1.0 for voxel/chunk/physics systems.
2. Use Jobs + Burst for mass processing.
3. Use Compute Shaders for meshing and bulk voxel operations.
4. Use URP for rendering.
5. Keep CPU <-> GPU sync explicit and documented.
6. Do not represent voxels as per-voxel GameObjects.

## 3) Code Quality Rules

1. Clear names, explicit behavior, no obscure abbreviations.
2. Strict separation of responsibilities.
3. No unresolved technical placeholders.
4. No dead or duplicated code.

Required file header for new runtime/editor source files:

1. Purpose
2. Invariants
3. Dependencies
4. Usage example

## 4) Safety Rules (Voxel Engine)

1. Validate voxel/chunk/SVO indices before reads/writes.
2. Clamp and sanitize tool/UI input.
3. Prevent out-of-range writes in compute kernels.
4. Do not write GPU buffers without validated capacity.
5. Document GPU memory limits per pipeline.
6. Avoid race conditions in chunk updates (single owner per writable region).

## 5) Rational Optimization Rules

1. Prefer clarity over micro-optimization.
2. Optimize only with measurement and user-visible impact.
3. Keep chunk storage compact.
4. Use SVO for LOD and sparse traversal.
5. Use compute where batch throughput is proven beneficial.

## 6) LLM Token Economy Rules

1. Keep diffs focused and small.
2. Avoid repeating unchanged explanations.
3. Regenerate only modified sections.
4. Documentation must stay concise and technical.

## 7) CI / GitHub Actions Rules

1. Fast checks run on pull_request and push to main branches.
2. Cache dependencies.
3. Avoid huge test matrices.
4. Heavy workflows run only on manual trigger.
5. Run only affected checks when possible.

## 8) Voxel Engine Practice Rules

Document and test:

1. Chunk and voxel bounds.
2. SVO limits and subdivision behavior.
3. CPU <-> GPU sync contract.
4. Meshing invariants.
5. Volumetric editing rules.

Unit tests minimum coverage:

1. Voxel access and bounds behavior.
2. SVO subdivision and queries.
3. Chunk regeneration signaling.
4. Destructive tools input sanitization and effects.

Logging:

1. Clear and actionable.
2. Low noise by default.

## 9) Delivery Contract

Every significant delivery should include:

1. Code changes
2. Short technical explanation
3. Dependencies touched
4. Integration steps
5. Technical warnings if applicable

## 10) LLM Operating Role

Allowed role:

1. Voxel architect
2. Safety reviewer
3. Code generator
4. Technical debt auditor
5. Rational optimizer
6. Best-practice guardian

Disallowed role:

1. Uncontrolled quick generator
2. Ambiguous improviser
3. Incomplete solution author
