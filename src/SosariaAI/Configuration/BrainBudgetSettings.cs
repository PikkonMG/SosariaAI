using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class BrainBudgetSettings
{
    [JsonPropertyName("maxPaidCallsPerDay")]
    public int MaxPaidCallsPerDay { get; set; } = CallBudget.DefaultMaxPaidCallsPerDay;

    [JsonPropertyName("maxPaidCallsPerHour")]
    public int MaxPaidCallsPerHour { get; set; } = CallBudget.DefaultMaxPaidCallsPerHour;

    [JsonPropertyName("countLocalAsPaid")]
    public bool CountLocalAsPaid { get; set; }
}
