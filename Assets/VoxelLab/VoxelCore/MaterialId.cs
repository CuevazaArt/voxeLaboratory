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
        Sand    = 8,
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

    /// <summary>
    /// Tabla central de materiales. Indexable por byte id. Soporta
    /// reemplazo en runtime via <see cref="VoxelMaterialRegistry"/>.
    /// </summary>
    public static class MaterialTable
    {
        // Defaults legacy (compatibilidad): se preservan si nadie aplica un registry.
        private static readonly MaterialDef[] _defaults =
        {
            new MaterialDef(MaterialId.Air,     "Air",     new Color(0,0,0,0),                0.0f, 0.0f),
            new MaterialDef(MaterialId.Rock,    "Rock",    new Color(0.45f,0.45f,0.45f),      0.7f, 1.0f),
            new MaterialDef(MaterialId.Dirt,    "Dirt",    new Color(0.42f,0.27f,0.13f),      0.3f, 0.9f),
            new MaterialDef(MaterialId.Iron,    "Iron",    new Color(0.65f,0.55f,0.45f),      0.85f,1.0f),
            new MaterialDef(MaterialId.Gold,    "Gold",    new Color(1.0f,0.84f,0.2f),        0.6f, 1.0f),
            new MaterialDef(MaterialId.Crystal, "Crystal", new Color(0.6f,0.85f,1.0f),        0.5f, 1.0f),
            new MaterialDef(MaterialId.Lava,    "Lava",    new Color(1.0f,0.35f,0.05f),       0.1f, 0.8f),
            new MaterialDef(MaterialId.Ice,     "Ice",     new Color(0.75f,0.9f,1.0f),        0.4f, 1.0f),
            new MaterialDef(MaterialId.Sand,    "Sand",    new Color(0.93f,0.85f,0.55f),      0.25f,0.95f),
        };

        // Tabla viva (mutable). Inicialmente apunta a los defaults.
        private static MaterialDef[] _all;
        private static VoxelMaterialDescriptor[] _extended;

        static MaterialTable()
        {
            _all = (MaterialDef[])_defaults.Clone();
            _extended = BuildDefaultExtended(_all);
        }

        /// <summary>Snapshot del array actual (no mutar; se reemplaza por Apply).</summary>
        public static MaterialDef[] All => _all;

        public static MaterialDef Get(byte id)
        {
            if (id >= _all.Length) return _all[0];
            return _all[id];
        }

        public static MaterialDef Get(MaterialId id) => Get((byte)id);
        public static int Count => _all.Length;

        /// <summary>Descriptor extendido (propiedades de destrucción/físicas).</summary>
        public static VoxelMaterialDescriptor GetExtended(byte id)
        {
            if (_extended == null || id >= _extended.Length) return _extended[0];
            return _extended[id];
        }

        public static VoxelMaterialDescriptor GetExtended(MaterialId id) => GetExtended((byte)id);

        /// <summary>Resetea la tabla a los defaults legacy. Útil para tests.</summary>
        public static void ResetToDefaults()
        {
            _all = (MaterialDef[])_defaults.Clone();
            _extended = BuildDefaultExtended(_all);
        }

        /// <summary>
        /// Aplica un set autoral. Garantiza slot 0 = Air (lo inyecta si falta) y
        /// rechaza ids duplicados con excepción descriptiva.
        /// </summary>
        public static void Apply(VoxelMaterialDef[] defs)
        {
            if (defs == null || defs.Length == 0)
            {
                ResetToDefaults();
                return;
            }

            // Detectar tamaño máximo y validar duplicados.
            int maxId = 0;
            bool hasAir = false;
            var seen = new System.Collections.Generic.HashSet<byte>();
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (!seen.Add(d.id))
                    throw new System.InvalidOperationException(
                        $"VoxelMaterialRegistry: id duplicado {d.id} ('{d.displayName}'). Ids deben ser únicos.");
                if (d.id == 0) hasAir = true;
                if (d.id > maxId) maxId = d.id;
            }

            int len = maxId + 1;
            var legacy = new MaterialDef[len];
            var ext = new VoxelMaterialDescriptor[len];

            // Air por defecto en slot 0.
            legacy[0] = new MaterialDef(MaterialId.Air, "Air", new Color(0, 0, 0, 0), 0f, 0f);
            ext[0] = new VoxelMaterialDescriptor(0, "Air", new Color(0, 0, 0, 0),
                0f, 0f, 0f, 0f, 0f, DestructionMode.Vaporize, string.Empty, 0f);

            foreach (var d in defs)
            {
                if (d == null) continue;
                var desc = d.ToDescriptor();
                legacy[d.id] = new MaterialDef(
                    (MaterialId)d.id, desc.name, desc.color, desc.durezaBase, desc.densidadBase);
                ext[d.id] = desc;
            }

            // Slots intermedios sin material -> placeholder seguro.
            for (int i = 1; i < len; i++)
            {
                if (string.IsNullOrEmpty(legacy[i].name))
                {
                    legacy[i] = new MaterialDef((MaterialId)i, $"Unknown_{i}", Color.magenta, 0.5f, 0.5f);
                    ext[i] = new VoxelMaterialDescriptor((byte)i, $"Unknown_{i}", Color.magenta,
                        0.5f, 0.5f, 0f, 0.3f, 0.3f, DestructionMode.Crumble, string.Empty, 0f);
                }
            }

            _all = legacy;
            _extended = ext;

            if (!hasAir)
            {
                // Air ya quedó en slot 0; nada extra que hacer.
            }
        }

        private static VoxelMaterialDescriptor[] BuildDefaultExtended(MaterialDef[] basis)
        {
            var arr = new VoxelMaterialDescriptor[basis.Length];
            for (int i = 0; i < basis.Length; i++)
            {
                var b = basis[i];
                DestructionMode mode = i switch
                {
                    (int)MaterialId.Crystal => DestructionMode.Shatter,
                    (int)MaterialId.Ice => DestructionMode.Shatter,
                    (int)MaterialId.Lava => DestructionMode.Melt,
                    (int)MaterialId.Air => DestructionMode.Vaporize,
                    _ => DestructionMode.Crumble,
                };
                float frag = i switch
                {
                    (int)MaterialId.Crystal => 0.85f,
                    (int)MaterialId.Ice => 0.7f,
                    (int)MaterialId.Iron => 0.15f,
                    (int)MaterialId.Rock => 0.35f,
                    _ => 0.4f,
                };
                arr[i] = new VoxelMaterialDescriptor(
                    id: (byte)i,
                    name: b.name,
                    color: b.color,
                    densidadBase: b.baseDensidad,
                    durezaBase: b.baseDureza,
                    restitution: 0.05f,
                    friction: 0.3f,
                    fragilidad: frag,
                    destructionMode: mode,
                    debrisProfileKey: string.Empty,
                    secondaryEffectRadius: 0f);
            }
            return arr;
        }
    }
}
