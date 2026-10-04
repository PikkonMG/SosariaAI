namespace SosariaAI.Social;

/// <summary>
/// Talk category names. Each one is a file under Configuration/sosariaai/talk/ with the same
/// name and a .txt extension.
/// </summary>
public static class TalkCategory
{
    // Answers to a person at a keyboard.
    public const string RespondName = "respond_name";
    public const string RespondRoomGreet = "respond_room_greet";
    public const string RespondGreet = "respond_greet";
    public const string RespondGoodbye = "respond_goodbye";
    public const string RespondShrug = "respond_shrug";
    public const string RespondDoing = "respond_doing";
    public const string RespondInsult = "respond_insult";
    public const string RespondAck = "respond_ack";
    public const string RespondWhat = "respond_what";
    public const string FriendArrival = "friend_arrival";

    // Meetings between characters.
    public const string GreetWarm = "greet_warm";
    public const string GreetPlain = "greet_plain";
    public const string GreetCold = "greet_cold";
    public const string GreetReply = "greet_reply";
    public const string SmallTalk = "small_talk";
    public const string SmallTalkReply = "small_talk_reply";
    public const string FriendChain = "friend_chain";
    public const string GreetOldFriend = "greet_old_friend";
    public const string RecallAdventure = "recall_adventure";
    public const string GoingAsk = "going_ask";
    public const string GoingReply = "going_reply";
    public const string TavernNight = "tavern_night";
    public const string NightTalk = "night_talk";
    public const string Traveling = "traveling";
    public const string HuntTalk = "hunt_talk";
    public const string PlanFailed = "plan_failed";
    public const string Emotes = "emotes";

    // Groups.
    public const string LfgShout = "lfg_shout";
    public const string LfgJoinCall = "lfg_join_call";
    public const string LfgOfferPlayer = "lfg_offer_player";
    public const string LfgInvite = "lfg_invite";
    public const string LfgStart = "lfg_start";
    public const string LfgGiveUp = "lfg_giveup";
    public const string LfgGg = "lfg_gg";
    public const string LfgGgReply = "lfg_gg_reply";
    public const string LfgDecline = "lfg_decline";
    public const string LfgAskPlayer = "lfg_ask_player";
    public const string PartyDescend = "party_descend";
    public const string PartyTakeLead = "party_take_lead";
    public const string PartyReady = "party_ready";
    public const string PartyDepartBanter = "party_depart_banter";
    public const string PartyReturnBanter = "party_return_banter";
    public const string PartyMuster = "party_muster";
    public const string PartyGate = "party_gate";
    public const string DungeonEnter = "dungeon_enter";
    public const string DungeonLeave = "dungeon_leave";

    // Guilds.
    public const string GuildChatter = "guild_chatter";
    public const string GuildWelcome = "guild_welcome";
    public const string GuildAskGroup = "guild_ask_group";
    public const string GuildOnMyWay = "guild_omw";
    public const string GuildCantCome = "guild_cant";
    public const string GuildInvitePlayer = "guild_invite_player";
    public const string GuildJoin = "guild_join";
    public const string GuildWarRally = "guild_war_rally";
    public const string OrderBattle = "order_battle";
    public const string ChaosBattle = "chaos_battle";
    public const string OrderTaunt = "order_taunt";
    public const string ChaosTaunt = "chaos_taunt";
    public const string ScuffleWatch = "scuffle_watch";
    public const string ScuffleYield = "scuffle_yield";
    public const string ScuffleCall = "scuffle_call";
    public const string WarBandDepart = "warband_depart";
    public const string ConvoyDepart = "convoy_depart";
    public const string SweepDepart = "sweep_depart";
    public const string PosseDepart = "posse_depart";

    // Fighting.
    public const string CombatEngage = "combat_engage";
    public const string CombatAssist = "combat_assist";
    public const string CombatPull = "combat_pull";
    public const string CombatFlee = "combat_flee";
    public const string CombatTurn = "combat_turn";
    public const string CombatClear = "combat_clear";
    public const string CombatVictory = "combat_victory";
    public const string LootHaul = "loot_haul";
    public const string HuntHaul = "hunt_haul";
    public const string HuntDry = "hunt_dry";
    public const string RedReply = "red_reply";
    public const string RedOnMyWay = "red_omw";
    public const string PkAttack = "pk_attack";
    public const string PkRan = "pk_ran";
    public const string PkLoot = "pk_loot";
    public const string PkWentRed = "pk_went_red";
    public const string RedWarn = "red_warn";
    public const string GuildWarStart = "guild_war_start";
    public const string GuardStandDown = "guard_stand_down";
    public const string HouseRetreat = "house_retreat";
    public const string HouseReturn = "house_return";
    public const string ThiefVictim = "thief_victim";

    // Death and raising.
    public const string GhostHaunt = "ghost_haunt";
    public const string GhostPlea = "ghost_plea";
    public const string ResThanks = "res_thanks";
    public const string ResOffer = "res_offer";
    public const string ResWelcome = "res_welcome";
    public const string DeathLooted = "death_looted";
    public const string DeathJoke = "death_joke";
    public const string DeathJokeReply = "death_joke_reply";
    public const string LootedReply = "looted_reply";

    // Duels.
    public const string DuelChallenge = "duel_challenge";
    public const string DuelAccept = "duel_accept";
    public const string DuelWin = "duel_win";
    public const string DuelLoss = "duel_loss";
    public const string DuelOnlooker = "duel_onlooker";
    public const string DuelCheer = "duel_cheer";

    // The bank floor.
    public const string Wts = "wts";
    public const string Wtb = "wtb";
    public const string WtsReply = "wts_reply";
    public const string WtbReply = "wtb_reply";

    // Crafting.
    public const string CraftDone = "craft_done";
    public const string SmithTalk = "smith_talk";
    public const string TailorTalk = "tailor_talk";
    public const string CarpenterTalk = "carpenter_talk";
    public const string CraftTalk = "craft_talk";
    public const string CraftAsk = "craft_ask";
    public const string CraftPrice = "craft_price";
    public const string CraftSoldOut = "craft_sold_out";
    public const string CraftThanks = "craft_thanks";
    public const string CraftMasterwork = "craft_masterwork";
    public const string CraftBuyStock = "craft_buy_stock";
    public const string CraftNeed = "craft_need";
    public const string GatherDeliver = "gather_deliver";
    public const string StockToBank = "stock_to_bank";
    public const string CraftBankStock = "craft_bank_stock";
    public const string GearFromCrafter = "gear_from_crafter";

    // Gathering and taming.
    public const string GatherHaul = "gather_haul";
    public const string MiningTalk = "mining_talk";
    public const string LumberTalk = "lumber_talk";
    public const string FishingTalk = "fishing_talk";
    public const string TameSuccess = "tame_success";
    public const string TameFail = "tame_fail";

    // The street at the bank.
    public const string StreetBeg = "street_beg";
    public const string StreetBegGiveUp = "street_beg_giveup";
    public const string StreetNewbie = "street_newbie";
    public const string StreetNewbieGiveUp = "street_newbie_giveup";
    public const string ShopBrowse = "shop_browse";

    // Treasure and the sea.
    public const string SosHawk = "sos_hawk";
    public const string SosBottleCatch = "sos_bottle_catch";
    public const string SosMapCatch = "sos_map_catch";
    public const string TreasureCannotRead = "thunt_cannot_read";
    public const string TreasureDecoded = "thunt_decoded";
    public const string TreasureChestUp = "thunt_chest_up";
    public const string TreasureLoot = "thunt_loot";
}
