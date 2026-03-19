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

        private const int BIQ_SECTION_HEADERS_START = 736;
        private const int BIQ_GAME_LEN_1 = 16;   // Length + DefaultGameRules + DefaultVictoryConditions + NumberOfPlayableCivs
        private const int BIQ_GAME_LEN_2 = 5304;
        private const int BIQ_GAME_LEN_3 = 2017;

        public static QueryCiv3.Biq.GAME? GetBiqGameData(string biqPath) =>
            GetBiqGameData(Util.ReadFile(biqPath));

        public static unsafe QueryCiv3.Biq.GAME? GetBiqGameData(byte[] biqBytes)
        {
            fixed (byte* bytePtr = biqBytes)
            {
                byte* scan = bytePtr + BIQ_SECTION_HEADERS_START;
                byte* end = bytePtr + biqBytes.Length;

                while (scan + 8 < end)
                {
                    if (*(int*)scan == GAME_HEADER)
                    {
                        byte* dataPtr = scan + 8; // skip "GAME" tag + count int

                        if (dataPtr + BIQ_GAME_LEN_1 > end) return null;

                        QueryCiv3.Biq.GAME game = default;
                        byte* gamePtr = (byte*)&game;

                        Buffer.MemoryCopy(dataPtr, gamePtr, BIQ_GAME_LEN_1, BIQ_GAME_LEN_1);
                        int playableCivs = game.NumberOfPlayableCivs == 0 ? 31 : game.NumberOfPlayableCivs;
                        dataPtr += BIQ_GAME_LEN_1 + playableCivs * sizeof(int); // skip GameCiv

                        if (dataPtr + BIQ_GAME_LEN_2 > end) return null;
                        Buffer.MemoryCopy(dataPtr, gamePtr + BIQ_GAME_LEN_1, BIQ_GAME_LEN_2, BIQ_GAME_LEN_2);
                        dataPtr += BIQ_GAME_LEN_2 + playableCivs * sizeof(int); // skip GameAlliance

                        if (dataPtr + BIQ_GAME_LEN_3 > end) return null;
                        Buffer.MemoryCopy(dataPtr, gamePtr + BIQ_GAME_LEN_1 + BIQ_GAME_LEN_2, BIQ_GAME_LEN_3, BIQ_GAME_LEN_3);

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

                        TILE_VP tileVP = default;
                        Buffer.MemoryCopy(scan, &tileVP.Tile, sizeof(TILE), sizeof(TILE));
                        Buffer.MemoryCopy(scan + FLAGS2_OFFSET_IN_TILE, tileVP.Flags2, FLAGS2_LENGTH, FLAGS2_LENGTH);

                        tiles.Add(tileVP);
                        scan += sizeof(TILE);
                    }
                    else scan++;
                }
            }

            return tiles.ToArray();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
        public unsafe struct TILE_VP
        {
            public TILE Tile;
            public fixed byte Flags2[12];
        }
    }
}
