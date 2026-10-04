using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using EngineParty = Server.Engines.PartySystem.Party;

namespace SosariaAI.Behaviour;

/// <summary>
/// Where scenes start: each entry point is called from a real moment (a shout, a death, a
/// duel, a scream, a finished batch, a group setting out or coming back, a night at the inn,
/// friends meeting). Each rolls its chance first, then asks the runner for room, then casts
/// bystanders with one range query. No model call.
/// </summary>
public static class Scenes
{
    /// <summary>Bystanders comment on a real WTS or WTB shout at a bank.</summary>
    public static void ShoutAnswer(SosariaCharacter shouter, bool selling, string item, string price)
    {
        if (!Rolled(SceneRules.ShoutAnswerPercent) || shouter == null)
        {
            return;
        }

        Stage(
            SceneKind.ShoutAnswer,
            shouter,
            everyday: true,
            SceneRules.BystanderRange,
            cast => SceneScripts.ShoutAnswer(selling, shouter.Name, item, price, SceneCast.Bystanders(cast), Roll())
        );
    }

    /// <summary>Near a person at a keyboard, now and then: friends greet in a chain, or someone asks the crowd for news of its hunting place.</summary>
    public static void ConsiderMeeting(SosariaCharacter character)
    {
        if (Rolled(SceneRules.MeetingScenePercent))
        {
            TryEveryday(character);
        }
    }

    /// <summary>The same everyday scenes anywhere, at a far lower rate, from the world scan.</summary>
    public static void ConsiderIdle(SosariaCharacter character)
    {
        if (Utility.Random(SceneRules.IdleSceneOdds) == 0)
        {
            TryEveryday(character);
        }
    }

    /// <summary>Bystanders joke about a death they just saw.</summary>
    public static void DeathJoke(SosariaCharacter ghost)
    {
        if (!Rolled(SceneRules.DeathJokePercent) || ghost == null)
        {
            return;
        }

        Stage(
            SceneKind.DeathJoke,
            ghost,
            everyday: false,
            SceneRules.BystanderRange,
            cast => SceneScripts.DeathJoke(ghost.Name, SceneCast.Bystanders(cast), Roll())
        );
    }

    /// <summary>A bystander answers the shout about a looted body.</summary>
    public static void LootedReply(SosariaCharacter owner)
    {
        if (!Rolled(SceneRules.LootedReplyPercent) || owner == null)
        {
            return;
        }

        Stage(SceneKind.LootedReply, owner, everyday: false, SceneRules.BystanderRange, _ => SceneScripts.LootedReply(Roll()));
    }

    /// <summary>Onlookers call the duel as the two walk out.</summary>
    public static void DuelStart(SosariaCharacter challenger, SosariaCharacter partner)
    {
        if (!Rolled(SceneRules.DuelStartPercent) || challenger == null || partner == null)
        {
            return;
        }

        Stage(
            SceneKind.DuelStart,
            challenger,
            everyday: false,
            SceneRules.BystanderRange,
            cast => SceneScripts.DuelStart(challenger.Name, partner.Name, SceneCast.Bystanders(cast), Roll()),
            other => other != partner
        );
    }

    /// <summary>An onlooker cheers the winner once the floor stops the duel.</summary>
    public static void DuelEnd(Mobile winner, Mobile loser)
    {
        if (!Rolled(SceneRules.DuelEndPercent) || winner is not SosariaCharacter lead || loser == null)
        {
            return;
        }

        Stage(
            SceneKind.DuelEnd,
            lead,
            everyday: false,
            SceneRules.BystanderRange,
            _ => SceneScripts.DuelEnd(winner.Name, loser.Name, Roll()),
            other => other != loser
        );
    }

