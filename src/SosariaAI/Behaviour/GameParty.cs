using System;
using System.Collections.Generic;
using Server;
using EngineParty = Server.Engines.PartySystem.Party;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Logging;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// Real ModernUO parties. Packet sends no-op when NetState is null.
/// </summary>
public static class GameParty
{
    private static readonly ILogger logger = SosariaLog.For(typeof(GameParty));

    /// <summary>
    /// The same party line from the same party within this gap is one event in the log: every
    /// member of a hunting party called "heal" as it bandaged, 383 lines in 78 minutes.
    /// </summary>
    public static readonly TimeSpan ChatRepeatQuiet = TimeSpan.FromSeconds(30);

    private static readonly LogGate<(Serial Party, string Line)> ChatLines = new(ChatRepeatQuiet);

    /// <summary>When anyone last asked each player along, so a player is not asked by one after another.</summary>
    private static readonly Dictionary<Serial, DateTime> PlayerAskedAt = new();

    public static EngineParty Of(Mobile mobile) => EngineParty.Get(mobile);

    public static bool InParty(Mobile mobile) => Of(mobile) != null && Of(mobile).Count > 1;

    public static bool TryForm(Mobile leader, Mobile member)
    {
        if (leader == null || member == null || leader == member)
        {
            return false;
        }

        var party = Of(leader);

        // A leader that sits in a player's party, or follows another leader, must not pull others in.
        if (HasPlayer(party) || party != null && party.Leader != leader)
        {
            return false;
        }

        if (party == null)
        {
            party = new EngineParty(leader);
            leader.Party = party;
        }

        if (party.Contains(member))
        {
            return true;
        }

        if (HasPlayer(Of(member)) && Of(member) != party)
        {
            return false;
        }

        if (People.IsHuman(member))
        {
            EngineParty.Invite(leader, member);
            Log(InviteAskRules.LogInvite(leader.Name, member.Name));
            return true;
        }

        if (member is not SosariaCharacter character || !InviteCharacter(leader, character))
        {
            return false;
        }

        Log($"{leader.Name} formed a party with {member.Name}");
        return true;
    }

    /// <summary>
    /// The engine's own invite and accept between a leader and a character. The character has
    /// no client, so it answers in code the moment the invite lands, as it would have said yes.
    /// </summary>
    public static bool InviteCharacter(Mobile leader, SosariaCharacter member)
    {
        if (leader == null || member == null || leader == member)
        {
            return false;
        }

        EngineParty.Invite(leader, member);
        var party = Of(leader);

        // The engine clears the invitee's pending leader before it answers. Do the same.
        member.Party = null;

        if (party == null || !party.Candidates.Contains(member))
        {
            return false;
        }

        party.OnAccept(member, force: true);
        return party.Contains(member);
    }

    public static string MemberNames(Mobile mobile)
    {
        var party = Of(mobile);

        if (party == null)
        {
            return null;
        }

        var names = new string[party.Members.Count];

        for (var i = 0; i < party.Members.Count; i++)
        {
            names[i] = party.Members[i].Mobile?.Name ?? "?";
        }

        var leader = party.Leader?.Name ?? names[0];
        return $"{leader} with {string.Join(", ", names)}";
    }

    public static bool HasPlayer(EngineParty party)
    {
        if (party == null)
        {
            return false;
        }

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (People.IsHuman(party.Members[i].Mobile))
            {
                return true;
            }
        }

