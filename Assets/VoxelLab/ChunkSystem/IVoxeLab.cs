using UnityEngine;

namespace VoxelLab.Core
{
    public interface IVoxeLab
    {
        VoxelWorld World { get; }
        float lodScale { get; set; }
        void RegenerateWorld();
        int ChunkCount { get; }
        int SolidVoxelEstimate { get; }
    }
}
