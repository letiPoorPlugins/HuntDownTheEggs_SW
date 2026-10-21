<div align="center">
    <img alt="header_banner" src="/repoAssets/banner.png"/>
    <p>HuntDownTheEggs is a plugin that spawns collectables and allows players to collect them.</p>
    <h2>
        <a href="https://discord.com/invite/mEmdyqM3Um" target="_blank"><img src="https://img.shields.io/badge/Discord%20Server-7289da?style=for-the-badge&logo=discord&logoColor=white" /></a>
        <a href="https://ko-fi.com/letaryat" target="_blank"><img src="https://img.shields.io/badge/Ko--fi-F16061?logo=ko-fi&logoColor=white&style=for-the-badge" /></a>
    </h2>
</div>

## [🛠️] Features
- **Placeable Collectables** – Server owners can place collectables at specific locations across the map.
- **Random Spawning** – Collectables can spawn at random positions using the map's NavMesh.
- **Round-Based Spawning** – Collectables spawn each round, keeping gameplay fresh and engaging.
- **Custom Rewards & Rarities** – Configure different rewards and rarity levels for collecting eggs.
- **Developer API** – Includes a dedicated API for developers to integrate with other plugins.
- **Collection Statistics** – Tracks collected eggs and stores collection data in tables for statistics and further use.
- **Multiple Collection Methods** – Players can collect eggs by shooting prop models or walking into them, depending on the server configuration.

> [!TIP]
> If you notice any problems with plugin please create issue on github. If you have any questions, please ask me on discord server.

