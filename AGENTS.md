# SosariaAI: rules for contributors and agents

Read this file before you change this project. It applies to people and AI
agents alike.

## Purpose

SosariaAI is a ModernUO plugin. Its goal is an offline shard that looks and
behaves like a populated live Ultima Online shard. See [README.md](README.md)
for install, runtime files, and staff commands.

- [`docs/behavior.md`](docs/behavior.md) is the product contract: what the bots
  do and the rules they follow. Keep it true to the code.
- [`docs/ultima-online-player-activity-corpus.md`](docs/ultima-online-player-activity-corpus.md)
  is the source of truth for real player behavior.

## Stop rules

Stop and ask before you:

- edit any repository other than this one, outside the ModernUO exceptions
  below;
- delete or replace config, navigation, world, save, or operator data;
- change population size, enabled facets, PK counts, model budget, or API keys;
- add staff-like teleporting as a normal character action;
- weaken an era or facet rule to hide a bug;
- make a destructive git change.

## Repository scope

This repository is the only source to edit. ModernUO, and every other
repository next to it, is reference only. Read them when useful. Do not edit
their source, documents, settings, assets, or git state.

ModernUO has only these narrow write exceptions:

- Build SosariaAI to `../ModernUO/Distribution/Assemblies/SosariaAI.dll`.
  Related output for this DLL, such as its PDB and deps file, is allowed.
- Add `SosariaAI.dll` to `../ModernUO/Distribution/Data/assemblies.json`.
- Let SosariaAI read and write its own runtime files under
  `../ModernUO/Distribution/Configuration/sosariaai/`.

You may run ModernUO and read its source, data, saves, logs, and terminal
output. Do not change those files. A build, test, run, or diagnosis request
does not give permission to edit ModernUO source. Adapt this plugin to the
ModernUO API.

## Runtime rules

- Every plugin-owned runtime file belongs under
  `../ModernUO/Distribution/Configuration/sosariaai/`. The plugin must not
  create its own files in `Distribution/Logs`, `Data`, `Saves`, or other
  ModernUO folders.
- Create defaults only when a config file is missing. Preserve operator edits.
  Nav graphs can change when `nav.rebuildOnBoot` is true. Do not delete config
  or nav files unless the operator explicitly asks.
- Every plan character stays in the world while the server is up. There is no
  parking and no logout. To shrink the population, lower the count in
  `characters.json`; at boot, saved characters over the count are removed.
- Behavior and social state persist across saves.
- Planning is event-driven. It must not poll a model while idle. Paid chat
  calls obey hourly and daily call caps. Paid System One calls (such as Jev)
  obey only the hourly input-token cap.
- `PresenceFocus` sets think rate from client range. `PathSearch` runs Dijkstra
  off the world thread. Neither has a config key. Do not add a second
  scan-pacing system next to `ScanPace`.

## Reading a run

The operator runs and restarts the server. Do not boot it yourself unless asked.
Stack fixes, build once, then ask for a restart.

With `logActivity` on, per-character lines go to `activity.log`, not the
console. Read that file to see what characters did.

Far from every client, slower think and skipped greetings are intended. They
are not a missing population.

To find a loop: strip the timestamp from each line, replace digits with `N`,
then `sort | uniq -c | sort -rn`. A line that repeats for one character every
few seconds is a bug. Trace it to the root cause before changing code.

Every ten minutes the activity log can have these census lines. A line is left
out when it has nothing to report.

- "Walk stalls": the five worst stall tiles and their legs. Start a stall hunt
  there.
- "Crafts in the last 10 minutes": pieces made per trade, sessions begun,
  pieces sold, stock bought from gatherers, and exceptional pieces called out.
  A trade at zero there is an empty shop.
- "Dungeon misses": for each live fighter above ground, the first thing that
  keeps it there (a red, a goal that is no job, no door in reach, no hall that
  fits its power, a delve the scorer bars, or dice that landed on another job).
- "Supplies low": for each live person low on fighting supplies, the supply and
  why it is not refilled (no gold, no shop in reach, or can refill).

A failed step names its reason in its end line, for example
`ended VendorBuy (Failed: nothing to buy)`. Group failures by skill and reason
before you look for a cause.

