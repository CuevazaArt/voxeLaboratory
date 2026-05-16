// =====================================================================
//  SvoGizmoDrawer.cs
//  SvoGizmoDrawer.cs
//  VoxelLab :: EditorTools
//
//  Pinta gizmos de las hojas del SparseVoxelOctree del VoxelWorld
//  activo. Color HSV por profundidad. Editor-only.
//
//  Para usarlo: anade el componente SvoGizmoTag a la instancia de
//  VoxeLab en escena.
// =====================================================================
using UnityEngine;
using VoxelLab.Boot;
using VoxelLab.SVO;

namespace VoxelLab.EditorTools
{
    /// <summary>Tag MonoBehaviour para activar gizmos SVO solo donde se desea.</summary>
    [DisallowMultipleComponent]
    public sealed class SvoGizmoTag : MonoBehaviour
    {
        public bool drawLeavesOnly = true;
        [Range(0, 16)] public int maxDepth = 8;
        public bool wire = true;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SvoGizmoTag))]
    internal sealed class SvoGizmoTagEditor : UnityEditor.Editor { }

    internal static class SvoGizmoDrawer
    {
        [UnityEditor.DrawGizmo(UnityEditor.GizmoType.Selected | UnityEditor.GizmoType.NonSelected)]
        private static void Draw(SvoGizmoTag tag, UnityEditor.GizmoType gizmoType)
        {
            if (tag == null) return;
            var lab = tag.GetComponent<VoxeLab>();
            if (lab == null) lab = tag.GetComponentInParent<VoxeLab>();
            if (lab == null || lab.World == null) return;
            var octree = lab.World.octree;
            if (octree == null || octree.root == null) return;

            DrawNode(octree.root, tag);
        }

        private static void DrawNode(SVONode node, SvoGizmoTag tag)
        {
            if (node == null) return;
            if (node.depth > tag.maxDepth) return;

            if (!tag.drawLeavesOnly || node.isLeaf)
            {
                float hue = (node.depth * 0.13f) % 1f;
                var col = Color.HSVToRGB(hue, 0.7f, 0.95f);
                col.a = tag.wire ? 0.9f : 0.25f;
                Gizmos.color = col;
                var center = new Vector3(
                    node.origin.x + node.size * 0.5f,
                    node.origin.y + node.size * 0.5f,
                    node.origin.z + node.size * 0.5f);
                var size = new Vector3(node.size, node.size, node.size);
                if (tag.wire) Gizmos.DrawWireCube(center, size);
                else Gizmos.DrawCube(center, size);
            }

            if (!node.isLeaf && node.children != null)
            {
                for (int i = 0; i < node.children.Length; i++)
                    DrawNode(node.children[i], tag);
            }
        }
    }
#endif
}
