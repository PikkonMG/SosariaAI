namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    /// <summary>The bank's street life and the shop floor.</summary>
    private static TalkTopic[] Street() =>
    [
        new(
            TalkCategory.StreetBeg,
            "a beggar at the bank, to the room or to a player it trails. {name} is that player when there is one.",
            [
                "spare some gold?", "any gp for a poor guy", "got killed, lost it all, plz help", "anything helps", "gold plz",
                "cant afford bandages lol", "need 40gp for a hatchet", "{name} spare a few gp?", "{name} got any old armor?",
                "{name} plz, just 20 gold", "{name} u look rich, help a guy out", "pk took my stuff... any gold?"
            ]
        ),
        new(
            TalkCategory.StreetBegGiveUp,
            "a beggar gives up on the player it trailed.",
            [
                "bah", "fine", "cheapskate", "nm then", "ok ok", "stingy", "k thx anyway", "whatever", "rich and cheap",
                "guess not", "someone else then"
            ]
        ),
        new(
            TalkCategory.StreetNewbie,
            "a day-one player at the bank, to the room or to a player it trails. {name} is that player when there is one.",
            [
                "wheres the healer in this town?", "how do u get gold fast", "whats the best skill to raise?",
                "is it safe to go outside town?", "how do i put stuff in the bank", "anyone know where to buy a horse?",
                "{name} how do u train swords?", "{name} where do i sell stuff?", "{name} what dungeon is easy?",
                "{name} can u help a newb?", "why do some names show red?", "how do u open the paperdoll again"
            ]
        ),
        new(
            TalkCategory.StreetNewbieGiveUp,
            "a day-one player stops trailing the player it asked.",
            [
                "ok ty", "nvm", "ill ask someone else", "thx anyway", "k cya", "oh ok", "ok ill figure it out",
                "brb looking for a guide", "k", "hmm ok", "this game is confusing lol"
            ]
        ),
        new(
            TalkCategory.ShopBrowse,
            "someone looking round a shop with nothing on its list.",
            [
                "just looking", "how much is that?", "hmm", "too rich for me", "nice stuff in here", "maybe later",
                "anyone selling this cheaper at the bank?", "lol these prices", "ill think about it", "got anything gm made?",
                "whats good today?", "cant afford any of it yet"
            ]
        )
    ];
}
