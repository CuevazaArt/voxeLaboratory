// =====================================================================
//  TargetSpawner.cs
//  VoxelLab :: ChunkSystem
//
//  Propósito: utilidad estática para materializar un TargetDef en un
//  VoxelWorld a partir de un punto origen. Maneja shapes Sphere/Box/
//  Cylinder y Composite (multi-parte).
//
//  Invariantes:
//      - World y target no nulos (early return con warning).
//      - Tamaños / radios se clampan a límites de FillBox/FillSphere/FillCylinder.
//      - El conteo total de voxels llenados se reporta.
//
//  Dependencias: VoxelWorld, TargetDef, MaterialTable.
//
//  Uso:
//      int filled = TargetSpawner.Spawn(world, target, new Vector3(64,64,64));
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Core
{
    public static class TargetSpawner
    {
        /// <summary>Materializa un target en world. Devuelve cantidad de voxels llenados.</summary>
        public static int Spawn(VoxelWorld world, TargetDef target, Vector3 originWorld)
        {
            if (world == null || target == null) { Debug.LogWarning("TargetSpawner: parámetros nulos."); return 0; }

            int total = 0;
            switch (target.shape)
            {
                case TargetShape.Sphere:
                    total += world.FillSphere(originWorld, Mathf.Max(0.5f, target.radius),
                        target.materialId, target.densidad, target.dureza);
                    break;
                case TargetShape.Box:
                    total += world.FillBox(originWorld, ClampSize(target.size),
                        target.materialId, target.densidad, target.dureza);
                    break;
                case TargetShape.Cylinder:
                    total += world.FillCylinder(originWorld, target.radius, target.height, target.axis,
                        target.materialId, target.densidad, target.dureza);
                    break;
                case TargetShape.Composite:
                    if (target.parts != null)
                    {
                        for (int i = 0; i < target.parts.Length; i++)
                        {
                            total += SpawnPart(world, target.parts[i], originWorld);
                        }
                    }
                    break;
            }
            return total;
        }

        private static int SpawnPart(VoxelWorld world, TargetDef.CompositePart part, Vector3 origin)
        {
            Vector3 c = origin + part.offset;
            float dens = Mathf.Clamp01(part.densidad <= 0f ? 1f : part.densidad);
            float hard = Mathf.Clamp01(part.dureza);
            switch (part.shape)
            {
                case TargetShape.Sphere:
                    return world.FillSphere(c, Mathf.Max(0.5f, part.radius), part.materialId, dens, hard);
                case TargetShape.Box:
                    return world.FillBox(c, ClampSize(part.size), part.materialId, dens, hard);
                case TargetShape.Cylinder:
                    return world.FillCylinder(c, part.radius, part.height, part.axis, part.materialId, dens, hard);
                default:
                    return 0;
            }
        }

        private static Vector3Int ClampSize(Vector3Int s)
        {
            return new Vector3Int(Mathf.Max(1, s.x), Mathf.Max(1, s.y), Mathf.Max(1, s.z));
        }
    }
}
