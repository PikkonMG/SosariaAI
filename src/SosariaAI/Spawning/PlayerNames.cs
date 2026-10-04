namespace SosariaAI.Spawning;

/// <summary>
/// The raw parts of player names, written for this shard. Players of the period took a
/// plain fantasy name, added a surname or a home town, used a handle from a book or a
/// film, or typed their own first name in lower case. Letters, spaces, apostrophes and
/// hyphens only: the client refused digits in a character name.
/// </summary>
public static class PlayerNames
{
    public static readonly string[] Male =
    [
        "Aedan", "Albin", "Aldous", "Alric", "Ambrose", "Anselm", "Arlen", "Armand", "Arno", "Arvid",
        "Asher", "Audric", "Axel", "Balin", "Barnaby", "Bastian", "Benedikt", "Berin", "Bertil", "Bevis",
        "Birger", "Blaise", "Bodo", "Borin", "Brannoc", "Brennan", "Brom", "Bryce", "Caddoc", "Calder",
        "Callum", "Casimir", "Cathal", "Cedric", "Cerdic", "Ciaran", "Clovis", "Colby", "Conall", "Conrad",
        "Crispin", "Cullen", "Cyril", "Dagfinn", "Dain", "Dalton", "Damek", "Darian", "Davin", "Deacon",
        "Delmar", "Denholm", "Derwin", "Dietrich", "Dorian", "Drummond", "Dunstan", "Eadric", "Ector", "Edmund",
        "Egil", "Einar", "Eldon", "Elias", "Emrys", "Endre", "Erland", "Esmond", "Evander", "Ewan",
        "Falk", "Faramond", "Felix", "Fenwick", "Florian", "Folke", "Fulbert", "Gaius", "Galen", "Garth",
        "Gaspard", "Geraint", "Gervase", "Gilbert", "Godfrey", "Gorm", "Gregor", "Griffith", "Gunther", "Guthrie",
        "Hakon", "Halvard", "Hamish", "Harlan", "Hasso", "Hector", "Heinrich", "Herrick", "Hilliard", "Horst",
        "Howell", "Ignatius", "Ingram", "Isidore", "Ivo", "Jago", "Jarvis", "Jasper", "Jonas", "Jorund",
        "Josse", "Kalle", "Kasimir", "Keegan", "Kendrick", "Kenric", "Kieran", "Knut", "Konrad", "Lambert",
        "Lancel", "Lars", "Leander", "Lennart", "Leofwin", "Lionel", "Lothar", "Lucan", "Ludo", "Madoc",
        "Malcolm", "Manfred", "Marek", "Matthias", "Maxim", "Meinhard", "Milo", "Mordecai", "Morten", "Mungo",
        "Nestor", "Niall", "Nikolai", "Norbert", "Odo", "Olaf", "Osborn", "Oswald", "Otto", "Pascal",
        "Pelham", "Peregrin", "Pieter", "Quentin", "Radek", "Ragnall", "Rainer", "Randolf", "Reinhold", "Remy",
        "Reynard", "Rhodri", "Roald", "Robard", "Rodrick", "Roland", "Rowan", "Rufus", "Rupert", "Sander",
        "Saul", "Sebastien", "Selwyn", "Sigmund", "Simeon", "Stefan", "Sweyn", "Tancred", "Teague", "Terrin",
        "Thaddeus", "Theobald", "Thorald", "Tiernan", "Tobiah", "Torben", "Tybalt", "Ulf", "Urien", "Valdemar",
        "Vernon", "Viggo", "Vincent", "Walden", "Walther", "Warrick", "Wendel", "Wilfred", "Wolfram", "Wystan",
        "Yann", "Yorath", "Zacharias", "Zeno", "Ansgar", "Brandr", "Corbin", "Destrian", "Eberhard", "Fitzroy",
        "Gunnvald", "Hrolf", "Ivain", "Jerrik", "Kolbein", "Lorne", "Merric", "Norvel", "Orrin", "Perrin"
    ];

