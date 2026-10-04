using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Group and guild talk said where the speaker leads no group, walks no trip and opens no
/// gate. A line there that asks someone along or claims a trip promises what no code keeps:
/// "gg, im heading to bank" went out with no bank trip, and a random guildmate said "heading
/// to town" whatever it did.
/// </summary>
public class GroupTalkPromiseTests
{
    private static readonly HashSet<string> SaidWithNothingBehind =
    [
        TalkCategory.LfgJoinCall,
        TalkCategory.LfgOfferPlayer,
        TalkCategory.LfgInvite,
        TalkCategory.LfgGiveUp,
        TalkCategory.LfgGg,
        TalkCategory.LfgGgReply,
        TalkCategory.LfgDecline,
        TalkCategory.PartyReady,
        TalkCategory.PartyDepartBanter,
        TalkCategory.PartyReturnBanter,
        TalkCategory.GuildChatter,
        TalkCategory.GuildWelcome,
        TalkCategory.GuildCantCome,
        TalkCategory.GuildInvitePlayer,
        TalkCategory.GuildJoin,
        TalkCategory.GuildWarRally,
        TalkCategory.OrderBattle,
        TalkCategory.ChaosBattle,
        TalkCategory.OrderTaunt,
        TalkCategory.ChaosTaunt,
        TalkCategory.ScuffleWatch,
        TalkCategory.ScuffleYield
    ];

    [Fact]
    public void DefaultLines_SaidWithNothingBehind_PromiseNothing()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (!SaidWithNothingBehind.Contains(topic.Name))
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                Assert.False(PromiseLines.IsPromise(line), $"{topic.Name}: {line}");
            }
        }
    }

    [Fact]
    public void MusterLines_TheGateLineIsCaught_AndOthersServeAGroupThatWalks()
    {
        var gateLines = 0;
        var walkLines = 0;

        foreach (var topic in TalkDefaults.All)
        {
            if (topic.Name is not TalkCategory.PartyMuster)
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                if (PartyGateRules.SpeaksOfGate(line))
                {
                    gateLines++;
                }
                else
                {
                    walkLines++;
                }
            }
        }

        Assert.True(gateLines > 0);
        Assert.True(walkLines > gateLines);
    }
}
