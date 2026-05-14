// =====================================================================
//  VoxelTools.cs
//  VoxelLab :: Tools
//
//  Conjunto de herramientas de edición. Cada herramienta implementa
//  IVoxelTool; el ToolManager recibe inputs de mouse y delega.
//
//  Herramientas implementadas:
//      - DrillTool      : raymarch + carve continuo
//      - ExplosionTool  : carve esférico + impulso
//      - BrushTool      : pinta material en una esfera
//      - ErosionTool    : reduce densidad gradualmente (suaviza)
//      - CutTool        : corte plano (carve por semiespacio)
//
//  Dependencias: VoxelWorld, VolumetricPhysics (para empujar bodies).
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using VoxelLab.Core;
using VoxelLab.Physics;

namespace VoxelLab.Tools
{
    public interface IVoxelTool
    {
        string Name { get; }
        void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies);
    }

    [System.Serializable]
    public struct ToolParameters
    {
        public float radius;
        public float intensity;
        public byte material;
        public float maxDistance;
        public Vector3 planeNormal;     // sólo para CutTool
    }

    public class DrillTool : IVoxelTool
    {
        public string Name => "Drill";
        public void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies)
        {
            var hit = world.RaySample(ray.origin, ray.direction, p.maxDistance);
            if (!hit.hit) return;
            world.CarveSphere(hit.position, Mathf.Max(0.5f, p.radius), Mathf.Clamp01(p.intensity));
        }
    }

    public class ExplosionTool : IVoxelTool
    {
        public string Name => "Explosion";
        public void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies)
        {
            var hit = world.RaySample(ray.origin, ray.direction, p.maxDistance);
            if (!hit.hit) return;
            var res = world.Explosion(hit.position, p.radius, p.intensity);
            // Impulso a los rigidbodies en rango.
            if (bodies != null)
            {
                float r2 = p.radius * p.radius;
                foreach (var rb in bodies)
                {
                    if (rb == null) continue;
                    Vector3 d = rb.transform.position - res.center;
                    float dd = d.sqrMagnitude;
                    if (dd > r2 * 4f) continue;
                    float falloff = 1f - Mathf.Sqrt(dd) / (p.radius * 2f);
                    if (falloff <= 0f) continue;
                    rb.AddImpulse(d.normalized * res.force * falloff * 4f);
                }
            }
        }
    }

    public class BrushTool : IVoxelTool
    {
        public string Name => "Brush";
        public void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies)
        {
            var hit = world.RaySample(ray.origin, ray.direction, p.maxDistance);
            Vector3 c = hit.hit ? hit.position : ray.origin + ray.direction * Mathf.Min(20f, p.maxDistance);
            world.FillSphere(c, p.radius, p.material, Mathf.Clamp01(p.intensity));
        }
    }

    public class ErosionTool : IVoxelTool
    {
        public string Name => "Erosion";
        public void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies)
        {
            var hit = world.RaySample(ray.origin, ray.direction, p.maxDistance);
            if (!hit.hit) return;
            // Erosión: reduce densidad sin eliminar el material instantáneamente.
            int r = Mathf.CeilToInt(p.radius);
            for (int z = -r; z <= r; z++)
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                Vector3 q = hit.position + new Vector3(x, y, z);
                int xi = Mathf.FloorToInt(q.x), yi = Mathf.FloorToInt(q.y), zi = Mathf.FloorToInt(q.z);
                Vector3 cell = new Vector3(xi + 0.5f, yi + 0.5f, zi + 0.5f);
                float d = Vector3.Distance(cell, hit.position);
                if (d > p.radius) continue;
                var v = world.GetVoxel(xi, yi, zi);
                if (!v.solido) continue;
                v.densidad = Mathf.Max(0f, v.densidad - p.intensity * 0.05f * (1f - d / p.radius));
                if (v.densidad < 0.5f) v.material = 0;
                v.Recompute();
                world.SetVoxel(xi, yi, zi, v);
            }
        }
    }

    public class CutTool : IVoxelTool
    {
        public string Name => "Cut";
        public void Apply(VoxelWorld world, Ray ray, ToolParameters p, IList<VoxelRigidbody> bodies)
        {
            var hit = world.RaySample(ray.origin, ray.direction, p.maxDistance);
            if (!hit.hit) return;
            Vector3 n = p.planeNormal.sqrMagnitude < 1e-3f ? hit.normal : p.planeNormal.normalized;
            int r = Mathf.CeilToInt(p.radius);
            for (int z = -r; z <= r; z++)
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                Vector3 q = hit.position + new Vector3(x, y, z);
                int xi = Mathf.FloorToInt(q.x), yi = Mathf.FloorToInt(q.y), zi = Mathf.FloorToInt(q.z);
                Vector3 cell = new Vector3(xi + 0.5f, yi + 0.5f, zi + 0.5f);
                if (Vector3.Distance(cell, hit.position) > p.radius) continue;
                // Sólo eliminar lo que esté del lado positivo del plano.
                if (Vector3.Dot(cell - hit.position, n) <= 0) continue;
                world.SetVoxel(xi, yi, zi, Voxel.Empty);
            }
        }
    }
}
