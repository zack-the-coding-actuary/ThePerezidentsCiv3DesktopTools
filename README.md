# Civ3Tools

A .NET 8.0 library and desktop application for reading, parsing, and exporting data from Civilization III game files (`.biq` scenario/ruleset files and `.sav` save game files).

## Projects

### Civ3Tools
The core library exposing high-level functions for working with Civ3 game data. Built on top of QueryCiv3 and Blast.

Current features:
- **GetUnitInfo** — reads all unit prototype (PRTO) data from a `.biq` scenario file and exports it to CSV, with human-readable lookups for technologies, resources, governments, terrain, and civilization availability.
- **MapRevealer** — modifies a `.sav` save game file to reveal and make visible the entire map for Player 1.

### ThePerezidentsCiv3DesktopTools
A WinForms desktop application providing a GUI for Civ3Tools functions.

Current features:
- **Export units to CSV** — select a `.biq` scenario file and export all unit data to a CSV spreadsheet.
- **Reveal Map for Player 1** — select a `.sav` save game file and export a modified copy with the entire map revealed and visible for Player 1.

---

## API Reference

### `TurnOrchestrator` (static)

Low-level utility class for reading and writing fields in decompressed Civ3 `.sav` files. All methods operate on raw `byte[]` and do not require a `.biq` file.

#### Methods

| Signature | Description |
|---|---|
| `GAME? GetGameDataFromSav(string savPath)` | Reads and decompresses the file at `savPath`, scans for the `GAME` section, and returns it as a `GAME` struct. Returns `null` if the section is not found. |
| `GAME? GetGameDataFromSav(byte[] saveBytes)` | Same as above but operates on an already-decompressed byte array. |
| `string GetGameFingerprint(byte[] saveBytes)` | Returns a SHA-256 hex string that uniquely identifies a game instance. Hashes the embedded BIQ bytes (scenario/ruleset) and the world generation seed. Stable across all saves from the same game. Throws if the `WRLD` section is not found. |
| `void WriteNextPlayerID(byte[] saveBytes, int id)` | Writes `id` into the `NextPlayerID` field of the `GAME` section in-place. `id` is 1-indexed (0 = barbarians). |
| `void WriteTurnNumber(byte[] saveBytes, int turn)` | Writes `turn` into the `TurnNumber` field of the `GAME` section in-place. |

---

### `PitBossOrganizer`

Manages turn order and save file state for an asynchronous Play-By-Email game. Intended to be consumed by a bot or server-side API. Thread-safe — concurrent calls to any public method are safe.

> **Constructor requirement:** `firstSavFile` must be a save from turn 0 after the first player has already taken their turn. This is the standard handoff format for a new PBE game.

#### Constructor

```csharp
PitBossOrganizer(byte[] firstSavFile, string[] humanPlayers)
```

- `firstSavFile` — The initial `.sav` file (compressed or uncompressed).
- `humanPlayers` — Ordered array of player identifiers (e.g. Discord usernames) in the desired turn order. Index 0 is the first player to act each turn.

Throws if the save file cannot be fingerprinted.

#### Properties

| Property | Type | Description |
|---|---|---|
| `Fingerprint` | `string` | SHA-256 game fingerprint. Set once at construction, used to validate all uploaded saves. |
| `AdminPassword` | `string` | Admin password parsed from the `GAME` section of the initial save. Empty string if none was set. |
| `HumanPlayers` | `string[]` | Current turn order. |
| `DecompressedSave` | `byte[]` | The latest accepted save file in decompressed form. Updated each time `ReceiveNewTurn` succeeds. |
| `TurnTaken` | `bool[]` | Parallel to `HumanPlayers`. `true` for each player who has submitted their turn in the current cycle. |
| `CurrentTurn` | `int` | 0-based turn counter, incremented by `PassTurn`. |

#### Methods

---

**`byte[] GetConfiguredTurn(string player)`**

Prepares and returns `DecompressedSave` for the given player. Writes the correct `NextPlayerID` and `TurnNumber` into the save bytes before returning.

- Throws if the organizer is locked (another player has the save checked out).
- Throws if `player` is not found in `HumanPlayers`.
- Throws if the player has already taken their turn this cycle.
- Locks the organizer until `ReceiveNewTurn` or `ForceUnlock` is called.

> The returned byte array is the internal buffer. The caller should not modify it.

---

**`void ReceiveNewTurn(byte[] newSave)`**

Validates and accepts an uploaded save file as the new game state.

Validates that:
1. The fingerprint of `newSave` matches `Fingerprint`.
2. The turn order is coherent — either the same turn with `NextPlayerID` incremented by 1, or the next turn with `NextPlayerID` equal to 1.

On success: updates `DecompressedSave`, marks the submitting player's `TurnTaken` flag, and releases the lock.

- Throws if the fingerprint does not match.
- Throws if the turn order is invalid.

---

**`void PassTurn()`**

Resets all `TurnTaken` flags to `false`, writes the new turn number to `DecompressedSave`, and increments `CurrentTurn`. Call this when all players have submitted or when the turn timer expires.

---

**`void ForceUnlock()`**

Releases the lock without accepting a new save. Use this when a player's upload window expires (e.g. 15-minute timeout) so the next player can be served.

---

**`void ChangePlayerOrder(string[] newOrder)`**

Replaces `HumanPlayers` with `newOrder`. The new array must be the same length as the current one. Throws otherwise.

---

**`void Dispose()`**

Implements `IDisposable`. Clears all fields and releases the save buffer for GC. Call this when a game ends or is abandoned. It is recommended that the bot confirm intent with the requestor via DM and require the admin password before calling:

```csharp
// Bot: "Are you sure you want to end this game? Reply with the admin password to confirm."
if (confirmedPassword == game.AdminPassword)
    game.Dispose();
```

---

## Dependencies

### QueryCiv3
Parses Civilization III `.biq` and `.sav` binary file formats into strongly-typed C# structs. Identifies 4-character section headers and memory-copies binary data directly into struct layouts.

- **Author:** [myjimnelson](https://github.com/myjimnelson)
- **Source:** C# port of the original [Go library in c3sat](https://github.com/myjimnelson/c3sat/tree/master/queryciv3)

### Blast
Decompression library for the PKWare BLAST compression algorithm, used to decompress Civ3 game files before parsing.

- **C# port author:** James Telfer — Copyright © 2012, released under the [Apache 2.0 License](http://www.apache.org/licenses/LICENSE-2.0.html)
- **Source:** [github.com/jamestelfer/Blast](https://github.com/jamestelfer/Blast/tree/3f8c7919c0444c75121f7371c812ec5c2bb9905b/Blast)
- **Original C implementation:** Mark Adler (madler@alumni.caltech.edu) — Copyright © 2003, part of the [zlib/libpng license](https://www.zlib.net/zlib_license.html)
- **Algorithm:** Originally developed by PKWare

### Other dependencies
- [Serilog](https://serilog.net/) — Structured logging (used internally by QueryCiv3)
- [System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages) — Code page 1252 encoding support for Civ3 string data
