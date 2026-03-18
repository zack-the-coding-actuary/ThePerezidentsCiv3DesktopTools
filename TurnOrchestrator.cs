using QueryCiv3;
using QueryCiv3.Sav;
using System;
using System.Security.Cryptography;

namespace Civ3Tools
{
    public class PitBossOrganizer : IDisposable
    {
        public string Fingerprint { get; private set; }
        public string[] HumanPlayers { get; private set; }
        public byte[] DecompressedSave { get; private set; }
        public bool[] TurnTaken { get; private set; }
        public int CurrentTurn { get; private set; }
        public string AdminPassword { get; private set; }
        private bool locked;
        private readonly object _lock = new object();

        public PitBossOrganizer(byte[] firstSavFile, string[] humanPlayers)
        {
            // If save file is compressed, decompress it first
            DecompressedSave = EnsureDecompressed(firstSavFile);
            HumanPlayers = humanPlayers;

            // Instantiate TurnTaken to all false except first player, who we assume took their turn already
            TurnTaken = new bool[HumanPlayers.Length];
            TurnTaken[0] = true;

            CurrentTurn = 0;

            // If this function returns null, the error will fire and stop the constructor
            Fingerprint = TurnOrchestrator.GetGameFingerprint(DecompressedSave);

            AdminPassword = TurnOrchestrator.GetGameDataFromSav(DecompressedSave)?.AdminPassword ?? string.Empty;

            // Lock to prevent other players from downloading a save while another player has one checked out
            locked = false;
        }

        public byte[] GetConfiguredTurn(string player)
        {
            lock (_lock)
            {
                // If locked, don't give the save.
                if (locked) throw new ArgumentException("Error: Save state is currently locked by another player");

                // C# arrays are zero-indexed, but 0 correlates to barbarians, so we need to add 1
                int playerIdx = Array.IndexOf(HumanPlayers, player) + 1;

                // If playerIdx is zero, there was no match
                if (playerIdx == 0) throw new KeyNotFoundException("Player doesn't exist in this PitBoss registry");
                // Make sure players can't take more than one turn this cycle
                else if (TurnTaken[playerIdx - 1]) throw new ArgumentException("This player has already taken their turn.");
                // Otherwise, set NextPlayerID to the correct player
                else
                {
                    // Move turn to next player and make sure we didn't flip turns in case last player just went
                    ChangeNextPlayerID(playerIdx);
                    ChangeCurrentTurn(CurrentTurn);
                    locked = true;
                    return DecompressedSave;
                }
            }
        }

        public void ReceiveNewTurn(byte[] newSave)
        {
            // Decompress and fingerprint check outside the lock — these are read-only and potentially slow
            newSave = EnsureDecompressed(newSave);
            if (TurnOrchestrator.GetGameFingerprint(newSave) != Fingerprint)
                throw new ArgumentException("Error: Invalid save uploaded");

            var newData = TurnOrchestrator.GetGameDataFromSav(newSave);

            lock (_lock)
            {
                // Ensure turn order makes sense
                // Either the turn is the same and the next player is selected, or its the next turn and the first player is selected
                var oldData = TurnOrchestrator.GetGameDataFromSav(DecompressedSave);
                if (newData?.TurnNumber != null &&
                   ((newData?.TurnNumber == oldData?.TurnNumber && newData?.NextPlayerID == oldData?.NextPlayerID + 1) ||
                   (newData?.TurnNumber == oldData?.TurnNumber + 1 && newData?.NextPlayerID == 1)))
                {
                    DecompressedSave = newSave;
                    TurnTaken[(int)oldData?.NextPlayerID - 1] = true;
                    locked = false;
                }
                else
                {
                    throw new ArgumentException("Error: Save is correct, but turn order is wrong");
                }
            }
        }

        public void PassTurn()
        {
            lock (_lock)
            {
                for (int i = 0; i < TurnTaken.Length; i++)
                {
                    TurnTaken[i] = false;
                }
                ChangeCurrentTurn(CurrentTurn + 1);
                CurrentTurn++;
            }
        }

        public void ForceUnlock()
        {
            lock (_lock)
            {
                locked = false;
            }
        }

