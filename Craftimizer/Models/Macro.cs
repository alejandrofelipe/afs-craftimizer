using Craftimizer.Simulator.Actions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Craftimizer.Plugin;

public class StoredActionTypeConverter : JsonConverter<ActionType[]>
{
    public override ActionType[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected StartArray, got {reader.TokenType} when reading ActionType[]");

        var ret = new List<ActionType>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return [.. ret];
            else if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                if (Enum.TryParse(name, ignoreCase: false, out ActionType key))
                    ret.Add(key);
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // https://github.com/WorkingRobot/Craftimizer/blob/90f53de3d88344084bb2413161c8051ef073dc3d/Simulator/Actions/ActionType.cs#L6
                ActionType? key = reader.GetByte() switch
                {
                    0 => ActionType.AdvancedTouch,
                    1 => ActionType.BasicSynthesis,
                    2 => ActionType.BasicTouch,
                    3 => ActionType.ByregotsBlessing,
                    4 => ActionType.CarefulObservation,
                    5 => ActionType.CarefulSynthesis,
                    6 => ActionType.DelicateSynthesis,
                    7 => ActionType.FinalAppraisal,
                    // 8 => ActionType.FocusedSynthesis,
                    // 9 => ActionType.FocusedTouch,
                    10 => ActionType.GreatStrides,
                    11 => ActionType.Groundwork,
                    12 => ActionType.HastyTouch,
                    13 => ActionType.HeartAndSoul,
                    14 => ActionType.Innovation,
                    15 => ActionType.IntensiveSynthesis,
                    16 => ActionType.Manipulation,
                    17 => ActionType.MastersMend,
                    18 => ActionType.MuscleMemory,
                    19 => ActionType.Observe,
                    20 => ActionType.PreciseTouch,
                    21 => ActionType.PreparatoryTouch,
                    22 => ActionType.PrudentSynthesis,
                    23 => ActionType.PrudentTouch,
                    24 => ActionType.RapidSynthesis,
                    25 => ActionType.Reflect,
                    26 => ActionType.StandardTouch,
                    27 => ActionType.TrainedEye,
                    28 => ActionType.TrainedFinesse,
                    29 => ActionType.TricksOfTheTrade,
                    30 => ActionType.Veneration,
                    31 => ActionType.WasteNot,
                    32 => ActionType.WasteNot2,
                    33 => ActionType.StandardTouchCombo,
                    34 => ActionType.AdvancedTouchCombo,
                    // 35 => ActionType.FocusedSynthesisCombo,
                    // 36 => ActionType.FocusedTouchCombo,
                    _ => null,
                };
                if (key is not null)
                    ret.Add(key.Value);
            }
            else
                throw new JsonException($"Unexpected token {reader.TokenType} in ActionType array");
        }

        throw new JsonException("Unexpected end of JSON stream; expected EndArray for ActionType[]");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ActionType[] value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(Enum.GetName(item) ?? throw new JsonException($"ActionType value {item} has no enum name"));
        writer.WriteEndArray();
    }
}

public class Macro
{
    /// <summary>SQLite row ID. 0 when not yet persisted.</summary>
    [JsonIgnore]
    internal int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ushort? RecipeId { get; set; }

    /// <summary>Score from 0 to 1 representing quality/collectability achieved. Used to determine if a newer craft result is better.</summary>
    public float SavedScore { get; set; }

    [JsonInclude] [JsonPropertyName("Actions")]
    internal ActionType[] actions { get; set; } = [];
    [JsonIgnore]
    public IReadOnlyList<ActionType> Actions
    {
        get => actions;
        set => ActionEnumerable = value;
    }
    [JsonIgnore]
    public IEnumerable<ActionType> ActionEnumerable
    {
        set => actions = [.. value];
    }
}
