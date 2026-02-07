using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memorizer.Models.ValueTypes;

/// <summary>
/// A positive integer representing a version number (minimum 1).
/// Version numbers are 1-indexed and monotonically increasing.
/// </summary>
[JsonConverter(typeof(VersionNumberJsonConverter))]
public readonly record struct VersionNumber : IComparable<VersionNumber>
{
    private readonly int _value;

    /// <summary>
    /// Gets the underlying integer value (guaranteed to be >= 1).
    /// </summary>
    public int Value => _value;

    /// <summary>
    /// Creates a new VersionNumber.
    /// </summary>
    /// <param name="value">Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    public VersionNumber(int value)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Version number must be at least 1. Got: {value}");
        _value = value;
    }

    /// <summary>
    /// The initial version number (1).
    /// </summary>
    public static readonly VersionNumber Initial = new(1);

    /// <summary>
    /// Returns the next version number.
    /// </summary>
    public VersionNumber Increment() => new(_value + 1);

    /// <summary>
    /// Returns the previous version number, or null if this is version 1.
    /// </summary>
    public VersionNumber? Previous() => _value > 1 ? new VersionNumber(_value - 1) : null;

    /// <summary>
    /// Attempts to create a VersionNumber from an int.
    /// Returns false if the value is less than 1.
    /// </summary>
    public static bool TryCreate(int value, out VersionNumber result)
    {
        if (value >= 1)
        {
            result = new VersionNumber(value);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a string as a VersionNumber.
    /// </summary>
    public static bool TryParse(string? s, out VersionNumber result)
    {
        if (int.TryParse(s, out var value) && value >= 1)
        {
            result = new VersionNumber(value);
            return true;
        }
        result = default;
        return false;
    }

    public int CompareTo(VersionNumber other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString();

    // Implicit conversion to int for ease of use
    public static implicit operator int(VersionNumber vn) => vn._value;

    // Explicit conversion from int (requires validation)
    public static explicit operator VersionNumber(int i) => new(i);

    // Comparison operators
    public static bool operator <(VersionNumber left, VersionNumber right) => left._value < right._value;
    public static bool operator >(VersionNumber left, VersionNumber right) => left._value > right._value;
    public static bool operator <=(VersionNumber left, VersionNumber right) => left._value <= right._value;
    public static bool operator >=(VersionNumber left, VersionNumber right) => left._value >= right._value;
}

/// <summary>
/// JSON converter for VersionNumber that serializes as a plain integer.
/// </summary>
public class VersionNumberJsonConverter : JsonConverter<VersionNumber>
{
    public override VersionNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetInt32();
        return VersionNumber.TryCreate(value, out var result) ? result : VersionNumber.Initial;
    }

    public override void Write(Utf8JsonWriter writer, VersionNumber value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