        return false;
    }

    public static bool InPlayerParty(Mobile mobile) => HasPlayer(Of(mobile));

    /// <summary>
    /// The party member this one walks behind: the leader, or while the leader lies dead the
    /// first living member. Null outside a party, or when this one leads.
    /// </summary>
    public static Mobile LeaderToFollow(Mobile member)
    {
        var party = Of(member);

        if (party == null || party.Count < 2)
        {
            return null;
        }

        var alive = new bool[party.Members.Count];
        var self = PartyLead.NoOne;
        var leader = PartyLead.NoOne;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var mobile = party.Members[i].Mobile;
            alive[i] = mobile is { Deleted: false, Alive: true };
            self = mobile == member ? i : self;
            leader = mobile == party.Leader ? i : leader;
        }

        var follow = PartyLead.FollowIndex(self, leader, alive);
        return follow == PartyLead.NoOne ? null : party.Members[follow].Mobile;
    }

    /// <summary>
    /// A bandage on a party member the way a client puts one on (<see cref="CombatBrain.BandageOther"/>):
    /// one from the pack, the engine's reach, timer and Healing check. The heal began on the
    /// engine's bandage timer straight, with no bandage and from two tiles off, and the engine's
    /// one-tile reach failed it at the end: one healer bandaged the same friend 172 times in
    /// twenty minutes and never healed it. A gray friend gets no bandage: the heal is a crime that
    /// turns the healer gray, and the guards answer it.
    /// </summary>
    public static bool TryHeal(SosariaCharacter healer, Mobile patient)
    {
        if (healer == null || patient == null || healer.Deleted || patient.Deleted || !patient.Alive ||
            healer.IsBeneficialCriminal(patient) || !CombatBrain.BandageOther(healer, patient))
        {
            return false;
        }

        Chat(healer, PartyInviteRules.HealLine());
        Log($"{healer.Name} healed {patient.Name}");
        return true;
    }

    public static void NoticePlayerLeave(SosariaCharacter character)
    {
        if (character == null)
        {
            return;
        }

        var party = Of(character);
        var playerName = character.WatchedPlayerName;

        if (party != null)
        {
            for (var i = 0; i < party.Members.Count; i++)
            {
                var member = party.Members[i].Mobile;

                if (People.IsHuman(member))
                {
                    character.WatchedPlayerName = member.Name;
                    return;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        character.SpeakScripted(PartyInviteRules.GoodbyeLine());
        Log($"{character.Name} said goodbye to {playerName}");
        character.WatchedPlayerName = null;
    }

    public static bool TryAccept(Mobile member, Mobile leader)
    {
        if (member == null || leader == null)
        {
            return false;
        }

        var party = Of(leader);

        if (party == null)
        {
            return false;
        }

        party.OnAccept(member, force: true);
        Log($"{member.Name} joined {leader.Name}'s party");
        return party.Contains(member);
    }

    public static void Chat(Mobile from, string text)
    {
        var party = Of(from);

        if (party == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        party.SendPublicMessage(from, text);

        if (ChatLines.Opens((party.Leader?.Serial ?? from.Serial, text), Core.Now))
        {
            Log($"{from.Name} party chat: {text}");
        }
    }

    public static void Leave(Mobile mobile, string line)
    {
        if (mobile == null || Of(mobile) == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(line) && mobile is SosariaCharacter character && InPlayerParty(character))
        {
            character.SpeakScripted(line);
        }

        Of(mobile).Remove(mobile);
        Log($"{mobile.Name} left the party");
    }

    /// <summary>
    /// One world scan of party life: answer a pending invite, run the group board, and
    /// close an ask to a player that went unanswered.
    /// </summary>
    public static void ConsiderPlayerInvite(SosariaCharacter character)
    {
        if (!People.InWorld(character) || character.IsGhost)
        {
            return;
        }

        if (character.Party is Mobile inviter)
        {
            AnswerPending(character, inviter);
            return;
        }

        LfgBoard.Consider(character);
        TickAsk(character);
    }

    /// <summary>Nobody has asked this player along lately.</summary>
    public static bool PlayerAskRested(Mobile player, DateTime now) =>
        player != null && InviteAskRules.PlayerAskRested(PlayerAskedAt.GetValueOrDefault(player.Serial), now);

    /// <summary>A group leader asks a player standing near to come along, out loud, and waits for the answer.</summary>
    public static void AskPlayer(SosariaCharacter leader, Mobile player, string place)
    {
        Talk.Say(leader, TalkCategory.LfgAskPlayer, new TalkSlots { Name = player.Name, Place = place });
        leader.InviteAskPlayer = player.Name;
        leader.InviteAskedAt = Core.Now;
        leader.InviteTarget = player.Serial;
        PlayerAskedAt[player.Serial] = Core.Now;
        Log(InviteAskRules.LogAsk(leader.Name, player.Name));
    }

    public static void NoteSpokenAnswer(Mobile player, string text)
    {
        if (player == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter character && NoteSpokenAnswer(character, player, text))
            {
                return;
            }
        }
    }

    /// <summary>True when the line answered this character's open ask to the player.</summary>
    public static bool NoteSpokenAnswer(SosariaCharacter character, Mobile player, string text)
    {
        if (character == null || player == null || character.InviteTarget != player.Serial ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (InviteAskRules.IsYes(text))
        {
            Log(InviteAskRules.LogAnswer(player.Name, yes: true));
            LfgBoard.PlayerAccepted(character, player);
            ClearAsk(character);
            character.Planning.NoteAgreement();
            Brain.RequestPlan(character, PlanTrigger.Invitation);
            return true;
        }

        if (InviteAskRules.IsNo(text))
        {
            Log(InviteAskRules.LogAnswer(player.Name, yes: false));
            character.Memory.ShiftBond(player, InviteAskRules.DeclineBondShift, InviteAskRules.DeclineReason);
            StartCooldown(character);
            ClearAsk(character);
            return true;
        }

        return false;
    }

    /// <summary>Alive, visible, not staff, and for a human still connected.</summary>
    public static bool IsApproachable(Mobile self, Mobile mobile)
    {
        if (mobile == self || mobile.Deleted || !mobile.Alive || !People.Perceives(self, mobile) ||
            mobile.AccessLevel > AccessLevel.Player)
        {
            return false;
        }

        return !People.IsHuman(mobile) || mobile.NetState != null;
    }

    /// <summary>An ask left unanswered long enough is a quiet no: the leader stops waiting, and asks nobody for a while.</summary>
    private static void TickAsk(SosariaCharacter character)
    {
        if (character.InviteAskedAt == default)
        {
            return;
        }

        var seconds = SosariaSettings.Characters?.Career?.InviteAnswerSeconds ??
                      CareerSettings.DefaultInviteAnswerSeconds;

        if (!InviteAskRules.AnswerTimedOut(character.InviteAskedAt, Core.Now, seconds))
        {
            return;
        }

        Log(InviteAskRules.LogNoAnswer(character.Name, character.InviteAskPlayer));
        StartCooldown(character);
        ClearAsk(character);
    }

    private static void StartCooldown(SosariaCharacter character)
    {
        var minutes = SosariaSettings.Characters?.Career?.InviteCooldownMinutes ??
                      CareerSettings.DefaultInviteCooldownMinutes;
        character.InviteCooldownUntil = Core.Now + TimeSpan.FromMinutes(minutes);
    }

    private static void ClearAsk(SosariaCharacter character)
    {
        character.InviteAskPlayer = null;
        character.InviteAskedAt = default;
        character.InviteTarget = Serial.Zero;
    }

    private static void AnswerPending(SosariaCharacter character, Mobile inviter)
    {
        var party = Of(inviter);

        // The engine clears the invitee's Party before it answers. Do the same, or the
        // invite stays pending and gets answered again every think.
        character.Party = null;

        if (party == null || !party.Candidates.Contains(character))
        {
            return;
        }

        var hits = Vitals.HitsFraction(character);
        var accept = LfgBoard.Volunteered(character, inviter) || PartyInviteRules.Accepts(
            huntGoal: character.LastScore?.Goal.Kind is GoalKind.Hunt or GoalKind.Dungeon,
            healthy: hits >= PartyInviteRules.MinHitsFraction,
            powerFits: InviteAskRules.MayInviteFighter(
                GearScore.HasWeapon(character),
                CharacterPower.For(character),
                CharactersFile.GraveyardRequiredPower
            ) && PartyInviteRules.PowerFits(
                CharacterPower.For(character),
                CharacterPower.For(inviter)
            ),
            opinionScore: Recall.ScoreOf(MemoryStore.Shared, character, inviter),
            night: false,
            busyBanking: character.Routine?.CurrentSkill is BankDepositSkill
        );

        if (accept)
        {
            character.SpeakScripted(PartyInviteRules.AcceptLine());
            TryAccept(character, inviter);
            return;
        }

        character.SpeakScripted(PartyInviteRules.DeclineLine());
        party.OnDecline(character, inviter);
    }

    private static void Log(string line)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", line);
        }
    }
}
