# SVO

Propósito

- Gestionar LOD espacial con Sparse Voxel Octree sobre coordenadas globales de voxel.

Invariantes

- `rootSize` y `leafSize` son potencias de dos.
- `leafSize <= rootSize`.
- `Refresh` recibe un proveedor de chunks (sin acoplarse a `VoxelWorld`).

Dependencias

- VoxelCore (`VoxelChunk`, `MaterialTable`).
- Sin dependencias circulares hacia runtime.

Ejemplo de uso

```csharp
var octree = new SparseVoxelOctree(512, 16);
octree.MarkDirty(new Vector3Int(0, 0, 0));
octree.Refresh(cc => world.GetChunk(cc, create: false));
```
