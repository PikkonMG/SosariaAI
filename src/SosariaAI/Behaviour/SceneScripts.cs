using System;
using System.Collections.Generic;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// The beats of each scene, built from real names, goods, prices and places. Cast member 0 is
/// the one the scene is about; bystanders follow. A beat whose category has no fitting line
/// is left out. Pure apart from the talk library it reads.
/// </summary>
public static class SceneScripts
{
    public const int Lead = 0;
    public const int First = 1;
    public const int Second = 2;

    // Spreads one roll over the beats so each picks its own line and gap.
    private const int BeatStride = 7919;

    public static List<SceneBeat> ShoutAnswer(bool selling, string seller, string item, string price, int bystanders, int roll)
    {
        var beats = new List<SceneBeat>();
        var slots = new TalkSlots { Name = seller, Item = item, Price = selling ? price : null };
        Add(beats, roll, First, selling ? TalkCategory.WtsReply : TalkCategory.WtbReply, slots);

        if (bystanders >= Second)
        {
            Add(beats, roll, Second, TalkCategory.RespondAck, default);
        }

        return beats;
    }

    public static List<SceneBeat> GoingAsk(string place, int bystanders, int roll)
    {
        var beats = new List<SceneBeat>();
        var slots = new TalkSlots { Place = place };
        AddNow(beats, roll, Lead, TalkCategory.GoingAsk, slots);
        Add(beats, roll, First, TalkCategory.GoingReply, slots);

        if (bystanders >= Second)
        {
            Add(beats, roll, Second, TalkCategory.GoingReply, slots);
        }

        Add(beats, roll, Lead, TalkCategory.RespondAck, default);
        return beats;
    }

    public static List<SceneBeat> FriendChain(string lead, string first, string second, int roll)
    {
        var beats = new List<SceneBeat>();
        AddNow(beats, roll, Lead, TalkCategory.GreetWarm, new TalkSlots { Name = first });
        Add(beats, roll, First, TalkCategory.GreetReply, new TalkSlots { Name = lead });
        Add(beats, roll, Second, TalkCategory.FriendChain, new TalkSlots { Name = lead });
        Add(beats, roll, Lead, TalkCategory.GreetReply, new TalkSlots { Name = second });
        Add(beats, roll, First, TalkCategory.SmallTalk, new TalkSlots { Name = second });
        Add(beats, roll, Second, TalkCategory.SmallTalkReply, default);
        return beats;
    }

    public static List<SceneBeat> DeathJoke(string dead, int bystanders, int roll)
    {
        var beats = new List<SceneBeat>();
        var slots = new TalkSlots { Target = dead };
        Add(beats, roll, First, TalkCategory.DeathJoke, slots);

        if (bystanders >= Second)
        {
            Add(beats, roll, Second, TalkCategory.DeathJokeReply, slots);
            Add(beats, roll, First, TalkCategory.RespondAck, default);
        }

        return beats;
    }

    public static List<SceneBeat> LootedReply(int roll)
    {
        var beats = new List<SceneBeat>();
        Add(beats, roll, First, TalkCategory.LootedReply, default);
        return beats;
    }

    public static List<SceneBeat> DuelStart(string challenger, string partner, int onlookers, int roll)
    {
        var beats = new List<SceneBeat>();
        var watch = TimeSpan.FromMilliseconds(SceneRules.DuelWatchMs);
        AddAfter(beats, watch, roll, First, TalkCategory.DuelOnlooker, new TalkSlots { Name = challenger, Target = partner });

        if (onlookers >= Second)
        {
            Add(beats, roll, Second, TalkCategory.DuelOnlooker, new TalkSlots { Name = partner, Target = challenger });
        }

        return beats;
    }

    public static List<SceneBeat> DuelEnd(string winner, string loser, int roll)
    {
        var beats = new List<SceneBeat>();
        Add(beats, roll, First, TalkCategory.DuelCheer, new TalkSlots { Name = winner, Target = loser });
        return beats;
    }

