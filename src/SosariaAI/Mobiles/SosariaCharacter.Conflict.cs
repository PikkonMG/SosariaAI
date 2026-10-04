using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Social;

namespace SosariaAI.Mobiles;

/// <summary>
/// Person-against-person law for a character: which blows are lawful (an agreed duel, Order
/// against Chaos), the duel's hit floor, the murderer's gang and its first-day build.
/// </summary>
public partial class SosariaCharacter : PlayerMobile
{
    /// <summary>The red gang this murderer rides with, or <see cref="PkGangRules.NoGang"/>. Set at bind.</summary>
    public int OutlawGang { get; set; } = PkGangRules.NoGang;

    /// <summary>A duel blow, an Order-against-Chaos blow and a guild-war blow are not crimes: no gray flag, no guards.</summary>
    public override bool IsHarmfulCriminal(Mobile target) =>
        !ConflictLegal(target) && base.IsHarmfulCriminal(target);

    public bool ConflictLegal(Mobile target) => FactionWar.LawfulFight(this, target);

    /// <summary>A duel stops at the hit floor: the blow that reaches it ends the duel instead of the duelist.</summary>
    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from == null || amount <= 0 || !Duels.AreFighting(this, from) ||
            !DuelRules.EndsDuel(Hits, HitsMax, amount))
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
            return;
        }

        base.Damage(DuelRules.CappedDamage(Hits, HitsMax, amount), from, informMount, ignoreEvilOmen);
        Duels.End(Duels.Find(this), Serial, Core.Now);
    }

    /// <summary>
    /// A murderer's first day: a red name a few murders past the line, and the trade skills
    /// of its class's PK build. Both happen once; a later bind finds them done.
    /// </summary>
    public void ApplyOutlawStart()
    {
        if (!IsPk)
        {
            return;
        }

        if (!PkRules.IsRed(Kills))
        {
            Kills = PkBuildRules.StartingMurders(CharacterId);
        }

        var swaps = PkBuildRules.TradeSwaps(PersonProfile.Class);

        for (var i = 0; i < swaps.Length; i++)
        {
            var from = Skills[swaps[i].From];
            var to = Skills[swaps[i].To];

            if (!PkBuildRules.ShouldSwap(from.Base, to.Base))
            {
                continue;
            }

            var points = from.Base;
            from.Base = 0;
            to.Base = points;
        }
    }

    /// <summary>
    /// A red, or a gray hurting a person, is a lawful target for a blue fighter out of the
    /// guards' reach, and in Buccaneer's Den only on a Den raid or when it hurts a friend
    /// (<see cref="WorldPlay.MayStartFightInDen"/>). <c>IsEnemy</c> can read this so the combat
    /// brain ranks such a person above monsters without waiting for the first blow.
    /// </summary>
    public bool IsOutlawTarget(Mobile mobile) =>
        !IsPk && Build?.Role == CharacterRole.Fighter && mobile is PlayerMobile { Alive: true } person &&
        person != this && CanSee(person) && !UnderGuards(this) && !UnderGuards(person) &&
        LawfulRules.IsOutlaw(PkRules.IsRed(person.Kills), person.Criminal, People.IsLivingPlayer(person.Combatant)) &&
        WorldPlay.MayStartFightInDen(this, person);
}
