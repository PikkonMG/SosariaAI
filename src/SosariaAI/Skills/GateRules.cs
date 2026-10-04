using System;
using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Seventh-circle Gate Travel as a player casts it: the real spell opens a public gate pair
/// to a marked rune, and anyone standing by, party or stranger, may walk through while it
/// lasts. A book cast wants an adept's hand (the era's seventh circle starts at 60 and is
/// sure at 100); a scroll brings the circle down. A mage opens one for a party, or at a
/// crowded bank where others will use it; alone it recalls.
/// </summary>
/// <remarks>
/// A dungeon group gates through <see cref="Behaviour.PartyGate"/>: any member who can cast
/// opens the gate and holds it until the whole group stepped through. On a plain trip a
/// party leader gates with <see cref="GateSkill"/> built on the goal and a hold
/// (<see cref="PartyHold"/>), and a member finds the gate with
/// <see cref="GateTravel.FindSpellGateToward"/>. <see cref="CanOpenToward"/> answers before
/// the cast whether a gate is possible at all.
/// </remarks>
public static class GateRules
{
    public const string Kind = SkillKinds.Gate;

    /// <summary>
    /// The Magery a mage takes a book Gate on with: the era's seventh circle opens one time in
    /// four here and more often with every point, and a fizzle is cast again.
    /// </summary>
    public const double MinMagery = 70;

    /// <summary>
    /// A scroll lowers the circle by two: before Mondain's Legacy the engine checks a
    /// seventh-circle scroll from 40 to 80 Magery (MagerySpell.GetCastSkills); below 40 no cast takes.
    /// </summary>
    public const double ScrollMinMagery = 40;

    /// <summary>People within this many tiles see the gate open and may use it.</summary>
    public const int AudienceTiles = 8;

    /// <summary>Other people standing by that make a lone mage open a public gate instead of recalling.</summary>
    public const int AudienceForPublicGate = 3;

    /// <summary>How long a leader holds its gate open for the party before it steps through.</summary>
    public static readonly TimeSpan PartyHold = TimeSpan.FromSeconds(6);

    /// <summary>
    /// A mage who can gate opens one when it leads a party, when its pets must come along (a
    /// gate carries a following pet, a recall does not), or when enough people stand by to
    /// share the ride; otherwise a recall is the quieter way.
    /// </summary>
    public static bool PrefersGate(bool leadsParty, bool bringsPets, int bystanders) =>
        leadsParty || bringsPets || bystanders >= AudienceForPublicGate;

    /// <summary>
    /// True when the person could open a gate toward the goal right now: mark, skill, reagents,
    /// place. A gate opens to the same book entry or rune, for the same long trip, a recall
    /// would want (<see cref="RecallRules.RuneToward"/>).
    /// </summary>
    public static bool CanOpenToward(SosariaCharacter character, Point3D goal, int minTripTiles = RecallRules.MinTripTiles) =>
        RecallRules.RuneToward(character, goal, minTripTiles) is { } mark && TravelSpells.CanCastToward(character, TravelSpellKind.Gate, mark);

    /// <summary>Begins a real Gate Travel toward a goal. True when the words of power began.</summary>
    public static bool TryOpenToward(SosariaCharacter character, Point3D goal, int minTripTiles = RecallRules.MinTripTiles) =>
        RecallRules.RuneToward(character, goal, minTripTiles) is { } mark && TravelSpells.Begin(character, TravelSpellKind.Gate, mark);

    /// <summary>Living people other than the caster within <see cref="AudienceTiles"/>.</summary>
    public static int Bystanders(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return 0;
        }

        var count = 0;

        foreach (var mobile in character.Map.GetMobilesInRange<PlayerMobile>(character.Location, AudienceTiles))
        {
            if (mobile != character && mobile.Alive && character.CanSee(mobile))
            {
                count++;
            }
        }

        return count;
    }
}
