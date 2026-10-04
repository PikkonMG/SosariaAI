namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    private static TalkTopic[] Fighting() =>
    [
        new(
            TalkCategory.CombatEngage,
            "a character picks a fresh fight. {foe} is the monster or person, without its article.",
            [
                "come here {foe}", "this ones mine", "got it", "ill take the {foe}", "{foe}! mine", "attack!", "lets go {foe}",
                "easy one", "get over here", "die {foe}", "free loot", "lol {foe}", "for the gold", "one {foe}, no problem"
            ]
        ),
        new(
            TalkCategory.CombatAssist,
            "a character joins a fight someone else is already in. {foe} is the foe.",
            ["coming!", "got ur back", "helping", "ill hit the {foe} too", "need a hand?", "on it", "im here", "2 on 1 {foe} lol", "lets kill it", "adding in"]
        ),
        new(
            TalkCategory.CombatPull,
            "a character pulls one foe out of a group. {foe} is the one it pulls.",
            [
                "pulling one", "pull", "grabbing the {foe}", "pulling the {foe} out", "one at a time", "just the {foe}, stay back",
                "baiting one", "come to papa", "pulling, watch out", "splitting them"
            ]
        ),
        new(
            TalkCategory.CombatFlee,
            "a character runs from a fight that turned. {foe} is what it runs from.",
            [
                "run!!", "too many, im out", "nope nope", "outta here", "{foe} hits too hard", "retreat!", "brb healing",
                "gotta run", "cant take the {foe}", "running lol", "help!!", "im dying", "bail bail"
            ]
        ),
        new(
            TalkCategory.CombatTurn,
            "a runner turns on one chaser that got ahead of the pack. {foe} is the chaser.",
            [
                "ok just u then {foe}", "turning on it", "come on then", "one on one now", "ur alone now {foe}", "got u",
                "not so tough alone", "stand and fight", "turning back", "just one, i got this"
            ]
        ),
        new(
            TalkCategory.CombatClear,
            "a runner got clear of the fight.",
            [
                "phew", "made it", "that was close", "lost em", "safe", "whew", "ok im out", "too close lol", "need to heal up",
                "almost died", "never going back there", "catching my breath"
            ]
        ),
        new(
            TalkCategory.CombatVictory,
            "the foe fell. {foe} is the foe.",
            [
                "got it", "dead", "down", "easy", "{foe} down", "next", "loot time", "ez", "rip {foe}", "gold please gold",
                "that was nothing", "one more", "lol too easy", "[eras: t2a] pls be vanq", "[eras: ml,modern] no arties? lame"
            ]
        ),
        new(
            TalkCategory.LootHaul,
            "a hunter just cleared the corpse of a monster it killed. {price} is the gold, {foe} the monster.",
            [
                "{price} gold off the {foe}", "nice, {price}", "{foe} had {price} on it", "{price} gp, not bad",
                "ka-ching {price}", "{price} gold lol", "the {foe} was rich", "gold gold gold", "{price}, ill take it",
                "loot!", "decent drop from the {foe}", "{price} more for the bank"
            ]
        ),
        new(
            TalkCategory.HuntHaul,
            "a hunter heads home from a hunt that paid. {count} is the kills, {price} the gold taken.",
            [
                "{count} kills and {price} gold, good run", "{price} gold today, good haul", "not bad, {price}",
                "good hunt. {count} down", "pack is full, banking", "{price} richer lol", "that spawn was sweet",
                "made {price} gold", "{count} kills, time to bank", "going to bank {price}", "nice run, {count} dead",
                "ok thats enough for today"
            ]
        ),
        new(
            TalkCategory.HuntDry,
            "a hunter leaves a ground that gave it nothing. {place} is the ground when known.",
            [
                "spawn is dead", "nothing here", "someone camped {place}", "{place} is empty", "no spawn at all",
                "waste of time", "where is everything", "{place} got farmed", "dry hunt, moving on", "no monsters lol",
                "all camped out", "trying somewhere else"
            ]
        ),
        new(
            TalkCategory.RedReply,
            "a bystander hears someone scream about a red. {place} is where the red is.",
            [
                "where??", "run!!", "recall out", "not again", "{place}? leaving", "ty for the warning", "whos red?", "hide!", "omg",
                "stay away from {place}", "guards dont go there lol", "im outta here"
            ]
        ),
        new(
            TalkCategory.RedOnMyWay,
            "a lawful fighter already fighting the red hears someone scream about it. {place} is where the red is.",
            [
                "on him", "got him", "already on it", "im on the red", "stay back, i got him", "{place}, im on him",
                "anti pk here, hes mine", "this red is going down", "reds die today", "hes not getting away"
            ]
        ),
        new(
            TalkCategory.GhostHaunt,
            "a ghost wails over its own body.",
            [
                "noooo", "ugh...", "dang it", "not again", "rip me", "argh", "u gotta be kidding", "i had 2k on me",
                "my stuff!!", "shouldve recalled", "forgot to heal. brilliant", "one more bandage and i had it", "ran the wrong way lol",
                "so close", "never going in there alone again", "anyone got a res?", "the run back is gonna suck",
                "good thing i banked first", "there goes my armor", "my horse just stood there", "i was winning too",
                "ok that one was my fault", "should have stayed in town", "stupid lag", "that {foe} came outta nowhere",
                "{foe} hits way too hard", "didnt even see {foe}", "ok {foe}, u win this one", "who lets {foe} roam out here",
                "{foe} again. every time", "note to self: dont fight {foe} alone"
            ]
        ),
        new(
            TalkCategory.GhostPlea,
            "a ghost asks a living person for a res.",
            ["rez plz", "can u res me?", "res please?", "could you res me?", "rez me plz", "res pls", "anyone res?", "rez?", "res me pls, will pay", "need a res", "res plz i beg"]
        ),
        new(
            TalkCategory.ResThanks,
            "a raised character thanks the helper.",
            ["ty!", "thanks!!", "thank you", "tyvm", "ty ty", "thx a lot", "ur the best", "thank u so much", "tyvm!!", "owe u one"]
        ),
        new(
            TalkCategory.ResOffer,
            "a helper walks up to a ghost to raise it.",
            ["hold on, rezzing u", "one sec, i'll res you", "stand still, res incoming", "res coming", "stand still ill res", "hold on", "u need a res?", "got u, one sec", "rezzing", "ill get u"]
        ),
        new(
            TalkCategory.ResWelcome,
            "a helper sees the ghost raised.",
            ["there ya go", "welcome back", "up you get", "welcome back to the living", "np", "there u go", "go get ur stuff", "rise lol", "back in action", "good as new"]
        ),
        new(
            TalkCategory.DeathLooted,
            "a raised character finds its body looted or rotted away.",
            [
                "WHO LOOTED MY CORPSE", "who took my stuff??", "MY STUFF IS GONE", "somebody looted me", "who looted me?? not cool",
                "all my gear gone", "great, looted", "whoever looted me, i know who u r", "LOOTER", "corpse is empty!!"
            ]
        ),
        new(
            TalkCategory.DeathJoke,
            "a bystander sees someone die nearby. {target} is the dead one.",
            [
                "lol rip {target}", "{target} down lol", "should have run {target}", "rip", "lol", "nice one {target}",
                "{target} dies again", "that was fast", "rip {target}, it happens", "ouch", "u ok {target}? lol", "someone res {target}"
            ]
        ),
        new(
            TalkCategory.DeathJokeReply,
            "a second bystander answers the joke about a death. {target} is the dead one.",
            [
                "dont laugh, ur next", "rofl", "be nice", "lol", "poor {target}", "happens to the best", "{target} will be back",
                "at least it wasnt me", "haha", "they'll live lol", "someone get a healer"
            ]
        ),
        new(
            TalkCategory.LootedReply,
            "a bystander hears someone shout that its body was looted.",
            [
                "wasnt me", "lol", "thats fel for ya", "saw a thief run by", "should have been faster", "not me i swear",
                "always bank ur stuff lol", "happens", "gl finding them", "[eras: ml,modern] should have insured"
            ]
        ),
        new(
            TalkCategory.DuelChallenge,
            "a fighter by the bank challenges another to a friendly duel. {name} is the other.",
            [
                "{name} duel? outside the bank", "{name} wanna spar?", "{name} u and me, duel", "duel {name}? no loot",
                "{name} test ur build? duel me", "yo {name} duel", "{name} bet i can take u", "spar {name}? to a quarter hp",
                "{name} duel for fun?", "come on {name} one duel"
            ]
        ),
        new(
            TalkCategory.DuelAccept,
            "the challenged fighter accepts. {name} is the challenger.",
            ["gl", "gl {name}", "ok", "sure, gl", "ur on", "bring it", "k lets go", "gl hf", "u asked for it", "lets do it"]
        ),
        new(
            TalkCategory.DuelWin,
            "the winner of a duel. {name} is the loser.",
            ["gf", "gf {name}", "gg", "good fight", "gf, close one", "gf, rematch anytime", "nice try {name}", "gf gf", "that was fun", "gf, u almost had me"]
        ),
        new(
            TalkCategory.DuelLoss,
            "the loser of a duel.",
            [
                "gf. rematch later?", "gf. my bandages wouldnt take", "gf", "gg u got me", "gf, next time", "ok ok gf",
                "gf, i need better armor", "gf. that was lag i swear", "gf, u fight dirty lol", "gf, back to practice",
                "gf, pressed the wrong spell", "gf, ran outta mana", "nice dump, gf"
            ]
        ),
        new(
            TalkCategory.DuelOnlooker,
            "a bystander watches two duelists walk out. {name} and {target} are the duelists.",
            [
                "fight fight fight", "5k on {name}", "my money is on {target}", "{name} vs {target}, this should be good", "go {name}!",
                "{target} is gonna lose lol", "duel!! come watch", "who wins?", "{target} got this", "no looting guys lol",
                "bet 1k on {target}"
            ]
        ),
        new(
            TalkCategory.DuelCheer,
            "a bystander after a duel. {name} won, {target} lost.",
            [
                "gg {name}", "{target} got owned lol", "told u {name} would win", "nice fight", "pay up lol", "{name} wins",
                "rematch rematch", "{target} lag excuse incoming", "gf both", "that was close"
            ]
        ),
        new(
            TalkCategory.PkAttack,
            "a red goes for a lone victim. {target} is the victim.",
            [
                "{target} ur mine", "hey {target} :)", "nice gear {target}", "drop ur stuff {target}", "gg {target}",
                "{target}, bad spot to stand", "all kill {target} lol", "u picked the wrong road {target}", "hi {target} bye {target}",
                "free loot", "{target} run lol"
            ]
        ),
        new(
            TalkCategory.PkRan,
            "a red breaks off because the other side is too strong. {name} is the stronger one.",
            ["nope", "not today {name}", "later {name}", "too many", "gtfo", "{name} got friends, im out", "brb", "rofl no", "run!!", "another time {name}", "recall recall"]
        ),
        new(
            TalkCategory.PkLoot,
            "a red loots the body it just made. {target} is the victim.",
            [
                "ty for the gear {target}", "nice stuff", "thx {target}", "loot is loot", "come back for more {target}", "gg no re",
                "ur corpse is mine", "decent haul", "{target} should bank more lol", "mine now"
            ]
        ),
        new(
            TalkCategory.PkWentRed,
            "a murderer's count just turned it red.",
            ["red now lol", "welp im red", "blue is overrated", "guess im red", "5 counts, finally", "dont call the guards lol", "red and proud", "no more town for me", "bucs den here i come", "oops"]
        ),
        new(
            TalkCategory.ThiefVictim,
            "someone caught a thief lifting from their pack where no guard would come. {name} is the thief.",
            [
                "{name} is a thief!!", "hey!! {name} stole from me", "thief! {name}!", "get {name}, thief", "{name} give it back!",
                "someone kill {name}, thief", "{name} took my stuff", "watch ur packs, {name} steals", "argh {name}!!", "{name} ur dead thief"
            ]
        ),
        new(
            TalkCategory.RedWarn,
            "a lawful fighter warns someone that a red is after them. {name} is the one warned.",
            [
                "{name} red on u!!", "{name} watch out, red", "red behind u {name}", "{name} run, pk", "pk on {name}!",
                "heads up {name}, red", "{name} recall out!", "pk incoming {name}", "{name} behind u!!", "red coming for u {name}",
                "red!! run {name}", "{name} move, pk!!", "careful {name}, red", "{name} get out, red here", "pk here {name}, watch it",
                "{name} pk coming!!", "run {name}, its a pk", "{name} hes red, go"
            ]
        ),
        new(
            TalkCategory.GuildWarStart,
            "a guild member who just started a war by hitting someone. {name} is the one hit, {guild} is their guild.",
            ["war!!", "{guild} is going down", "this is war {name}", "hope ur guild likes losing {name}", "{guild}? war on", "get ur friends {name}", "tell {guild} its on", "{name} started it lol", "guild war, lets go", "call ur guild {name}"]
        ),
        new(
            TalkCategory.HouseRetreat,
            "a fighter hurt in a fight outside ducks into its guild's house to heal.",
            [
                "going in to heal", "cover me, healing up inside", "brb healing", "inside, need a sec", "low, going in",
                "heal up time", "grabbing bandies from the chest", "restocking, 1 min", "hold them off, im going in", "in the house, heal me"
            ]
        ),
        new(
            TalkCategory.HouseReturn,
            "a fighter comes back out of its guild's house, healed and restocked, to fight on.",
            [
                "back out", "ok im back", "full hp, lets go", "round 2", "back, where are they", "restocked, lets go",
                "im back out", "ready again", "ok again", "back in it"
            ]
        ),
        new(
            TalkCategory.GuardStandDown,
            "someone stops a fight because the guards are watching. {name} is the other fighter.",
            ["not here {name}, guards", "guards... later {name}", "lucky the guards r here {name}", "not in town", "later {name}, guards are watching", "fine, guards win {name}", "not worth a guard whack {name}", "guards are right there lol", "{name} not worth the guards", "another day {name}"]
        )
    ];
}