    /// <summary>
    /// <paramref name="onTheRed"/> says, for each bystander in cast order, whether it is a lawful
    /// fighter already fighting the red: only that one says it is on him.
    /// </summary>
    public static List<SceneBeat> RedAlert(string place, IReadOnlyList<bool> onTheRed, int roll)
    {
        var beats = new List<SceneBeat>();
        var slots = new TalkSlots { Place = place };

        for (var i = 0; i < onTheRed.Count; i++)
        {
            Add(beats, roll, First + i, onTheRed[i] ? TalkCategory.RedOnMyWay : TalkCategory.RedReply, slots);
        }

        return beats;
    }

    /// <summary>The shopper is cast member 1. A null <paramref name="price"/> means the batch sold out.</summary>
    public static List<SceneBeat> CraftCustomer(string crafter, string item, string price, int roll)
    {
        var beats = new List<SceneBeat>();
        Add(beats, roll, First, TalkCategory.CraftAsk, new TalkSlots { Name = crafter, Item = item });
        Add(beats, roll, Lead, price == null ? TalkCategory.CraftSoldOut : TalkCategory.CraftPrice, new TalkSlots { Item = item, Price = price });
        Add(beats, roll, First, TalkCategory.CraftThanks, new TalkSlots { Name = crafter });
        return beats;
    }

    public static List<SceneBeat> PartyDepart(string leader, string place, int members, int roll)
    {
        var beats = new List<SceneBeat>();
        Add(beats, roll, First, TalkCategory.PartyReady, new TalkSlots { Name = leader }, SceneChannel.Party);

        if (members >= Second)
        {
            Add(beats, roll, Second, TalkCategory.PartyDepartBanter, new TalkSlots { Place = place }, SceneChannel.Party);
        }

        return beats;
    }

    public static List<SceneBeat> PartyReturn(string place, int members, int roll)
    {
        var beats = new List<SceneBeat>();
        Add(beats, roll, First, TalkCategory.PartyReturnBanter, new TalkSlots { Place = place });
        Add(beats, roll, members >= Second ? Second : Lead, TalkCategory.SmallTalkReply, default);
        return beats;
    }

    /// <summary>
    /// Night talk in an inn. With a true <paramref name="story"/> from the journal, the second
    /// patron tells it and the first answers with <paramref name="storyReply"/>.
    /// </summary>
    public static List<SceneBeat> TavernNight(string town, string story, string storyReply, int patrons, int roll)
    {
        var beats = new List<SceneBeat>();
        AddNow(beats, roll, Lead, TalkCategory.TavernNight, new TalkSlots { Town = town });
        Add(beats, roll, First, TalkCategory.SmallTalkReply, default);

        if (patrons < Second)
        {
            return beats;
        }

        if (string.IsNullOrEmpty(story))
        {
            Add(beats, roll, Second, TalkCategory.TavernNight, new TalkSlots { Town = town });
            return beats;
        }

        beats.Add(new SceneBeat(SceneRules.Gap(StepRoll(roll, beats.Count)), Second, story, SceneChannel.Say));

        if (!string.IsNullOrEmpty(storyReply))
        {
            beats.Add(new SceneBeat(SceneRules.Gap(StepRoll(roll, beats.Count)), Lead, storyReply, SceneChannel.Say));
        }

        return beats;
    }

    private static void AddNow(List<SceneBeat> beats, int roll, int actor, string category, in TalkSlots slots) =>
        AddAfter(beats, TimeSpan.Zero, roll, actor, category, slots);

    private static void Add(
        List<SceneBeat> beats,
        int roll,
        int actor,
        string category,
        in TalkSlots slots,
        SceneChannel channel = SceneChannel.Say
    ) =>
        AddAfter(beats, SceneRules.Gap(StepRoll(roll, beats.Count)), roll, actor, category, slots, channel);

    private static void AddAfter(
        List<SceneBeat> beats,
        TimeSpan delay,
        int roll,
        int actor,
        string category,
        in TalkSlots slots,
        SceneChannel channel = SceneChannel.Say
    )
    {
        var text = Talk.Line(category, StepRoll(roll, beats.Count + actor), slots);

        if (!string.IsNullOrEmpty(text))
        {
            beats.Add(new SceneBeat(delay, actor, text, channel));
        }
    }

    private static int StepRoll(int roll, int step) => unchecked(roll + step * BeatStride) & int.MaxValue;
}
