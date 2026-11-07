# RESTBL Calculator

Builds the resource size table a mod needs for *Tears of the Kingdom*, *Ocarina of Time* (Switch 2) or *Breath of the Wild*
(Switch and Wii U).


## Build

```bash
git submodule update --init --recursive
dotnet build RestblCalculator.slnx -c Release
```

`dotnet run publish.cs` packages Windows, Linux and Mac builds into `publish/`.

## Licence

AGPL-3.0-or-later. See [license.md](license.md).

## Credits
LordBubblesDev - [restbl](https://github.com/LordBubblesDev/restbl) - TotK Reverse Engineering, TotK Configurations
dt-12345 - TotK Reverse Engineering
