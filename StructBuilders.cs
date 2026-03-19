using QueryCiv3;
using QueryCiv3.Sav;
using System;

namespace Civ3Tools
{
    internal static class StructBuilders
    {
        private const int BIQ_LENGTH_OFFSET = 38;
        private const int BIQ_SECTION_START = 562;
        private const int GAME_HEADER = 0x454D4147; // "GAME"

        // Scans only for the GAME section header in the save file and copies it directly,
        // bypassing the full SavData parse that fails on non-vanilla scenario saves.
        public static GAME? GetGameData(string savPath) =>
            GetGameData(Util.ReadFile(savPath));

        public static unsafe GAME? GetGameData(byte[] saveBytes)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            fixed (byte* bytePtr = saveBytes)
            {
                byte* scan = bytePtr + scanStart;
                byte* end = bytePtr + saveBytes.Length;

                while (scan < end)
                {
                    if (*(int*)scan == GAME_HEADER)
                    {
                        if (scan + sizeof(GAME) > end)
                            return null;

                        GAME game = default;
                        Buffer.MemoryCopy(scan, &game, sizeof(GAME), sizeof(GAME));
                        return game;
                    }
                    scan++;
                }
            }

            return null;
        }

        private const int TILE_HEADER = 0x454C4954; // "TILE"
        // Byte offset of Flags2 within the TILE struct (Pack=1, counting data fields only).
        private const int FLAGS2_OFFSET_IN_TILE = 52;
        private const int FLAGS2_LENGTH = 12;

        public static TILE_VP[] GetTileData(string savPath) =>
            GetTileData(Util.ReadFile(savPath));

        public static unsafe TILE_VP[] GetTileData(byte[] saveBytes)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;
            var tiles = new System.Collections.Generic.List<TILE_VP>();

            fixed (byte* bytePtr = saveBytes)
            {
                byte* scan = bytePtr + scanStart;
                byte* end = bytePtr + saveBytes.Length;

                while (scan < end)
                {
                    if (*(int*)scan == TILE_HEADER)
                    {
                        if (scan + sizeof(TILE) > end) break;

                        TILE tile = default;
                        Buffer.MemoryCopy(scan, &tile, sizeof(TILE), sizeof(TILE));

                        byte[] flags2 = new byte[FLAGS2_LENGTH];
                        byte* flags2Ptr = scan + FLAGS2_OFFSET_IN_TILE;
                        for (int i = 0; i < FLAGS2_LENGTH; i++)
                            flags2[i] = flags2Ptr[i];

                        tiles.Add(new TILE_VP(tile, flags2));
                        scan += sizeof(TILE);
                    }
                    else scan++;
                }
            }

            return tiles.ToArray();
        }

        public unsafe class TILE_VP
        {
            public TILE Tile { get; private set; }
            public byte[] Flags2 { get; private set; }

            internal TILE_VP(TILE tile, byte[] flags2)
            {
                Tile = tile;
                Flags2 = flags2;
            }
        }
    }
}
