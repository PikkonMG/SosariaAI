using Server;
using Server.Guilds;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A character a guild leader recruited walks to that guild's stone and uses it, the step
/// that makes an accepted recruit a full member in the era's guild system.
/// </summary>
public sealed class GuildstoneJoinSkill : Skill
{
    /// <summary>The engine joins an accepted recruit only within this reach of the stone.</summary>
    public const int StoneReach = 2;

    private readonly Guild _guild;
    private TravelSkill _walk;
    private SosariaCharacter _character;

    public GuildstoneJoinSkill(Guild guild) => _guild = guild;

    public override string Name => SkillKinds.GuildJoin;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;

        if (_guild?.Guildstone is not { Deleted: false } stone || stone.Map != character.Map)
        {
            return false;
        }

        _walk = new TravelSkill(stone.GetWorldLocation(), StoneReach);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_guild?.Guildstone is not { Deleted: false } stone || !_guild.Accepted.Contains(_character))
        {
            return _character.Guild == _guild ? SkillStatus.Done : SkillStatus.Failed;
        }

        if (!_character.InRange(stone.GetWorldLocation(), StoneReach))
        {
            return _walk.Tick() == SkillStatus.Failed ? SkillStatus.Failed : SkillStatus.Running;
        }

        stone.OnDoubleClick(_character);

        if (_character.Guild != _guild)
        {
            return SkillStatus.Failed;
        }

        GuildRecruits.Joined(_character, _guild);
        return SkillStatus.Done;
    }

    public override void Abort() => _walk?.Abort();
}
