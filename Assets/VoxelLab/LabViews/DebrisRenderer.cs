// =====================================================================
//  DebrisRenderer.cs
//  VoxelLab :: LabViews
//
//  Propósito: render por instancing (Graphics.DrawMeshInstanced) de las
//  partículas de un DebrisSimulator. Agrupa por perfil y respeta el
//  cap de 1023 instancias por draw call de Unity.
//
//  Invariantes:
//      - El simulador y sus perfiles deben estar inicializados antes
//        del primer LateUpdate.
//      - Si un perfil no tiene mesh o material, la instancia se omite.
//      - Si useMaterialColor=true, intenta tomar color del MaterialTable
//        (registro extendido) y pasarlo por MaterialPropertyBlock "_Color".
//      - El renderer no posee la simulación; sólo lee.
//
//  Dependencias: DebrisSimulator, DebrisProfileDef, MaterialTable.
//
//  Uso:
//      var renderer = gameObject.AddComponent<DebrisRenderer>();
//      renderer.Simulator = sim;
// =====================================================================
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelLab.Core;
using VoxelLab.Physics;

namespace VoxelLab.LabViews
{
    public class DebrisRenderer : MonoBehaviour
    {
        public const int BATCH_SIZE = 1023;

        [Header("Datos")]
        [Tooltip("Asignar tras crear el simulador en runtime.")]
        public bool autoFindSimulator = false;

        [Header("Sombras / capas")]
        public ShadowCastingMode shadowCasting = ShadowCastingMode.Off;
        public bool receiveShadows = false;
        public int layer = 0;

        public DebrisSimulator Simulator { get; set; }

        // Reuso de buffers por perfil para no allocar cada frame.
        private readonly Dictionary<int, List<Matrix4x4>> _matricesByProfile = new Dictionary<int, List<Matrix4x4>>();
        private readonly Dictionary<int, List<Vector4>> _colorsByProfile = new Dictionary<int, List<Vector4>>();
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (Simulator == null) return;
            var profiles = Simulator.Profiles;
            if (profiles == null || profiles.Count == 0) return;

            ResetBuffers(profiles.Count);

            var instances = Simulator.Instances;
            if (!instances.IsCreated) return;

            int n = instances.Length;
            for (int i = 0; i < n; i++)
            {
                var inst = instances[i];
                if (inst.alive == 0) continue;
                int profIdx = inst.profileIndex;
                if (profIdx < 0 || profIdx >= profiles.Count) continue;
                var profile = profiles[profIdx];
                if (profile == null || profile.mesh == null || profile.material == null) continue;

                if (!_matricesByProfile.TryGetValue(profIdx, out var matrices))
                {
                    matrices = new List<Matrix4x4>(BATCH_SIZE);
                    _matricesByProfile[profIdx] = matrices;
                }
                if (!_colorsByProfile.TryGetValue(profIdx, out var colors))
                {
                    colors = new List<Vector4>(BATCH_SIZE);
                    _colorsByProfile[profIdx] = colors;
                }

                Vector3 pos = new Vector3(inst.position.x, inst.position.y, inst.position.z);
                float s = Mathf.Max(0.001f, inst.scale);
                matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(s, s, s)));

                Color c = profile.useMaterialColor
                    ? MaterialTable.GetExtended(inst.materialId).color
                    : profile.colorTint;
                colors.Add(new Vector4(c.r, c.g, c.b, c.a));
            }

            DrawAll(profiles);
        }

        private void ResetBuffers(int profileCount)
        {
            foreach (var kv in _matricesByProfile) kv.Value.Clear();
            foreach (var kv in _colorsByProfile) kv.Value.Clear();
        }

        private void DrawAll(IReadOnlyList<DebrisProfileDef> profiles)
        {
            foreach (var kv in _matricesByProfile)
            {
                int profIdx = kv.Key;
                var matrices = kv.Value;
                if (matrices.Count == 0) continue;
                if (profIdx < 0 || profIdx >= profiles.Count) continue;
                var profile = profiles[profIdx];
                if (profile == null || profile.mesh == null || profile.material == null) continue;

                var colors = _colorsByProfile[profIdx];
                int total = matrices.Count;
                int offset = 0;
                while (offset < total)
                {
                    int batch = Mathf.Min(BATCH_SIZE, total - offset);
                    Matrix4x4[] mArr = new Matrix4x4[batch];
                    Vector4[] cArr = new Vector4[batch];
                    matrices.CopyTo(offset, mArr, 0, batch);
                    colors.CopyTo(offset, cArr, 0, batch);

                    _mpb.Clear();
                    if (profile.material.HasProperty("_Color"))
                        _mpb.SetVectorArray("_Color", cArr);
                    if (profile.material.HasProperty("_BaseColor"))
                        _mpb.SetVectorArray("_BaseColor", cArr);

                    Graphics.DrawMeshInstanced(
                        profile.mesh, 0, profile.material,
                        mArr, batch, _mpb,
                        shadowCasting, receiveShadows, layer);
                    offset += batch;
                }
            }
        }
    }
}
