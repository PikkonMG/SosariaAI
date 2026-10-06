# Bot behavior

This file says what the bots do and the rules they follow. The code is the
final word. When this file and the code do not agree, fix the one that is
wrong.

Use [`ultima-online-player-activity-corpus.md`](ultima-online-player-activity-corpus.md)
for real UO player actions and era rules. Do not invent a generic MMO.

## Goals

The world must stay active when the operator does nothing. Bots try to:

- gather, craft, buy, sell, haggle, bank, equip, and improve;
- talk at banks, shops, taverns, homes, roads, and moongates;
- form parties, hunt, enter dungeons, recover, die, and resurrect;
- remember people, places, danger, losses, and unfinished goals;
- join guilds, make enemies, and fight guild wars;
- use Felucca for theft, PK action, anti-PK action, and corpse risk;
- move without staff-like travel or permanent gate piles;
- stay in the world the whole time the server is up: nobody logs out;
- keep working when language-model providers are off.

Banks and moongates are traffic points, not storage lots. Dungeons and
wilderness need active hunters. Buccaneer's Den and busy Felucca moongates need
attackers and people who react to them.

Repeated action loops, route failures, greeting floods, and idle model calls are
defects.

## Era

Places follow the era. ModernUO loads Blighted Grove, Sanctuary, and other
Mondain's Legacy places in every era. A Second Age shard drops them
(`EraRules.PlaceFloor`). A character that a save left inside a dropped place
leaves it like a dungeon: first by recall, then by an exit pad, and after 5
minutes stuck, by the door.

The staff panel, the destination catalog, delve and party dungeon names, and
gossip know only the towns and dungeons that the running era's world has. A
place stays when a teleporter at the door its region records leads in (or the
door lies inside it) and the era's spawn files fill it. In a Second Age world
this keeps Sanctuary and Blighted Grove and drops the Painted Caves, the Prism
of Light, the Palace of Paroxysmus, and the Heartwood. "Misc Dungeons" is listed
as the dungeons it holds: the Britain Sewer and the Trinsic Passage, each at its
own teleporter.

## People

Every spawned character is its own person. The roster in `characters.json`
holds a few fixtures with authored routines. Every other slot rolls a person
from the roster's parts, seeded by the character id, so a reboot brings back
the same person:

- a trade: worker, fighter, tamer, or thief, in the mix set in `PersonMaker`;
- for about half the workers, a craft to live by (smith, tailor, carpenter,
  bowyer, alchemist, scribe, or tinker, in the mix set in `CraftCareerRules`).
  About one maker in seven is a grandmaster. The other workers gather;
