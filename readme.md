# RESTBL Generator

Builds the resource size table a mod needs for *Tears of the Kingdom*, *Ocarina of Time* (Switch 2) or *Breath of the Wild*
(Switch and Wii U).

Every file in the mod, and every entry of a changed archive, that isn't vanilla for the chosen game version gets an entry;
entries only ever grow past vanilla, except that BotW entries with no size estimate are removed. No game dump is needed: the
vanilla table sizes as JSON, file checksums and zstd dictionaries ship in `Data`. TotK's come from
[restbl](https://github.com/LordBubblesDev/restbl), and the others from `restbl import`.

```bash
RestblTool.exe [mod]                                    # the app; the mod folder can also be dropped on the window
restbl <mod> [--game id] [--version 1.4.3] [--dry-run]  # games: totk, oot, botw-switch, botw-wiiu
restbl import <game id> <version> <folder[|prefix]>...  # adds a dump's data, oldest version first; --out picks the Data folder
```

## Build

```bash
git submodule update --init --recursive
dotnet build RestblTool.slnx -c Release
```

## Licence

AGPL-3.0-or-later. See [license.md](license.md).

## Credits
LordBubblesDev - [restbl](https://github.com/LordBubblesDev/restbl) - Totk configuration
