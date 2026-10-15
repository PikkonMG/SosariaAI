using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PracticeSkillTests : IDisposable
{
    private const int ExpectedKindCount = 17;
    private const int ExpectedCompanyReachTiles = 8;
    private const double ExpectedPracticeMin = 0;
    private const double ClassicMax = 100;
    private const double ExtendedMax = 120;
    private const double CurseWeaponMax = 40;
    private const double TrainedSkill = 70;
    private static readonly Point3D Bank = new(100, 100, 0);

    private readonly List<IEntity> _placed = [];

    static PracticeSkillTests() => Timer.Init(0);

    public PracticeSkillTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
    }

    public void Dispose() => TestMap.Remove(_placed);

    public static TheoryData<string> TableKinds() => new(PracticeSkillTable.Kinds);

    [Theory]
    [InlineData(SkillKinds.Anatomy, SkillName.Anatomy, ClassicMax)]
    [InlineData(SkillKinds.Archery, SkillName.Archery, ClassicMax)]
    [InlineData(SkillKinds.ArmsLore, SkillName.ArmsLore, ClassicMax)]
    [InlineData(SkillKinds.Camp, SkillName.Camping, ClassicMax)]
    [InlineData(SkillKinds.EvalInt, SkillName.EvalInt, ExtendedMax)]
    [InlineData(SkillKinds.Fence, SkillName.Fencing, ClassicMax)]
    [InlineData(SkillKinds.Forensic, SkillName.Forensics, ClassicMax)]
    [InlineData(SkillKinds.Herd, SkillName.Herding, ClassicMax)]
    [InlineData(SkillKinds.ItemId, SkillName.ItemID, ClassicMax)]
    [InlineData(SkillKinds.Mace, SkillName.Macing, ClassicMax)]
    [InlineData(SkillKinds.Music, SkillName.Musicianship, ExtendedMax)]
    [InlineData(SkillKinds.Necro, SkillName.Necromancy, CurseWeaponMax)]
    [InlineData(SkillKinds.Parry, SkillName.Parry, ClassicMax)]
    [InlineData(SkillKinds.Sword, SkillName.Swords, ClassicMax)]
    [InlineData(SkillKinds.Tactics, SkillName.Tactics, ClassicMax)]
    [InlineData(SkillKinds.Taste, SkillName.TasteID, ClassicMax)]
    [InlineData(SkillKinds.Wrestle, SkillName.Wrestling, ClassicMax)]
    public void Table_ChecksTheKindsSkillInTheModernUOPracticeWindow(string kind, SkillName skill, double practiceMax)
    {
        Assert.True(PracticeSkillTable.TryGet(kind, out var rule));
        Assert.Equal(kind, rule.Kind);
        Assert.Equal(skill, rule.Skill);
        Assert.Equal(ExpectedPracticeMin, rule.PracticeMin);
        Assert.Equal(practiceMax, rule.PracticeMax);
    }

    [Fact]
    public void Table_HoldsEveryPlainPracticeKind() =>
        Assert.Equal(ExpectedKindCount, PracticeSkillTable.Kinds.Count);

    [Fact]
    public void Table_KnowsNoSkillWithItsOwnClass()
    {
        Assert.False(PracticeSkillTable.TryGet(SkillKinds.Peace, out _));
        Assert.False(PracticeSkillTable.TryGet(SkillKinds.Track, out _));
        Assert.False(PracticeSkillTable.TryGet(SkillKinds.Hunt, out _));
    }

    [Fact]
    public void Table_KindsAreCaseSensitiveLikeTheFactory() =>
        Assert.False(PracticeSkillTable.TryGet(SkillKinds.Anatomy.ToUpperInvariant(), out _));

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void NeedsCompany_OnlyTheStudySkills(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.Equal(kind is SkillKinds.Anatomy or SkillKinds.EvalInt, rule.NeedsCompany);
    }

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void TrainedOnly_FightingAndTastePractice(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.Equal(
            kind is SkillKinds.Archery or SkillKinds.Fence or SkillKinds.Mace or SkillKinds.Parry or SkillKinds.Sword
                or SkillKinds.Tactics or SkillKinds.Taste or SkillKinds.Wrestle,
            rule.TrainedOnly
        );
        Assert.Equal(rule.TrainedOnly, Array.IndexOf(PracticeSkillTable.TrainedOnlyKinds, kind) >= 0);
    }

    [Fact]
    public void GatedKinds_KeepTrainedOnlyPracticeFromOtherBuilds() =>
        Assert.All(PracticeSkillTable.TrainedOnlyKinds, kind => Assert.Contains(kind, SkillReadiness.GatedKinds));

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void MayBegin_NoCharacter_IsFalse(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.False(PracticeSkillTable.MayBegin(rule, null));
    }

    [Fact]
    public void MayBegin_Mage_PractisesItsOwnSkills_NotAFightersWeapon()
    {
        // A mage copied from a fighter's template kept its mace and sword practice.
        var mage = InWorld((Serial)0x7B01, BuildPresets.MageNovice());

        Assert.False(MayBegin(SkillKinds.Mace, mage));
        Assert.False(MayBegin(SkillKinds.Sword, mage));
        Assert.False(MayBegin(SkillKinds.Tactics, mage));
        Assert.True(MayBegin(SkillKinds.Wrestle, mage));
        Assert.True(MayBegin(SkillKinds.ItemId, mage));
    }

    [Fact]
    public void MayBegin_Swordsman_PractisesSwords_NotMace()
    {
        var swordsman = InWorld((Serial)0x7B03, BuildPresets.SwordsmanNovice());

        Assert.True(MayBegin(SkillKinds.Sword, swordsman));
        Assert.True(MayBegin(SkillKinds.Parry, swordsman));
        Assert.False(MayBegin(SkillKinds.Mace, swordsman));
        Assert.False(MayBegin(SkillKinds.Archery, swordsman));
    }

    [Fact]
    public void MayBegin_Taste_OnlyABuildThatTrainsTasteID()
    {
        var alchemist = InWorld((Serial)0x7B05, Trains(SkillName.Alchemy, SkillName.TasteID));
        var fighter = InWorld((Serial)0x7B07, Trains(SkillName.Swords, SkillName.Healing));

        Assert.True(MayBegin(SkillKinds.Taste, alchemist));
        Assert.False(MayBegin(SkillKinds.Taste, fighter));
    }

    [Fact]
    public void CompanyReach_IsEightTiles() =>
        Assert.Equal(ExpectedCompanyReachTiles, PracticeSkillTable.CompanyReachTiles);

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void PracticeSkill_CarriesTheKindAsItsName(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.Equal(kind, new PracticeSkill(rule).Name);
    }

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void Begin_NoCharacter_IsFalse(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.False(new PracticeSkill(rule).Begin(null));
    }

    [Theory]
    [MemberData(nameof(TableKinds))]
    public void Tick_NoCharacter_Fails(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.Equal(SkillStatus.Failed, new PracticeSkill(rule).Tick());
    }

    private static bool MayBegin(string kind, SosariaCharacter character) =>
        PracticeSkillTable.TryGet(kind, out var rule) && PracticeSkillTable.MayBegin(rule, character);

    /// <summary>A person of <paramref name="build"/> standing on the test land.</summary>
    private SosariaCharacter InWorld(Serial serial, ResolvedBuild build)
    {
        var character = new SosariaCharacter(serial) { Build = build };
        character.DefaultMobileInit();
        character.MoveToWorld(Bank, TestMap.EnsureLand());
        _placed.Add(character);
        return character;
    }

    private static ResolvedBuild Trains(params SkillName[] skills)
    {
        var trained = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            trained[skill.ToString()] = TrainedSkill;
        }

        return new ResolvedBuild { Skills = trained };
    }
}
