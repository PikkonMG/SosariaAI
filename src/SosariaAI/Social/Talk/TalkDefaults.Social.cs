namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    private static TalkTopic[] Social() =>
    [
        new(
            TalkCategory.RespondName,
            "a person at a keyboard said this character's name. {name} is that person.",
            ["yes?", "ya?", "sup", "hm?", "wat", "yo", "whats up {name}", "?", "here", "yea {name}?", "whats up"]
        ),
        new(
            TalkCategory.RespondRoomGreet,
            "a person greeted the room, not this character by name.",
            ["hey", "hi", "yo", "hail", "sup", "hiya", "o/", "heya", "hi there", "hey all", "hello"]
        ),
        new(
            TalkCategory.RespondGreet,
            "a person greeted this character by name, or a friend greeted it. {name} is the greeter.",
            [
                "hey {name}", "hi {name}", "yo {name}", "sup {name}", "hail {name}", "{name}!", "hiya {name}",
                "heya {name}", "hey hey {name}", "o/ {name}"
            ]
        ),
        new(
            TalkCategory.FriendArrival,
            "a friend or guildmate at a keyboard walked up. {name} is that person.",
            [
                "hey {name}!", "{name}!! sup", "yo {name}", "hiya {name}", "look who it is, {name}", "{name} whats up",
                "hey {name} long time", "{name}! o/", "{name}!!! where u been", "yo {name} good to see u"
            ]
        ),
        new(
            TalkCategory.RespondGoodbye,
            "a person said goodbye. {name} is that person.",
            [
                "cya", "later", "bye", "cya {name}", "take care", "gn", "later {name}", "bye {name}", "cya around",
                "take it easy {name}"
            ]
        ),
        new(
            TalkCategory.RespondShrug,
            "a person asked something the character cannot answer.",
            [
                "dunno", "no idea", "ask a guard", "idk", "no clue sorry", "dunno, ask at the bank", "not sure",
                "beats me", "no clue", "ask someone else lol"
            ]
        ),
        new(
            TalkCategory.RespondDoing,
            "a person asked what the character is doing. {doing} is what it really does, like \"mining at minoc\".",
            [
                "{doing}", "just {doing}", "{doing}, u?", "{doing} lol", "busy, {doing}", "{doing}, same old",
                "eh {doing}", "{doing} as usual", "{doing}. u?", "{doing}, bored"
            ]
        ),
        new(
            TalkCategory.RespondInsult,
            "a person insulted this character by name.",
            ["whatever", "k", "ok noob", "lol ok", "go away", "rofl", "uh huh", "cry more", "yeah yeah", "u too"]
        ),
        new(
            TalkCategory.RespondAck,
            "a person said something to this character that needs no real answer.",
            ["heh", "k", "ya", "lol", "true", "hmm", "ok", "yea", "nice", "sure"]
        ),
        new(
            TalkCategory.RespondWhat,
            "a person said something right beside this character, not by name, that it did not follow.",
            ["?", "hm?", "what", "huh", "u talking to me?", "wat", "say again?", "eh?", "who me?", "what?", "sorry what"]
        ),
        new(
            TalkCategory.GreetWarm,
            "a character greets a friend it likes when its persona has no fitting greeting. {name} is the friend. Keep lines 12 letters or longer.",
            [
                "hey {name}!! good to see u", "{name}!! whats up friend", "yo {name}, long time", "{name}!! where u been",
                "hey {name}, missed u", "{name} my friend, sup", "look who it is, {name}!", "hiya {name}! hows things",
                "{name}! been a while", "heya {name}, good to see ya"
            ]
        ),
        new(
            TalkCategory.GreetPlain,
            "a character greets someone it knows a little. {name} is the other. Keep lines 12 letters or longer.",
            [
                "hey {name}, sup", "hail {name}, hows it going", "hi {name}, whats up", "hey {name}, hows it goin",
                "yo {name}, whats new", "{name}, hows the hunting", "hi {name}, busy day?", "hey {name}, u good?",
                "hail {name}, whats new", "sup {name}, been busy?"
            ]
        ),
        new(
            TalkCategory.GreetCold,
            "a character greets someone it dislikes. {name} is the other. Keep lines 12 letters or longer.",
            [
                "oh great, its {name}", "keep walking {name}", "ugh, {name} again", "what do u want {name}",
                "{name}... oh great", "not u again {name}", "go away already {name}", "{name}. whatever",
                "dont talk to me {name}", "oh look, its {name}"
            ]
        ),
        new(
            TalkCategory.GreetReply,
            "the answer to a named greeting. {name} is the greeter.",
            [
                "hey {name}", "hi {name}", "yo {name}", "sup {name}", "hail {name}", "{name}!", "hey hey", "o/ {name}",
                "hiya", "heya {name}, whats up"
            ]
        ),
        new(
            TalkCategory.SmallTalk,
            "an opener between two characters standing together. {name} is the listener.",
            [
                "hows the hunting {name}?", "roads r quiet today", "ingots cost a fortune lately", "saving up for a house",
                "any news from ur guild {name}?", "mines were packed this morning", "lost my pack horse last week lol",
                "thinking bout trying covetous", "u look beat up {name}", "hardly any guards out today",
                "bank is so laggy right now", "anyone buying hides?", "my boots are older than me", "need regs, so broke",
                "{name} how long u been on today?", "felucca is nuts today", "some noob dumped a whole pack at the gate",
                "tavern food sucks lol", "anyone seen a tame ostard for sale?", "been mining all day, so bored",
                "sup {name}", "brb afk a sec", "how much for a gm katana these days?"
            ]
        ),
        new(
            TalkCategory.SmallTalkReply,
            "the answer to small talk.",
            [
                "ya same", "tell me about it", "heh", "not bad", "maybe tomorrow", "careful out there", "gl with that",
                "true", "could be worse", "lol", "nice", "doubt it", "gotta get that gold", "ha maybe", "dream on lol",
                "better u than me", "same here", "i hear ya", "no kidding", "for real", "u got that right",
                "eh, maybe", "right?", "sounds rough", "lucky u", "been there lol", "who knows", "weird day huh",
                "and then some"
            ]
        ),
        new(
            TalkCategory.FriendChain,
            "a third friend walks into two others greeting. {name} is one of them.",
            [
                "o/ all", "hey guys", "sup all", "look, the whole gang", "{name}! and everyone", "hi hi",
                "the gangs all here", "yo everyone", "hey {name}, hey all", "party at the bank lol"
            ]
        ),
        new(
            TalkCategory.GreetOldFriend,
            "a character meets someone it shared a real adventure with. {friend} is that person, {place} is where it " +
            "happened, {deed} is what they did, like \"that dungeon run\". Keep lines 12 letters or longer.",
            [
                "{friend}! still sore from {place}?", "hey {friend}, remember {deed} at {place}?",
                "{friend}!! good to see u, not since {place}", "{friend}! still thinking bout {deed} lol",
                "yo {friend}! still dream about {place}?", "{friend}!! {deed} was crazy", "hey {friend}, long time since {place}",
                "{friend}! u still remember {deed}?", "{friend}, my old buddy from {place}", "yo {friend}, never forget {deed}",
                "{friend}!! last time i saw u was {place}", "hey {friend}! {deed}, good times",
                "{friend} o/ {place} feels like ages ago", "{friend}! havent seen u since {deed}"
            ]
        ),
        new(
            TalkCategory.RecallAdventure,
            "a character standing with an old friend brings up an adventure they really shared. {friend} is the " +
            "listener, {place} is where it happened, {deed} is what they did, like \"that red we dropped\".",
            [
                "remember {deed} at {place}?", "still laughing about {deed}", "{place} was wild that day huh",
                "{place} was something else {friend}", "{friend} u remember {deed}?", "been telling everyone bout {deed}",
                "still thinking bout {place} lol", "{deed}... good times {friend}", "that was some day at {place}",
                "ever gonna top {deed}?", "nobody believes me bout {deed}", "{friend} we made a good team at {place}"
            ]
        ),
        new(
            TalkCategory.GoingAsk,
            "a character at a bank or meeting spot asks the crowd for news of the place it hunts. It is not setting " +
            "out and opens no group, so ask for news only: never ask who is going or say it is going. {place} is that place.",
            [
                "anyone been to {place} today?", "hows {place} today, anyone know?", "is {place} camped?", "{place} safe today?",
                "whos been to {place}", "is {place} worth it today?", "any reds at {place}?", "any news from {place}?",
                "{place} still dead or what?", "spawn up at {place} today?", "heard anything bout {place}?"
            ]
        ),
        new(
            TalkCategory.GoingReply,
            "someone in the crowd answers a question about a place. {place} is that place.",
            [
                "no clue", "went this morning, its dead", "{place} was packed earlier", "saw reds near {place}", "dunno, sorry",
                "no idea", "{place} is fine i think", "too dangerous for me", "went yesterday, good loot",
                "stay out of {place} lol", "heard {place} is quiet"
            ]
        ),
        new(
            TalkCategory.TavernNight,
            "patrons talking in an inn during the game's night. {town} is the town.",
            [
                "another round lol", "this ale is watered down", "long day of hunting", "who wants to hear how i died today",
                "anyone else cant sleep", "late night crew", "whos buying", "zzz so tired", "night hunting is scary",
                "love this tavern", "the barkeep never smiles", "one more drink then bed", "{town} is dead tonight",
                "its so dark out there"
            ]
        ),
        new(
            TalkCategory.NightTalk,
            "a character outdoors remarks on the game's night. {town} is the town when it stands in one.",
            [
                "so dark out", "cant see a thing", "need a torch", "night again already", "night sight pls",
                "creepy at night", "night time = pk time", "stars r nice tonight", "gonna head in, too dark",
                "who turned the lights off lol", "moon is out", "{town} at night gives me the creeps"
            ]
        ),
        new(
            TalkCategory.Traveling,
            "a character walking a long way talks to nobody in particular.",
            [
                "on the road again", "long walk lol", "need a recall rune for here", "walking is so slow",
                "anyone know the way?", "almost there", "why is everything so far", "running running", "road trip",
                "shoulda bought a horse", "these roads r never safe", "watch for reds on the road"
            ]
        ),
        new(
            TalkCategory.HuntTalk,
            "a hunter between fights. {place} is the hunting ground when known.",
            [
                "anything good around here?", "need more gold", "spawn is slow today", "these guys hit hard",
                "looking for something to kill", "need a better weapon", "hunting is slow", "where did all the monsters go",
                "bandages low", "one more kill then bank", "tactics going up", "who took all the spawn at {place}"
            ]
        ),
        new(
            TalkCategory.PlanFailed,
            "a character gave up on a plan it could not get done. {doing} is its work said like a player, like \"mining\".",
            [
                "that went nowhere", "wasted the whole day", "plans fell through", "not my day lol",
                "whole trip for nothing", "gave up on it", "maybe tomorrow", "nothing went right today",
                "well that was pointless", "scratched that plan", "couldnt get anything done today", "days like this",
                "no luck {doing} today", "spent all day {doing}, nothing to show", "{doing} was a bust today",
                "tried {doing}, went nowhere"
            ]
        ),
        new(
            TalkCategory.Emotes,
            "wordless gestures a character makes now and then. Keep each one wrapped in *stars*.",
            [
                "*stretches*", "*yawns*", "*looks around*", "*shrugs*", "*smiles*", "*nods*", "*sighs*",
                "*hums a tune*", "*leans against the wall*", "*cracks knuckles*", "*taps foot*", "*rubs eyes*"
            ]
        )
    ];
}
