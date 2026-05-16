// =====================================================================
//  TargetSpawner.cs
//  VoxelLab :: Planet
//
//  Propósito: convertir un TargetDef en voxels dentro de un VoxelWorld
//  delegando en las primitivas FillSphere / FillBox / FillCylinder.
//  Soporta Composite (recursivo) con profundidad acotada.
//
//  Invariantes:
//      - world y def != null (no-op silencioso si null).
//      - Cada sub-spawn aplica positionOffset acumulativamente.
//      - Profundidad de recursión limitada a MAX_DEPTH para evitar
//        ciclos accidentales en Composite mal autoreferenciados.
//
//  Dependencias: VoxelWorld, TargetDef.
//
//  Uso:
//      int placed = TargetSpawner.Spawn(world, target, originVoxel);
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Planet
{
    public static class TargetSpawner
    {
        public const int MAX_DEPTH = 4;

        /// <summary>Spawnea un target en <paramref name="origin"/> y devuelve voxels colocados.</summary>
        public static int Spawn(VoxelWorld world, TargetDef def, Vector3 origin)
        {
            return SpawnInternal(world, def, origin, depth: 0);
        }

        private static int SpawnInternal(VoxelWorld world, TargetDef def, Vector3 origin, int depth)
        {
            if (world == null || def == null) return 0;
            if (depth > MAX_DEPTH)
            {
                Debug.LogWarning($"TargetSpawner: profundidad máxima {MAX_DEPTH} alcanzada.");
                return 0;
            }

            Vector3 pos = origin + def.positionOffset;
            float dens = Mathf.Clamp01(def.densidad);
            float hard = Mathf.Clamp01(def.dureza);

            switch (def.shape)
            {
                case TargetShape.Sphere:
                {
                    float radius = Mathf.Max(0.5f, def.size.x);
                    return world.FillSphere(pos, radius, def.material, dens, hard);
                }
                case TargetShape.Box:
                {
                    var sz = new Vector3Int(
                        Mathf.Max(1, Mathf.RoundToInt(def.size.x)),
                        Mathf.Max(1, Mathf.RoundToInt(def.size.y)),
                        Mathf.Max(1, Mathf.RoundToInt(def.size.z)));
                    return world.FillBox(pos, sz, def.material, dens, hard);
                }
                case TargetShape.Cylinder:
                {
                    float radius = Mathf.Max(0.5f, def.size.x);
                    float height = Mathf.Max(1f, def.size.y);
                    int axis = Mathf.Clamp(def.cylinderAxis, 0, 2);
                    return world.FillCylinder(pos, radius, height, axis, def.material, dens, hard);
                }
                case TargetShape.Composite:
                {
                    if (def.children == null || def.children.Length == 0) return 0;
                    int total = 0;
                    for (int i = 0; i < def.children.Length; i++)
                    {
                        var child = def.children[i];
                        if (child == null) continue;
                        total += SpawnInternal(world, child, pos, depth + 1);
                    }
                    return total;
                }
                default:
                    return 0;
            }
        }
    }
}
