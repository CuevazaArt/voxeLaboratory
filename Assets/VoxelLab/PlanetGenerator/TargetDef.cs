// =====================================================================
//  TargetDef.cs
//  VoxelLab :: Planet
//
//  Propósito: definición autoral (ScriptableObject) de un objetivo
//  destruible. Encapsula forma, tamaño, material y propiedades para
//  que el laboratorio pueda spawnear targets diversos en runtime.
//
//  Invariantes:
//      - shape != Composite implica children sin uso.
//      - shape == Composite implica children no nulos: cada hijo se
//        spawnea relativo al origen (positionOffset).
//      - size se interpreta según shape:
//          Sphere   -> size.x = radio (voxels).
//          Box      -> size = (sx, sy, sz) en voxels.
//          Cylinder -> size.x = radio, size.y = altura, axis = 0/1/2.
//      - densidad y dureza en [0..1].
//
//  Dependencias: UnityEngine. Sin acoplamiento a Physics/LabViews.
//
//  Uso:
//      var t = ScriptableObject.CreateInstance<TargetDef>();
//      t.shape = TargetShape.Box;
//      t.size = new Vector3(8, 4, 8);
//      t.material = (byte)MaterialId.Rock;
// =====================================================================
using UnityEngine;
using VoxelLab.Core;

namespace VoxelLab.Planet
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

        [Header("Forma")]
        public TargetShape shape = TargetShape.Sphere;
        [Tooltip("Sphere: x=radio. Box: (sx,sy,sz). Cylinder: x=radio, y=altura.")]
        public Vector3 size = new Vector3(4f, 4f, 4f);
        [Tooltip("Cylinder: 0=X, 1=Y, 2=Z. Ignorado en otras formas.")]
        [Range(0, 2)] public int cylinderAxis = 1;

        [Header("Posición relativa")]
        public Vector3 positionOffset = Vector3.zero;

        [Header("Material")]
        public byte material = (byte)MaterialId.Rock;
        [Range(0f, 1f)] public float densidad = 1f;
        [Range(0f, 1f)] public float dureza = 0.5f;

        [Header("Composite")]
        [Tooltip("Sub-targets aplicados relativos a este origen. Solo si shape=Composite.")]
        public TargetDef[] children;
    }
}
