# Seraph Leveling

A progression system for [Vintage Story](https://www.vintagestory.at/) that improves your seraph's traits through gameplay. Mine blocks to increase your mining speed, deal damage to improve your combat prowess, sneak past animals to become harder to detect, and more. Progress is tracked per player and persists in the world save, with admin commands to export and import progress across worlds.

Download it from the [Vintage Story mod DB](https://mods.vintagestory.at/show/mod/38354).

## Features

- 20+ trainable traits covering mining, melee, ranged, movement, hunger, armor, crafting, foraging, stealth, and more
- Progress notifications and trait info integrated into the character screen
- Compatibility with Combat Overhaul and its community fork, including bow and poleaxe proficiency tracking
- Server config via `ModConfig/SeraphLeveling.json`, reloadable in-game with `/trait reloadconfig`
- Optional rulesets: `GlobalXPRateMultiplier` for overall progression speed, `DeathPenaltyFullReset` for full level loss on death, `EnableClassCapOffsets` so starting class traits shift each skill's endgame ceiling
- Experimental features, each off by default: Temporal Resistance and Temporal Recharge traits, bow draw speed and aim-assist by Ranged level, melee swing speed by Melee level
- Admin commands: `/trait` for progress management, `/trait export` and `/trait import` for cross-world transfers
- Works in singleplayer and on dedicated servers

See [TRAITS.md](TRAITS.md) for the full trait list and [TRAITS_GUIDE.txt](TRAITS_GUIDE.txt) for the in-depth acquisition guide.

## Building

Requires the .NET SDK and a Vintage Story installation. The csproj resolves game DLLs via the `APPDATA` environment variable, builds the mod zip, and deploys it to your `VintagestoryData/Mods` folder automatically:

```
dotnet build
```

## Forks and credit

Forks and derivative mods are welcome under the MIT license. If you publish a fork or reuse substantial parts of this code, please credit Soareverix and link back to this repository or the mod DB page.

## License

[MIT](LICENSE)
