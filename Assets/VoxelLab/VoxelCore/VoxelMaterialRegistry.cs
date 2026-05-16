// =====================================================================
//  VoxelMaterialRegistry.cs
//  VoxelLab :: VoxelCore
//
//  Propósito: contenedor autoral (ScriptableObject) de materiales voxel
//  que se aplica al MaterialTable estático en runtime para que el resto
//  del engine consuma ids canónicos sin acoplarse a Unity asset DB.
//
//  Invariantes:
//      - Slot 0 es siempre Air (se inyecta automáticamente si falta).
//      - Ids únicos: dos VoxelMaterialDef con mismo id => excepción.
//      - Apply() es idempotente: puede llamarse múltiples veces sin
//        corromper el estado previo.
//
//  Dependencias: VoxelMaterialDef, MaterialTable.
//
//  Uso:
//      var registry = Resources.Load<VoxelMaterialRegistry>("Configs/Default");
//      registry.Apply();
// =====================================================================
using System;
using UnityEngine;

namespace VoxelLab.Core
{
    [CreateAssetMenu(menuName = "VoxelLab/Material Registry", fileName = "MaterialRegistry")]
    public class VoxelMaterialRegistry : ScriptableObject
    {
        [Tooltip("Materiales autorales. Si falta el id 0 (Air), se inyecta automáticamente.")]
        public VoxelMaterialDef[] materials = Array.Empty<VoxelMaterialDef>();

        /// <summary>Aplica esta tabla al MaterialTable global. Lanza si hay ids duplicados.</summary>
        public void Apply()
        {
            MaterialTable.Apply(materials);
        }
    }
}