        public void ChangePlayerOrder(string[] newOrder)
        {
            lock (_lock)
            {
                if (newOrder.Length == HumanPlayers.Length)
                    HumanPlayers = newOrder;
                else throw new ArgumentException($"New player list is invalid size, expected {HumanPlayers.Length} names.");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                DecompressedSave = null;
                TurnTaken = null;
                HumanPlayers = null;
                Fingerprint = null;
                AdminPassword = null;
                locked = false;
                CurrentTurn = 0;
            }
        }

        private void ChangeNextPlayerID(int id) =>
            TurnOrchestrator.WriteNextPlayerID(DecompressedSave, id);

        private void ChangeCurrentTurn(int turn) =>
            TurnOrchestrator.WriteTurnNumber(DecompressedSave, turn);

        private static byte[] EnsureDecompressed(byte[] savFile)
        {
            if (savFile[0] == 0x00 && (savFile[1] == 0x04 || savFile[1] == 0x05 || savFile[1] == 0x06))
            {
                return Util.Decompress(savFile);
            }
            return savFile;
        }

    }

    public static class TurnOrchestrator
    {
        // Offset within the save file where the embedded BIQ section length is stored.
        private const int BIQ_LENGTH_OFFSET = 38;
        // Offset where the embedded BIQ section data begins.
        private const int BIQ_SECTION_START = 562;
        // Little-endian int32 encodings of section header ASCII strings.
        private const int GAME_HEADER = 0x454D4147; // "GAME"
        private const int WRLD_HEADER = 0x444C5257; // "WRLD"
        // Byte offsets of writable fields within the GAME struct (Pack=1, no padding).
        private const int TURN_NUMBER_OFFSET_IN_GAME = 64;
        private const int NEXT_PLAYER_ID_OFFSET_IN_GAME = 324;
        // Byte offset of WorldSeed within the WRLD struct (Pack=1, no padding).
        private const int WORLD_SEED_OFFSET_IN_WRLD = 174;

        // Returns a SHA256 hex string that stably identifies a specific game instance.
        // Hashes the embedded BIQ bytes (scenario/ruleset) + WorldSeed (map generation seed),
        // both of which are written once at game creation and never change.
        public static string GetGameFingerprint(byte[] saveBytes)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            for (int i = scanStart; i <= saveBytes.Length - 4; i++)
            {
                if (BitConverter.ToInt32(saveBytes, i) == WRLD_HEADER)
                {
                    int seedOffset = i + WORLD_SEED_OFFSET_IN_WRLD;
                    if (seedOffset + 4 > saveBytes.Length)
                        return null;

                    byte[] toHash = new byte[biqLength + 4];
                    Array.Copy(saveBytes, BIQ_SECTION_START, toHash, 0, biqLength);
                    Array.Copy(saveBytes, seedOffset, toHash, biqLength, 4);
                    return Convert.ToHexString(SHA256.HashData(toHash));
                }
            }
            throw new ArgumentNullException("Error, could not generate fingerprint for this save file.");
        }

        public static void WriteNextPlayerID(byte[] saveBytes, int id) =>
            WriteIntToGameSection(saveBytes, NEXT_PLAYER_ID_OFFSET_IN_GAME, id);

        public static void WriteTurnNumber(byte[] saveBytes, int turn) =>
            WriteIntToGameSection(saveBytes, TURN_NUMBER_OFFSET_IN_GAME, turn);

        private static void WriteIntToGameSection(byte[] saveBytes, int offsetInGame, int value)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            for (int i = scanStart; i <= saveBytes.Length - 4; i++)
            {
                if (BitConverter.ToInt32(saveBytes, i) == GAME_HEADER)
                {
                    int writeOffset = i + offsetInGame;
                    if (writeOffset + 4 > saveBytes.Length)
                        throw new InvalidOperationException("Save file too short to write to GAME section.");

                    byte[] bytes = BitConverter.GetBytes(value);
                    Array.Copy(bytes, 0, saveBytes, writeOffset, 4);
                    return;
                }
            }
            throw new InvalidOperationException("GAME section not found in save file.");
        }

        // Scans only for the GAME section header in the save file and copies it directly,
        // bypassing the full SavData parse that fails on non-vanilla scenario saves.
        public static GAME? GetGameDataFromSav(string savPath) =>
            GetGameDataFromSav(Util.ReadFile(savPath));

        public static unsafe GAME? GetGameDataFromSav(byte[] saveBytes)
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
    }
}
