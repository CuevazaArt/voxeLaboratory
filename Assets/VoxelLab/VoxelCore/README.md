# VoxelCore

Propósito

- Definir datos voxel base y contratos de chunk/material para todo el motor.

Invariantes

- `VoxelChunk.size` debe ser potencia de dos y mayor que 0.
- Accesos fuera de rango de chunk deben resolverse con guardas seguras.
- `Voxel.solido` se deriva de `material` y `densidad`.

Dependencias

- UnityEngine (tipos matemáticos y color).
- Sin dependencias a módulos de más alto nivel.

Ejemplo de uso

```csharp
var chunk = new VoxelChunk(16, Vector3Int.zero);
chunk.SetLocal(1, 2, 3, new Voxel((byte)MaterialId.Rock, 1f, 0.7f));
var voxel = chunk.GetLocal(1, 2, 3);
```
