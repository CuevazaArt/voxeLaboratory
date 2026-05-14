// =====================================================================
//  MaterialId.cs
//  VoxelLab :: VoxelCore
//
//  Catálogo de materiales del laboratorio. Mantener corto y plano: el
//  id viaja como byte dentro del Voxel para minimizar el tamaño.
//  Cada material declara color/dureza/densidad por defecto consultables
//  por shaders y herramientas.
//
//  Dependencias: UnityEngine.Color (solo tipo de datos).
// =====================================================================
using UnityEngine;

namespace VoxelLab.Core
{
    /// <summary>Ids canónicos de material. 0 siempre es aire.</summary>
    public enum MaterialId : byte
    {
        Air     = 0,
        Rock    = 1,
        Dirt    = 2,
        Iron    = 3,
        Gold    = 4,
        Crystal = 5,
        Lava    = 6,
        Ice     = 7,
    }

    /// <summary>Datos de presentación/dureza por material.</summary>
    public readonly struct MaterialDef
    {
        public readonly MaterialId id;
        public readonly string name;
        public readonly Color color;
        public readonly float baseDureza;
        public readonly float baseDensidad;

        public MaterialDef(MaterialId id, string name, Color color, float dureza, float densidad)
        {
            this.id = id;
            this.name = name;
            this.color = color;
            this.baseDureza = dureza;
            this.baseDensidad = densidad;
        }
    }

    /// <summary>Tabla central de materiales. Indexable por byte id.</summary>
    public static class MaterialTable
    {
        public static readonly MaterialDef[] All =
        {
            new MaterialDef(MaterialId.Air,     "Air",     new Color(0,0,0,0),                0.0f, 0.0f),
            new MaterialDef(MaterialId.Rock,    "Rock",    new Color(0.45f,0.45f,0.45f),      0.7f, 1.0f),
            new MaterialDef(MaterialId.Dirt,    "Dirt",    new Color(0.42f,0.27f,0.13f),      0.3f, 0.9f),
            new MaterialDef(MaterialId.Iron,    "Iron",    new Color(0.65f,0.55f,0.45f),      0.85f,1.0f),
            new MaterialDef(MaterialId.Gold,    "Gold",    new Color(1.0f,0.84f,0.2f),        0.6f, 1.0f),
            new MaterialDef(MaterialId.Crystal, "Crystal", new Color(0.6f,0.85f,1.0f),        0.5f, 1.0f),
            new MaterialDef(MaterialId.Lava,    "Lava",    new Color(1.0f,0.35f,0.05f),       0.1f, 0.8f),
            new MaterialDef(MaterialId.Ice,     "Ice",     new Color(0.75f,0.9f,1.0f),        0.4f, 1.0f),
        };

        public static MaterialDef Get(byte id)
        {
            if (id >= All.Length) return All[0];
            return All[id];
        }

        public static MaterialDef Get(MaterialId id) => Get((byte)id);
        public static int Count => All.Length;
    }
}
