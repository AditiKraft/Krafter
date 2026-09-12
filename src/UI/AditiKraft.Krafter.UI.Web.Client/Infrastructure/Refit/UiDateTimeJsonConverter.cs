using System.Text.Json;
using System.Text.Json.Serialization;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public sealed class UiDateTimeJsonConverter(bool localizeResponses) : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        DateTime value = reader.GetDateTime();
        // Preserve database infinity values, including the root tenant's unlimited expiry.
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        DateTime utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return localizeResponses ? utc.ToLocalTime() : utc;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Date pickers produce Unspecified values; ToUniversalTime treats them as local.
        DateTime utc = value == DateTime.MinValue || value == DateTime.MaxValue
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        writer.WriteStringValue(utc);
    }
}
