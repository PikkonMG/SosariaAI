using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

[JsonConverter(typeof(AreaDefinitionConverter))]
public sealed class AreaDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    public bool IsNamed => !string.IsNullOrWhiteSpace(Name);

    public Rectangle2D ToRectangle() => new(X, Y, Width, Height);

    public static AreaDefinition FromName(string name) => new() { Name = name };
}

public sealed class AreaDefinitionConverter : JsonConverter<AreaDefinition>
{
    public override AreaDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return AreaDefinition.FromName(reader.GetString());
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An area must be a name or an object with x, y, width and height.");
        }

        var area = new AreaDefinition();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return area;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var property = reader.GetString();
            reader.Read();

            switch (property)
            {
                case "name":
                    area.Name = reader.GetString();
                    break;
                case "x":
                    area.X = reader.GetInt32();
                    break;
                case "y":
                    area.Y = reader.GetInt32();
                    break;
                case "width":
                    area.Width = reader.GetInt32();
                    break;
                case "height":
                    area.Height = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Area object was not closed.");
    }

    public override void Write(Utf8JsonWriter writer, AreaDefinition value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.IsNamed)
        {
            writer.WriteStringValue(value.Name);
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("width", value.Width);
        writer.WriteNumber("height", value.Height);
        writer.WriteEndObject();
    }
}