    /// <summary>
    /// People near a red scream answer it: civilians ask where or run, and a lawful fighter already
    /// fighting that red says it is on him. A lawful fighter not on the red stays out of the scene:
    /// nothing sends it after the red, so an "omw" from it is a promise nobody keeps.
    /// </summary>
    public static void RedAlert(SosariaCharacter screamer, Mobile red, string place)
    {
        if (!Rolled(SceneRules.RedAlertPercent) || screamer == null)
        {
            return;
        }

        Stage(
            SceneKind.RedAlert,
            screamer,
            everyday: false,
            RedAlarm.ScatterRange,
            cast => SceneScripts.RedAlert(place, OnTheRed(cast), Roll()),
            other => other != red && !other.IsPk && (!HuntsReds(other) || other.Combatant == red)
        );
    }

    /// <summary>A shopper in the shop asks about the batch the crafter just finished. A null price means it all sold.</summary>
    public static void CraftCustomer(SosariaCharacter crafter, string item, string price)
    {
        if (!Rolled(SceneRules.CraftCustomerPercent) || crafter == null)
        {
            return;
        }

        Stage(
            SceneKind.CraftCustomer,
            crafter,
            everyday: true,
            SceneRules.ShopperRange,
            _ => SceneScripts.CraftCustomer(crafter.Name, item, price, Roll()),
            maxBystanders: 1
        );
    }

    /// <summary>Members answer the leader setting out, in party chat.</summary>
    public static void PartyDepart(SosariaCharacter leader, string place)
    {
        if (Rolled(SceneRules.PartyDepartPercent) && leader != null)
        {
            PlayWithGroup(
                SceneKind.PartyDepart,
                leader,
                MembersOf(GameParty.Of(leader)),
                cast => SceneScripts.PartyDepart(leader.Name, place, SceneCast.Bystanders(cast), Roll())
            );
        }
    }

    /// <summary>Members still standing with the leader talk about the run once it is over.</summary>
    public static void PartyReturn(SosariaCharacter leader, IEnumerable<Mobile> members, string place)
    {
        if (Rolled(SceneRules.PartyReturnPercent) && leader != null)
        {
            PlayWithGroup(
                SceneKind.PartyReturn,
                leader,
                members,
                cast => SceneScripts.PartyReturn(place, SceneCast.Bystanders(cast), Roll())
            );
        }
    }

    /// <summary>
    /// At the game's night, patrons talk in turn and one may tell a true story from the journal.
    /// False when no scene started, so the inn's plain small talk runs instead.
    /// </summary>
    public static bool TavernNight(SosariaCharacter patron)
    {
        if (patron?.Map == null || !IsNight(patron) || !Rolled(SceneRules.TavernNightPercent) ||
            !SceneRunner.MayStage(patron, everyday: true, out var heard))
        {
            return false;
        }

        var cast = SceneCast.Around(patron, patron.Location, MeetingRules.ChatRange, SceneRules.MaxBystanders);

        if (SceneCast.Bystanders(cast) == 0)
        {
            return false;
        }

        var roll = Roll();
        string story = null;
        string reply = null;

        if (SceneCast.Bystanders(cast) >= SceneScripts.Second)
        {
            var teller = cast[SceneScripts.Second];
            var now = Core.Now;
            var news = SosariaSettings.Journal?.PickGossip(teller.Name, teller.Location, now, teller.HomeFacet, Utility.Random(TalkOdds.PercentScale));

            if (news != null && !news.Involves(patron.Name))
            {
                story = Meeting.GossipLine(news, teller.Name, teller.Location, now, roll);
                reply = GossipLines.Reply(news, roll);
            }
        }

        SceneRunner.Play(
            SceneKind.TavernNight,
            cast,
            SceneScripts.TavernNight(TalkWords.Town(patron), story, reply, SceneCast.Bystanders(cast), roll),
            heard
        );
        return true;
    }

    /// <summary>The game's night where the mobile stands.</summary>
    public static bool IsNight(Mobile mobile)
    {
        Clock.GetTime(mobile.Map, mobile.X, mobile.Y, out int hours, out int _);
        return SceneRules.IsNight(hours);
    }

