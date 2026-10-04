# SosariaAI

AI-enhanced bots for ModernUO that try to make an Ultima Online world feel
alive.

SosariaAI is a plugin for the [ModernUO](https://github.com/modernuo/ModernUO)
server. It fills a private shard with characters that live like players:
they work, trade, hunt, talk, join guilds, die, and come back. Reds hunt on
Felucca, and blues hunt the reds.

**Status: work in progress.** Many behaviors are new and still have bugs. Help
is welcome. See [Helping](#helping).

## How it works

- Each bot is a real player character (`PlayerMobile`) with no client and no
  account. The engine treats it as a player: notoriety, murder counts, guilds,
  parties, death, ghosts, and resurrection are the game's own.
- Game rules in the plugin drive every bot. The rules run the whole world with
  no AI model at all.
- AI models are optional. A chat model can write speech, personas, and plans. A
  small decision model can pick jobs and fight stances at big moments. When a
  model is off, slow, unsure, or over budget, the rules decide.
- Every bot stays in the world while the server is up. Nobody logs out.
- The bots follow the era the shard runs (T2A, ML, or later).

What the bots do, and the rules they follow, is in
[docs/behavior.md](docs/behavior.md).

## Requirements

- ModernUO, checked out next to this repository (`../ModernUO`). The project
  builds against its source.
- The .NET 10 SDK.
- A licensed Ultima Online client for the game data. SosariaAI ships no game
  data.

## Install

1. Build. The DLL goes straight to `../ModernUO/Distribution/Assemblies/SosariaAI.dll`.

   ```bash
   dotnet build src/SosariaAI/SosariaAI.csproj -c Release
   ```

2. Add `"SosariaAI.dll"` after `"UOContent.dll"` in
   `../ModernUO/Distribution/Data/assemblies.json`.
3. Start the server from `../ModernUO/Distribution/` with `dotnet ModernUO.dll`.
4. On a fresh world, open `[SosariaPanel` as an Administrator and press First
   Time Setup. It places the world's items and builds the navigation graphs.
   Then the characters come in.

Restart the server after each new DLL. There is no hot reload.

## Runtime files

The plugin writes only its DLL and the files under:

```text
ModernUO/Distribution/Configuration/sosariaai/
```

First boot creates missing files and keeps existing operator edits. Do not
delete config as a normal upgrade step.

| Path | Contents |
|---|---|
| `characters.json` | Facets, population, rosters, and rules. See [docs/characters-config.md](docs/characters-config.md). |
| `brain.json` | AI model providers, routes, limits, and budgets. See [docs/brain-config.md](docs/brain-config.md). |
| `personas/`, `persona-parts/` | Authored personas and the part pools copies are built from. |
| `personas-generated/` | Personas the chat model wrote, one per copy. |
| `talk/` | One text file of lines for each talk category. |
| `nav/` | Nav graphs and destination catalogs. |
| `memory.db` | Long-term memory (SQLite): the people met, bonds, shared adventures, places seen, and shard news. Real players are included. The plugin never deletes it. Its `-wal` and `-shm` files sit beside it while the server runs. |
| `memory/` | The old day book files. No longer used; left on disk for the operator to remove. |
| `activity.log`, `activity.prev.log` | Per-character activity, this boot and the last. Written when `logActivity` is `true`. |
| `status.html` | The status page. |
| `sosaria-list.txt` | The last `[Sosaria` list. |
| `live-proof-*.tsv` | Population check results. |

The console shows boot lines, warnings, and one summary line a minute: people in
the world, steps done, steps failed, walks with no route, and plans given up.
ModernUO's own logs in `Distribution/Logs/` can mention SosariaAI commands, but
they are not plugin files.

## Staff commands

| Command | Access | Result |
|---|---|---|
| `[Sosaria` | Counselor | Write every character with its location, work, power, and goal to `sosaria-list.txt` and the console. |
| `[SosariaGo <name>` | GameMaster | Go to a character, on any facet. |
| `[SosariaBring <name>` | GameMaster | Bring a character to you. |
| `[SosariaWatch <name>` | Counselor | Show one character's state, persona background, voice, idle line, choices, its bond to you, and its recent thoughts. |
| `[SosariaMemory <name>` | Counselor | Show one character's long-term memory: its warmest bonds (name, tone, score, shared adventures, where and when they first met) and its newest adventures (when, kind, summary, place, who). |
| `[SosariaSay <name> <text>` | GameMaster | Make a character speak, for a test. |
| `[SosariaRoute <x> <y> <z>` | Counselor | Explain the walk from you to that tile: tile router result, nearby graph nodes, graph route. |
| `[SosariaRoute <name>` | Counselor | Explain the walk from that character to you. |
| `[SosariaTile [<x> <y> <z>]` | Counselor | Show what the walkers see at the tile under you, or at the given tile: land, statics, and the stand checks. |
| `[SosariaPanel` | GameMaster | Open the staff panel: live counts, staff travel to towns and dungeon doors, a status page write, and the First Time Setup button (Administrator). |
| `[SosariaStatus` | Counselor | Write `status.html` now, with fresh counts and the stuck-character list. |
| `[SosariaRebuildNav` | Administrator | Rebuild navigation from ModernUO data. It waits while the world waits for First Time Setup. |
| `[SosariaLiveProof` | Counselor | Run a timed population check (a sample every 30 seconds for 20 minutes) into `live-proof-*.tsv`. One also starts 15 seconds after the server starts. |

The server console also takes `sosaria` and `sosariawatch <name>`.

## Tests

```bash
dotnet test tests/SosariaAI.Tests/SosariaAI.Tests.csproj -c Release
```

`dotnet test` builds the plugin in Debug into the same
`Distribution/Assemblies` folder. Build Release again before you start the
server.

Tests in `tests/SosariaAI.Tests/RealMap/` run nav code on the real Felucca
tiles. They read the UO client folder from `SOSARIAAI_UO_DATA`, or else from the
first `dataDirectories` entry of
`../ModernUO/Distribution/Configuration/modernuo.json`, the server data from
`../ModernUO/Distribution/Data`, and the saved graph and catalog from
`../ModernUO/Distribution/Configuration/sosariaai/nav`. They write nothing, and
they skip when the data is missing. The full Felucca build test is slow and
runs only with `SOSARIAAI_FULL_NAV=1`.

## Helping

- Read [AGENTS.md](AGENTS.md) first. It has the code rules, the project layout,
  and how to read a run's logs. It applies to people and AI agents alike.
- [docs/behavior.md](docs/behavior.md) says what the bots should do.
  [docs/ultima-online-player-activity-corpus.md](docs/ultima-online-player-activity-corpus.md)
  says what real UO players did.
- The build must have zero warnings, and the tests must pass.
- A bug report is most useful with the `activity.log` lines that show it.

## Legal

SosariaAI is not affiliated with the owners of the Ultima Online trademark. It
ships no game data. You must own a licensed client and provide its data.

SosariaAI uses the GNU GPL v3.0. See [LICENSE](LICENSE).

Some parts of SosariaAI were inspired by uo-offline. No code was copied verbatim.
