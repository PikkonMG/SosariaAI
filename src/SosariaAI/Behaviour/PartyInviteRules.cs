using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// Whether a character accepts a real party invite. Free. No model.
/// </summary>
public static class PartyInviteRules
{
    /// <summary>
    /// Tiles within which a party member adds its power to the character's side
    /// (<see cref="CountsAsAlly"/>) and a character looks for a guild-war enemy:
    /// career.partyInviteRange, or its default before the file is loaded.
    /// </summary>
    public static int InviteRange =>
        SosariaSettings.Characters?.Career?.PartyInviteRange ?? CareerSettings.DefaultPartyInviteRange;

    public const int PowerSlack = 40;
    public const double MinHitsFraction = 0.60;
    public const int FriendScore = 20;

    public static bool PowerFits(int selfPower, int otherPower) =>
        otherPower > 0 && selfPower + PowerSlack >= otherPower && otherPower + PowerSlack >= selfPower;

    /// <summary>A party member adds power only while alive, on the same map, and close enough to help.</summary>
    public static bool CountsAsAlly(bool sameMap, bool alive, int distance, int range) =>
        sameMap && alive && distance >= 0 && distance <= range;

    public static bool Accepts(
        bool huntGoal,
        bool healthy,
        bool powerFits,
        int opinionScore,
        bool night,
        bool busyBanking
    )
    {
        if (!healthy || !powerFits || night)
        {
            return false;
        }

        if (busyBanking)
        {
            return false;
        }

        if (opinionScore <= -FriendScore)
        {
            return false;
        }

        return huntGoal || opinionScore >= FriendScore;
    }

    public static string AcceptLine() => "I am with you.";

    public static string DeclineLine() => "Not this time.";

    public static string GoodbyeLine() => "Good hunting. I am off.";

    public static string PullLine() => "pulling";

    public static string HealLine() => "heal";

    public static string BackLine() => "back";
}
