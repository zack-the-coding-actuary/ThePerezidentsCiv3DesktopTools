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
- **Toggle Human/AI Players** — select a `.sav` save game file, toggle each active player slot between Human and AI, and export the modified save.

---

## API Reference

### `PitBossOrganizer`

Manages turn order and save file state for an asynchronous Play-By-Email game. Intended to be consumed by a bot or server-side API. Thread-safe — concurrent calls to any public method are safe.

> **Game setup requirement:** The last human player slot in the Civ 3 game must be a **dummy player** controlled by the server admin — not a real participant. This is required because Civ 3 only runs inter-turn calculations (AI turns, production, healing, diplomacy, etc.) after the last human slot ends its turn. Since `PitBossOrganizer` allows real players to take turns in any order, it cannot guarantee which real player goes last. The dummy player goes last every round by design: once all real players have submitted, the admin loads the save as the dummy civ, immediately ends turn without making any moves, and uploads. Civ 3 then fires inter-turn processing and advances to the next turn.

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
| `CurrentPlayer` | `int?` | Index into `HumanPlayers` of the player who currently has the save checked out. `HumanPlayers.Length` when the dummy player has it checked out. `null` when no save is checked out. |
| `DecompressedSave` | `byte[]` | The latest accepted save file in decompressed form. Updated each time `ReceiveNewTurn` or `ReceiveDummyTurn` succeeds. |
| `TurnTaken` | `bool[]` | Parallel to `HumanPlayers`. `true` for each player who has submitted their turn in the current cycle. Reset to all `false` by `ReceiveDummyTurn`. |

#### Methods

---

**`byte[] GetConfiguredTurn(string player)`**

Prepares and returns a copy of `DecompressedSave` for the given player. Writes the correct `NextPlayerID` into the save bytes before returning.

- Throws if the organizer is locked (another player has the save checked out).
- Throws if `player` is not found in `HumanPlayers`.
- Throws if the player has already taken their turn this cycle.
- Locks the organizer until `ReceiveNewTurn` or `ForceUnlock` is called.

---

**`void ReceiveNewTurn(byte[] newSave)`**

Validates and accepts an uploaded save file as the new game state.

Validates that:
1. The fingerprint of `newSave` matches `Fingerprint`.
2. The turn number is unchanged and `NextPlayerID` is incremented by 1.

On success: updates `DecompressedSave`, marks the submitting player's `TurnTaken` flag, and releases the lock.

- Throws if the fingerprint does not match.
- Throws if the turn order is invalid.

---

**`byte[] GetDummyTurn()`**

Prepares and returns a copy of `DecompressedSave` with `NextPlayerID` set to the dummy player slot. Call this once all real players have submitted their turns. Locks the organizer regardless of prior lock state.

The admin should load this save, immediately end turn without making any moves, and upload the result via `ReceiveDummyTurn`. Civ 3 will run inter-turn calculations and advance to the next turn number.

---

**`void ReceiveDummyTurn(byte[] dummySave)`**

Validates and accepts the dummy player's uploaded save as the new game state.

Validates that:
1. The fingerprint of `dummySave` matches `Fingerprint`.
2. The turn number has incremented by 1 and `NextPlayerID` is less than the previous value (i.e. Civ 3 wrapped back to player 1).

On success: updates `DecompressedSave`, resets all `TurnTaken` flags to `false`, increments `CurrentTurn`, and releases the lock.

- Throws if the fingerprint does not match.
- Throws if the turn order is invalid.

---

**`void ForceUnlock()`**

Releases the lock without accepting a new save. Use this when a player's upload window expires (e.g. 15-minute timeout) so the next player can be served.

---

**`void ChangePlayerOrder(string[] newOrder)`**

Replaces `HumanPlayers` with `newOrder`. The new array must be the same length as the current one. Throws otherwise.

- Throws if the organizer is locked (a save is currently checked out).

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
- **License:** MIT — Copyright © OpenCiv3 contributors

### Blast
Decompression library for the PKWare BLAST compression algorithm, used to decompress Civ3 game files before parsing.

- **C# port author:** James Telfer — Copyright © 2012, released under the [Apache 2.0 License](http://www.apache.org/licenses/LICENSE-2.0.html)
- **Source:** [github.com/jamestelfer/Blast](https://github.com/jamestelfer/Blast/tree/3f8c7919c0444c75121f7371c812ec5c2bb9905b/Blast)
- **Original C implementation:** Mark Adler (madler@alumni.caltech.edu) — Copyright © 2003, part of the [zlib/libpng license](https://www.zlib.net/zlib_license.html)
- **Algorithm:** Originally developed by PKWare

### Other dependencies
- [Serilog](https://serilog.net/) — Structured logging (used internally by QueryCiv3)
- [System.Text.Encoding.CodePages](https://www.nuget.org/packages/System.Text.Encoding.CodePages) — Code page 1252 encoding support for Civ3 string data
