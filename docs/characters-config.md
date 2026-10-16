# characters.json reference

`characters.json` sets the facets, the population, the rosters, and the rules
for work, fights, scuffles, and navigation. It lives here:

```text
ModernUO/Distribution/Configuration/sosariaai/characters.json
```

First boot writes the file when it is missing. Later boots keep your edits. The
server reads the file only at boot, so restart it after each edit. Invalid JSON
does not crash the server: the log names the file and the line, and the plugin
uses safe defaults for that file.

A key that is not in the file gets its default, except in `maps` (see
[maps](#maps)). A value of 0 or less gets the default where the table says so.

Far-character think rate and path-search workers are code constants. They are
not in this file.

## Top-level keys

| Key | Default | What it does |
|---|---|---|
| `logActivity` | `false` | `true` writes per-character lines and the ten-minute census lines to `activity.log`. |
| `musingIntervalMinutes` | `8` | The least time, in minutes, between two tries at an unprompted line by one character. A try also needs a new event. 0 or less uses 8. |
| `feluccaGuardedMoongates` | `["Britain", "Moonglow", "Jhelom"]` | The Felucca moongates, by town, that keep their guards. See [Guarded moongates](#guarded-moongates). |
| `lifeRoutines` | built-in set | The routines every character shares: `tavern`, `bankcrowd`, `visit`, `sightsee`, `house`, and `stable`. Each entry has `enabled` (default `true`), `weight` (default 1, how often a character wants it), `description`, an optional `requiredPower`, and `steps`. `"enabled": false` takes the routine away from every character. A roster entry that has its own routine or choice of the same id keeps it. A missing or empty block uses the built-in set. |
| `housePlots` | `britain-east` at (1755, 1474, 0) on Felucca and Trammel | Where a character puts the house it buys. Each entry has `map`, `x`, `y`, and `z`. A character takes the first plot on its home facet, or the first plot with no `map`. With no plot that fits, it uses the default spot. The default spot is in Britannia, so give a [custom map](#custom-maps) its own plot. |
| `career` | see [career](#career) | Work, fight, and group rules. |
| `townScuffles` | see [townScuffles](#townscuffles) | Small Order and Chaos fights in towns. |
| `nav` | see [nav](#nav) | Navigation graphs. |
| `maps` | see [maps](#maps) | Which facets have people, and how many. |
| `facets` | see [facets](#facets) | The roster and places for each facet. |

## career

| Key | Default | What it does |
|---|---|---|
| `threatMultiple` | `1.15` | A character starts a fight only when the foe's threat is at most its own power times this value. Its party adds to its power, and no healing takes some away. The same multiple sets which hunt spots are in reach. |
| `minThreatToFlee` | `40` | The threat that makes a character run. A worker runs from it. A fighter runs only when the fight is also too much for it. A creature below it counts as harmless. A hunter does not go onto hunt ground this dangerous when the ground is too much for it. 0 or less uses 40. |
| `goldReserve` | `200` | Gold a character keeps back when it buys gear. Bank gold counts toward it first. 0 keeps none. A negative value uses 200. |
| `ignorePriceLimits` | `false` | Test switch. `true` lets a character spend all its gold on gear and ignores `goldReserve`. Vendor prices still apply. |
| `vendorSearchRange` | `8` | How far, in tiles, a character looks from its stall spot for a vendor that buys its goods. 0 or less uses 8. |
| `houseGold` | `43800` | Gold, in the pack and the bank, a character needs before it buys a house. |
| `leisureRadius` | `200` | How far, in tiles, from home a character goes to look at a place. |
| `leashRadius` | `400` | How far, in tiles, from home a character goes to hunt and to sell. A character far from home with no trip goes back. |
| `partyInviteRange` | `8` | Tiles within which a party member adds its power to a character's side (fight, flee and hunt odds), and how far a character looks for a guild-war enemy to fight. |
| `inviteAnswerSeconds` | `60` | How long, in seconds, a character waits for a player to answer its party invite. |
| `inviteCooldownMinutes` | `10` | How long, in minutes, a character waits after an unanswered invite before it asks anyone again. |
| `partyFollowRange` | `12` | How far, in tiles, a hunter in a party with a player may be from that player before it stops to find the player. |
| `deathAvoidHours` | `24` | For this many hours after a death, a character keeps away from the place where it died, unless it is strong enough for that place. It also keeps off its hunt ground or dungeon for this long after a warm friend fell there. 0 or less uses 24. |

## townScuffles

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | `false` stops all town scuffles. |
| `townGapMinMinutes` | `20` | The shortest wait between two scuffles in one town. |
| `townGapMaxMinutes` | `45` | The longest wait between two scuffles in one town. |
| `shardGapMinutes` | `5` | The shortest wait between two scuffles anywhere on the shard. |
| `maxActive` | `3` | The most scuffles at one time on the shard. |
| `maxSide` | `3` | The most fighters on one side. |
| `maxFighters` | `6` | The most fighters in one scuffle. |
| `bankClearTiles` | `20` | No scuffle starts this close to a bank. |
| `peaceClearTiles` | `12` | No scuffle starts this close to a healer, a shrine, or a moongate. |
| `timeLimitSeconds` | `150` | The longest scuffle, 30 to 180 seconds. |
| `fighterRestMinutes` | `30` | How long a fighter rests from scuffles after one. |
| `pairRestMinutes` | `120` | How long the same Order and Chaos pair waits before it scuffles again. |
| `callTiles` | `150` | Faction members this many tiles from the street spot hear a call. |
| `gatherMinutes` | `4` | How long a call waits for its fighters. Then the scuffle starts with those who came, one a side at least. |

## nav

| Key | Default | What it does |
|---|---|---|
| `rebuildOnBoot` | `false` | `true` builds the nav graphs again at each boot. |
| `gateCost` | `400` | The cost of one moongate or teleporter step in the route search, in tiles of walking. A higher value makes a walk win over a gate. A value below 0 uses 400. Only the `Traveler` route helpers read this key. The main walk planner and the red gang reach always use 400. |

A nav build walks the world's own items: decoration, doors, and teleporters. A
fresh save has none until First Time Setup places them, and a graph built on
that bare ground runs roads through furniture. So while the world waits for
First Time Setup, nothing is built: `nav.rebuildOnBoot` and `[SosariaRebuildNav`
wait, the saved graphs load as they are (or none load), and the boot log says
`Nav: the world waits for First Time Setup`. The last step of First Time Setup
checks the saved graphs against the items it placed, as any boot does
(`First Time Setup placed the world's items; checking the saved nav graphs
against them`), and then the characters come in. This takes about a second.
Only a facet with no saved graph is built then, or every facet when
`nav.rebuildOnBoot` is `true`. A full build on a set-up world
takes several minutes and gives back the graph that was saved, because the
setup places the same items each time. To build anyway, set
`nav.rebuildOnBoot` to `true` or use `[SosariaRebuildNav` after the setup.

## maps

`maps.<facet>` turns each facet on and sets its people. A facet is a map of the
server, by its name in ModernUO's `map-definitions.json`: `Felucca`, `Trammel`,
`Ilshenar`, `Malas`, `Tokuno`, `TerMur`, or the name of a
[custom map](#custom-maps). A name the server has no map for is skipped, and the
boot log says so.

The defaults below are what a new file writes. In an existing file, a missing
`enabled` reads `false` and a missing `count` reads `0`. A file with no `maps`
block spawns nobody.

| Key | New file | What it does |
|---|---|---|
| `enabled` | `true` for Felucca; `true` for Trammel except in the T2A era; `false` for the others | `true` puts people on this facet. |
| `count` | `200` for Felucca, `5` for Trammel, `200` for a custom map, `0` for the others | How many people live on the facet. The plan goes through the roster in order and repeats it until it has this many. When the count is below the roster size, the last roster people do not spawn, and the boot log says `Felucca count N is below its roster of M`. At boot, saved people over the count are removed. Saved people of a map the server does not have are kept, and the boot log names the map. |
| `pkEnabled` | `true` for Felucca and for a custom map with open PvP, `false` for the others | `true` makes some of the facet's people reds. Reds live only on a map with open PvP: its rules let a person harm a person, as Felucca's do. A missing key reads `true`. |
| `pkCount` | one in ten of `count` | How many reds (20 for a Felucca count of 200). It can not be more than `count`. A missing key uses the default. |
| `pkGangPercent` | `80` | The share of the reds, 0 to 100, that ride in gangs of two to four. The other reds hunt alone. A missing key uses the default. |
| `openPvp` | not written | `false` stops PvP on a map whose rules allow it: no reds, duels, Order and Chaos fights, or thieves there. A missing key, or `true`, uses the map's rules. `true` does not open a map with Trammel's rules, because the engine stops the attack there. |
| `redHaven` | not written | Where the reds of this map live, bank, and rest. Write a region name of the map, such as `"Pirate Town"`, or a rectangle, such as `{ "x": 100, "y": 200, "width": 60, "height": 60 }`. A missing key uses Buccaneer's Den on a stock map. On a custom map, it uses a place from the map's nav catalog. See [custom maps](#custom-maps). |

## Custom maps

SosariaAI works on a shard with maps of its own, also a shard with one map that
is not named Felucca. A map is stock when its name is a stock name and its size
is the stock size of that map. Felucca and Trammel are stock at 7168 x 4096 and
at 6144 x 4096. Each other map is a custom map.

- First boot writes a block in `maps` and in `facets` for each map the server
  has. A custom map gets the Britain roster. When no stock Felucca or Trammel is
  on, the first custom map is on with 200 people.
- On a custom map, the Britannia spots of the roster do not set homes, banks,
  work, moongate trips, or hunts. Each bank of the map is a town, so a custom map
  needs bankers. Each person lives at one of the map's banks.
  Each person works at the nearest wood, mine, or shore that the nav build found
  near the bank, in the leash (`career.leashRadius`). A hunt goes to a hunt place
  of the map. The public moongates are the moongate items on the map. The log
  counts the banks, work places, and moongates that SosariaAI found.
- With two banks or more, the banks take Order and Chaos in turn. With one
  bank, the town has no side.
- PvP follows the map's rules, not the map's name. Reds, duels, Order and Chaos
  fights, thieves, and the red alarm live only on a map with open PvP. Use
  `openPvp` to stop PvP on a map.
- The reds' haven is the first enabled map with reds in `maps`. Set `redHaven`
  to choose it. Without `redHaven`, a custom map uses its unguarded bank that is
  farthest from the other banks, when the map has two banks or more. If not, it
  uses an unguarded dungeon entrance. If not, it uses the bank that is farthest
  from the others. The haven is a square of 65 x 65 tiles round that spot. The
  log names the spot. The shard has one haven.
- First Time Setup runs `Decorate`, `DoorGen`, `SignGen`, `TelGen`, `MoonGen`,
  and `GenChamps` only when an enabled map is stock. These commands place
  Britannia's items. On a custom map they do not run. The spawner import still
  runs for each map that has a folder in `Data/Spawns`.
- The guarded moongate setting changes only a stock Felucca.
- Give each custom map a house plot in `housePlots`.

## facets

`facets.<facet>` holds the content for one facet. A new file writes Britain
content for Felucca, for Trammel, and for each custom map. An enabled facet with
no block logs that it has no content block and spawns nobody.

| Key | What it holds |
|---|---|
| `roster` | The people. See [Roster entries](#roster-entries). |
| `areas` | Named rectangles: `<id>: { name, x, y, width, height }`. |
| `parties` | Authored parties: `{ id, leader, members, meetAt }`. |

A point is written `{ "x": 0, "y": 0, "z": 0 }`.

### Roster entries

| Key | Default | What it does |
|---|---|---|
| `id` | none | The fixture's id. Copies get an id from it. |
| `name` | none | The display name. |
| `persona` | none | The persona file id in `personas/`. |
| `spawn` | none | Its home point. |
| `returnAfterDeath` | 5 minutes | How long a ghost waits before it returns, as a time span. |
| `build` | worker | See [Builds](#builds). |
| `routine` | none | One list of steps. |
| `routines` | none | Named step lists: `<id>: [steps]`. |
| `choices` | none | The routines it picks between: `{ routine, weight, description, requiredPower }`. `weight` defaults to 1. |

### Builds

| Key | Default | What it does |
|---|---|---|
| `preset` | worker | `swordsman`, `archer`, `mage`, `thief`, or `tamer`. Any other value is a worker. |
| `style` | from the preset | `Melee`, `Archer`, or `Mage`. |
| `role` | from the preset | `Worker` or `Fighter`. |
| `veteran` | `false` | `true` trains every preset skill to grandmaster, fitted inside the era's 700-point cap. |
| `skills` | from the preset | Skill name to value. These win over the preset. |
| `stats` | from the preset | `{ str, dex, int }`. |
| `kit` | from the preset | Item type names, such as `"Katana"` or `"LeatherChest"`. A list that is not empty replaces the preset kit. |
| `canHeal` | from the preset | Whether it heals. |
| `healInterval` | 3 seconds | The least time between two heals. |

### Steps

Each step has a `skill`, the step kind, such as `GoTo`, `Lumberjack`, `Mine`,
`Fish`, `BankDeposit`, `VendorSell`, `Hunt`, `Dungeon`, or `IdleWander`. The
full list is in `src/SosariaAI/Configuration/SkillKinds.cs`. A step uses only
the keys its kind reads. The step kinds are in `src/SosariaAI/Skills/`.

| Key | Default | What it does |
|---|---|---|
| `target` | none | The point to walk to. The nav graph plans the way. |
| `range` | 1 for `GoTo` | How close to `target` counts as there. |
| `fillFraction` | 0.8 | The share of the carry limit a gatherer fills before it stops. |
| `duration` | 90 seconds | How long an idle step lasts, as a time span. |
| `stopBelowHitsFraction` | 0.4 | A fight step stops below this share of hits. |
| `dungeon` | none | On a `GoTo` step with no `destination` and no `target`: a place name to walk to, read as `destination` reads it. |
| `area`, `bankSpot`, `center`, `radius`, `points`, `minutes`, `destination`, `party`, `leaderId` | none | Read by the step kinds that need them. |

Older files with hand-made waypoints (`routes`, `route`, `reverse`, `via`) still
load. The loader skips those keys, and the step walks to its `target`.

## Guarded moongates

This setting changes only a stock Felucca.

The engine data puts every Felucca moongate pad in one guarded region, and in a
T2A-era shard the guards strike a red who steps on a guarded pad. At boot the
plugin turns those shared guards off and keeps them only on the pads that
`feluccaGuardedMoongates` lists. The default list is the gates as they were
played in 1999. Reds then travel by the other five gates, and the Yew gate is a
PK spot again. Reds keep away from the guarded pads and from guarded towns, but
may stand on a town's outskirts outside the guard line. Town guards do not
change. An empty list takes the guards off every pad.

## Personas

Persona files live in `personas/<id>.json`. Each one has:

| Key | What it holds |
|---|---|
| `id`, `displayName` | The persona id and its shown name. |
| `background`, `voice`, `likes`, `dislikes` | Text that sets who it is and how it talks. |
| `idleLines`, `greetings`, `returnLines`, `combatLines`, `lootLines` | Lines it says. |
| `drives` | `greed`, `caution`, and `valor`, each 0 to 1. A missing drive is 0.5. |
| `disposition` | `lawful`, `neutral`, or `outlaw`. |
| `jobs` | The trades it fits: `worker`, `fighter`, `thief`, `tamer`. Add a file with `jobs` set and it joins the pool. |
| `eras` | Optional. The era bands it fits: `t2a`, `ml`, `modern`. |
| `activeStartHour`, `activeEndHour` | Optional. Its waking hours. A fixture outside its hours stands about where it is. Unset hours are rolled per person. |

A fixture keeps its authored file as it is. A copy keeps the base persona's
`id`, jobs, and disposition, then fills the rest from part pools in
`persona-parts/<pool>.json`: origin, habit, want, voice, likes, dislikes, idle,
greeting, return, combat, and loot. Each part has an `id`, `text`, the `jobs` it
fits, an optional `exclude` list of part ids that must not share a person, and
optional `eras`. The rolls use the unique id, so a reboot rebuilds the same
person. The persona is not written to the save.

The default part library is built into the DLL: 2000 parts in each of the 11
pools, with each trade listed on at least 500 of them, so two people rarely
share a line. It needs no model. First boot copies a pool out only when its file
is missing, so operator edits are kept. To take a newer built-in library, delete
the `persona-parts/` folder once before the boot.

ModernUO's expansion picks the era band: T2A up to LBR, ML from AOS to ML,
modern from SA on. A copy only rolls voices and parts that fit the band;
untagged content fits every band. A fixture keeps its persona in any band, and
the boot log warns when that persona is tagged for other eras.

With a chat provider on, each copy can also get a persona written by the model.
See `personaWriter` in [brain-config.md](brain-config.md#top-level-keys).
