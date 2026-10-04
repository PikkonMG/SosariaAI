using System.Text.Json.Serialization;
using SosariaAI.Combat;

namespace SosariaAI.Configuration;

public sealed class CareerSettings
{
    public const int DefaultGoldReserve = 200;
    public const int DefaultVendorSearchRange = 8;
    public const int DefaultHouseGold = Behaviour.HouseRules.ArchitectPrice;
    public const int DefaultPartyInviteRange = 8;
    public const int DefaultLeisureRadius = 200;
    public const int DefaultLeashRadius = 400;
    public const int DefaultMinThreatToFlee = 40;
    public const int DefaultInviteAnswerSeconds = 60;
    public const int DefaultInviteCooldownMinutes = 10;
    public const int DefaultDeathAvoidHours = 24;
    public const int DefaultPartyFollowRange = 12;
    public const int NoGoldReserve = 0;
    public const bool DefaultIgnorePriceLimits = false;

    [JsonPropertyName("threatMultiple")]
    public double ThreatMultiple { get; set; } = ThreatRating.DefaultThreatMultiple;

    [JsonPropertyName("goldReserve")]
    public int GoldReserve { get; set; } = DefaultGoldReserve;

    [JsonPropertyName("vendorSearchRange")]
    public int VendorSearchRange { get; set; } = DefaultVendorSearchRange;

    [JsonPropertyName("houseGold")]
    public int HouseGold { get; set; } = DefaultHouseGold;

    [JsonPropertyName("partyInviteRange")]
    public int PartyInviteRange { get; set; } = DefaultPartyInviteRange;

    [JsonPropertyName("leisureRadius")]
    public int LeisureRadius { get; set; } = DefaultLeisureRadius;

    [JsonPropertyName("leashRadius")]
    public int LeashRadius { get; set; } = DefaultLeashRadius;

    [JsonPropertyName("minThreatToFlee")]
    public int MinThreatToFlee { get; set; } = DefaultMinThreatToFlee;

    [JsonPropertyName("inviteAnswerSeconds")]
    public int InviteAnswerSeconds { get; set; } = DefaultInviteAnswerSeconds;

    [JsonPropertyName("inviteCooldownMinutes")]
    public int InviteCooldownMinutes { get; set; } = DefaultInviteCooldownMinutes;

    [JsonPropertyName("deathAvoidHours")]
    public int DeathAvoidHours { get; set; } = DefaultDeathAvoidHours;

    [JsonPropertyName("partyFollowRange")]
    public int PartyFollowRange { get; set; } = DefaultPartyFollowRange;

    /// <summary>
    /// Testing switch: gear buys ignore the gold reserve so a character can spend every
    /// coin it carries. Vendor prices still apply. Off is the normal rule.
    /// </summary>
    [JsonPropertyName("ignorePriceLimits")]
    public bool IgnorePriceLimits { get; set; } = DefaultIgnorePriceLimits;

    /// <summary>Gold a character keeps in the bank when it shops for gear.</summary>
    public int EffectiveGoldReserve() =>
        IgnorePriceLimits ? NoGoldReserve : GoldReserve > 0 ? GoldReserve : DefaultGoldReserve;

    /// <summary>
    /// The threat that sends a character running: career.minThreatToFlee, or the default when
    /// the file is not loaded or the value is not positive. A zero threshold would send
    /// everyone running from an empty field.
    /// </summary>
    public static int FleeThreshold(CareerSettings career) =>
        career is { MinThreatToFlee: > 0 } ? career.MinThreatToFlee : DefaultMinThreatToFlee;
}
