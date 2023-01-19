using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Client.Tools;

public class SmartDateTimeConverter : IsoDateTimeConverter
{
    
    
    public static string FormatDate(DateTime date)
    {
        return date == date.Date
            ? date.ToString("yyyy-MM-dd")
            : date.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is DateTime dtValue)
        {
            writer.WriteValue(FormatDate(dtValue));
        }
        else
        {
            base.WriteJson(writer, value, serializer);
        }
        
    }
}