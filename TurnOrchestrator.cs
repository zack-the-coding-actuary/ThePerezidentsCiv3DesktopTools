using QueryCiv3;
using QueryCiv3.Sav;
using System;

namespace Civ3Tools
{
    public static class TurnOrchestrator
    {
        // Offset within the save file where the embedded BIQ section length is stored.
        private const int BIQ_LENGTH_OFFSET = 38;
        // Offset where the embedded BIQ section data begins.
        private const int BIQ_SECTION_START = 562;
        // Little-endian int32 encoding of the ASCII string "GAME".
        private const int GAME_HEADER = 0x454D4147;

        // Scans only for the GAME section header in the save file and copies it directly,
        // bypassing the full SavData parse that fails on non-vanilla scenario saves.
        public static unsafe GAME? GetGameDataFromSav(string savPath)
        {
            byte[] saveBytes = Util.ReadFile(savPath);

            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            fixed (byte* bytePtr = saveBytes)
            {
                int* scan = (int*)(bytePtr + scanStart);
                byte* end = bytePtr + saveBytes.Length;

                while ((byte*)scan < end)
                {
                    if (*scan == GAME_HEADER)
                    {
                        if ((byte*)scan + sizeof(GAME) > end)
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
    }
}
