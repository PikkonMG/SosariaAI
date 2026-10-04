namespace SosariaAI.Social;

public static class ShardEventType
{
    public const string Pk = "pk";
    public const string Death = "death";
    public const string Party = "party";
    public const string Theft = "theft";
    public const string Red = "red";
    public const string GuildWar = "guild-war";
    public const string Treasure = "treasure";
    public const string SeaFind = "sea-find";
    public const string Duel = "duel";

    /// <summary>A red put down: the red is the actor, the people who killed it the others.</summary>
    public const string RedKill = "red-kill";
}