- a base voice from the persona pool that fits that trade, then a composed
  persona unique to that copy (see [Personas](characters-config.md#personas));
- experience: veteran or green;
- a name, face, clothes, home town, work patch, and ambition of their own.

Armor follows the build. A mage who meditates wears cloth or leather, and a
tank mage wears studded at most. Heavier armor goes into the pack.

## Memory

A character remembers like a 1999 player: the people it played with, and what
they did together. Long-term memory lives in one file, `sosariaai/memory.db`.
It survives a restart and a world wipe. Real players are remembered too, by
their serial. A memory never keys on a name, so a new name keeps it.

**What is kept.**

- A bond from each person to each person met, one way, from -100 to 100. A
  heal or a raise, a greeting, a chat, a friendly duel, and an adventure shared
  on the same side warm it. A kill, an insult, and a failed haggle cool it. 20 or more is warm (a friend).
  -20 or less is cold (an enemy). A bond keeps where and when the two first
  met, how many adventures they shared, and the last deed that moved it. There
  is no cap on bonds.
- An adventure for each party outing, red put down, death, rescue, duel, house,
  first kill, and notable solo outing (mining, fishing, a hunt, a bank trip, and
  the like). It names everyone who was there and what each did: with the side,
  against it, healer, killer, or fallen. A party outing starts when a party with
  a SosariaAI character in it, real players included, enters a dungeon or
  starts a fight. It ends when the party leaves, splits, or has no fight for 10
  minutes.
- The places each person saw, and the shard news that gossip tells.

**How long.** Deaths, red kills, rescues, house buys, first kills, and dungeon
runs with company are kept forever. Small moments fade after 30 days. Each
telling of an adventure adds 7 days. A bond that only came from greetings or
chats fades after 30 days without a meeting. A character removed from
`characters.json` stays in the memories of the others. Recent thoughts (the
last 12 lines a character thought) are kept in RAM only and start empty after a
restart.

**How memories are used.** No memory makes a model call of its own. Memories
only add facts to the calls that already run.

- Two old friends who meet bring up an adventure they shared, now and then:
  "Halvard! still sore from despise?" Small talk between them can turn to it
  too. Each telling makes the adventure last longer. A cold bond tells nothing.
- A chat prompt carries the character's recent thoughts (12 lines at most),
  its adventures of today and yesterday (8 at most), and, when a person is
  present, its bond to them and the 3 biggest adventures it shared with that
  person.
- A plan prompt carries the same, and one more list of 8 lines at most: its
  newest adventures and its recent thoughts, with up to 4 lines kept for the
  thoughts.
- A Jev decision carries no memory. A Jev check on whether to answer a person
  carries only the bond to that person.
- A greeting is warm, plain, or cold by the bond. A cold bond gets no answer to
  a greeting.
- A visit picks the person the character likes best and skips anyone with a
  cold bond. Following its friend scores higher, following its enemy lower.
- A hunter keeps off hunting for `deathAvoidHours` (24 by default) after its
  own death, and keeps off its hunt ground or dungeon while a warm friend's
  fall there is inside the same window.
- An LFG call is answered first by friends, guildmates, and people who shared 2
  adventures or more with the leader. A leader asks the player near it that it
  shares the most with, before a stranger.
- Gossip tells a party run with its leader, its members, and the dungeon it
  went to: "heard Halvard and Tamsin cleared despise".

The old `sosariaai/memory/*.tsv` day book files are no longer read or written.
They are left on disk; the operator may remove them.

## Crafters and the market

A crafter's day is its trade. The craft job holds about five phases in six, at
any hour, and the bank, the shops, and the inn fill the rest. Crafters spend
long sessions at the forge or the shop counter.

**Stock.** Raw stock goes from player to player first. A crafter that cannot
get stock at its station walks to a bank in reach where stock is held up, if
there is one, and shouts WTB. It goes only when its purse, bank balance
included, pays for one unit. A crafter with no seller walks once to another
bank with stock held up, then tries one more NPC shop. A smith, carpenter, or
bowyer digs or cuts its own stock when it runs low, unless a gatherer at the
bank sells that stock. When it runs out, it does not wait at the station: it
tries the sellers at the bank, then goes to its harvest. Carpenters and bowyers
ask for logs: the T2A craft lists burn logs, and boards stand in for them.
Blank maps, blank scrolls, and empty bottles come only from shops, so a crafter
does not wait for a seller of them.

**Gatherers.** A miner or lumberjack in reach brings its load to that bank
before it sells to the crafters at the shop. It reads the want when its selling
trip starts and again at the shop. At the bank it holds up the lot for the
crafter that waits there now, then the rest for the next one. A gatherer keeps
none of its harvest back, though its routine fletches or smiths now and then.
Only a crafter who lives by the trade and gathers its own stock keeps a hundred
units back.

**When a craft starts.** A craft job, map drawing too, starts only with the
materials for one piece carried, held up at a bank in reach, or on a shelf in
reach at a price the pack pays for. A tool on a shelf counts only when the pack
can pay its price. A crafter with a spare tool keeps no gold back for a new
tool. A crafter waits for the mana, stamina, or hits that a try needs, and
pauses 3 to 8 seconds between tries.

**What it makes.** A crafter makes goods that someone buys: a vendor near its
station, by the engine's sell tables, or people who want them (fighters' gear,
arrows, bandages, potions). The pick weighs the gold a try earns, and an item
that still teaches weighs more. When no piece makes a profit, it makes the
cheapest piece that still teaches it. A crafter prices a material at the
gatherer's price only when people bring that stock to it. Every other material
costs the full shelf price, so a crafter drops a potion or scroll that loses
gold when another piece earns gold. A grandmaster answers the T2A maker's-mark
question with its mark, so its exceptional pieces come into the pack. An item
that makes nothing in two batches is dropped for the session, and three empty
batches in a row end the session. Anyone who works a station keeps the stock it
burns: a cook does not sell the meat on its fire to the next cook.

**Selling.** A crafter's exceptional pieces are its shop stock. They never go
to an NPC shop. It carries up to eight and banks the rest, and takes more out
when its pack runs low. Between batches it keeps a shop at its station: it holds
its best piece up, shouts a WTS every two to four minutes when somebody is in
sight, lists its best four pieces when asked "what do you have", and quotes any
piece by name. A "wtb" for a piece it stocks gets an answer. Each piece has one
price from the market table: a GM plate chest asks 1,800 to 4,200 gold, a GM
katana 700 to 2,000. Plain goods still sell to the NPC shop, and a crafter still
hawks its best pieces at the bank. A hawker does not wait for a shopper: every
15 seconds a character on the bank floor that wants the goods and can pay
answers. It walks over and haggles, and the deal closes in the engine's trade
window. A "wtb" call draws a seller that carries the goods. A fighter on a gear
trip buys a crafter's piece at a bank in reach when its purse there, bank
balance included, meets the ask, or walks to a crafter's station for a GM piece
that betters what it wears. If not, it goes to the NPC shop. A house owner puts
goods on its vendor at market prices and collects the takings. Browsers buy
from player vendors in reach. A red plans no sale under the guards.

**Orders.** "Can you make me a GM katana" or "I need a full plate suit" to a
crafter at its station gets a quote: the market price plus a tenth, half paid
up front, and the deposit stays with the crafter. "Yes" opens the trade window
for the deposit. The crafter takes up to three orders, and only work its trade
makes at even odds that can come out exceptional: a grandmaster smith makes an
exceptional plate chest about one try in twenty, so it can take that order.
It makes orders first and holds each exceptional piece for its buyer. When the
buyer comes by or asks "is my order ready", it opens the window with the work
for the rest of the coin; a suit comes in a bag. An order not picked up in
three days goes to its shop stock. A crafter with orders open stays in town.
Fighters order too, for the GM version of a plain piece they wear.

**Supplies.** A character sells the supplies it has above its own restock
target (reagents, bandages, arrows, bolts, and recall scrolls) to a character
that is short, at the bank, in a trade window. It sells no more than the buyer
lacks. When the shop shelves are bare, a shopping trip goes to the bank and
calls "wtb" before it fails.

**Banking.** A bank trip banks only what the box takes and what the supply draw
would not take back. A thief keeps its lockpicks in the pack, as others keep
arrows, bandages, reagents, runes, and recall scrolls. When the box is full,
the pieces it refuses stay in the pack, and no new trip starts for them. A heavy
purse is a reason to go to the bank only when the box still takes gold.

**Vendors.** A vendor keeps what it buys on its resale shelf for an hour, by the
engine's rule. The engine clears the older pieces only when a client opens the
buy list, so each sale by a character clears them too, and the vendors' shelves
stay small.

## Gathering

A gatherer works to the body's carry limit, not to the backpack's. The engine
taxes each step of a body past its limit (40 plus 3.5 per point of strength)
and blocks the step when stamina runs out. A lumberjack, miner, or fisher stops
at its fill share of that limit, or when one more swing would pass it. A
gatherer that already carries a full load does not start a harvest: it sells
or banks first. A pack beast's load comes back into the pack only up to nine
tenths of the limit. A person who is overloaded anyway puts the haul on its
pack beast, then into the bank box at a banker, and drops the rest on the
ground, heaviest pieces first, until it can walk.

A harvest works eight minutes from its arrival at the patch, not from the start
of the walk. A whole trip, walk and work, ends after 20 minutes. A copy whose
own site has no patch with work tries the other sites of its harvest within its
leash, nearest first. When every site is bare, it does no harvest until the
engine's banks refill.

## Tamers

Tamers are mage-tamers, most of them Expert or better. A tamer keeps only a pet
strong enough for its tier: below Adept, a polar or grizzly bear (below Expert,
a dire wolf too); from Adept, a grizzly, a lava lizard, a hell hound, or a drake
as its skill reaches them; and drakes, dragons, nightmares, and white wyrms
from Master on. Nobody keeps a bull, a black or brown bear, or a giant spider,
and a horse only as the mount it rides. A tamer keeps one fighting pet and,
while it rides none, one mount. It tames a new fighter only when that beast is
at least 1.5 times as strong, and then lets the old one go. Every other pet is
let go.

A tamer practises only while its Animal Taming still gains by the engine's rules
(below its cap, set to rise, room in the 700 points), on a beast with at most an
even chance to tame, and from Expert on never on wolves, cats, or harts. It lets
its practice beasts go at once. The tame took each one off its spawner, which
has already spawned a replacement, so a released beast that nobody tames again
leaves the world a minute later instead of three days later. One taming trip
lets at most four practice beasts go. After that its taming rests for two hours
while it does other work.

Two tamers at most work one area at a time. An area is a whole dungeon, or open
ground within 64 tiles. The next tamer goes somewhere else. A taming trip goes
only to a ground where a live beast of the wanted kind stands within 30 tiles
now.

**Stables.** A tamer uses the animal trainer's own stable, at 30 gold a pet from
the pack or the bank, up to the trainer's limit. It looks at its pets between
two steps and while it stands about. It stables them when its slots are too full
for the beast it wants to tame, and when a pet is below half its hits and it has
no bandage or heal spell. It claims them before its next job while no fighter is
out, when its taming session ends, or when the hurt pet is back to 90% of its
hits (after one hour at most). It stables again only 30 minutes after a claim. A
stabled pet is not lost. A tamer with pets in the stables and no stable with a
live trainer within its leash tames a new pet instead of walking to claim one.
The activity log writes `X stabled N pets at ...` and `X claimed N pets at ...`.

**On the road.** A tamer does not leave its pets behind. It stops for up to 15
seconds when a following pet drops more than 3 tiles back, and it steps onto a
teleporter only with its pets close, because the teleporter takes only pets
within 3 tiles. A pet out of the tamer's earshot is lost: the idle tamer walks
back for it. A lost pet is let go after 15 minutes, or after 30 minutes while
the tamer walks back for it.

**Healing.** A pet in bandage reach gets a bandage. A pet farther off, up to 12
tiles away and in sight, gets a spell, in a fight or out of one: Cure when it is
poisoned, and Greater Heal when it is below 70% of its hits (Heal when Greater
Heal is past the tamer's Magery or mana).

The save keeps the taming rest, the stable holds, and how long each pet has been
lost, so a restart does not end them.

## Parties

A healer in a party bandages a party member who is poisoned or below 85% of its
hits and stands next to it (one tile, the era's bandage reach). It uses a
bandage from its pack, as a player does, and leaves a scratch to heal alone. It
does not bandage a gray member: that heal would make the healer gray too. A
party member takes up the leader's foe only when its own blow is no crime. A
party member raises a fallen mate only within one walking leg (32 tiles).

A crew member does not follow its crew after the crew breaks up. It waits until
the leader gathers the crew again.

## Guilds, Order and Chaos

Guilds are real engine guilds. A guild war is a real war: enemies show orange,
a blow is not a crime, and town guards stay out of it. A blow between two
guilds starts a war for 6 hours, and each new blow restarts the 6 hours. No
blow by a red or on a red starts a war, and neither does a duel or a blow
between Order and Chaos.

Most guilds are ordinary (social, crafting, PvM, PvP, small friend groups), and
most people wear no tag. Order and Chaos are 4 of the 26 guilds, and two of them
are big zergs: about one fighter in seven wears each side. Only people with a
fighting build join them (tamers too); crafters, gatherers, and thieves keep
ordinary guilds. Order takes no red: a red never rolls into Order, and an Order
member who turns red leaves it for another guild, unless a player recruited it.
Chaos takes reds and blues.

Order and Chaos fight on sight (18 tiles, one screen) wherever they meet on
Felucca: in the wild, in town, and at the bank, the healer, and the moongate, as
on the 1999 shards. The blows are lawful, so the guards stay out. A war band
leader calls free fighters of its side from up to 400 tiles away. When the two
may not fight (the pair rests from each other, a side already has four, or the
odds would make it run), they trade words instead.

Order and Chaos do not share a home town. A fighter's home town is split by side:
of the bank towns in order, the first, third, fifth, and seventh are Order homes,
and the rest are Chaos homes. A fresh world that spawned both sides at one bank
opened with 32 fights in its first minute. Both sides still live all over the
map, so they meet on the roads, at the hunts, and in each other's towns. Everyone
else, reds included, homes as before. A saved person keeps the home it has.

A guild war keeps to the open. It starts only 18 tiles or more from a bank, a
healer, a moongate, or a shrine, and clear of the guard line, so its first steps
do not cross into one. A guild war fight that still reaches one stops there, and
both fighters are left alone for 2 minutes. A red passes over a victim that just
got away under the guards for those 2 minutes too.

A guild war or an Order and Chaos fight brings in free fighters of both sides
who stand near, up to four a side.

Only armed people take part in Order and Chaos fights, guild war draws, drafts,
war bands, and town scuffles. A person without its combat kit, or without a
weapon when its build carries one, draws on nobody there, and nobody there
draws on it. A person without its arms runs from a person who hits it. This
does not protect it from a red: a red picks unarmed victims too.

## Town scuffles

Now and then a small scuffle starts on a town street under the guards. The
`townScuffles` block in `characters.json` sets the numbers (see
[characters-config.md](characters-config.md#townscuffles)). The defaults are
below.

Only blues fight: no red, no gray, and no one with pets. Each side has one to
three fighters, and a scuffle has six at most. A town has one scuffle at a time,
and the shard has three at most. A town waits 20 to 45 minutes between
scuffles. A scuffle never starts within 20 tiles of a bank, or within 12 tiles
of a healer, a shrine, or a moongate.

Most scuffles are called. Once a minute, when the shard is due, an idle blue of
Order or Chaos who stands in a due town calls two or three of each side from
within 150 tiles to a street of that town away from the bank. The call goes out
in guild chat, and each called fighter walks to the spot. The scuffle starts
when all of them are there, or after four minutes with at least one a side. The
call lapses when a side has nobody, and the town can call again five minutes
later. A scuffle also starts when an Order and a Chaos member meet on such a
street. Nobody else joins; up to two people watch and call out.

A fighter that runs, leaves the ground, dies, or turns gray or red is out, and
nobody chases it. A scuffle ends when one side has nobody left, or after 150
seconds, and then the more hurt side yields. Each fighter then rests from
scuffles for 30 minutes, and the save keeps that rest. The same Order and Chaos
pair does not scuffle again for two hours.

The activity log writes `Town scuffle call in X: ...` for a call, `Town scuffle
call in X lapsed: ...` for a lapsed call, `Town scuffle in X: ...` at the start,
and `Town scuffle in X ended: ...` at the end.

## Reds and PvP

Reds are murderers. Buccaneer's Den is their home: they bank there and wait
there between runs. A red picks no victim inside the Den.

### Murder counts

Murder counts decay with time online, by ModernUO's `murderSystem` settings. The
engine defaults take one short-term murder every 8 hours and one long-term
murder every 40 hours. A red that the spawn plan made stays red. A red that
earned its counts goes blue again when its long-term count drops below 5, and
the activity log writes `X is blue again`.

### Guards

A living red never steps from open ground onto ground the town guards cover.
This holds for a flight, a chase, and any other walk. A red caught under the
guards recalls home when it can cast now. If it cannot, it runs to the nearest
open ground that a straight walk reaches, and only then walks home. A person
under the guards who is neither red nor criminal is safe there and has no
reason to flee.

Reds keep off every road node the guards cover, and off every walked leg that
cuts through guarded ground. Each time the nav graphs load (at boot, after the
First Time Setup check, and after `[SosariaRebuildNav`) the plugin reads
Felucca's guarded nodes and legs once and writes `Felucca roads: N nodes and M legs lie under the
guards; reds keep off them`. Walks add more guarded legs while the shard runs.
A red on open ground never plans a road across the guards. If no road keeps
clear of them, it recalls. Only a red that already stands under the guards
plans once more across them, by the least guarded way, and writes `has no road
clear of the guards from ... and crosses them by the least guarded way`. The
goal itself is never under the guards. Red ghosts get the same road bars as
living reds.

### Moongates

`feluccaGuardedMoongates` in `characters.json` lists the Felucca moongates that
keep their guards (default: Britain, Moonglow, Jhelom). A red takes a public
moongate, living or as a ghost, only between two unguarded Felucca pads. A
guarded pad stays barred to it. A red's last way home is the nearest moongate it
may use, never a guarded pad.

### Heat of battle

For 30 seconds after a blow dealt to or taken from another player-bodied person
(every character is one), no character takes a public moongate, steps through a
spell gate, or casts Recall or Gate Travel. A gray (criminal) person takes no
public moongate or spell gate at all.

A red that runs from a fight recalls home to the Den once it breaks contact,
that is, when every blow on it would land after the words of the spell end.
While the heat lasts, it keeps running. A runner with Hiding 30 or more hides
and stands still until the heat cools, then tries the recall again.

A runner with Hiding 30 or more hides once no attacker can see it, and stands
still while hidden. It does not bandage or drink while hidden, because that
shows it again. A chaser that cannot see its foe gives it up.

A fighter that a person keeps chasing turns and fights that person in two
cases. The first case is when it ran below 80% of its hits and healed back to
80% on the way, and the fight is one it would hold. The second case is when the
person is still on it 45 seconds after the run began. A worker, or a person
without its arms, keeps running.

A hunted runner that is cornered, or that cannot outrun the person hunting it,
stands and fights to the end of that fight. A fighter that loses a living foe
leaves that foe alone for 2 minutes.

### Gangs and hot spots

A red gang musters at its point in the Den for 15 to 90 seconds, then rides out
one way together: each by recall to one rune when every rider can recall, or
else on foot through the Den gate. At the camp, the first there wait up to two
minutes for the rest. A mate joins a gang's ride only when its own run can
start: its Conflict job does not rest, and a camp is in its reach.

A red that is run off its hot spot three times on one run gives the spot up and
rides home. A red whose pack is full or whose supplies are short does not ride
out. A red whose combat kit or weapon is gone does not ride out until it
re-arms in the Den, when the spare kit bag or the shops can arm it. With nothing
to re-arm from, it rides out as it is and strips what it can.

Nobody draws on a person who stands within 6 tiles of guarded ground: the fight
would end at the guard line the next step. This holds for a red's pick, a
blue's draw on an outlaw, and a guild war. It does not hold for Order and Chaos
fights, which do not end at the guard line. A draw also counts the people
already after the drawer.

### Blues in the Den

Blue sweeps ride out to clear reds off PvP hot spots, but a sweep never picks
the Den. A blue goes into the Den only on a Den raid, a posse (PK hunters near a
murder ride after the killer), or a PK hunter's Den run. PK hunters are a
quarter of the blue fighters at Adept or better. A quarter of their runs ride
into the Den, with at most two hunters there at once.

| Den raid rule | Value |
|---|---|
| Raids out at one time | 1 |
| Gap between raids | 20 to 40 minutes |
| Band size | 3 or 4 blues |
| Rest for each rider before its next raid | 3 hours |
| Time before a raid that is not home breaks up | 30 minutes |

The save keeps each rider's raid rest, and a leader's 30-minute rest after it
leads a war band, sweep, or raid, as a UTC time. A restart does not end or
lengthen a rest. Time while the server is down counts.

In the Den a blue starts no fight unless it is on a raid, a posse, or a hunter's
Den run, or an outlaw is hurting its friend. This holds for the lawful draw, the
gray watch, guild war, Order and Chaos, the draft, and a party follower. A blue
does not bandage an outlaw there either. No shop trip or murder report sends a
blue there. A red in the Den starts no fight on another red, not even in a guild
war, and does not answer a guild mate's call against one. A blue that rides into
the Den is fair game.

## Death and resurrection

A blue rides only to a murder report inside its leash that its router did not
just fail to reach.

**Helpers.** A helper raises a ghost with up to five casts or bandages. It waits
while its hands are busy (a cast, a cursor, a bandage, the recovery after a
cast). It stops when a foe fights it or when the ghost's killer stands near the
body. Nobody raises a ghost that it, or its pet, helped to kill, and nobody
raises a criminal or a red ghost under the guards. The helper checks this again
at each step up to the cast, and a character ghost refuses a raise that lands
while the guards want it. A red raises any red ghost it sees, asked or not. A
red ghost out of the guards calls a gang mate within 32 tiles, in sight or not,
before any other red. Another red must stand in sight within 20 tiles.

**Ghosts.** A ghost walks only to an ankh or a healer it can reach. It plans the
route before it sets out, over the roads and gates it may take, and picks the
site with the shortest trip. It skips a site it failed to reach for a while. A
ghost that finds no such site and no helper in sight, and makes no headway for
three minutes, leaves the world and stands up at home when its return-after-death
delay ends, instead of waiting 30 minutes. The log line is
`ghost was stuck N minutes (no healer, shrine or friend it can reach from
here)`, where N is the time since its death.

**Red corpse runs.** After a raise, a red goes back for its body when the body
lies on the same facet and clear of the guards, near or far, and it travels
there without a step onto guarded ground. When the body lies on another facet
or under the guards, or the walk fails 4 times or takes 10 minutes, it recalls
home to the Den. A raised red with an empty runebook and enough Magery for a
charge gets one free charge for that recall. The criminal flag, or a refusal
that passes, makes it wait for the recall instead of walking. Every red
runebook keeps an entry at the Den bank.

A person who dies again goes back for the earlier body if it still holds more
of the gear. It compares the worn pieces left in each body first, then all
items.

**Newbied gear.** A starting weapon and spellbook are newbied, as a 1999
character's were: they stay with their owner through a death. A body that gives
back no arms sends its owner to the bank and the shops, as a lost body does. A
person with no weapon spends its last gold on one: the gold reserve does not
hold that buy back.

## Spare kits

Each red keeps a spare kit bag in its Den bank box, and each blue fighter keeps
one in its own bank box, as a 1999 fighter kept a second suit in the bank. The
bag holds one of each combat piece of the build, with reagents (when the build
uses them) and bandages (when the build heals) for three restocks. After a
death the owner takes out the pieces it lacks at the bank, so a blue that a red
stripped dresses again.

A piece of the same role counts as the kit piece: a weapon of the build's weapon
row that uses the same ammunition, a shield when the build carries one, or armor
for a slot it covers and no heavier than it wears. At the bank, the owner stows
any loose piece that fits the bag, bought or picked up.

A red refills a low supply at home only when its bank box holds enough, or a
Den shop has every type it is short of on its shelf. The Den provisioner sells
arrows and bolts, and the Den healer sells bandages. No Den shop sells a
caster's reagents. A low supply that a red cannot refill does not hold it at
home: it fights on without it.

A red fills its bag in three more ways. A red that strips a body keeps the
pieces of the roles that its bag or its body lacks, within what it can carry,
and does not sell them. A red buys a spare at a shop out of the guards' reach
that stocks it; in the Den, the smith and the tanner sell weapons and armor. A
red can also buy a spare from a crafter at the Den bank. A red that finds no
shop out of the guards' reach for its piece buys no gear for 20 minutes. Reds
also strip gold and fight supplies (reagents, potions, scrolls, bandages,
arrows, and bolts) from the people they kill.

Count each path in `activity.log`: `for its spare kit` (kept off a body),
`bought a spare`, `put N spare pieces into the spare kit`, and `found no shop
out of the guards' reach`.

The gear shop's gold reserve (`career.goldReserve`) is kept in the bank: bank
gold counts toward it first, and only the rest stays in the pack.

## Everyday behavior

**Jobs and plans.** For each phase, a person rolls one long job: bank, hunt,
dungeon, travel, craft, shop, tavern, or idle. The job has a plan of steps. A
person runs only the steps it has the skill for, and it skips a step that has
nothing to do. The save keeps the plan and its step, and after a restart the
person continues at that same step. The hour of the person's own day changes
each pick. By day, work, trade, hunts, and delves weigh more, and the tavern
weighs less. In the evening, the tavern and the street weigh more, and work and
outings weigh less. At night, rest weighs more, and work, hunts, and delves
weigh much less. The job roll does not cut a crafter's own craft job at any
hour.

A fight holds the running job instead of ending it: the walk home or the errand
goes on from where it stood once the fight is over, with no new decision. A
fighter keeps one foe until a clearly nearer attacker or a higher-ranked foe
appears. A red warning goes to the same listener about the same red once in
five minutes, and each speaker says at most one warning or red scream in 30
seconds.

A recall or gate that fizzled is cast again when the recovery ends or when a
person steps off the landing. A visit picks a settled friend within 100 tiles
and follows that friend.

A character tends or studies a beast only when a beast is in reach. It searches
for the hidden only when someone hides in reach, disarms only a trapped chest
in reach, and picks a lock only with a lockpick and a locked chest in reach. A
lock picker whose last pick broke buys 20 at a tinker, or else at a
provisioner, as a supply. With no seller in reach it does not pick the job. A
poisoner coats a one-handed bladed or piercing weapon, or food, only with a
poison potion in its pack, and the engine's check runs in that potion's window.

**Thieves.** A pickpocket picks a mark with a free tile beside it and peeks up
to three times before it grabs blind or walks off. A thief works no crowd under
the guards: a caught lift turns it gray and the guards come at once. It works
the Den, the roads, and the dungeons.

**Failures.** A failed job writes its reason on its end line: `ended Visit
(Failed: the friend logged out)`. Walks, flights, and mounts name their causes
too. A job that fails the same way three times in a row at one target rests for
that target: a ghost, a mount, a friend, a dungeon, a stable, or the goal of a
walk. The rest starts at five minutes and doubles up to an hour while the
streak runs on (`rests ResurrectAid for 1234 5 minutes after 3 failures the
same way (...)`). The save keeps each streak and the end of its rest. The
planner does not work around a job that rests after repeated failure: the
remount, the pet keeper's taming trips and claims, and a thief's crowd work wait
for the rest to end. A wanderer whose walks to its spot keep failing strolls
where it stands.

**Houses.** Houses decay as on a live shard. When the sign of its house reads
"somewhat worn" or worse, an owner goes back and uses the sign. A house whose
owner stops coming falls, and then the owner may buy another.

**Danger on the road.** A town trip reads its planned road. When the road passes
a place the walker ran from, the walker recalls over that place or stops the
trip. A trip that a threat or a dangerous road stopped rests that bank for 10
minutes. A trip that a fight or a flight carried off its road does not walk back
past the danger. It recalls or ends. The check reads the tile walk as well as
the road, and counts a place run from only when the way leads nearer to it than
the walker stands: a run ends inside that place's 24-tile radius, and a way
that leads off from there is no way past it. A walk home that ends this way ends
with `the road there passes a place it ran from` and does not turn to the
moongate; the next walk home goes round the danger first. A walk home that fails
every way names the last walk's reason, for example `every way home failed: no
route to the goal`, and a failed walk to a dungeon door does the same.

A rider whose mount is too tired to run walks for one minute and then runs
again.

## World loop

ModernUO runs the world on one thread. `Mobile`, `Item`, `Map`, and timers stay
on that thread. SosariaAI does not give each character its own thread.

Near a logged-in player (24 tiles), or while in combat, hunting, or as a ghost,
a character thinks four times a second and still greets, notices, and muses.
Far from every client, it thinks once every two seconds. Greetings and nearby
player notices wait, though its speech timer still lets it muse. The character
still walks, works, scores a new job, and flees.

Scans stay paced (`ScanPace`): the danger scan every 1000 ms when watched and
every 2500 ms when far, the world scan every 1000 ms, and the ambient scan
every 2500 ms when watched.

Long graph walks (Dijkstra on the nav graph) run on background workers: one
less than the CPU core count, at most eight. Those workers see only a frozen
graph. They do not read a live `Mobile` or `Map`. The world thread still proves
the walker's own hop to its first node, by a straight line or a tile route, and
starts the walk; node-to-node edges are trusted. Results return through
`Core.LoopContext.Post`. If the worker queue is full, that trip fails with that
reason and cools down; the world thread does not search in its place. A leg
that fails marks its edge (`EdgeHealth`), and later searches pay extra to use
it. Tile walks inside a building, stand checks, and item work stay on the world
thread.

The world thread's half of a plan, and the marooned rescue, are paid from the
planning budget (`PlanBudget`), as the plan's start is. A walker with no node
within a leg, or with a blocked walk to its near nodes, walks back onto the
road by one tile search to whichever near node the plan's own search routes.
Every tile search on the world thread earns 40 cells per tile of trip, at least
4,000 and at most 12,000 (`TileRoute.WorldCellBudget`; a cell costs about eight
microseconds, so a tenth of a second at most). A trip whose tile walk runs out
plans over the roads. Only a nav build, and the boot repair of an old saved
graph, searches up to 80,000 cells. Loops of proofs share one allowance
(`TileRoute.CellAllowance`): 12,000 cells for a moongate's exit proofs, and
24,000 cells and three street-reach proofs for the marooned rescue. The rescue
waits when the budget is spent.

With no road and a rune near the goal, a trip waits for the recall over the gap
when the refusal passes: 40 seconds for most refusals (the heat of battle, a
fight, a cast, recovery, low mana, a resting book, a taken landing), and three
minutes for a criminal flag. With no road back found, every trip from that
16-tile cell rests for two minutes, whatever its goal, and the log says `found
no road back from (x, y, z) and rests 120 s before it plans again`.

A fight, a flight, or a pad that carries a walker well off its leg (onto another
map, or more than 6 tiles further from the leg than one think before) makes the
trip set out again from where it stands, with the activity line `was carried off
its road from (x, y, z) to (x, y, z) and sets out again`. That line is not a
stall: no edge is marked and no replan is spent.

## Navigation

### Moongates and teleporters

A gate that will not take a walker ends its wait. At the pad itself the trip
ends, the hop's edge is marked, and the log says `found no moongate to take at
(x, y, z); the trip ends and later plans pay for the hop` (or `teleporter` for a
pad).

A teleporter pad is taken only the way it sends, unless ModernUO's
`teleporters.json` marks it `back`. The way back from a landing is the pad
beside it.

A walker beside a teleporter pad takes the step onto it that the engine allows.
If a corner blocks the diagonal step, it steps round the corner first. It picks
a pad that a step can land on before a pad that no step reaches. A walker on a
pad that did not fire steps off onto a tile with no pad, then steps back on. A
pad up a short stair is still taken: the walker climbs to the tile that steps
onto it.

A pad fires only for a person who lands on its tile level with it, or below its
top and less than 15 below its base, as the engine's own move-over test does.
Some world pads lie under the floor a walker lands on: the server data lays the
Jhelom pad at (1406,3996) at 5 on a tile that stands at 6, and it carries
nobody. At every boot, on a saved or a new graph (except while the world waits
for First Time Setup), the plugin first lays each such pad level with the
highest floor a step onto its tile lands on, when that floor is no more than 15
from the pad. The engine then fires it for walkers and players alike. The log
says `laid N teleporter pads level with their floor so a step sets them off`.
On Felucca the server data lays 14 such pads, among them the Jhelom pad, the
Shame exits at (5686,385..387), and the third Trinsic passage pad down at
(5918,1410).

The plugin then drops the teleporter gate of each pad that no step onto its
tile still sets off. When such a pad was the only way back from a place, the
pads that lead into that place go too. Shop searches skip shops that no road
reaches. The graph is then saved. The log says `dropped N teleporter gates
whose pad never fires and M into places they left with no way out`. A gate
dropped this way stays out of the saved graph until `[SosariaRebuildNav` builds
it again. A trip that still meets a dead pad gives it up at once, with the line
`found the teleporter at (x,y,z) fires for no step onto it`.

A person who steps onto a live pad is carried off at once, so a plan never
walks off a pad that no gate sets a person down on. At every boot the plugin
drops each walking link whose straight walk steps onto a live pad between its
ends (a pad in the same row that lands where the link's own end pad lands does
no harm). A node left with no walk but onto a pad, and every landing with no
walk but onto a pad, gets the straight walk to the nearest node that is not a
pad and crosses none. A walk over other landings counts only when it gets past
them: the Destard stair-up landings at (5129..5132,908) walked only to each
other and onto the pads down. When every straight walk off crosses a pad, the
plugin adds a node on a free tile beside the landing first, as at the Trinsic
passage top (1630,3320). The boot log says `dropped N walking links that step onto
a teleporter pad and relinked M nodes they left`, and the graph is saved.

### Islands and graph pieces

The nav build joins every pair of graph pieces whose ground joins, even where no
walk or pad from a town leads. All pieces walk out at once, and where two walks
meet or come within a few cells, a road runs between them (a tile walk crosses
a bent door or a narrow path). Some Felucca islands have no land way off: the
Deceit island (only the Deceit door pads) with the Honesty shrine, the Valor
island (no pad or public moongate), and the south Jhelom island. A person
reaches them by recall, or through a dungeon's pads. The Fire dungeon's exit
pads join its upper floor, and the three parts of the lower Trinsic passage
join, which opens the walk from Trinsic into the Lost Lands. The catalog keeps a
shrine on a piece of its own, since a ghost that falls on the Valor island has
no other ankh; other places still need the main piece.

A graph built before these rules needs one nav rebuild (`nav.rebuildOnBoot` or
`[SosariaRebuildNav`).

### Traps and doors

Characters know the engine's floor traps (spike, giant spike, saw, gas, fire
column, flame spurt, stone face, and mushroom traps), hidden ones too. Walks,
tile routes, fight steps, and flight legs never step from a safe tile onto a
trapped one. A corridor trapped wall to wall is crossed only when no other way
leads on. A character that stands on a trap, or idles beside one, steps off. A
nav rebuild lays the graph's roads round the traps.

Walkers meet doors as the engine does. A character opens a closed door straight
ahead as it walks. An open door's leaf blocks the tile beside the doorway, and
no step cuts the corner of a doorway or passes a leaf diagonally, so the
walker's own step lists go round them. A character that stands still in a
doorway steps out onto a free tile, and a stroll never ends in one: the engine
never shuts a door on a person, and the open leaf then blocks the way for
everyone. A walk onto a pad (range 0) steps onto the pad tile. A goal on the
roof over a walled tile, such as a bank crowd seat on a wall, is reached beside
it, and bank crowd seats move off walls.

## Dungeons

A fighter may delve into any dungeon of the era, however far from home, when it
can walk there (gates and teleporters count) or carries a rune for it. The
dungeon is rolled again after every run. A dungeon weighs more when its halls
fit the fighter's power and tier, less when a crowd is already there, and less
for a long walk without a rune. Novices keep to easy dungeons and the first
floors; veterans leave novice ground and go deep. An established fighter's
runebook gets the dungeon doors outside the Lost Lands that a recall may land
at, while the book has room. Every ten minutes the activity log writes one
`Dungeon visitors:` line. A crawl that reached no room says why the last room
failed: `leaves Despise after 0 minutes: no room could be reached: the hunt
ground is too dangerous`.

**Hunt ratings.** Every hunt spot is rated by the spawn that stands there, from
the spawners of the era's spawn files. A dungeon hall is as hard as its whole
floor. A spot on open ground is as hard as the spawners whose creatures walk
into its hunt square. The rating counts each creature by how many of it stand
at once, and takes the creature that four in five of them are at or below. The
strongest creature on the ground counts at three tenths of its own threat,
however few of it stand there. A creature's threat is its hits, strength, and
blow, as the engine rolls them, raised for its spells (by Magery), a breath,
poison on its blows (by level), and a bow. A spot is in reach up to
`career.threatMultiple` times the fighter's power. The power is its own at full
hits, with its pets and the living members of its party at 0.85 each. The boot
rates the catalog again, so a saved catalog needs no rebuild. The activity log
writes one `Floors of <dungeon>:` line for each dungeon with the power each
floor asks.

A fight reads each foe the same way: the focus pick, the engage and run tests,
the hunter's sight scan, and the answer to a blow count a creature's own blow
and its arts, so a fighter does not open on a dread spider whose spells and bite
make it the floor's rating. A person counts a blow of 8 and no arts.

**Floor levels** are the levels players use. The server's location list marks
"Level 1" to "Level 4" in most dungeons, and the floor under each mark takes
that level. An unmarked floor with a door out is level 1; any other unmarked
floor is one level below the floor that a stair joins it to. A stair down goes
to a deeper floor, and a stair up to a shallower one.

**Crawling.** A crawler takes a stair down only to a floor that fits its power
and that it did not sweep bare on this run. On a bare floor whose stairs down
lead too deep, it goes up a stair to a floor that fits, or it leaves. A floor
past its reach, for example one a teleporter dropped it on, is left the same
way. A crawler that is losing on its floor also goes up or leaves: it ran from
fights there twice, a pet or a party member died there, or it took bad wounds
there. It then keeps off that floor for two hours, and the save keeps that
rest. The activity log names each floor taken or left with the power against
the floor: `X goes down to Shame level 2 (fits power 191 vs floor 202)`, and `X
keeps off Shame level 4 (power 191 below floor 948): the floor is too hard for
it`. A hunt start line ends with the same fit.

**One-way pads.** A living walker never plans a road over a one-way pad that
drops it onto a floor harder than the ground the pad stands on, when that floor
does not fit its power: the Despise and Fire pads onto Destard level 3, a stair
down, a pad from the overland into a dungeon. No pad leads straight back, so the
walk would cross that floor. A stair up stays open, and a ghost takes every
pad. Walks home, taming trips, and a red's roads to its camps go round, recall,
or find no road. A crawl or a hunt took its floor by the same fit, so its own
pads stay open. A walker that finds no road at all with those pads barred, and
cannot recall, plans once more with them open, so it always gets out. Deceit
stands on an island with no moongate, and its only road off runs through Fire
and Despise level 3. The log says `X has no road from A to B that keeps off the
floors above its reach and takes the pads onto them`. A red's second search
still keeps off the guards; it crosses them only by the rules above.

**Group calls.** A group call from a bank, a moongate, or a dungeon door picks a
dungeon fit for three. When the party musters with too few for that place, the
leader takes it to a place that fits the party. When nobody answers, the caller
goes alone only where it fits alone: `X keeps off destard alone (power 206
below floor 583)`.
