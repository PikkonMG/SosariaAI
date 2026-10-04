using Server;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PracticeSkillTests
{
    private const int ExpectedKindCount = 17;
    private const int ExpectedCompanyReachTiles = 8;
    private const double ExpectedPracticeMin = 0;
    private const double ClassicMax = 100;
    private const double ExtendedMax = 120;
    private const double CurseWeaponMax = 40;

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
    public void TastersOnly_OnlyTaste(string kind)
    {
        PracticeSkillTable.TryGet(kind, out var rule);
        Assert.Equal(kind == SkillKinds.Taste, rule.TastersOnly);
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
}
