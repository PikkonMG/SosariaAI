using SosariaAI.Behaviour;

namespace SosariaAI.Combat;

/// <summary>
/// Blues against reds. A lawful fighter warns a victim; adventurers and dungeon crews draw
/// on a red wherever the guards do not reach; a red or gray hurting a person is a target
/// on sight. Under the guards the answer is a shout for them instead.
/// </summary>
public static class LawfulRules
{
    /// <summary>A hunter takes on a red when its side reaches this share of the red's power.</summary>
    public const double HuntOdds = 0.8;

    public static bool ShouldWarn(DispositionKind disposition, bool seesRedOrGrey, bool victimNamed) =>
        disposition == DispositionKind.Lawful && seesRedOrGrey && victimNamed;

    /// <summary>A red is always an outlaw; a gray is one while it is hurting a person.</summary>
    public static bool IsOutlaw(bool isRed, bool isCriminal, bool attackingPerson) =>
        isRed || isCriminal && attackingPerson;

    /// <summary>
    /// Who hunts reds: a fighter that is not an outlaw itself, and is lawful, out with a
    /// party or out hunting (the adventurers and dungeon crews of the era).
    /// </summary>
    public static bool IsHunter(bool fighter, DispositionKind disposition, bool inParty, bool hunting) =>
        fighter && disposition != DispositionKind.Outlaw &&
        (disposition == DispositionKind.Lawful || inParty || hunting);

    /// <summary>
    /// A hunter draws when the guards reach neither side and its side is strong enough. A
    /// red caught hurting this hunter's friend is fought whatever the odds.
    /// </summary>
    public static bool ShouldHunt(bool underGuards, int sidePower, int outlawPower, bool outlawOnFriend) =>
        !underGuards && (outlawOnFriend || outlawPower <= 0 || sidePower >= outlawPower * HuntOdds);

    /// <summary>
    /// A blue starts a new fight in Buccaneer's Den only when it rides against the reds there: on
    /// a Den raid, a posse or a PK hunter's run (see <see cref="PartyRoads.RidesAgainstDen"/>), or
    /// for a friend an outlaw is hurting. The Den is the reds' town, full of them between runs: a
    /// blue passing through that drew on every red in sight fought there for an hour. Out of the
    /// Den this rule does not hold a blue back.
    /// </summary>
    public static bool MayDrawInDen(bool inDen, bool ridesAgainstDen, bool outlawOnFriend) =>
        !inDen || ridesAgainstDen || outlawOnFriend;

    /// <summary>
    /// A red starts no fight on another red in Buccaneer's Den: it is their home, and they strike
    /// nobody there. Two reds of guilds at war drew on each other in the Den and their guild mates
    /// took it up blow after blow, one fight begun again and again for half an hour. A blue on a
    /// Den raid is fair game.
    /// </summary>
    public static bool RedMayDrawInDen(bool inDen, bool foeIsRed) => !inDen || !foeIsRed;

    /// <summary>
    /// A blue in Buccaneer's Den that rides against no red there leaves it before anything
    /// else. The Den is the reds' home: a blue with no fight there has no business in it.
    /// </summary>
    public static bool LeavesDen(bool red, bool inDen, bool ridesAgainstDen) => !red && inDen && !ridesAgainstDen;

    public static string HelpLine() => "help";
}