Group "has no route" lines by target and by the skill that started them. Use
`[SosariaRoute` at the start tile and `[SosariaTile` on a doubtful tile to see
why a walk fails. A tile route covers a walk inside one town; a graph route
covers the world and its gates.

## Layout

```text
docs/                         behavior contract, config references, player corpus
src/SosariaAI/Admin/          staff panel, status page, First Time Setup, census, fleet watchdog
src/SosariaAI/Behaviour/      goals, needs, meetings, parties, presence
src/SosariaAI/Combat/         threats, builds, recovery, PK
src/SosariaAI/Configuration/  config files and defaults
src/SosariaAI/Defaults/       embedded default persona parts
src/SosariaAI/Deliberation/   optional model planning, chat, and System One decisions
src/SosariaAI/Economy/        trade, vendors, gear, prices
src/SosariaAI/Logging/        loggers, activity log, minute summary
src/SosariaAI/Memory/         what a character remembers (see below)
src/SosariaAI/Migrations/     save schema versions for the serialization generator
src/SosariaAI/Mobiles/        the character, its drivers, and the staff commands
src/SosariaAI/Navigation/     graphs, routes, world generation, path workers
src/SosariaAI/Population/     session hours (day parts, idle fixtures off-hours), minute pulse, murder decay, live proof
src/SosariaAI/Skills/         world actions
src/SosariaAI/Social/         guilds, gossip, talk lines, danger map, shard events
src/SosariaAI/Spawning/       roster binding, placement, persona composition
tests/SosariaAI.Tests/        automated tests
```

Long-term memory is one SQLite file, `sosariaai/memory.db`, behind
`MemoryStore.Shared`: people, one-way bonds, shared adventures, places seen,
and shard news. Game code reads and writes its RAM only; one writer thread
drains changes to the file. The plugin never deletes `memory.db`. The old
`sosariaai/memory/*.tsv` day book files are no longer used and stay on disk.

Each mobile has one `CharacterMemory` facade. It holds the RAM-only `Working`
set (recent thoughts, recent speech, skill streaks, the open conversation), the
`Danger` and `Unreachable` spot lists, and writes the character's bonds and
places seen to the store (`ShiftBond`, `Met`, `SawPlace`). `AdventureTracker`
makes adventures. Shard-wide news lives in `Social/EventJournal.cs`.

Read long-term memory through `Memory/Recall.cs` (bonds, adventures, tellable
adventures, prompt facts) and `Behaviour/MemoryChoiceRules.cs` (friend, enemy, friend
deaths). Never key a memory on a name: use `PersonRef`.

A rest that must last through a restart is a rule clock on the character
(`ClockAt`, `StartClock`; the names are in `Behaviour/RuleClock.cs`). Job
target streaks live in `TargetStreaks` through `JobTargetRest`. Do not keep
such state in a static dictionary.

## Code rules

- No stubs, placeholders, dead code, or duplicated code.
- Use clear names and named constants.
- Use English in code, comments, and documents.
- Follow `../ModernUO/dev-docs/`.
- Target `net10.0` and C# 14. Treat warnings as errors.
- Only the main game thread may touch the world (`Mobile`, `Item`, `Map`,
  timers). Graph search may run on `PathSearch` workers. Those workers may
  read only a frozen `NavGraph` and the `PathSearchBars` data they are given,
  which nobody changes after it is handed out. They return paths with
  `Core.LoopContext.Post`.
- Never log an API key.

## Finish check

Before you say a task is done, confirm all applicable items:

- The change matches the operator request, `docs/behavior.md`, and the corpus.
- `docs/behavior.md` and the config references still match the code.
- No other repository changed.
- No operator config or save was replaced.
- The DLL uses the current ModernUO API.
- The build has zero warnings and zero errors.
- Relevant tests pass.
- Travel finishes at the real destination.
- Characters do not repeat the same completed or failed action each tick.
- Model calls follow a real event and their cooldown.
- Live output has no new exception, tight loop, or crowd pile.
- The final report says what changed, whether it worked, and what to do next.

## Git

Do not run a git command unless the operator asks.
