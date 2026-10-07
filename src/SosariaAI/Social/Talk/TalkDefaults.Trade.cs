namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    private static TalkTopic[] Trade() =>
    [
        new(
            TalkCategory.Wts,
            "a seller shouts at a bank. {item} is the real goods, {price} the asking price.",
            [
                "WTS {item} {price}", "wts {item} {price}", "selling {item} {price}", "{item} {price} obo", "wts {item}, {price}",
                "{item} for sale, {price}", "got a {item}, {price}", "WTS {item}!! {price}", "anyone need a {item}? {price}",
                "{item} cheap, {price}", "{price} for a {item}, anyone"
            ]
        ),
        new(
            TalkCategory.Wtb,
            "a buyer shouts at a bank. {item} is what it really needs.",
            [
                "WTB {item}", "wtb {item}", "buying {item}", "need {item}, paying gold", "anyone selling {item}?",
                "looking for {item}", "WTB {item}!!", "need {item}, will pay", "who has {item}?", "buying {item}, bring it here"
            ]
        ),
        new(
            TalkCategory.WtsReply,
            "a bystander comments on a WTS shout. {name} is the seller, {item} the goods, {price} the price.",
            [
                "lol {price} for that?", "{price}?? its worth half", "nice {item}", "bump lol", "{name} ur prices r nuts",
                "i'd take it if i had gold", "too rich for me", "already got one", "who even uses a {item}", "gl selling that"
            ]
        ),
        new(
            TalkCategory.WtbReply,
            "a bystander comments on a WTB shout. {name} is the buyer, {item} what it wants.",
            [
                "try the vendor", "havent seen any {item} in days", "{item}? good luck lol", "everyone wants {item} today",
                "i sold mine yesterday", "gl {name}", "ask a crafter", "{item} prices went up", "same, need {item} too",
                "shop's out of {item} i think"
            ]
        ),
        new(
            TalkCategory.CraftDone,
            "a crafter finishes a batch. {item} is the trade's goods, {count} how many it made.",
            [
                "{count} {item} done", "another batch of {item} done", "made {count}, selling to the shop", "{item} done, my arms hurt lol",
                "batch done", "thats {count} more {item}", "skill up lol", "done with this round", "ok thats enough {item} for now",
                "{count} {item}, not bad"
            ]
        ),
        new(
            TalkCategory.ItemIdentified,
            "a merchant that just identified a customer's magic piece for its fee. {name} is the customer, {item} the piece as named.",
            [
                "{name} its a {item}", "thats a {item} {name}", "{item}, nice one {name}", "{name} u got a {item}",
                "{item} {name}, not bad", "its a {item}", "{name}: {item}", "{item}, enjoy {name}",
                "there u go {name}, {item}", "{item}, good find {name}"
            ]
        ),
        new(
            TalkCategory.CraftTakingOrders,
            "a crafter at its station that can take an order right now. {item} is the trade's goods.",
            [
                "taking orders, {item} and more", "gm {item} made to order, ask me", "orders open, tell me what u need",
                "taking orders for {item}", "need {item}? i take orders", "orders open here, just ask", "taking orders, gm work only",
                "tell me what u need, ill make it", "taking orders today, {item} mostly", "custom {item}, ask me"
            ]
        ),
        new(
            TalkCategory.SmithTalk,
            "a smith at work.",
            [
                "ingots r so expensive", "need iron ingots, anyone?",
                "this forge is always crowded", "90 smith, almost there", "making kryss for skill, dont judge",
                "anyone got colored ore?", "[eras: ml,modern] runic hammers anyone?", "selling plate at the bank later",
                "halberds for skill, so boring", "someone buy these daggers pls"
            ]
        ),
        new(
            TalkCategory.TailorTalk,
            "a tailor at work.",
            [
                "need cloth, anyone?", "sewing for skill, zzz", "bolts of cloth r cheap at the weaver", "anyone want dyed robes?",
                "making caps for skill lol", "need hides for leather", "studded armor for sale later",
                "this is so slow", "who wants a fancy shirt", "[eras: ml,modern] need spined leather"
            ]
        ),
        new(
            TalkCategory.CarpenterTalk,
            "a carpenter at work.",
            [
                "sawing boards all day", "need logs, anyone?", "making chairs for skill lol", "carpentry is so slow",
                "anyone need a chest?", "gm carp here", "boards anyone? got plenty", "making staves, want one?",
                "furniture for ur house?", "lumber prices r crazy", "almost 80 carp"
            ]
        ),
        new(
            TalkCategory.CraftTalk,
            "any other crafter at work: tinker, alchemist, scribe, bowyer, cook.",
            [
                "crafting for skill, so slow", "anyone need anything made?", "out of mats again", "this skill takes forever",
                "just need a few more points", "vendor prices for mats r a ripoff", "anyone selling mats?", "almost gm lol",
                "selling what i make at the bank", "tools keep breaking", "one more batch then im done"
            ]
        ),
        new(
            TalkCategory.CraftAsk,
            "a shopper asks a crafter who just finished a batch. {name} is the crafter, {item} its goods.",
            [
                "{name} u selling any {item}?", "how much for {item}?", "got any {item} left?",
                "whats {item} going for?", "nice work {name}, selling?", "need {item}, u got any?",
                "{name} u gm yet?", "any exceptional {item}?", "{name} got any gm {item}?", "is that {item} for sale {name}?"
            ]
        ),
        new(
            TalkCategory.CraftPrice,
            "a crafter names what it kept back. {price} is the real price a piece, {item} the goods.",
            [
                "{price} each", "{price} for {item}", "{price} ea, cheaper than the shop", "{price}, for u", "{price} and its yours",
                "{price} gold", "kept a few, {price} each", "{price}, sold the rest to the shop", "{price} a piece", "for {price} sure"
            ]
        ),
        new(
            TalkCategory.CraftSoldOut,
            "a crafter sold the whole batch to the shop.",
            [
                "sold it all to the shop sry", "none left, try later", "all gone lol", "shop bought everything",
                "next batch, gimme a few", "out, sorry", "just sold the last one", "come back in a bit", "nothing left, sry",
                "ask me later"
            ]
        ),
        new(
            TalkCategory.CraftThanks,
            "the shopper ends the talk with the crafter. {name} is the crafter.",
            ["k ty", "cool thx", "ill think about it", "maybe later", "too rich lol", "ok ty", "nice", "ill come back", "k", "thx {name}"]
        ),
        new(
            TalkCategory.CraftMasterwork,
            "a grandmaster crafter finishes an exceptional piece. {item} is the piece.",
            [
                "exceptional {item}, first try", "gm {item} done, who wants it?", "look at this {item}, exceptional",
                "another exceptional {item} lol", "made an exceptional {item}!", "{item} came out exceptional",
                "gm made {item}, selling at the bank later", "now thats a {item}", "exceptional {item}, my mark on it",
                "finally an exceptional {item}"
            ]
        ),
        new(
            TalkCategory.CraftNeed,
            "a crafter at its station is out of stock or without its tool and asks the room. {item} is what it lacks.",
            [
                "anyone got {item}? paying", "need {item}, out again", "who has {item}? will pay", "out of {item} lol",
                "anyone selling {item}?", "cant work without {item}", "need {item} bad", "shop is out of {item}, anyone?",
                "paying for {item}, bring it here", "{item} anyone? will pay well", "buying {item}, anyone?", "any {item} around?"
            ]
        ),
        new(
            TalkCategory.CraftBuyStock,
            "a crafter buys a gatherer's load. {name} is the gatherer, {item} the stock.",
            [
                "ill take the {item}, {name}", "{item}? yes pls", "perfect, i was out of {item}", "ty {name}, needed that",
                "{item} for the forge, nice", "been waiting on {item} all day", "sold, hand it over lol", "good price {name}",
                "just what i needed", "bring me more {item} later"
            ]
        ),
        new(
            TalkCategory.GatherDeliver,
            "a gatherer sells its load to a crafter. {name} is the crafter, {item} the load.",
            [
                "{name} want some {item}?", "got {item} for u {name}", "fresh {item}, cheaper than the vendor",
                "{item} here, who needs it", "selling {item} to the crafters", "{name} u buying {item}?",
                "{item} for sale, {name}", "full pack of {item}", "ty {name}", "pleasure doing business"
            ]
        ),
        new(
            TalkCategory.StockToBank,
            "a gatherer heads for the bank, where a crafter waits for its load. {name} is the crafter, {item} the load.",
            [
                "{name} wants {item} at the bank, omw", "someone at the bank needs {item}, going", "bank first, {name} is buying",
                "heard {name} wants {item}", "gonna sell these {item} at the bank", "{item} for {name}, be right there",
                "bank pays better for {item}", "{name} hold on, bringing {item}", "wtb {item}? on my way {name}",
                "a crafter at the bank wants my {item}"
            ]
        ),
        new(
            TalkCategory.CraftBankStock,
            "a crafter out of stock gives up waiting at its station and tries the bank. {item} is the stock.",
            [
                "no {item} here, trying the bank", "gonna go shout for {item} at the bank", "bank, somebody there has {item}",
                "nobody brought {item}, off to the bank", "maybe a miner is at the bank", "cant work, need {item}, brb",
                "heading to the bank for {item}", "{item} hunt at the bank lol", "someone at the bank must have {item}",
                "back soon, need {item}"
            ]
        ),
        new(
            TalkCategory.GearFromCrafter,
            "a buyer puts on a piece it bought from a crafter at the bank. {name} is the crafter, {item} the piece.",
            [
                "nice work {name}", "{item} fits great, ty", "way better than the shop stuff", "ty {name}, looks good",
                "fresh {item} from {name}", "love this {item}", "{name} makes good gear", "finally a real {item}",
                "wearing it now, thx", "will buy from u again {name}"
            ]
        ),
        new(
            TalkCategory.GatherHaul,
            "a gatherer ends a session with a full pack. {item} is the resource, {count} how much it holds.",
            [
                "pack is full of {item}", "{count} {item}, time to go bank", "heavy lol, back to town", "full pack, cya",
                "got my {item}, done here", "{item} run done", "so much {item}, cant move lol", "overweight again",
                "that should sell ok", "enough {item} for today", "{count} {item}, not bad at all"
            ]
        ),
        new(
            TalkCategory.MiningTalk,
            "a miner at work.",
            [
                "mining is so boring", "wheres all the ore", "no ore here, moving", "this cave is packed", "anyone seen colored ore?",
                "i hate mining lol", "my pickaxe broke again", "dig dig dig", "careful reds love the mines",
                "an earth elemental nearly got me earlier", "[eras: ml,modern] a fire beetle would be nice", "need a pack horse"
            ]
        ),
        new(
            TalkCategory.LumberTalk,
            "a lumberjack at work.",
            [
                "chop chop", "trees respawn so slow", "anyone need logs?", "lumberjacking = boredom", "my arms lol",
                "chopping for bowcraft", "reapers in these woods, watch out", "logs r heavy", "hatchet is almost broke",
                "back to the woods", "[eras: ml,modern] need heartwood lol", "wood wood wood"
            ]
        ),
        new(
            TalkCategory.FishingTalk,
            "a fisher at work.",
            [
                "fish r biting today", "nothing but fish lol", "hope for an sos", "fishing is so relaxing", "caught a boot once lol",
                "anyone buying fish steaks?", "big fish!!", "zzz fishing", "sea serpents out there careful", "fishing for skill",
                "need a boat so bad", "the docks are the best spot"
            ]
        ),
        new(
            TalkCategory.TameSuccess,
            "a tamer tamed a creature. {item} is the creature.",
            [
                "tamed it!", "got the {item}!", "yes, new pet", "{item} is mine lol", "finally tamed it", "whos a good {item}",
                "tame success", "took forever but got it", "new {item}, gonna name it", "pet time"
            ]
        ),
        new(
            TalkCategory.TameFail,
            "a tame attempt failed. {item} is the creature.",
            [
                "wont take", "grr it attacked me", "stupid {item}", "failed again", "{item} hates me lol", "come on...",
                "almost had it", "why wont u love me {item}", "taming fail", "its angry now, run"
            ]
        ),
        new(
            TalkCategory.SosHawk,
            "a fisher with a find it will not use. {item} is the find, {price} the asking price.",
            [
                "dont dig myself. {item} for sale, {price}", "{item} off my line, {price} to whoever wants it",
                "reeled up a {item}. {price} and its yours", "any treasure hunters? {item}, {price}", "wts {item} {price}, fresh off the hook",
                "{item} here, {price} obo", "fished a {item}, {price} takes it", "selling {item} {price}, no use to me",
                "{item} {price}, cheaper than the shop lol", "who digs? {item} for {price}"
            ]
        ),
        new(
            TalkCategory.SosBottleCatch,
            "a fisher pulls up a message in a bottle.",
            [
                "corked bottle on the hook, note inside", "huh, a bottle with a letter in it", "that aint a fish... its a bottle",
                "bottle!! finally", "got a message in a bottle", "sos bottle on the line", "ooh a bottle, somebody's in trouble lol",
                "fishing up bottles again", "a bottle... wonder whats in it", "nice, sos"
            ]
        ),
        new(
            TalkCategory.SosMapCatch,
            "a fisher pulls up a treasure map.",
            [
                "soggy old map on my hook", "pulled up a waterlogged map lol", "is that a treasure map? on my hook?", "fished a tmap lol",
                "old map came up with the fish", "who throws maps in the water", "got a map fishing, weird", "soggy tmap, still readable?",
                "nice, a map", "map fished up, gross its wet"
            ]
        ),
        new(
            TalkCategory.TreasureCannotRead,
            "a treasure hunter sells a map too hard for it. {item} is the map, {price} the asking price.",
            [
                "cant read this {item}. {price} obo", "wts {item} {price}, too hard for me", "{item} for sale, cant decode it. {price}",
                "{item} too high for me, {price}", "wts {item} {price}, my carto sucks", "anyone want a {item}? {price}",
                "{item} {price}, cant decode", "selling {item}, {price} obo", "got a {item} i cant read, {price}",
                "{price} for a {item}, any takers"
            ]
        ),
        new(
            TalkCategory.TreasureDecoded,
            "a treasure hunter decoded its map.",
            [
                "decoded it!", "finally decoded this map", "carto came through, map is read", "map read, off to dig",
                "decoded, its not even far", "ok got the spot", "map decoded, need a shovel lol", "read it on the first try!",
                "decoded. wish me luck", "alright treasure time"
            ]
        ),
        new(
            TalkCategory.TreasureChestUp,
            "a treasure hunter dug the chest out.",
            [
                "chest is up!", "got it, chest is out of the ground", "theres the chest", "CHEST!", "its up!!", "here come the guardians",
                "chest up, watch for the spawn", "got it out", "chest's up, kill the spawn first", "yes!! chest"
            ]
        ),
        new(
            TalkCategory.TreasureLoot,
            "a treasure hunter emptied the chest. {price} is the gold it held.",
            [
                "{price} gold in the chest, not bad", "chest had {price}, meh", "{price} gold, worth it", "only {price}?? lol",
                "sweet {price} gold", "looted {price} out of it", "{price} and some junk", "nice, {price}", "decent loot", "worth the dig"
            ]
        )
    ];
}
