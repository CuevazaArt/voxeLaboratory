# ChunkSystem

Propósito: ciclo de vida y direccionamiento de chunks 16³/32³, fachada `VoxelWorld`.

Invariantes:

1. Coordenadas globales se proyectan con división piso (incluye negativas).
2. `GetVoxel` fuera de rango retorna `Voxel.Empty`.
3. `SetVoxel` crea chunks bajo demanda y emite `OnChunkDirty` exactamente una vez por mutación.

Dependencias: `VoxelLab.VoxelCore`, `VoxelLab.SVO`.

Uso:

```csharp
var world = new VoxelWorld(chunkSize: 32, octreeSizePow2: 9);
world.SetVoxel(0, 0, 0, new Voxel(material: 1, densidad: 1f, dureza: 0.5f));
```
