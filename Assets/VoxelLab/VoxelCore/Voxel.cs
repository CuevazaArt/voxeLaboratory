// =====================================================================
//  Voxel.cs
//  VoxelLab :: VoxelCore
//
//  Unidad mínima de información volumétrica.
//  El laboratorio trata cada voxel como DATO, no como un cubo:
//      - material : id de material (0 = aire)
//      - densidad : escalar [0..1] usado por meshing/raymarching
//      - dureza   : escalar [0..1] resistencia a herramientas
//      - solido   : flag rápido para colisiones / culling
//
//  Tamaño compactado: 8 bytes (1 + 1 + 1 + 1 padding + 4 float densidad/dureza
//  empaquetados como halfs) -> aquí mantenemos legibilidad sobre compactación.
//
//  Dependencias: ninguna (POD).
// =====================================================================
using System;

namespace VoxelLab.Core
{
    /// <summary>Voxel mínimo. Struct para evitar GC y empaquetar en arrays planos.</summary>
    [Serializable]
    public struct Voxel : IEquatable<Voxel>
    {
        /// <summary>Id de material (0 = aire/vacío).</summary>
        public byte material;
        /// <summary>Densidad normalizada [0..1]. >= 0.5 se considera sólido por defecto.</summary>
        public float densidad;
        /// <summary>Dureza [0..1]. Reduce el efecto de las herramientas.</summary>
        public float dureza;
        /// <summary>Flag rápido de solidez (cacheado por SetVoxel).</summary>
        public bool solido;

        public static readonly Voxel Empty = new Voxel { material = 0, densidad = 0f, dureza = 0f, solido = false };

        public Voxel(byte material, float densidad, float dureza)
        {
            this.material = material;
            this.densidad = densidad;
            this.dureza = dureza;
            this.solido = densidad >= 0.5f && material != 0;
        }

        /// <summary>Recalcula el flag <see cref="solido"/> a partir de los datos actuales.</summary>
        public void Recompute()
        {
            solido = densidad >= 0.5f && material != 0;
        }

        public bool Equals(Voxel other) =>
            material == other.material &&
            densidad == other.densidad &&
            dureza == other.dureza &&
            solido == other.solido;

        public override bool Equals(object obj) => obj is Voxel v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(material, densidad, dureza, solido);
        public override string ToString() => $"Voxel(mat={material}, d={densidad:0.00}, h={dureza:0.00}, s={solido})";
    }
}