    public static readonly string[] Female =
    [
        "Adela", "Adeline", "Agathe", "Ailsa", "Alarice", "Alys", "Amabel", "Amice", "Anneke", "Annora",
        "Ariadne", "Astrid", "Aurelie", "Avis", "Beatrix", "Belisent", "Berit", "Bettina", "Blanche", "Bronwen",
        "Brunhild", "Calla", "Camille", "Cassia", "Catrin", "Cecily", "Celeste", "Clemence", "Colette", "Constance",
        "Cordelia", "Dagny", "Dalla", "Delphine", "Desiree", "Dorcas", "Dulcie", "Ebba", "Edda", "Eira",
        "Elinor", "Eloise", "Elspeth", "Emeline", "Enid", "Estrid", "Etta", "Eudora", "Evangeline", "Fenella",
        "Fiora", "Flavia", "Fleur", "Frida", "Gaela", "Gisela", "Godiva", "Grainne", "Gudrun", "Gwendolyn",
        "Hadewych", "Halla", "Hedda", "Heloise", "Hilde", "Honora", "Hulda", "Ida", "Ilse", "Imogen",
        "Ingrid", "Iona", "Isaura", "Isobel", "Jehanne", "Jessamy", "Jolene", "Josette", "Juliane", "Karin",
        "Katla", "Kerensa", "Kestra", "Laurel", "Leocadia", "Lettice", "Liesel", "Linnea", "Lisbet", "Lorna",
        "Lucinda", "Lunet", "Mabyn", "Madlen", "Maelis", "Magda", "Maida", "Malin", "Marit", "Marjory",
        "Matilde", "Mavis", "Melisande", "Meriel", "Minna", "Mirabel", "Morwenna", "Nadia", "Nell", "Nerys",
        "Nicolette", "Ninette", "Noor", "Odile", "Olwen", "Orla", "Osanna", "Petra", "Philippa", "Piety",
        "Primrose", "Ragna", "Rhiannon", "Rilla", "Rosalind", "Rosamund", "Runa", "Sabine", "Saoirse", "Selene",
        "Seren", "Sibyl", "Signy", "Solveig", "Sorcha", "Svana", "Tamsin", "Thyra", "Tilda", "Tova",
        "Ulla", "Ursel", "Valka", "Verena", "Viola", "Wilhelmina", "Wren", "Ysabel", "Yvaine", "Zelda",
        "Adalie", "Brisa", "Cressida", "Dervla", "Elowen", "Fianna", "Gilda", "Hesper", "Idony", "Jorunn",
        "Kaisa", "Leonie", "Maren", "Nesta", "Oriane", "Perpetua", "Rowena", "Senna", "Teodora", "Vesna",
        "Aldith", "Bryony", "Carys", "Damaris", "Evadne", "Freydis", "Gwenith", "Hollis", "Inga", "Jocasta",
        "Kinna", "Lark", "Mireille", "Nimue", "Ottilie", "Peony", "Quenby", "Roswitha", "Sunniva", "Theda",
        "Una", "Vianne", "Wynne", "Xanthe", "Yseult", "Zinnia", "Alwen", "Branwen", "Clio", "Delia",
        "Elva", "Fern", "Gerd", "Hazel", "Isolde", "Juno", "Kelda", "Lysa", "Mirren", "Neve"
    ];

    public static readonly string[] Surnames =
    [
        "Ashdown", "Ashford", "Barrow", "Beckett", "Blackwood", "Blythe", "Bramble", "Brightwater", "Brook", "Burrows",
        "Caldwell", "Carver", "Chandler", "Cinder", "Coldbrook", "Cooper", "Crane", "Crowley", "Dale", "Darrow",
        "Dunmore", "Eastwick", "Emberly", "Fairbairn", "Falconer", "Fallow", "Fenn", "Fletcher", "Flint", "Forester",
        "Foxley", "Frost", "Gale", "Garrow", "Gildersleeve", "Glass", "Goodale", "Graves", "Greaves", "Greenholt",
        "Grimsby", "Hale", "Hallow", "Hargrove", "Harrow", "Hatch", "Hawke", "Hayward", "Heath", "Holloway",
        "Holt", "Hornby", "Hunter", "Ironside", "Ivers", "Kettle", "Kingsley", "Kipling", "Knowles", "Lacey",
        "Lamb", "Larkin", "Latch", "Leach", "Lockwood", "Lowell", "Mallory", "Marsh", "Mason", "Merriweather",
        "Millbrook", "Moor", "Morrow", "Nettle", "Northam", "Oakes", "Oldfield", "Orme", "Page", "Pennick",
        "Pike", "Pollard", "Quarry", "Radley", "Ravenhill", "Reed", "Ridley", "Roper", "Rook", "Rowntree",
        "Rushby", "Sallow", "Sawyer", "Scrivener", "Sedge", "Shaw", "Shepherd", "Slate", "Smythe", "Sparrow",
        "Stark", "Stone", "Storrow", "Strand", "Summerby", "Swift", "Tanner", "Tarrant", "Thackeray", "Thatcher",
        "Thorne", "Tolliver", "Trask", "Tull", "Underhill", "Vale", "Vane", "Varley", "Wainwright", "Walsh",
        "Warden", "Webber", "Weller", "Westbrook", "Whitlock", "Wick", "Wilde", "Winter", "Woodward", "Wren",
        "Yardley", "Yates", "Ambrey", "Birchall", "Colter", "Drake", "Elder", "Farrier", "Garner", "Hollins",
        "Ingle", "Jessop", "Keel", "Linden", "Marlowe", "Nash", "Osgood", "Pritchard", "Quill", "Rainsford",
        "Stroud", "Tamsett", "Upton", "Voss", "Wardle", "Ashgrove", "Bellamy", "Corwen", "Dunlow", "Eldridge",
        "Fairweather", "Goldhawk", "Harker", "Inchbald", "Jarrow", "Kestrel", "Loxley", "Moorcroft", "Nightingale", "Ormsby",
        "Penhallow", "Ravensworth", "Saltmarsh", "Thornbury", "Wolfe", "Blackmoor", "Copperfield", "Duskwood", "Everhart", "Greymane",
        "Hollowell", "Ironwood", "Mossbank", "Oakhart", "Redfern", "Stormcrow", "Tallow", "Whitmore", "Wyvern", "Ashcombe"
    ];

