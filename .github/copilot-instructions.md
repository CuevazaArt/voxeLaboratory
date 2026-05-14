# Copilot Instructions For VoxelLab

Apply these rules in every code/doc change.

## Core rules

1. Follow docs/ENGINE_DIRECTIVES.md and AGENTS.md.
2. Keep answers concise and technical.
3. Propose and apply small, reviewable diffs.
4. Do not introduce unresolved placeholders.

## Unity rules

1. Prefer ECS + Jobs + Burst for mass voxel/chunk processing.
2. Prefer compute shaders for meshing and bulk operations.
3. Keep CPU <-> GPU synchronization explicit in code and docs.
4. Avoid per-voxel GameObjects.

## Safety rules

1. Validate all voxel/chunk/SVO indices.
2. Clamp and sanitize tool/UI inputs.
3. Guard compute buffer accesses and capacities.
4. Document practical GPU memory limits when touching meshing.

## Testing rules

1. Add unit tests for new behavior.
2. Cover bounds, SVO subdivision and destructive tools.
3. Keep tests deterministic and fast.

## Delivery rules

Each significant change should include:

1. Code
2. Short explanation
3. Dependencies touched
4. Integration instructions
5. Technical warnings if relevant
