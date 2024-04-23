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
        var str = DateHelper.FormatDateTime(value);
        writer.WriteStringValue(str);
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

        var str = DateHelper.FormatDateTimeOffset(value);
        writer.WriteStringValue(str);

    }
}

public static class DateHelper
{
    public static string FormatDateTime(DateTime value)
    {

        if (value.Kind is DateTimeKind.Unspecified or DateTimeKind.Utc)
        {
            if (value == value.Date)
            {
                return value.ToString("yyyy-MM-dd");
            }
            
        }
        
        return value.ToString("o");
        
    }

    public static string FormatDateTimeOffset(DateTimeOffset value)
    {
        if (value.Offset.TotalSeconds == 0)
        {
            if (value == default)
            {
                return value.ToString("o");
            }

            if (value == value.Date)
            {
                return value.ToString("yyyy-MM-dd");
            }
            
            return value.ToString("o");
            

        }

        return value.ToString("o");

    }
}