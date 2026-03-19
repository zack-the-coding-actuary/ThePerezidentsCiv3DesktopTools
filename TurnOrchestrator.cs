using QueryCiv3;
using System;
using System.Security.Cryptography;

namespace Civ3Tools
{
    public class PitBossOrganizer : IDisposable
    {
        public string Fingerprint { get; private set; }
        public string[] HumanPlayers { get; private set; }
        public int? CurrentTurn { get => DecompressedSave is null ? null : StructBuilders.GetGameData(DecompressedSave)?.TurnNumber; }
        public int? CurrentPlayer { get; private set; }
        public byte[] DecompressedSave { get; private set; }
        public bool[] TurnTaken { get; private set; }
        public string AdminPassword { get; private set; }
        private bool locked;
        private readonly object _lock = new object();

        public PitBossOrganizer(byte[] firstSavFile, string[] humanPlayers)
        {
            // If save file is compressed, decompress it first
            DecompressedSave = EnsureDecompressed(firstSavFile);
            HumanPlayers = humanPlayers;
            CurrentPlayer = null;

            // Instantiate TurnTaken to all false except first player, who we assume took their turn already
            TurnTaken = new bool[HumanPlayers.Length];
            TurnTaken[0] = true;

            // If this function returns null, the error will fire and stop the constructor
            Fingerprint = TurnOrchestrator.GetGameFingerprint(DecompressedSave);

            AdminPassword = StructBuilders.GetGameData(DecompressedSave)?.AdminPassword ?? string.Empty;

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
                    ChangeNextPlayerID(playerIdx);
                    CurrentPlayer = playerIdx - 1;
                    locked = true;
                    return DecompressedSave.ToArray(); // Return a clone
                }
            }
        }

        public void ReceiveNewTurn(byte[] newSave)
        {
            // Decompress and fingerprint check outside the lock — these are read-only and potentially slow
            newSave = EnsureDecompressed(newSave);
            if (TurnOrchestrator.GetGameFingerprint(newSave) != Fingerprint)
                throw new ArgumentException("Error: Invalid save uploaded");

            var newData = StructBuilders.GetGameData(newSave);

            lock (_lock)
            {
                // Ensure turn order makes sense: same turn number, NextPlayerID incremented by 1
                var oldData = StructBuilders.GetGameData(DecompressedSave);
                if (newData?.TurnNumber != null && (newData?.TurnNumber == oldData?.TurnNumber && newData?.NextPlayerID == oldData?.NextPlayerID + 1))
                {
                    DecompressedSave = newSave;
                    TurnTaken[(int)oldData?.NextPlayerID - 1] = true;
                    CurrentPlayer = null;
                    locked = false;
                }
                else
                {
                    throw new ArgumentException("Error: Save is correct, but turn order is wrong");
                }
            }
        }

        public byte[] GetDummyTurn()
        {
            // Lock game while this process is occurring. We do not care if there is an outstanding lock.
            locked = true;

            // Set NextPlayerID to dummy player
            ChangeNextPlayerID(HumanPlayers.Length + 1);
            CurrentPlayer = HumanPlayers.Length; // Set to after the end of current HumanPlayers array since this is the dummy
            return DecompressedSave.ToArray();
        }

        public void ReceiveDummyTurn(byte[] dummySave)
        {
            // Decompress if needed
            dummySave = EnsureDecompressed(dummySave);

            // Check fingerprint
            if (TurnOrchestrator.GetGameFingerprint(dummySave) != Fingerprint)
                throw new ArgumentException("Error: Invalid save uploaded");

            var newData = StructBuilders.GetGameData(dummySave);
            var oldData = StructBuilders.GetGameData(DecompressedSave);

            lock (_lock)
            {
                // Check turn order makes sense
                if (newData?.TurnNumber != null && (newData?.TurnNumber == oldData?.TurnNumber + 1 && newData?.NextPlayerID < oldData?.NextPlayerID))
                {
                    // Set save to passed interturn step and reset TurnTaken for all human players
                    DecompressedSave = dummySave;
                    for (int i = 0; i < TurnTaken.Length; i++)
                    {
                        TurnTaken[i] = false;
                    }
                    CurrentPlayer = null;
                    locked = false;
                }
                else
                {
                    throw new ArgumentException("Error: Save is correct, but turn order is wrong");
                }
            }
        }

        public void ForceUnlock()
        {
            lock (_lock)
            {
                CurrentPlayer = null;
                locked = false;
            }
        }

        public void ChangePlayerOrder(string[] newOrder)
        {
            lock (_lock)
            {
                if (locked) throw new ArgumentException("Cannot change player names/order while save checked out.");
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
                CurrentPlayer = null;
                Fingerprint = null;
                AdminPassword = null;
                locked = false;
            }
        }

        private void ChangeNextPlayerID(int id) =>
            TurnOrchestrator.WriteNextPlayerID(DecompressedSave, id);

        private static byte[] EnsureDecompressed(byte[] savFile)
        {
            if (savFile[0] == 0x00 && (savFile[1] == 0x04 || savFile[1] == 0x05 || savFile[1] == 0x06))
            {
                return Util.Decompress(savFile);
            }
            return savFile;
        }

    }

    internal static class TurnOrchestrator
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
    }
}
