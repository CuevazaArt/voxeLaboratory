// =====================================================================
//  TargetDef.cs
//  VoxelLab :: ChunkSystem
//
//  Propósito: definición autoral (ScriptableObject) de un "blanco" de
//  pruebas: una construcción voxel parametrizada por forma, tamaño y
//  material principal. Pensado para iteración rápida en el sandbox de
//  ballistics y experimentación de destrucción.
//
//  Invariantes:
//      - shape válido en TargetShape.
//      - size componentes >= 1 (clamp en spawner).
//      - radius > 0 si aplica (sphere/cylinder).
//      - children sólo se aplican si shape == Composite.
//      - materialId puede ser cualquiera registrado en MaterialTable.
//
//  Dependencias: UnityEngine, VoxelLab.Core (sólo MaterialId).
//
//  Uso:
//      var t = ScriptableObject.CreateInstance<TargetDef>();
//      t.shape = TargetShape.Box; t.size = new Vector3Int(8,8,8);
//      TargetSpawner.Spawn(world, t, originVoxel);
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Core
{
    public enum TargetShape : byte
    {
        Sphere = 0,
        Box = 1,
        Cylinder = 2,
        Composite = 3,
    }

    [CreateAssetMenu(menuName = "VoxelLab/Target", fileName = "Target")]
    public class TargetDef : ScriptableObject
    {
        [Header("Identidad")]
        public string displayName = "Target";
        [Tooltip("Material id registrado en MaterialTable.")]
        public byte materialId = (byte)MaterialId.Rock;

        [Header("Forma")]
        public TargetShape shape = TargetShape.Box;
        [Tooltip("Densidad inicial (0..1).")]
        [Range(0f, 1f)] public float densidad = 1f;
        [Tooltip("Dureza inicial (0..1). Mayor = más resistente a la destrucción.")]
        [Range(0f, 1f)] public float dureza = 0.5f;

        [Header("Box / Composite child size")]
        public Vector3Int size = new Vector3Int(8, 8, 8);

        [Header("Sphere / Cylinder")]
        [Min(0.5f)] public float radius = 4f;
        [Header("Cylinder only")]
        [Min(1f)] public float height = 8f;
        [Range(0, 2)] public int axis = 1; // 0=X 1=Y 2=Z

        [Header("Composite (sub-targets relativos)")]
        public CompositePart[] parts;

        [System.Serializable]
        public struct CompositePart
        {
            public TargetShape shape;
            public byte materialId;
            public Vector3 offset;
            public Vector3Int size;
            public float radius;
            public float height;
            public int axis;
            [Range(0f, 1f)] public float densidad;
            [Range(0f, 1f)] public float dureza;
        }
    }
}
