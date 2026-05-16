// =====================================================================
//  ProjectileTypeDef.cs
//  VoxelLab :: Physics
//
//  Propósito: definición autoral (ScriptableObject) de un tipo de
//  proyectil. Encapsula magnitudes balísticas + modo de impacto +
//  parámetros por modo, para que el ProjectileLauncher pueda lanzar
//  proyectiles polimórficos sin código duplicado.
//
//  Invariantes:
//      - mass, radius, initialSpeed > 0 (saneados al consumir).
//      - drag, lifetimeSeconds >= 0.
//      - Cluster.subType referencia otro ProjectileTypeDef (puede ser null).
//      - El consumidor (ProjectileLauncher) controla la profundidad de
//        recursión para Cluster, no este SO.
//
//  Dependencias: UnityEngine. Sin referencias cross-asmdef al runtime
//  de visualización/UI.
//
//  Uso:
//      var t = ScriptableObject.CreateInstance<ProjectileTypeDef>();
//      t.impactMode = ProjectileImpactMode.Explosive;
//      t.explosive.blastRadius = 4f;
// =====================================================================
using UnityEngine;

namespace VoxelLab.Physics
{
    public enum ProjectileImpactMode : byte
    {
        /// <summary>Cráter pequeño escalado por energía cinética.</summary>
        Kinetic = 0,
        /// <summary>Explosión esférica con radio y fuerza fijos.</summary>
        Explosive = 1,
        /// <summary>Spawnea N sub-proyectiles en cono al impactar.</summary>
        Cluster = 2,
        /// <summary>Drena densidad/dureza en un radio durante varios segundos.</summary>
        Plasma = 3,
        /// <summary>Tunela rectilíneamente carvando un cilindro tras el impacto.</summary>
        Penetrator = 4,
    }

    [System.Serializable]
    public struct KineticImpactParams
    {
        public float craterRadiusBase;
        public float radiusPerSqrtEnergy;
        public float forceScale;
        public float maxRadius;

        public static KineticImpactParams Default => new KineticImpactParams
        {
            craterRadiusBase = 0.4f,
            radiusPerSqrtEnergy = 0.06f,
            forceScale = 0.05f,
            maxRadius = 6f,
        };
    }

    [System.Serializable]
    public struct ExplosiveImpactParams
    {
        public float blastRadius;
        public float blastForce;
        public bool secondaryFires;

        public static ExplosiveImpactParams Default => new ExplosiveImpactParams
        {
            blastRadius = 4f,
            blastForce = 8f,
            secondaryFires = false,
        };
    }

    [System.Serializable]
    public struct ClusterImpactParams
    {
        public ProjectileTypeDef subProjectileType;
        public int count;
        [Range(0f, 180f)] public float spreadAngleDeg;
        public float subSpeedScale;

        public static ClusterImpactParams Default => new ClusterImpactParams
        {
            subProjectileType = null,
            count = 6,
            spreadAngleDeg = 45f,
            subSpeedScale = 0.6f,
        };
    }

    [System.Serializable]
    public struct PlasmaImpactParams
    {
        public float meltRadius;
        public float densityDrainPerSec;
        public float lingerSeconds;

        public static PlasmaImpactParams Default => new PlasmaImpactParams
        {
            meltRadius = 2.5f,
            densityDrainPerSec = 0.6f,
            lingerSeconds = 1.2f,
        };
    }

    [System.Serializable]
    public struct PenetratorImpactParams
    {
        public float tunnelDepth;
        public float tunnelRadius;
        public float secondaryForce;

        public static PenetratorImpactParams Default => new PenetratorImpactParams
        {
            tunnelDepth = 8f,
            tunnelRadius = 0.6f,
            secondaryForce = 1.5f,
        };
    }

    [CreateAssetMenu(menuName = "VoxelLab/Projectile Type", fileName = "ProjectileType")]
    public class ProjectileTypeDef : ScriptableObject
    {
        [Header("Identidad")]
        public string displayName = "Projectile";
        public Color tracerColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("Balística")]
        [Min(0.0001f)] public float mass = 1f;
        [Min(0.01f)] public float radius = 0.25f;
        [Min(0f)] public float initialSpeed = 80f;
        [Min(0f)] public float drag = 0f;
        [Min(0.1f)] public float lifetimeSeconds = 10f;

        [Header("Impacto")]
        public ProjectileImpactMode impactMode = ProjectileImpactMode.Kinetic;
        public KineticImpactParams kinetic = KineticImpactParams.Default;
        public ExplosiveImpactParams explosive = ExplosiveImpactParams.Default;
        public ClusterImpactParams cluster = ClusterImpactParams.Default;
        public PlasmaImpactParams plasma = PlasmaImpactParams.Default;
        public PenetratorImpactParams penetrator = PenetratorImpactParams.Default;

        [Header("Render")]
        public Mesh mesh;
        public Material material;
    }
}
