# Warband

A client-side BepInEx mod for Valheim: a party for you and your friends, with an ally HUD (health, stamina, eitr, effects, weapon), silhouettes of allies behind walls and allies on the Nav Compass bar.

Mod para Valheim (BepInEx, client-side): uma party para você e seus amigos, com HUD dos aliados (vida, stamina, eitr, efeitos, arma), silhueta dos aliados atrás de paredes e aliados na barra do Nav Compass.

For players, see [package/README.md](package/README.md). That file is also the Thunderstore page.
Para jogadores, veja [package/README.md](package/README.md).

Download: [Thunderstore](https://thunderstore.io/c/valheim/p/DJAventuras/Warband/) · [Nexus Mods](https://www.nexusmods.com/valheim/mods/4900) (named **Shieldmates** there) · [GitHub Releases](https://github.com/Felihens/Warband/releases)

Sister mods / mods irmãos: [Auto Walk Around](https://github.com/Felihens/AutoWalkAround), [Gather Tweaks](https://github.com/Felihens/GatherTweaks), [Build Tweaks](https://github.com/Felihens/BuildTweaks), [DigForHoe](https://github.com/Felihens/DigForHoe), [Resource Scanner](https://github.com/Felihens/ResourceScanner), [Ore Weak Spots](https://github.com/Felihens/OreWeakSpots), [Tree Weak Spots](https://github.com/Felihens/TreeWeakSpots).

## Building

`sh build.sh` compiles `src/*.cs` against the game's assemblies and installs the dll into `BepInEx/plugins/Warband/` (`sh build.sh nocopy` only compiles). Then `powershell -File pack.ps1` builds `dist/Warband-<version>.zip` from `package/`.

## License

MIT
