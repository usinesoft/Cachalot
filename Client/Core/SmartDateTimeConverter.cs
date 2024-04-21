using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.Core;

public class SmartDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(typeToConvert == typeof(DateTime));
#pragma warning disable S6580
        return DateTime.Parse(reader.GetString() ?? string.Empty);
#pragma warning restore S6580
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.Kind is DateTimeKind.Unspecified or DateTimeKind.Utc)
        {
            if (value == value.Date)
            {
                writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
            }
            else
            {
                writer.WriteStringValue(value.ToString("o"));
            }
            
            
        }
        else
        {
            writer.WriteStringValue(value.ToString("o"));
        }

        
    }
}

public class SmartDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(typeToConvert == typeof(DateTimeOffset));
#pragma warning disable S6580
        return DateTime.Parse(reader.GetString() ?? string.Empty);
#pragma warning restore S6580
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value.Offset.TotalSeconds == 0)
        {
            if (value == default)
            {
                writer.WriteStringValue(value.ToString("o"));
            }
            else if (value == value.Date)
            {
                writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
            }
            else
            {
                writer.WriteStringValue(value.ToString("o"));
            }
            
        }
        else
        {
            writer.WriteStringValue(value.ToString("o"));
        }

        
    }
}