## [📌] Dependiencies
- [Swiftly2](https://swiftlys2.net/)

## [🛠️] Base Setup
- Download latest [release](https://github.com/letiPoorPlugins/HuntDownTheEggs_SW/releases)
- Drag files to /plugins/,
- Restart your server,
- Config file should be created in configs/plugins/HuntDownTheEggs,
- Configure to your liking,
- Restart server

## [⚙️] Commands

### Admin Commands
| Command | Description |
|---|---|
| `!eggplace` | Places an egg at the selected location. |
| `!tpegg` | Teleports the admin to an egg. |
| `!removeegg` | Removes an egg. |
| `!reloadeggs` | Reloads the eggs for the current map. |
| `!placingmode` | Toggles egg placement mode on or off. |

### Player Commands
| Command | Description |
|---|---|
| `!myeggs` | Displays the eggs collected by the player. |
| `!topplaced` | Shows the leaderboard for collected placed eggs on the current map. |
| `!toprandom` | Shows the leaderboard for collected randomly spawned eggs on the current map. |
| `!topkills` | Shows the leaderboard for collected kill eggs on the current map. |
| `!topeggsmap` | Shows the leaderboard for all collected egg types on the current map. |
| `!topplacedall` | Shows the leaderboard for collected placed eggs across all maps. |
| `!toprandomall` | Shows the leaderboard for collected randomly spawned eggs across all maps. |
| `!topkillsall` | Shows the leaderboard for collected kill eggs across all maps. |
| `!topeggsall` | Shows the leaderboard for all collected egg types across all maps. |


## [🚨] Config setup

> [!CAUTION]
> In theory plugin should work with all types of databases, but only MySQL was tested.

```json
{
  "HuntDownTheEggs": {
    "DatabaseConnection": "default",
    "EggSetup": {
      "EggRootPermission": "hdte.root", # Permission to use admin commands,
      "EggModel": "models/chicken/chicken.vmdl", # vmdl Model of collectable,
      "TriggerModel": "models/chicken/chicken.vmdl", # vmdl Model of collectable for trigger,
      "EggAnimation": "challenge_coin_idle", # Idle animation for a model if model supports it,
      "EggCollectAnimation": "challenge_coin_collect", # Collect animation for a model if model supports it,
      "EggModelHeight": 0, # How higher should model spawn from the ground,
      "EggModelScale": 1, # How big should model be,
      "Glowing": true, # If collectables should glow,
      "GlowingColor": "Red", # Glow Color,
      "GlowingRange": 1500, # Glow range,
      "HidePickedEggsForPlayer": false, #If picked up placed eggs by admin should be invisible for players that picked them,
      "KillEggOnlyForKiller": false # If kill egg should be picked up only by killer,
    },
    "ModesSetup": {
      "SpawnEggsOnWarmup": false, # Disable on warmup,
      "DeathMode": true, # If eggs should be dropped after player death,
      "SearchMode": true, # If spawning placed eggs from .json files should be enabled,
      "PickUpMode": true, # If Eggs should be picked up by walking into them,
      "ShootEggMode": true, # If eggs should be picked up via shooting,
      "ShootEggHealth": 100, # Egg health,
      "SpawnDeathEggOnVictim": true, # If deathmode enabled, egg should spawn on victim or attacker,
      "ChanceToSpawn": 100, # Chance to spawn after player death,
      "RemoveOnFind": false, # Eggs placed by admins should be removed after being picked up,
      "SpawnPlacedEggsOnce": false, # Need RemoveOnFind: true, Placed eggs will not spawn in the same match after being picked up 
      "SpawnRandomEggs": false, # Spawn random eggs using navmash on map,
      "SpawnRandomEggsOnWarmup": false, # Spawn random eggs on warmup,
      "MinPlayersToSpawnRandomEggs": 1, # Minimum online players to spawn random eggs
      "NumberOfRandomEggs": 20, # How many random eggs will spawn each round,
      "SpawnRandomEggsByPercentageOfPlayers": true # Random eggs will be spawned by percentage of players online
    },
    "PrizeSetup": {
      "ReceivePrize": true, # If prizes should be turned on/off
      "PresentTypes": {
        "common": {         # Rarity name
          "chance": 30,     # Chance to get
          "color": "Green", # Color on chat
          "rewards": {      # Rewards using commands
            "Punkty sklepu": "say Common Prize 1",
            "Vip": "css_addvip {USERID} 69"
          }
        },
        "rare": {
          "chance": 70,
          "color": "Red",
          "rewards": {
            "Rare Prize": "say Rare Prize 1",
            "Rare Vip": "css_addvip {USERID} 420"
          }
        }
      }
    },
    "CommandsSetup": {
      "EnableCommands": true,
      "PlaceEggCommands": [ # Admin command to place eggs
        "eggplace"
      ],
      "TeleportToEggCommands": [ # Admin command to teleport to egg
        "tpegg"
      ],
      "RemoveEggCommands": [ # Admin command to remove egg
        "removeegg"
      ],
      "ReloadEggsCommands": [ # Admin command to reload egg
        "reloadeggs"
      ],
      "PlacingModeCommands": [ # Admin command to turn on / off placing mode
        "placingmode"
      ],
      "ShowMyEggsCommands": [ # Player command to show all eggs 
        "myeggs"
      ],
      "ShowTopPlacedEggsPerMapCommands": [ # Player command to top leaderboard of gathered placed eggs per map
        "topplaced"
      ],
      "ShowTopRandomEggsPerMapCommands": [ # Player command to top leaderboard of gathered random spawn eggs per map
        "toprandom"
      ],
      "ShowTopKillEggsPerMapCommands": [ # Player command to top leaderboard of gathered kill eggs per map
        "topkills"
      ],
      "ShowTopAllEggsPerMapCommands": [ # Player command to top leaderboard of gathered all eggs per map
        "topeggsmap"
      ],
      "ShowTopPlacedEggsCommands": [ # Player command to top leaderboard of gathered placed eggs
        "topplacedall"
      ],
      "ShowTopRandomEggsCommands": [ # Player command to top leaderboard of gathered random spawn eggs
        "toprandomall"
      ],
      "ShowTopKillEggsCommands": [ # Player command to top leaderboard of gathered kill eggs
        "topkillsall"
      ],
      "ShowTopAllEggsCommands": [ # Player command to top leaderboard of gathered all eggs
        "topeggsall"
      ]
    },
    "DebugNavMesh": true, # Debug Navmesh logs
    "Debug": true # Debug plugin logs
  }
}
```


> [!TIP]
> Reward descriptions support dynamic translations. To add a custom description, add a new entry to lang/*language*.json using the key format recievedDesc.Reward_Name. Replace spaces in the reward name with underscores (_). Example: For a reward named Punkty sklepu, add the following entry:
```
  "hdte.received_desc.VIP": "[gold]★ [white]Enjoy your VIP benefits!",
```

> [!NOTE]
> This plugin also provides API/Contract for developers. 
- [ApiExamples](/ApiExamples/)
- [Modules](/Modules/)

## [💥] Model
[HuntDownTheEggs plugin now also comes in with few models depending on holiday.](https://github.com/letiPoorPlugins/HuntDownTheEggs_Models)  

## [💖] Special thanks to:
- [Exkludera Gift Packages](https://github.com/exkludera/cs2-gift-packages) - Basically yoinked the whole idea of gifts and how to make presents/triggers/hookentityoutput,
- [Mesharsky](https://github.com/Mesharsky/) - Complete plugin structure rework and for fixing issues.
- [CS2-Ranks](https://github.com/partiusfabaa/cs2-ranks), [SimpleAdmin](github.com/daffyyyy/CS2-SimpleAdmin) used as an examples of Dapper and MySQLConnector, 
- [Dliix66](https://discord.com/channels/1160907911501991946/1311638450881167523/1311973008122052618) - Method for glow effect,
- [21Joakim](https://gist.github.com/21Joakim/6a264623e4ac8ae217e0eb15fc43e3e5#file-navmesh-cs) - Navmesh,
- [oqyh / GoldKingZ](https://github.com/oqyh/cs2-Prop-Health-GoldKingZ) - OnTakeDamage function for props,
- CounterStrikeSharp Discord,

## [🚨] 
Plugin is written by me. That means it might be poorly written and have some issues. Sometimes I have no idea what am I doing but when tested it works.
It was written using a lot of research and also a chatgpt when I was stuck. Any help and or advice on the code is welcome. Thanks!
