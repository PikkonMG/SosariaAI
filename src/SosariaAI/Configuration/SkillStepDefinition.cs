using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

/// <summary>
/// One routine step. An older file may still carry hand-made waypoints on a walk step
/// ("route", "reverse", "via"); the reader skips those keys, and the step travels by the
/// graph to its target.
/// </summary>
public sealed class SkillStepDefinition
{
    [JsonPropertyName("skill")]
    public string Skill { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("target")]
    public Point3D Target { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("range")]
    public int? Range { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("area")]
    public AreaDefinition Area { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("fillFraction")]
    public double? FillFraction { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("bankSpot")]
    public Point3D BankSpot { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("center")]
    public Point3D Center { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("radius")]
    public int? Radius { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("duration")]
    public TimeSpan? Duration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("points")]
    public List<Point3D> Points { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("minutes")]
    public int? Minutes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("stopBelowHitsFraction")]
    public double? StopBelowHitsFraction { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("destination")]
    public string Destination { get; set; }

    /// <summary>A place name a GoTo step with no destination and no target walks to.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dungeon")]
    public string Dungeon { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("party")]
    public string Party { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("leaderId")]
    public string LeaderId { get; set; }
}