    /// <summary>Towns and keeps of the period, for the "Name of Town" style.</summary>
    public static readonly string[] Towns =
    [
        "Britain", "Yew", "Minoc", "Vesper", "Trinsic", "Moonglow", "Magincia", "Jhelom", "Skara Brae", "Cove",
        "Nujel'm", "Ocllo", "Wind", "Papua", "Delucia", "Buc's Den", "Serpent's Hold"
    ];

    public static readonly string[] Epithets =
    [
        "the Bold", "the Red", "the Black", "the Grim", "the Quiet", "the Tall", "the Lame", "the Lucky",
        "the Wise", "the Young", "the Old", "the Fair", "the Wanderer", "the Tanner", "the Brave", "the Sly",
        "the Mad", "the Just", "the Stout", "the Swift", "the Pale", "the Kind", "the Cruel", "the Poor",
        "the Hunter", "the Scribe", "the Bard", "the Grey", "the Loud", "the Green", "the Tired", "the Slow"
    ];

    /// <summary>Handles players picked: borrowed heroes, grim nouns and misspelled menace.</summary>
    public static readonly string[] Handles =
    [
        "Shadowblade", "Nightshade", "Deathstalker", "Grimreaper", "Bloodraven", "Darkstar", "Stormbringer", "Frostbite",
        "Ironfist", "Dragonslayer", "Wolfsbane", "Ravenclaw", "Soulreaver", "Doomhammer", "Hellfire", "Skullcrusher",
        "Viper", "Reaper", "Phantom", "Wraith", "Spectre", "Banshee", "Havoc", "Mayhem",
        "Venom", "Talon", "Striker", "Bladedancer", "Silverhand", "Thunderfist", "Firebrand", "Moonshadow",
        "Blackheart", "Deathwish", "Nightwalker", "Warlock", "Necromancer", "Sorcerer", "Lich", "Paladin",
        "Crusader", "Templar", "Berserker", "Gladiator", "Mercenary", "Assassin", "Ninja", "Samurai",
        "Kaos", "Deth", "Skullz", "Killah", "Mistik", "Phyre", "Darkk", "Xtreme",
        "Dethknight", "Shadoe", "Dragyn", "Bloodlust", "Soulz", "Kewl Dude", "Phreak", "Kraken",
        "Merlin", "Gandolf", "Elric", "Conan", "Tasslehoff", "Drizzle", "Raistlin", "Tanis",
        "Beowulf", "Lancelot", "Galahad", "Morgana", "Excalibur", "Mordred", "Zorro", "Rambo",
        "Maverick", "Goose", "Iceman", "Blade", "Spike", "Tank", "Hammer", "Axe Man",
        "Big Tom", "Lil Bob", "Mr Grumbles", "Dr Evil", "Sir Loin", "Captain", "Pirate Pete", "Stinky",
        "Bubba", "Chainsaw", "Tuna", "Moose", "Goober", "Sparky", "Muffin", "Pickles",
        "Loki", "Odin", "Thor", "Freya", "Zeus", "Hades", "Anubis", "Osiris",
        "Grendel", "Balrog", "Smaug", "Gollum", "Orcbane", "Trollslayer", "Lichbane", "Daemonbane",
        "Wildfire", "Brimstone", "Cinderfall", "Ashen", "Hollow", "Grimm", "Vex", "Rascal"
    ];

    /// <summary>
    /// Plain first names typed in lower case, as many first-time players did. None reads as a
    /// word of the line it stands in: a tamer named "me" wrote "me says: heh, tamed a whole pen
    /// of chickens" and "me tamed and let go a WhiteWolf".
    /// </summary>
    public static readonly string[] Casual =
    [
        "bob", "steve", "mike", "dave", "chris", "matt", "jeff", "brian", "kevin", "scott",
        "jason", "eric", "greg", "tony", "rick", "danny", "joey", "tim", "andy", "paul",
        "jen", "amy", "lisa", "sara", "katie", "missy", "becky", "tina", "heather", "nikki",
        "bobby", "stevo", "mikey", "davey", "jimbo", "billy", "johnny", "tommy", "frank", "ed",
        "carl", "doug", "gary", "larry", "phil", "ron", "ted", "walt", "earl", "chuck",
        "kim", "dana", "jess", "mandy", "shelly", "stacy", "tara", "wendy", "carrie", "angie",
        "dude", "newbie", "newb", "guest", "player", "someguy", "whoami", "sam", "kenny", "lurker"
    ];
}
