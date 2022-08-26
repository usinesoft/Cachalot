using System;
using System.Globalization;
using Client.Messages;
using Newtonsoft.Json.Linq;

namespace Client.Core
{
    public static class JExtensions
    {
        public static KeyValue JTokenToKeyValue(this JToken jToken, KeyInfo info)
        {
            // as we ignore default values on json serialization 
            // the value can be absent because it is an int value 0
             
            if(jToken == null) return info.IndexType == IndexType.Primary?  new KeyValue(0): new KeyValue(null);

            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken?.Type == JTokenType.Integer)
            {
                return new KeyValue((long)valueToken);
            }

            if (valueToken?.Type == JTokenType.Float)
            {
                return new KeyValue((double)valueToken);
            }


            if (valueToken?.Type == JTokenType.Boolean)
            {
                return new KeyValue((bool)valueToken);
            }

            if (valueToken?.Type == JTokenType.Date)
            {
                return new KeyValue((DateTime)valueToken);
            }

            return new KeyValue((string)valueToken);

        }

        public static object SmartParse(string valueAsString)
        {

            KeyValue.OriginalType type = KeyValue.OriginalType.SomeInteger;

            // try an educated guess to avoid useless TryParse
            foreach (char c in valueAsString)
            {
                if(char.IsLetter(c))
                {
                    type = KeyValue.OriginalType.String;
                    break;
                }

                if(c=='-' || c == '/')
                {
                    type = KeyValue.OriginalType.Date;
                    break;
                }

                if(c=='.' || c == ',')
                {
                    type = KeyValue.OriginalType.SomeFloat;
                    break;
                }
            }

            if (type == KeyValue.OriginalType.String)
            {
                if (valueAsString == "null")
                {
                    return null;
                }

                if (bool.TryParse(valueAsString, out var bv))
                {
                    return bv;
                }

                return valueAsString;
            }

            if (type == KeyValue.OriginalType.SomeFloat)
            {
                if(double.TryParse(valueAsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv))
                {
                    return fv;
                }

                return valueAsString;
            }

            if (type == KeyValue.OriginalType.Date)
            {
                if (DateTimeOffset.TryParse(valueAsString, out var dv))
                {
                    return dv;
                }

                return valueAsString;
            }

            if (type == KeyValue.OriginalType.SomeInteger)
            {
                if (int.TryParse(valueAsString, out var vi))
                {
                    return vi;
                }
            }

            return valueAsString;
        }
    }
}