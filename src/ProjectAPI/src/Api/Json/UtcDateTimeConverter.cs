using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectAPI.Api.Json;

/// <summary>
/// Every DateTime the API stores is UTC (DateTime.UtcNow), but EF Core reads
/// them back with Kind = Unspecified, which System.Text.Json writes without a
/// zone ("2026-10-19T20:41:00"). Browsers parse such a string as LOCAL time,
/// so every time was shown shifted by the viewer's UTC offset (one hour early
/// in Morocco). Values are now written with an explicit "Z".
///
/// Reading: a value with a zone or "Z" is converted to UTC; a value without a
/// zone is taken as UTC (the frontend always sends toISOString()).
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}
