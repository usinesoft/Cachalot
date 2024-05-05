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

        var str = reader.GetString();

        return DateHelper.ParseDateTime(str!) ?? throw new FormatException($"Can not convert {str} to date");

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
        var str = reader.GetString();
        return DateHelper.ParseDateTimeOffset(str!) ?? throw new FormatException($"Can not convert {str} to date");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {

        var str = DateHelper.FormatDateTimeOffset(value);
        writer.WriteStringValue(str);

    }
}