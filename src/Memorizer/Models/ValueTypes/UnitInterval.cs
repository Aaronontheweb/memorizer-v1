using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memorizer.Models.ValueTypes;

/// <summary>
/// A double value constrained to the range [0.0, 1.0].
/// Used as a base for similarity scores, confidence values, etc.
/// </summary>
[JsonConverter(typeof(UnitIntervalJsonConverter))]
public readonly record struct UnitInterval : IComparable<UnitInterval>
{
    private readonly double _value;

    /// <summary>
    /// Gets the underlying double value (guaranteed to be in [0.0, 1.0]).
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// Creates a new UnitInterval with the specified value.
    /// </summary>
    /// <param name="value">Must be between 0.0 and 1.0 inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside [0.0, 1.0].</exception>
    public UnitInterval(double value)
    {
        if (value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Value must be between 0.0 and 1.0. Got: {value}");
        _value = value;
    }

    /// <summary>
    /// UnitInterval representing 0.0.
    /// </summary>
    public static readonly UnitInterval Zero = new(0.0);

    /// <summary>
    /// UnitInterval representing 1.0.
    /// </summary>
    public static readonly UnitInterval One = new(1.0);

    /// <summary>
    /// Default value (0.7 - common threshold for similarity).
    /// </summary>
    public static readonly UnitInterval DefaultThreshold = new(0.7);

    /// <summary>
    /// Attempts to create a UnitInterval from a double.
    /// Returns false if the value is outside [0.0, 1.0].
    /// </summary>
    public static bool TryCreate(double value, out UnitInterval result)
    {
        if (value >= 0.0 && value <= 1.0)
        {
            result = new UnitInterval(value);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Creates a UnitInterval, clamping the value to [0.0, 1.0].
    /// </summary>
    public static UnitInterval Clamp(double value)
    {
        return new UnitInterval(Math.Clamp(value, 0.0, 1.0));
    }

    public int CompareTo(UnitInterval other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString("F4");

    // Implicit conversion to double for ease of use in calculations
    public static implicit operator double(UnitInterval ui) => ui._value;

    // Explicit conversion from double (requires validation)
    public static explicit operator UnitInterval(double d) => new(d);
}

/// <summary>
/// JSON converter for UnitInterval that serializes as a plain number.
/// </summary>
public class UnitIntervalJsonConverter : JsonConverter<UnitInterval>
{
    public override UnitInterval Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDouble();
        return UnitInterval.TryCreate(value, out var result) ? result : UnitInterval.Zero;
    }

    public override void Write(Utf8JsonWriter writer, UnitInterval value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Confidence score for a memory (0.0 = no confidence, 1.0 = full confidence).
/// Semantic wrapper around UnitInterval.
/// </summary>
[JsonConverter(typeof(ConfidenceJsonConverter))]
public readonly record struct Confidence : IComparable<Confidence>
{
    private readonly UnitInterval _value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public double Value => _value.Value;

    /// <summary>
    /// Creates a new Confidence value.
    /// </summary>
    /// <param name="value">Must be between 0.0 and 1.0 inclusive.</param>
    public Confidence(double value)
    {
        _value = new UnitInterval(value);
    }

    /// <summary>
    /// Full confidence (1.0).
    /// </summary>
    public static readonly Confidence Full = new(1.0);

    /// <summary>
    /// No confidence (0.0).
    /// </summary>
    public static readonly Confidence None = new(0.0);

    /// <summary>
    /// Default confidence level (0.9).
    /// </summary>
    public static readonly Confidence Default = new(0.9);

    /// <summary>
    /// Attempts to create a Confidence from a double.
    /// </summary>
    public static bool TryCreate(double value, out Confidence result)
    {
        if (UnitInterval.TryCreate(value, out var ui))
        {
            result = new Confidence(ui.Value);
            return true;
        }
        result = default;
        return false;
    }

    public int CompareTo(Confidence other) => _value.CompareTo(other._value);

    public override string ToString() => $"Confidence: {_value}";

    public static implicit operator double(Confidence c) => c._value.Value;
    public static explicit operator Confidence(double d) => new(d);
}

/// <summary>
/// JSON converter for Confidence that serializes as a plain number.
/// </summary>
public class ConfidenceJsonConverter : JsonConverter<Confidence>
{
    public override Confidence Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDouble();
        return Confidence.TryCreate(value, out var result) ? result : Confidence.Default;
    }

    public override void Write(Utf8JsonWriter writer, Confidence value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Similarity score between memories (0.0 = completely different, 1.0 = identical).
/// Semantic wrapper around UnitInterval.
/// </summary>
[JsonConverter(typeof(SimilarityScoreJsonConverter))]
public readonly record struct SimilarityScore : IComparable<SimilarityScore>
{
    private readonly UnitInterval _value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public double Value => _value.Value;

    /// <summary>
    /// Creates a new SimilarityScore.
    /// </summary>
    /// <param name="value">Must be between 0.0 and 1.0 inclusive.</param>
    public SimilarityScore(double value)
    {
        _value = new UnitInterval(value);
    }

    /// <summary>
    /// Identical content (1.0).
    /// </summary>
    public static readonly SimilarityScore Identical = new(1.0);

    /// <summary>
    /// No similarity (0.0).
    /// </summary>
    public static readonly SimilarityScore None = new(0.0);

    /// <summary>
    /// Default minimum threshold (0.7).
    /// </summary>
    public static readonly SimilarityScore DefaultThreshold = new(0.7);

    /// <summary>
    /// Attempts to create a SimilarityScore from a double.
    /// </summary>
    public static bool TryCreate(double value, out SimilarityScore result)
    {
        if (UnitInterval.TryCreate(value, out var ui))
        {
            result = new SimilarityScore(ui.Value);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Creates a SimilarityScore from a distance value (converts distance to similarity).
    /// Distance of 0 = similarity of 1, distance of 1 = similarity of 0.
    /// </summary>
    public static SimilarityScore FromDistance(double distance)
    {
        var similarity = 1.0 - Math.Clamp(distance, 0.0, 1.0);
        return new SimilarityScore(similarity);
    }

    /// <summary>
    /// Converts similarity to distance for vector search.
    /// </summary>
    public double ToDistance() => 1.0 - _value.Value;

    public int CompareTo(SimilarityScore other) => _value.CompareTo(other._value);

    public override string ToString() => $"{_value.Value:P1}";

    public static implicit operator double(SimilarityScore s) => s._value.Value;
    public static explicit operator SimilarityScore(double d) => new(d);
}

/// <summary>
/// JSON converter for SimilarityScore that serializes as a plain number.
/// </summary>
public class SimilarityScoreJsonConverter : JsonConverter<SimilarityScore>
{
    public override SimilarityScore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDouble();
        return SimilarityScore.TryCreate(value, out var result) ? result : SimilarityScore.None;
    }

    public override void Write(Utf8JsonWriter writer, SimilarityScore value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
