// =====================================================================
//  OverlayController.cs
//  VoxelLab :: Overlays
//
//  Activa/desactiva visualizaciones técnicas:
//      - Wireframe
//      - Densidad (color por densidad)
//      - Materiales (color por material)
//      - Bordes de chunks
//      - Bordes de octree
//
//  Implementación: cada overlay sustituye temporalmente el material o
//  inyecta un componente Gizmo-like (OnDrawGizmos en debug). Los
//  overlays de chunks/octree se dibujan con GL.Lines para no requerir
//  shaders custom mientras se prototipa.
//
//  Dependencias: VoxelWorld.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Meshing;
using VoxelLab.SVO;

namespace VoxelLab.Overlays
{
    [System.Flags]
    public enum OverlayFlags
    {
        None = 0,
        Wireframe   = 1 << 0,
        Density     = 1 << 1,
        Material    = 1 << 2,
        ChunkBounds = 1 << 3,
        OctreeNodes = 1 << 4,
    }

    public class OverlayController : MonoBehaviour
    {
        public OverlayFlags flags = OverlayFlags.None;

        public Material defaultMat;     // Material PBR estándar para los chunks.
        public Material wireframeMat;   // Material que pinta wireframe (puede ser unlit).
        public Material densityMat;     // Pinta por densidad (vertex color modulado).
        public Material materialOverlayMat; // Pinta por id de material.

        public VoxeLab.Core.VoxelWorld World { get; set; }
        public ChunkRenderer[] Renderers { get; set; }
        public Camera DebugCamera { get; set; }

        private static Material _lineMat;
        private static Material LineMat
        {
            get
            {
                if (_lineMat != null) return _lineMat;
                var s = Shader.Find("Hidden/Internal-Colored");
                if (s == null) return null;
                _lineMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
                _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMat.SetInt("_ZWrite", 0);
                return _lineMat;
            }
        }

        public void Set(OverlayFlags f, bool on)
        {
            if (on) flags |= f; else flags &= ~f;
            ApplyMaterials();
        }

        public void Toggle(OverlayFlags f) => Set(f, (flags & f) == 0);

        public void ApplyMaterials()
        {
            if (Renderers == null) return;
            Material chosen = defaultMat;
            if ((flags & OverlayFlags.Wireframe) != 0 && wireframeMat != null) chosen = wireframeMat;
            else if ((flags & OverlayFlags.Density) != 0 && densityMat != null) chosen = densityMat;
            else if ((flags & OverlayFlags.Material) != 0 && materialOverlayMat != null) chosen = materialOverlayMat;
            foreach (var r in Renderers)
                if (r != null) r.GetComponent<MeshRenderer>().sharedMaterial = chosen;
        }

        private void OnRenderObject()
        {
            if (LineMat == null) return;
            if ((flags & OverlayFlags.ChunkBounds) != 0 && World != null)
                DrawChunkBounds();
            if ((flags & OverlayFlags.OctreeNodes) != 0 && World != null)
                DrawOctree(World.octree.root);
        }

        private void DrawChunkBounds()
        {
            LineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.2f, 1f, 0.2f, 0.6f));
            foreach (var c in World.AllChunks())
            {
                if (c.empty) continue;
                DrawAabb(c.worldBounds);
            }
            GL.End();
            GL.PopMatrix();
        }

        private void DrawOctree(SVONode node)
        {
            if (node == null) return;
            LineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            DrawNode(node);
            GL.End();
            GL.PopMatrix();
        }

        private void DrawNode(SVONode n)
        {
            if (n == null) return;
            if (n.anySolid)
            {
                GL.Color(new Color(1f, 0.5f, 0f, 0.4f));
                var b = new Bounds(
                    new Vector3(n.origin.x + n.size * 0.5f, n.origin.y + n.size * 0.5f, n.origin.z + n.size * 0.5f),
                    new Vector3(n.size, n.size, n.size));
                DrawAabb(b);
            }
            if (!n.isLeaf && n.children != null)
                for (int i = 0; i < 8; i++) DrawNode(n.children[i]);
        }

        private void DrawAabb(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            Vector3[] c = {
                new Vector3(mn.x,mn.y,mn.z), new Vector3(mx.x,mn.y,mn.z),
                new Vector3(mx.x,mn.y,mx.z), new Vector3(mn.x,mn.y,mx.z),
                new Vector3(mn.x,mx.y,mn.z), new Vector3(mx.x,mx.y,mn.z),
                new Vector3(mx.x,mx.y,mx.z), new Vector3(mn.x,mx.y,mx.z),
            };
            int[,] e = {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7},
            };
            for (int i = 0; i < 12; i++)
            {
                GL.Vertex(c[e[i,0]]);
                GL.Vertex(c[e[i,1]]);
            }
        }
    }
}
