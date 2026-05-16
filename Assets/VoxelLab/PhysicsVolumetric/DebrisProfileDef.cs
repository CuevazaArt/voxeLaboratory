// =====================================================================
//  DebrisProfileDef.cs
//  VoxelLab :: Physics
//
//  Propósito: definición autoral (ScriptableObject) de un perfil de
//  partículas/debris que se generan al destruir voxels. Cada perfil
//  controla apariencia, vida y dispersión.
//
//  Invariantes:
//      - sampleFraction en [0..1]: porcentaje de muestras de destrucción
//        convertidas en debris reales.
//      - maxConcurrent >= 0; el simulador hace clamp al cap global.
//      - lifetimeSeconds > 0.
//      - mesh y material pueden ser null: el simulador usa fallbacks.
//      - profileKey identifica este perfil para resolución cruzada
//        desde VoxelMaterialDef.debrisProfileKey.
//
//  Dependencias: UnityEngine.
//
//  Uso:
//      var profile = ScriptableObject.CreateInstance<DebrisProfileDef>();
//      profile.profileKey = "GlassShards"; profile.lifetimeSeconds = 2f;
// =====================================================================
using UnityEngine;

namespace VoxelLab.Physics
{
    [CreateAssetMenu(menuName = "VoxelLab/Debris Profile", fileName = "DebrisProfile")]
    public class DebrisProfileDef : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Clave única para resolución cruzada desde VoxelMaterialDef.")]
        public string profileKey = "Default";

        [Header("Apariencia")]
        public Mesh mesh;
        public Material material;
        public Color colorTint = Color.white;
        [Tooltip("Si true, el color del debris se toma del material voxel destruido.")]
        public bool useMaterialColor = true;
        [Tooltip("Escala visual de cada partícula (multiplicador del tamaño voxel).")]
        [Range(0.05f, 2f)] public float scale = 0.35f;

        [Header("Cinemática inicial")]
        [Tooltip("Velocidad escalar inicial mínima (voxels/s).")]
        [Min(0f)] public float initialSpeedMin = 2f;
        [Tooltip("Velocidad escalar inicial máxima (voxels/s).")]
        [Min(0f)] public float initialSpeedMax = 8f;
        [Tooltip("Apertura del cono de dispersión (grados).")]
        [Range(0f, 180f)] public float spreadAngleDeg = 90f;
        [Tooltip("Multiplicador de la gravedad global aplicado a este perfil.")]
        public float gravityScale = 1f;

        [Header("Físicas")]
        [Min(0.0001f)] public float mass = 0.1f;
        [Range(0f, 5f)] public float drag = 0.4f;
        [Range(0f, 1f)] public float restitution = 0.1f;

        [Header("Vida")]
        [Min(0.05f)] public float lifetimeSeconds = 2f;

        [Header("Spawn / capacidad")]
        [Tooltip("Fracción de muestras de destrucción que se convierten en debris (0..1).")]
        [Range(0f, 1f)] public float sampleFraction = 0.5f;
        [Tooltip("Máximo concurrente para este perfil (0 = sin límite específico, sólo el cap global).")]
        [Min(0)] public int maxConcurrent = 1024;
    }
}
