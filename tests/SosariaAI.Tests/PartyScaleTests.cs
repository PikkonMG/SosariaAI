using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyScaleTests
{
    private const int EmptyPopulation = 0;
    private const int TwoPopulationBuckets = 2;
    private const int FourHuntParties = 4;
    private const int SoloMemberCount = 1;
    private const int OversizedMemberCount = 9;

    [Fact]
    public void HuntPartyCap_AtOrBelowThreeBuckets_IsMinimum()
    {
        Assert.Equal(PartyScale.MinHuntParties, PartyScale.HuntPartyCap(EmptyPopulation));
        Assert.Equal(PartyScale.MinHuntParties, PartyScale.HuntPartyCap(PartyScale.PopulationPerHuntParty));
        Assert.Equal(
            PartyScale.MinHuntParties,
            PartyScale.HuntPartyCap(PartyScale.PopulationPerHuntParty * TwoPopulationBuckets)
        );
        Assert.Equal(
            PartyScale.MinHuntParties,
            PartyScale.HuntPartyCap(PartyScale.PopulationPerHuntParty * PartyScale.MinHuntParties)
        );
    }

    [Fact]
    public void HuntPartyCap_FourBuckets_IsFour() =>
        Assert.Equal(FourHuntParties, PartyScale.HuntPartyCap(PartyScale.PopulationPerHuntParty * FourHuntParties));

    [Fact]
    public void PartySize_ClampsToThreeThroughFive()
    {
        Assert.Equal(PartyScale.MinPartySize, PartyScale.PartySize(SoloMemberCount));
        Assert.Equal(PartyScale.MinPartySize, PartyScale.PartySize(PartyScale.MinPartySize));
        Assert.Equal(PartyScale.MaxPartySize, PartyScale.PartySize(PartyScale.MaxPartySize));
        Assert.Equal(PartyScale.MaxPartySize, PartyScale.PartySize(OversizedMemberCount));
    }
}
