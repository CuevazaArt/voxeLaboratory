// =====================================================================
//  VoxelMaterialDef.cs
//  VoxelLab :: VoxelCore
//
//  Propósito: definición de un material voxel como ScriptableObject.
//  Encapsula propiedades físicas y de comportamiento ante destrucción
//  para que distintos tipos de voxel reaccionen diferente al impacto.
//
//  Invariantes:
//      - id != 0 salvo para el material reservado "Air".
//      - todos los escalares normalizados se clampan en [0..1].
//      - debrisProfileKey puede ser null/empty (sin debris).
//      - VoxelCore no conoce PhysicsVolumetric: la resolución del
//        perfil de debris ocurre por nombre en otra capa.
//
//  Dependencias: UnityEngine (ScriptableObject, Color).
//
//  Uso:
//      var rock = ScriptableObject.CreateInstance<VoxelMaterialDef>();
//      rock.id = (byte)MaterialId.Rock; rock.densidadBase = 1f; ...
// =====================================================================
using UnityEngine;

namespace VoxelLab.Core
{
    /// <summary>Cómo reacciona un voxel al ser destruido.</summary>
    public enum DestructionMode : byte
    {
        /// <summary>Caída de fragmentos pequeños (defecto).</summary>
        Crumble = 0,
        /// <summary>Fragmentos veloces y dispersos (vidrio, hielo).</summary>
        Shatter = 1,
        /// <summary>Densidad/dureza decae progresivamente; pocos debris.</summary>
        Melt = 2,
        /// <summary>Debris explota radialmente con velocidad alta.</summary>
        Explode = 3,
        /// <summary>Sin debris (sublimación / vaporización limpia).</summary>
        Vaporize = 4,
    }

    /// <summary>
    /// Definición autoral de un material voxel. Reemplaza/extiende a
    /// <see cref="MaterialDef"/> con datos físicos y de destrucción.
    /// </summary>
    [CreateAssetMenu(menuName = "VoxelLab/Material", fileName = "VoxelMaterial")]
    public class VoxelMaterialDef : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Id canónico del material (0 reservado para Air).")]
        public byte id = 1;
        public string displayName = "Material";
        public Color color = Color.gray;

        [Header("Físicas base")]
        [Range(0f, 1f)] public float densidadBase = 1f;
        [Range(0f, 1f)] public float durezaBase = 0.5f;
        [Range(0f, 1f)] public float restitution = 0.05f;
        [Range(0f, 1f)] public float friction = 0.3f;

        [Header("Destrucción")]
        public DestructionMode destructionMode = DestructionMode.Crumble;
        [Tooltip("0 = muy resistente al fracturado, 1 = se astilla con cualquier impacto.")]
        [Range(0f, 1f)] public float fragilidad = 0.3f;
        [Tooltip("Clave del perfil de debris asociado. Resuelta por DebrisSimulator.")]
        public string debrisProfileKey = "";
        [Tooltip("Radio extra (voxels) de daño residual tras destrucción (lava, ácido). 0 = ninguno.")]
        [Min(0f)] public float secondaryEffectRadius = 0f;

        /// <summary>Snapshot inmutable y compactado para consumo en runtime.</summary>
        public VoxelMaterialDescriptor ToDescriptor()
        {
            return new VoxelMaterialDescriptor(
                id: id,
                name: string.IsNullOrEmpty(displayName) ? "Material" : displayName,
                color: color,
                densidadBase: Mathf.Clamp01(densidadBase),
                durezaBase: Mathf.Clamp01(durezaBase),
                restitution: Mathf.Clamp01(restitution),
                friction: Mathf.Clamp01(friction),
                fragilidad: Mathf.Clamp01(fragilidad),
                destructionMode: destructionMode,
                debrisProfileKey: debrisProfileKey ?? string.Empty,
                secondaryEffectRadius: Mathf.Max(0f, secondaryEffectRadius));
        }
    }

    /// <summary>Descriptor inmutable derivado de <see cref="VoxelMaterialDef"/>.</summary>
    public readonly struct VoxelMaterialDescriptor
    {
        public readonly byte id;
        public readonly string name;
        public readonly Color color;
        public readonly float densidadBase;
        public readonly float durezaBase;
        public readonly float restitution;
        public readonly float friction;
        public readonly float fragilidad;
        public readonly DestructionMode destructionMode;
        public readonly string debrisProfileKey;
        public readonly float secondaryEffectRadius;

        public VoxelMaterialDescriptor(
            byte id, string name, Color color,
            float densidadBase, float durezaBase,
            float restitution, float friction,
            float fragilidad, DestructionMode destructionMode,
            string debrisProfileKey, float secondaryEffectRadius)
        {
            this.id = id;
            this.name = name;
            this.color = color;
            this.densidadBase = densidadBase;
            this.durezaBase = durezaBase;
            this.restitution = restitution;
            this.friction = friction;
            this.fragilidad = fragilidad;
            this.destructionMode = destructionMode;
            this.debrisProfileKey = debrisProfileKey ?? string.Empty;
            this.secondaryEffectRadius = secondaryEffectRadius;
        }
    }
}
