using System;
using System.Globalization;
using Client.Messages;
using Newtonsoft.Json.Linq;

namespace Client.Core;

public static class JExtensions
{
    public static KeyValue JTokenToKeyValue(this JToken jToken, KeyInfo info)
    {
        // as we ignore default values on json serialization 
        // the value can be absent because it is an int value 0

        if (jToken == null) return info.IndexType == IndexType.Primary ? new(0) : new KeyValue(null);

        var valueToken = jToken.HasValues ? jToken.First : jToken;

        if (valueToken?.Type == JTokenType.Integer) return new((long)valueToken);

        if (valueToken?.Type == JTokenType.Float) return new((double)valueToken);


        if (valueToken?.Type == JTokenType.Boolean) return new((bool)valueToken);

        if (valueToken?.Type == JTokenType.Date) return new((DateTime)valueToken);

        return new((string)valueToken);
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
                break;
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

            KeyValue.OriginalType.Date when DateTime.TryParse(valueAsString, out var dt) => dt,

            KeyValue.OriginalType.Date when DateTimeOffset.TryParse(valueAsString, out var dv) => dv,

            KeyValue.OriginalType.Date => valueAsString,

            KeyValue.OriginalType.SomeInteger when int.TryParse(valueAsString, out var vi) => vi,

            _ => valueAsString
        };
    }
}