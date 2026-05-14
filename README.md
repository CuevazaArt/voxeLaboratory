# voxeLaboratory

**Voxel Simulation Lab** — entorno mínimo, modular y funcional para
experimentar con un planeta voxel destructible: físicas volumétricas,
SVO, meshing en GPU/CPU y cámaras configurables.

## Resumen

El proyecto es una librería + escena bootstrap para Unity 2022.3+. Cada
voxel es un dato (material, densidad, dureza), no un cubo. El mundo se
descompone en chunks 16³/32³ indexados por un Sparse Voxel Octree para
LOD. La malla se genera con un compute shader (con fallback CPU) y las
físicas se resuelven por muestreo trilineal de densidad.

## Apertura rápida

1. Unity 2022.3 LTS o superior → `File → Open Project` → carpeta raíz.
2. Nueva escena vacía → GameObject vacío → añadir componente
   `VoxelLab.Scene.VoxeLabBootstrap`.
3. Arrastrar `Assets/VoxelLab/Shaders/VoxelMeshing.compute` al campo
   `meshingCompute`.
4. **Play**.

## Controles

| Tecla | Acción |
|-------|--------|
| `F1` / `F2` / `F3` | Orbital / Fly / FPS |
| `WASD` `Q/E` | Movimiento (Fly/FPS) |
| Botón derecho ratón | Rotar (Fly/Orbital) |
| Scroll | Zoom (Orbital) |
| `Space` | Saltar (FPS) |
| `Tab` | Mostrar/ocultar UI |
| Click izquierdo | Aplicar herramienta activa |

## Módulos

Ver [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) para diagrama y
dependencias completas. Resumen:

1. **VoxelCore** — `Voxel`, `VoxelChunk`, `VoxelWorld`, `MaterialTable`.
   Operaciones: `GetVoxel/SetVoxel`, `CarveSphere`, `FillSphere`,
   `Explosion`, `RaySample`.
2. **SVO** — `SparseVoxelOctree` con subdivisión perezosa, refresh
   bottom-up de agregados (densidad media, material dominante).
3. **Meshing** — `ChunkMeshBuilder` (CPU greedy-light por caras),
   `GPUChunkMesher` (compute shader con AppendBuffers), `ChunkRenderer`
   (decimación LOD).
4. **Physics** — `VolumetricPhysics` (densidad trilineal, gradiente,
   resolución de colisión, gravedad local) y `VoxelRigidbody`.
5. **Planet** — `NoiseHelper` (Perlin/FBM sin libs externas) y
   `PlanetGenerator` (núcleo de roca, corteza de tierra, vetas de
   minerales, núcleo de lava).
6. **Cameras / Overlays** — Fly, Orbital, FPS + Switcher; overlays
   wireframe / densidad / material / chunk bounds / octree.
7. **Tools** — Drill, Explosion, Brush, Erosion, Cut.
8. **UI** — `LabUI` (IMGUI sin dependencias).

## Tests

`Window → General → Test Runner → EditMode → Run All`. Los tests usan
NUnit y no requieren entrar en Play Mode.

## Diseño

* Código optimizado para **claridad**, no para producción.
* Cada módulo es ejecutable y testeable de forma independiente.
* Sin librerías externas más allá de los módulos base de Unity.
* Cualquier cosa que requiera GPU tiene fallback CPU.

## Licencia

Apache-2.0 — ver [LICENSE](LICENSE).
