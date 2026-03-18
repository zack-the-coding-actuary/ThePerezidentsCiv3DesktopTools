# Civ3Tools

A .NET 8.0 library and desktop application for reading, parsing, and exporting data from Civilization III game files (`.biq` scenario/ruleset files and `.sav` save game files).

## Projects

### Civ3Tools
The core library exposing high-level functions for working with Civ3 game data. Built on top of QueryCiv3 and Blast.

Current features:
- **GetUnitInfo** — reads all unit prototype (PRTO) data from a `.biq` scenario file and exports it to CSV, with human-readable lookups for technologies, resources, governments, terrain, and civilization availability.

### The Perezident's Civ 3 Desktop Tools
A WinForms desktop application providing a GUI for Civ3Tools functions.

Current features:
- **Export units to CSV** — select a `.biq` scenario file and export all unit data to a CSV spreadsheet.

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
