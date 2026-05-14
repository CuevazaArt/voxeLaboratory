# Bootstrap

Propósito: composición runtime de la escena de laboratorio.

Contiene:

1. `VoxeLab` — componente principal: crea `VoxelWorld`, genera planeta, conecta meshing, físicas, herramientas y UI.
2. `VoxeLabBootstrap` — crea por código una escena funcional sin prefab serializado.

Invariantes:

1. No define lógica de dominio; solo orquesta módulos existentes.
2. Es el único módulo autorizado a referenciar todos los demás.

Dependencias: todos los módulos de runtime.
