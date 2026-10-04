namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    private static TalkTopic[] Groups() =>
    [
        new(
            TalkCategory.LfgShout,
            "a fighter at a meeting spot calls for a hunting group. {place} is where, {count} how many more it wants.",
            [
                "lfg {place} anyone?", "lfm {place}, need {count}", "anyone wanna do {place}?", "{place} group? need {count} more",
                "lf {count} more for {place}", "lfg {place}, need a few", "lfm {place}", "{place} run, need {count}",
                "group for {place}? meet here", "anyone up for {place}?"
            ]
        ),
        new(
            TalkCategory.LfgJoinCall,
            "a fighter answers a group call. {name} is the one who called.",
            ["me", "me!", "sure", "im in", "me pls", "count me in", "i'll come", "me, inv", "me {name}", "inv me"]
        ),
        new(
            TalkCategory.LfgOfferPlayer,
            "a fighter answers a person's own group call; the person leads. {name} is that person.",
            [
                "me, where to?", "sure, inv me", "me! inv", "im down, inv", "sure where u going?", "me {name}, inv",
                "sure {name}, where?", "me me, inv", "im in {name}", "count me in, inv"
            ]
        ),
        new(
            TalkCategory.LfgInvite,
            "a group leader invites a person who answered. {name} is that person.",
            [
                "{name} inv", "k inv", "sent inv {name}", "inv {name}", "k {name}, check inv", "inv sent {name}",
                "{name} accept the inv", "inving {name}", "k {name}, inv", "got u {name}"
            ]
        ),
        new(
            TalkCategory.LfgStart,
            "a group leader sets out, in party chat.",
            [
                "ok lets go", "k lets move", "all here? lets go", "moving out, stay close", "go go", "lets roll",
                "ok everyone follow me", "k were off", "stick together", "follow me and dont pull lol"
            ]
        ),
        new(
            TalkCategory.LfgGiveUp,
            "nobody answered a group call, and the caller sets out on the run alone.",
            [
                "nvm", "nm going solo", "guess not lol", "nobody? ok solo", "fine ill go alone", "guess im solo",
                "forget it", "solo then", "no takers lol", "k solo it is"
            ]
        ),
        new(
            TalkCategory.LfgGg,
            "a group leader ends the run, in party chat.",
            [
                "gg all", "gg ty all", "good run all, gg", "gg, that was fun", "gg, nice run", "ty all",
                "gg, see u next time", "good group gg", "gg, good loot", "gg everyone"
            ]
        ),
        new(
            TalkCategory.LfgGgReply,
            "a group member answers the leader's gg, in party chat.",
            ["gg", "ty", "gg ty", "gg all", "thx for the group", "ty for the inv", "gg that was fun", "gg cya", "gg, again sometime", "gg!"]
        ),
        new(
            TalkCategory.LfgDecline,
            "a character that cannot join turns down a person's group call.",
            ["cant rn", "busy sorry", "maybe later", "not now", "sry busy", "nah im good", "not today", "cant, doing something", "pass", "maybe tomorrow"]
        ),
        new(
            TalkCategory.LfgAskPlayer,
            "a group leader asks a person standing near to come along. {name} is that person, {place} where.",
            [
                "{name} wanna come {place}?", "{name}, want to join us for {place}?", "hey {name}, {place}? we need 1 more",
                "{name} u want in? going {place}", "{name}, come {place} with us?", "{name} wanna group? {place}",
                "need 1 more for {place}, {name} u in?", "{name} {place} run, join?", "{name}, got room for u, {place}",
                "{name} want to tag along? {place}"
            ]
        ),
        new(
            TalkCategory.PartyDescend,
            "a group leader takes the stairs down to the next dungeon level, in party chat.",
            [
                "going down", "stairs, follow", "down we go", "next level, stay close", "going deeper", "taking stairs",
                "down, dont get lost", "lvl down, follow me", "going down, heal up first", "stairs here, come on"
            ]
        ),
        new(
            TalkCategory.PartyTakeLead,
            "a group member leads on after the leader died, in party chat. {name} is the fallen leader.",
            [
                "{name} is down, follow me", "i'll lead, someone res {name}", "leader down, on me", "k follow me now",
                "{name} died, im leading", "on me guys", "follow me, we get {name} after", "i got lead",
                "stick with me now", "rip {name}, follow me"
            ]
        ),
        new(
            TalkCategory.PartyReady,
            "a group member answers the leader setting out, in party chat. {name} is the leader.",
            ["rdy", "ready", "k", "right behind u", "lets go", "go go", "following", "ok {name}", "rdy when u r", "got bandages", "all set"]
        ),
        new(
            TalkCategory.PartyDepartBanter,
            "a group member jokes as the group sets out, in party chat. {place} is where they go.",
            [
                "dont pull everything this time lol", "who has the regs?", "{place} here we come", "i call dibs on the loot",
                "stay close", "if i die someone res me", "last time i died 3 times lol", "someone watch for reds",
                "lets make some gold", "hope {place} isnt camped"
            ]
        ),
        new(
            TalkCategory.PartyReturnBanter,
            "a group member talks after the run ends. {place} is where they went.",
            [
                "that was sick", "lost my bag lol", "{place} was nuts", "good loot", "i need to repair", "so much gold",
                "i died like twice lol", "same time tomorrow?", "gonna bank this", "{place} is way easier with a group",
                "fun run", "ty for healing me"
            ]
        ),
        new(
            TalkCategory.PartyMuster,
            "a group leader calls the members round it before they set out, in party chat. {place} is where they go. A line about a gate is said only when one of them can open it.",
            [
                "gather up on me", "all to me, {place} in a sec", "form up here", "on me, gating to {place} soon", "come here everyone",
                "stack up on me", "everyone over here pls", "wait up, {place} when all here", "regroup on me", "get over here, we leaving"
            ]
        ),
        new(
            TalkCategory.PartyGate,
            "a group member casts Gate Travel for the whole group, in party chat. {place} is where the gate goes.",
            [
                "gating to {place}", "casting gate, stand still", "gate to {place} up in a sec", "gate up, hurry", "i got the gate, {place}",
                "vas rel por, get in after lead", "gate to {place}, dont run off", "opening gate, dont move", "gate going up, go go",
                "ill hold the gate, get in"
            ]
        ),
        new(
            TalkCategory.DungeonEnter,
            "a character reaches the inside of a dungeon. {dungeon} is the dungeon.",
            [
                "{dungeon} here we go", "made it to {dungeon}", "in {dungeon}, careful", "{dungeon} time", "smells bad in here lol",
                "lets clear {dungeon}", "watch for reds in {dungeon}", "down we go", "{dungeon}! loot time", "{dungeon} again lol"
            ]
        ),
        new(
            TalkCategory.DungeonLeave,
            "a character heads home out of a dungeon. {dungeon} is the dungeon.",
            [
                "out of {dungeon}", "done with {dungeon}", "made it out", "{dungeon} was rough", "sunlight!", "heading home",
                "{dungeon} cleared lol", "time to bank", "pack is full, going home", "never again {dungeon}",
                "ok thats enough {dungeon}"
            ]
        ),
        new(
            TalkCategory.GuildChatter,
            "small talk in guild chat.",
            [
                "lag is bad tonight", "anyone got spare regs?", "who's on?", "bank is packed lol", "anyone selling bandages?",
                "brb", "back", "need a smith, anyone?", "anyone around britain?", "so bored",
                "quiet tonight", "lol"
            ]
        ),
        new(
            TalkCategory.GuildWelcome,
            "guild chat greets a guildmate at a keyboard. {name} is that person.",
            [
                "wb {name}", "hey {name}", "{name}! o/", "sup {name}", "yo {name}", "welcome back {name}", "hi {name}",
                "{name} is here!", "hey {name}, whats up", "o/ {name}"
            ]
        ),
        new(
            TalkCategory.GuildAskGroup,
            "a guildmate asks guild chat for a group. {place} is where.",
            [
                "anyone wanna hunt {place}?", "need 1-2 for {place}, anyone?", "who wants to do {place}?",
                "{place} anyone? group up", "who's up for {place}?", "{place} run, need people",
                "going to {place}, join me", "anyone free? {place}", "{place} group forming", "need help at {place}"
            ]
        ),
        new(
            TalkCategory.GuildOnMyWay,
            "a guildmate says it is coming, in guild chat.",
            ["omw", "coming", "recalling now", "on my way", "me, omw", "sure, coming", "k coming", "omw!", "be there soon", "coming, wait for me"]
        ),
        new(
            TalkCategory.GuildCantCome,
            "a guildmate cannot come, in guild chat.",
            [
                "cant rn sorry", "busy atm", "maybe later", "cant, sry", "not now sry", "busy, sorry", "cant, maybe later",
                "in the middle of something", "no can do", "next time"
            ]
        ),
        new(
            TalkCategory.GuildInvitePlayer,
            "a guildmate invites a person who answered its guild call. {name} is that person.",
            [
                "k inv {name}", "sent inv {name}", "cool, inv {name}", "inv {name}", "{name} check inv", "got u {name}, inv",
                "inv sent {name}", "accept inv {name}", "sending inv {name}", "k {name}, look for inv"
            ]
        ),
        new(
            TalkCategory.GuildJoin,
            "a character joins a friend's guild. {guild} is the guild, {name} the friend.",
            [
                "{name} got me into {guild}", "joined {guild}!", "im {guild} now", "{guild} for life", "ty {name}, joined {guild}",
                "new guild, {guild}", "{guild}! finally in a guild", "just joined up with {name}", "{guild} rocks",
                "joined {guild} with {name}"
            ]
        ),
        new(
            TalkCategory.GuildWarRally,
            "a character charges a guild it is at war with. {guild} is the enemy guild's tag, {foe} the enemy.",
            [
                "{guild}! get em", "{guild} scum, attack", "war!! {foe} is {guild}", "{foe} {guild}, kill", "enemy {guild}!",
                "get {foe}!!", "{guild} on sight", "its {foe}, {guild}, go go", "all on {foe}", "{guild} here, fight!"
            ]
        ),
        new(
            TalkCategory.OrderBattle,
            "an Order guild member draws on a Chaos member. {foe} is the enemy.",
            ["for order!", "order! get {foe}", "chaos scum", "{foe} is chaos, kill", "order forever", "shield up, {foe}", "chaos dog {foe}", "for the order", "order!!", "die chaos"]
        ),
        new(
            TalkCategory.ChaosBattle,
            "a Chaos guild member draws on an Order member. {foe} is the enemy.",
            ["for chaos!", "chaos! get {foe}", "order sheep lol", "{foe} is order, kill", "chaos rules", "blood for chaos", "order dog {foe}", "chaos!!", "die order", "hail chaos"]
        ),
        new(
            TalkCategory.WarBandDepart,
            "an Order or Chaos fighter rides out with a few faction-mates to hunt the other side. {place} is where they ride.",
            [
                "who's riding? {place}, now", "patrol to {place}, form up", "lets go hunt some shields at {place}", "{place}, lets see who shows",
                "grab ur shields, {place}", "moving out to {place}", "war band to {place}, stay tight", "they're out at {place} i bet, go",
                "on me, {place}", "ride to {place}, kill anything with the wrong shield"
            ]
        ),
        new(
            TalkCategory.SweepDepart,
            "a blue fighter rides out with friends to clear reds off a known killing spot. {place} is the spot.",
            [
                "reds at {place}, lets clear it", "who wants to hunt pks at {place}?", "{place} is camped again, come on",
                "anti pk run to {place}, on me", "lets go kill some reds at {place}", "{place}, reds been camping it all day",
                "sweep {place} with me", "going to clean up {place}", "pk hunting, {place}, now", "{place} again. lets end it"
            ]
        ),
        new(
            TalkCategory.PosseDepart,
            "a blue fighter rides after the red who just murdered a friend. {foe} is the killer, {name} is the dead friend.",
            [
                "{foe} just killed {name}, get em", "after {foe}! they killed {name}", "{name} is down, {foe} did it, lets go",
                "who's with me? {foe} killed {name}", "rez {name} later, kill {foe} now", "{foe} wont get far, on me",
                "hunt {foe} down", "{foe} murdered {name}. not today", "saddle up, {foe} is running", "{foe} pk'd {name}, lets get em"
            ]
        ),
        new(
            TalkCategory.ConvoyDepart,
            "a guild member sets out on a long walk to another town and takes guildmates along. {place} is where, {guild} is the guild tag.",
            [
                "heading to {place}, anyone coming?", "{guild} going to {place}, walk with me", "walk to {place}? safer in a group",
                "going {place}, tag along", "{place} run, stick with me", "anyone need {place}? lets go together", "{guild} to {place}, move out",
                "long walk to {place}, come on", "{place}, stay close on the road", "k were walking to {place}"
            ]
        ),
        new(
            TalkCategory.OrderTaunt,
            "an Order member sees a Chaos member in town, where nobody fights. Words only. {foe} is the enemy.",
            [
                "{foe} hiding behind the guards again", "meet us outside town {foe}", "chaos only brave in town lol", "see u on the road {foe}",
                "nice shield {foe}, shame about the guild", "outside the gate, {foe}. anytime", "go home chaos", "watch the roads {foe}",
                "order owns the crossroads", "{foe} wouldnt last a minute out there"
            ]
        ),
        new(
            TalkCategory.ChaosTaunt,
            "a Chaos member sees an Order member in town, where nobody fights. Words only. {foe} is the enemy.",
            [
                "order sheep baa", "{foe} safe in town huh", "see u outside {foe}", "guards wont save u out there {foe}",
                "chaos owns the roads", "{foe} nice shiny shield lol", "run home order", "later {foe}, outside",
                "order carebears everywhere", "the road is ours {foe}"
            ]
        ),
        new(
            TalkCategory.ScuffleWatch,
            "a passer-by sees a small Order and Chaos scuffle break out on a town street. Watches, never joins. {name} is one of the fighters.",
            [
                "shield fight!", "here we go again lol", "go {name}!", "{name} is toast", "not again, every day with these guys",
                "order vs chaos, grab a seat", "lol {name} get him", "take it outside town guys", "whos winning?", "my money on {name}",
                "guards dont care, its a guild thing", "{name} u got this"
            ]
        ),
        new(
            TalkCategory.ScuffleCall,
            "an Order or Chaos member in a town calls its faction-mates, in guild chat, to a street there to meet the other side. {place} is the town.",
            [
                "shields in {place}, meet me off the bank", "{place}, chaos and order both here, come", "street fight in {place}, who's near",
                "they're in {place}, come show them", "{place} side street, bring bandies", "anyone near {place}? the other side is here",
                "come to {place}, lets settle it", "{place}, away from the bank, now", "wrong shields walking {place}, come",
                "on me in {place}"
            ]
        ),
        new(
            TalkCategory.ScuffleYield,
            "an Order or Chaos member gives way in a small town scuffle: it runs or calls it off.",
            [
                "ok ok im out", "gg, u win this one", "enough, need heals", "next time", "bah, out of bandies", "fine, u got me",
                "im done lol", "later, this isnt over", "cant win em all", "heal break, brb", "u got lucky", "ok truce"
            ]
        )
    ];
}
