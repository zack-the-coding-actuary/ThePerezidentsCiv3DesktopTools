using QueryCiv3;
using QueryCiv3.Sav;
using System.IO;

namespace Civ3Tools
{
    internal static class VictoryPointRandomizer
    {
        private const int GAME_HEADER = 0x454D4147; // "GAME"
        private const int TILE_HEADER = 0x454C4954; // "TILE"
        private const int SAV_BIQ_LENGTH_OFFSET = 38;
        private const int SAV_BIQ_SECTION_START = 562;
        private const int SAV_VPL_OFFSET_IN_TILE = 38; // short at bytes [2..3] of UnknownBuffer3
        private const int BIQ_SECTION_HEADERS_START = 736;
        private const int GAME_LEN_1 = 16; // Length + DefaultGameRules + DefaultVictoryConditions + NumberOfPlayableCivs
        private const int NUMBER_OF_PLAYABLE_CIVS_OFFSET = 12; // offset within LEN_1

        // Offsets within the LEN_2 block
        private const int VICTORY_LOCATIONS_BYTE_OFFSET = 1; // Flags[1], within Flags[4]
        private const byte VICTORY_LOCATIONS_BIT = 5;
        private const int AUTO_PLACE_VICTORY_LOCATIONS_OFFSET = 12; // Flags(4) + PlaceCaptureUnits(4) + AutoPlaceKings(4)

        private const int SAV_BASE_TERRAIN_OFFSET_IN_TILE = 57; // Flags2[5] & 0x0F in C3C block
        private const int WATER_TERRAIN_MIN = 11; // 11=coast, 12=sea, 13=ocean

        private static int CoordinatesToTileIndex(int x, int y, int mapWidth)
            => y * (mapWidth / 2) + x / 2;

        private static int TileDistance(int x1, int y1, int x2, int y2)
            => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

        private static unsafe bool IsWaterTile(byte* tilePtr)
            => (*(tilePtr + SAV_BASE_TERRAIN_OFFSET_IN_TILE) & 0x0F) >= WATER_TERRAIN_MIN;

        private static unsafe bool AssignVictoryPoint(byte[] savBytes, int tileIndex)
        {
            int biqLength = BitConverter.ToInt32(savBytes, SAV_BIQ_LENGTH_OFFSET);
            int savStart  = SAV_BIQ_SECTION_START + biqLength;
            int tileSize  = sizeof(TILE);
            int count     = 0;

            fixed (byte* bytePtr = savBytes)
            {
                byte* scan = bytePtr + savStart;
                byte* end  = bytePtr + savBytes.Length;

                while (scan + tileSize <= end)
                {
                    if (*(int*)scan == TILE_HEADER)
                    {
                        if (count == tileIndex)
                        {
                            *(short*)(scan + SAV_VPL_OFFSET_IN_TILE) = 0;
                            return true;
                        }
                        count++;
                        scan += tileSize;
                    }
                    else scan++;
                }
            }
            return false;
        }


        // Dead code below, this was just for testing something off the wall
        private static bool PatchBiq(string biqPath, bool victoryLocationsEnabled, int autoPlaceVictoryLocations)
        {
            byte[] bytes = Util.ReadFile(biqPath);
            if (!PatchBiq(bytes, victoryLocationsEnabled, autoPlaceVictoryLocations)) return false;
            File.WriteAllBytes(biqPath, bytes);
            return true;
        }

        private static unsafe bool PatchBiq(byte[] biqBytes, bool victoryLocationsEnabled, int autoPlaceVictoryLocations)
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

                        if (dataPtr + GAME_LEN_1 > end) return false;

                        int playableCivs = *(int*)(dataPtr + NUMBER_OF_PLAYABLE_CIVS_OFFSET);
                        if (playableCivs == 0) playableCivs = 31;

                        byte* len2Start = dataPtr + GAME_LEN_1 + playableCivs * sizeof(int); // skip GameCiv
                        if (len2Start + AUTO_PLACE_VICTORY_LOCATIONS_OFFSET + sizeof(int) > end) return false;

                        // Set VictoryLocations flag (Flags[1], bit 5)
                        byte* flagsByte1 = len2Start + VICTORY_LOCATIONS_BYTE_OFFSET;
                        if (victoryLocationsEnabled)
                            *flagsByte1 |= (byte)(1 << VICTORY_LOCATIONS_BIT);
                        else
                            *flagsByte1 &= unchecked((byte)~(1 << VICTORY_LOCATIONS_BIT));

                        // Write AutoPlaceVictoryLocations
                        *(int*)(len2Start + AUTO_PLACE_VICTORY_LOCATIONS_OFFSET) = autoPlaceVictoryLocations;

                        return true;
                    }
                    scan++;
                }
            }
            return false;
        }
    }
}
