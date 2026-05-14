## Cómo abrir la escena del laboratorio

VoxelLab no incluye un asset `.unity` serializado para mantener el repo libre de YAML binario de Unity. En su lugar:

1. Abre Unity 2022.3 LTS o superior.
2. **File → Open Project…** y selecciona la raíz de este repositorio.
3. Crea una nueva escena vacía (`File → New Scene → Empty`).
4. Crea un GameObject vacío y añádele el componente `VoxeLabBootstrap`
   (namespace `VoxelLab.Scene`).
5. Arrastra `Assets/VoxelLab/Shaders/VoxelMeshing.compute` al campo
   `meshingCompute` del componente.
6. Pulsa **Play**.

Al iniciar verás:

* Un planeta voxel esférico generado proceduralmente.
* Tres cámaras (Orbital `F1`, Fly `F2`, Primera persona `F3`).
* Panel de laboratorio (toggle con `Tab`).
* Click izquierdo aplica la herramienta activa.
