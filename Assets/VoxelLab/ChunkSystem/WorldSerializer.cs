// =====================================================================
//  WorldSerializer.cs
//  VoxelLab :: ChunkSystem
//
//  Persistencia binaria del VoxelWorld con codificacion RLE por chunk.
//
//  Formato (little-endian):
//      magic      : 4 bytes  = 'V','L','A','B'
//      version    : u16      (actual = 1)
//      chunkSize  : u16
//      chunkCount : u32
//      [por chunk]:
//          cx, cy, cz : i32 (coordenada de chunk)
//          runCount   : u32
//          [por run]:
//              runLength : u16  (1..chunkSize^3)
//              packed    : u32  (mat:8, dens:8, dur:8, solid:1, _:7)
//
//  Voxels se recorren en orden lineal por <c>VoxelChunk.Index</c>.
//
//  Dependencias: VoxelWorld, VoxelChunk, Voxel.
//
//  Invariantes:
//      - Magic + version se validan al cargar.
//      - chunkSize debe coincidir con el de <c>VoxelWorld</c> destino.
//      - Cada run tiene length >= 1 y la suma debe ser exactamente
//        chunkSize^3.
// =====================================================================
using System;
using System.IO;
using UnityEngine;

namespace VoxelLab.Core
{
    /// <summary>Codec binario RLE para VoxelWorld.</summary>
    public static class WorldSerializer
    {
        public const uint MAGIC = 0x42414C56u; // 'V''L''A''B' en little-endian
        public const ushort VERSION = 1;

        /// <summary>Serializa el mundo a un stream binario.</summary>
        public static void Save(VoxelWorld world, Stream destination)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            using (var bw = new BinaryWriter(destination, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write(MAGIC);
                bw.Write(VERSION);
                bw.Write((ushort)world.chunkSize);

                int chunkCount = 0;
                foreach (var _ in world.AllChunks()) chunkCount++;
                bw.Write((uint)chunkCount);

                foreach (var kv in world.chunks)
                {
                    var coord = kv.Key;
                    var chunk = kv.Value;
                    bw.Write(coord.x);
                    bw.Write(coord.y);
                    bw.Write(coord.z);
                    WriteChunkRle(bw, chunk);
                }
            }
        }

        /// <summary>Serializa el mundo a un array de bytes.</summary>
        public static byte[] SaveToBytes(VoxelWorld world)
        {
            using (var ms = new MemoryStream())
            {
                Save(world, ms);
                return ms.ToArray();
            }
        }

        /// <summary>Carga un mundo previamente serializado. Reemplaza los chunks existentes.</summary>
        public static void Load(VoxelWorld world, Stream source)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (source == null) throw new ArgumentNullException(nameof(source));

            using (var br = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                uint magic = br.ReadUInt32();
                if (magic != MAGIC)
                    throw new InvalidDataException($"WorldSerializer: magic invalido (0x{magic:X8})");

                ushort version = br.ReadUInt16();
                if (version != VERSION)
                    throw new InvalidDataException($"WorldSerializer: version no soportada ({version})");

                ushort chunkSize = br.ReadUInt16();
                if (chunkSize != world.chunkSize)
                    throw new InvalidDataException(
                        $"WorldSerializer: chunkSize del fichero ({chunkSize}) != world.chunkSize ({world.chunkSize})");

                uint chunkCount = br.ReadUInt32();

                world.ClearChunks();

                int volume = chunkSize * chunkSize * chunkSize;
                for (uint i = 0; i < chunkCount; i++)
                {
                    int cx = br.ReadInt32();
                    int cy = br.ReadInt32();
                    int cz = br.ReadInt32();
                    var coord = new Vector3Int(cx, cy, cz);
                    var origin = coord * world.chunkSize;
                    var chunk = new VoxelChunk(world.chunkSize, origin);
                    ReadChunkRle(br, chunk, volume);
                    world.RegisterChunk(coord, chunk);
                }
            }
        }

        /// <summary>Carga desde un byte array.</summary>
        public static void LoadFromBytes(VoxelWorld world, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var ms = new MemoryStream(data, writable: false))
                Load(world, ms);
        }

        /// <summary>Conveniencia: guarda a fichero, atomico via .tmp + replace.</summary>
        public static void SaveToFile(VoxelWorld world, string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path vacio", nameof(path));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
                Save(world, fs);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Conveniencia: carga desde fichero.</summary>
        public static void LoadFromFile(VoxelWorld world, string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("WorldSerializer: fichero no encontrado", path);
            using (var fs = File.OpenRead(path))
                Load(world, fs);
        }

        // ------------------------------------------------------------------
        //  Internals
        // ------------------------------------------------------------------

        private static void WriteChunkRle(BinaryWriter bw, VoxelChunk chunk)
        {
            int volume = chunk.size * chunk.size * chunk.size;

            // Primer pase: contar runs.
            uint runs = 0;
            {
                uint cur = Pack(chunk.voxels[0]);
                int len = 1;
                for (int i = 1; i < volume; i++)
                {
                    uint p = Pack(chunk.voxels[i]);
                    if (p == cur && len < ushort.MaxValue) { len++; continue; }
                    runs++;
                    cur = p;
                    len = 1;
                }
                runs++; // ultimo run
            }
            bw.Write(runs);

            // Segundo pase: escribir runs.
            {
                uint cur = Pack(chunk.voxels[0]);
                int len = 1;
                for (int i = 1; i < volume; i++)
                {
                    uint p = Pack(chunk.voxels[i]);
                    if (p == cur && len < ushort.MaxValue) { len++; continue; }
                    bw.Write((ushort)len);
                    bw.Write(cur);
                    cur = p;
                    len = 1;
                }
                bw.Write((ushort)len);
                bw.Write(cur);
            }
        }

        private static void ReadChunkRle(BinaryReader br, VoxelChunk chunk, int volume)
        {
            uint runs = br.ReadUInt32();
            int written = 0;
            for (uint r = 0; r < runs; r++)
            {
                ushort len = br.ReadUInt16();
                if (len == 0)
                    throw new InvalidDataException("WorldSerializer: run de longitud 0");
                uint packed = br.ReadUInt32();
                if (written + len > volume)
                    throw new InvalidDataException("WorldSerializer: runs exceden el volumen del chunk");
                var v = Unpack(packed);
                for (int k = 0; k < len; k++)
                    chunk.voxels[written + k] = v;
                written += len;
            }
            if (written != volume)
                throw new InvalidDataException(
                    $"WorldSerializer: runs cubren {written}/{volume} voxels");
        }

        internal static uint Pack(Voxel v)
        {
            uint mat = v.material;
            uint dens = (uint)Mathf.Clamp(Mathf.RoundToInt(v.densidad * 255f), 0, 255);
            uint dur = (uint)Mathf.Clamp(Mathf.RoundToInt(v.dureza * 255f), 0, 255);
            uint sol = v.solido ? 1u : 0u;
            return (mat & 0xFFu)
                 | ((dens & 0xFFu) << 8)
                 | ((dur & 0xFFu) << 16)
                 | ((sol & 0x1u) << 24);
        }

        internal static Voxel Unpack(uint packed)
        {
            byte mat = (byte)(packed & 0xFFu);
            float dens = ((packed >> 8) & 0xFFu) / 255f;
            float dur = ((packed >> 16) & 0xFFu) / 255f;
            // solido se deriva de densidad>=0.5 en el ctor de Voxel,
            // pero respetamos el bit guardado por simetria.
            return new Voxel(mat, dens, dur);
        }
    }
}
