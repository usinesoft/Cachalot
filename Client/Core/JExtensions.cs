using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Client.Messages;

namespace Client.Core;

public static class JExtensions
{

    public static KeyValues JsonPropertyToKeyValues(this JsonElement parent, KeyInfo info)
    {
        if (parent.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Not a json array", nameof(parent));
        }

        return new KeyValues(info.Name, parent.EnumerateArray().Select(x => JsonElementToKeyValue(info, x)));

    }

    public static void ExtractTextFromJsonElement(this JsonElement parent, List<string> lines)
    {
        switch (parent.ValueKind)
        {
            case JsonValueKind.String:
            {
                

                var txt = parent.GetString();
                bool isDate = txt.Length >= 10 && txt.Substring(0, 10).Count(x => x == '-') == 2;
                
                // ignore dates for full-text indexation
                if (!string.IsNullOrWhiteSpace(txt) && !isDate)
                {
                    lines.Add(txt);
                }

                return;
            }
            case JsonValueKind.Object:
            {
                foreach (var child in parent.EnumerateObject())
                {
                    child.Value.ExtractTextFromJsonElement(lines);
                }

                return;
            }
            case JsonValueKind.Array:
            {
                foreach (var child in parent.EnumerateArray())
                {
                    child.ExtractTextFromJsonElement(lines);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Convert child element to key value. Can be done only for scalar properties
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static KeyValue JsonPropertyToKeyValue(this JsonElement parent, KeyInfo info)
    {

        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Not a json object", nameof(parent));
        }

        // as we ignore default values on json serialization 
        // the value can be absent because it is an int value 0
        if (!parent.TryGetProperty(info.JsonName, out var property))
        {
            return info.IndexType == IndexType.Primary ? new(0) : new KeyValue(null);
        }

        
        return JsonElementToKeyValue(info, property);

        
    }

    private static KeyValue JsonElementToKeyValue(KeyInfo info, JsonElement property)
    {
        switch (property.ValueKind)
        {
            case JsonValueKind.String:
                return property.TryGetDateTime(out var date) ? new KeyValue(date) : new KeyValue(property.GetString());

            case JsonValueKind.Number:
                var value = property.GetDecimal();
                return value == (int)value ? new KeyValue((int)value) : new KeyValue(value);

            case JsonValueKind.True:
                return new KeyValue(true);

            case JsonValueKind.False:
                return new KeyValue(false);

            case JsonValueKind.Null:
                return new KeyValue(null);

            default:
                throw new ArgumentOutOfRangeException(
                    $"Poperty {info.JsonName} = {property.GetString()} can not be parsed");
        }
    }


    public static object StringToDate(string valueAsString)
    {
        var trimmed = valueAsString.Trim('\'', '"');
        var dt = DateHelper.ParseDateTime(trimmed);
        if (dt.HasValue)
            return dt;

        var dto = DateHelper.ParseDateTimeOffset(trimmed);
        if (dto.HasValue) return dto;

        return valueAsString;

    }

    public static object SmartParse(string valueAsString)
    {
        var type = KeyValue.OriginalType.SomeInteger;

        var firstPosition = true;
        // try an educated guess to avoid useless TryParse
        foreach (var c in valueAsString)
        {
            if (char.IsLetter(c) || c == '\'') // strings may be quoted or not
            {
                type = KeyValue.OriginalType.String;
                
            }

            if (!firstPosition && c is '-' or '/')
            {
                type = KeyValue.OriginalType.Date;
                break;
            }

            if (c is '.' or ',')
            {
                type = KeyValue.OriginalType.SomeFloat;
                break;
            }

            firstPosition = false;
        }

        return type switch
        {
            KeyValue.OriginalType.String when valueAsString == "null" => null,

            KeyValue.OriginalType.String when bool.TryParse(valueAsString, out var bv) => bv,

            KeyValue.OriginalType.String => valueAsString.Trim('\'', '"'),

            KeyValue.OriginalType.SomeFloat when double.TryParse(valueAsString, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var fv) => fv,

            KeyValue.OriginalType.SomeFloat => valueAsString,

            KeyValue.OriginalType.Date  => StringToDate(valueAsString),

            KeyValue.OriginalType.SomeInteger when int.TryParse(valueAsString, out var vi) => vi,

            _ => valueAsString
        };
    }
}