    private static void TryEveryday(SosariaCharacter character)
    {
        var now = Core.Now;

        if (character.Motor.Action != CharacterAction.Wander || !SceneCast.Free(character, now) ||
            !SceneRunner.MayStage(character, everyday: true, out var heard))
        {
            return;
        }

        var friends = SceneCast.Around(
            character,
            character.Location,
            SceneRules.BystanderRange,
            SceneRules.MaxBystanders,
            other => SpeechResponder.KnowsWell(character, other)
        );

        if (SceneCast.Bystanders(friends) >= SceneScripts.Second)
        {
            var first = friends[SceneScripts.First];
            var second = friends[SceneScripts.Second];
            Brain.Conversations.RecordGreeting(character.Serial, first.Serial, now);
            Brain.Conversations.RecordGreeting(character.Serial, second.Serial, now);
            SceneRunner.Play(
                SceneKind.FriendChain,
                friends,
                SceneScripts.FriendChain(character.Name, first.Name, second.Name, Roll()),
                heard
            );
            return;
        }

        var place = Meeting.AtMeetingPoint(character.Location) ? LfgBoard.PlaceOf(character) : null;

        if (place == null)
        {
            return;
        }

        var crowd = SceneCast.Around(character, character.Location, SceneRules.BystanderRange, SceneRules.MaxBystanders);

        if (SceneCast.Bystanders(crowd) > 0)
        {
            SceneRunner.Play(
                SceneKind.GoingAsk,
                crowd,
                SceneScripts.GoingAsk(place, SceneCast.Bystanders(crowd), Roll()),
                heard
            );
        }
    }

    private static void Stage(
        SceneKind kind,
        SosariaCharacter lead,
        bool everyday,
        int range,
        Func<IReadOnlyList<SosariaCharacter>, List<SceneBeat>> script,
        Func<SosariaCharacter, bool> fits = null,
        int maxBystanders = SceneRules.MaxBystanders
    )
    {
        if (!SceneRunner.MayStage(lead, everyday, out var heard))
        {
            return;
        }

        var cast = SceneCast.Around(lead, lead.Location, range, maxBystanders, fits);

        if (SceneCast.Bystanders(cast) > 0)
        {
            SceneRunner.Play(kind, cast, script(cast), heard);
        }
    }

    /// <summary>A scene cast from the leader's own group standing with it, not from whoever is near.</summary>
    private static void PlayWithGroup(
        SceneKind kind,
        SosariaCharacter leader,
        IEnumerable<Mobile> members,
        Func<IReadOnlyList<SosariaCharacter>, List<SceneBeat>> script
    )
    {
        if (!SceneRunner.MayStage(leader, everyday: false, out var heard))
        {
            return;
        }

        var cast = new List<SosariaCharacter> { leader };
        var now = Core.Now;

        foreach (var mobile in members)
        {
            if (SceneCast.Bystanders(cast) >= SceneRules.MaxBystanders)
            {
                break;
            }

            if (mobile is SosariaCharacter member && member != leader && SceneCast.Free(member, now) &&
                member.Map == leader.Map && member.InRange(leader, SceneRules.StageRange))
            {
                cast.Add(member);
            }
        }

        if (SceneCast.Bystanders(cast) > 0)
        {
            SceneRunner.Play(kind, cast, script(cast), heard);
        }
    }

    private static IEnumerable<Mobile> MembersOf(EngineParty party)
    {
        for (var i = 0; party != null && i < party.Members.Count; i++)
        {
            yield return party.Members[i].Mobile;
        }
    }

    // The red alert's cast holds a lawful fighter only when it already fights the red.
    private static List<bool> OnTheRed(IReadOnlyList<SosariaCharacter> cast)
    {
        var onTheRed = new List<bool>();

        for (var i = SceneScripts.First; i < cast.Count; i++)
        {
            onTheRed.Add(HuntsReds(cast[i]));
        }

        return onTheRed;
    }

    // A lawful fighter: the one kind that goes after reds.
    private static bool HuntsReds(SosariaCharacter character) =>
        character.Build?.Role == CharacterRole.Fighter && character.Disposition == DispositionKind.Lawful;

    private static bool Rolled(int percent) => Utility.Random(TalkOdds.PercentScale) < percent;

    private static int Roll() => Utility.Random(int.MaxValue);
}
