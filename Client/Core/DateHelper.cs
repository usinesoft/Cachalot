using System;
using System.Globalization;
using JetBrains.Annotations;

namespace Client.Core;

/// <summary>
/// Formatting:
///     Utc dates or dates with unspecified timezone, without time, are formatted as yyyy-MM-dd
///     Everything else is formatted as ISO-8601
/// Parsing:
///     If no timezone is specified, the result DateTime is Utc and DateTimeOffset has offset 0
///     Otherwise <see cref="DateTimeOffset"/> values are parsed as-is and <see cref="DateTime"/> is converted to UTC
/// </summary>
public static class DateHelper
{
    public static string FormatDateOnly(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd");
    }

    public static DateOnly? ParseDateOnly([NotNull] string strValue)
    {
        if (string.IsNullOrWhiteSpace(strValue))
            return null;

        strValue = strValue.Trim();


        if (DateOnly.TryParseExact(strValue, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        return null;
    }

    public static string FormatDateTime(DateTime value)
    {
        if (value == value.Date)
            return value.ToString("yyyy-MM-dd");

        value = value.ToUniversalTime();

        return value.ToString("o", CultureInfo.InvariantCulture);
    }

    public static DateTime? ParseDateTime([NotNull] string strValue, bool usFormat = false)
    {
        if (string.IsNullOrWhiteSpace(strValue))
            return null;

        strValue = strValue.Trim();

        try
        {
            if (DateTime.TryParseExact(strValue, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return dt;
            }

            if (DateTime.TryParseExact(strValue, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "o" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return dt;
            }

            var culture = CultureInfo.InvariantCulture;
            if (usFormat)
            {
                culture = new CultureInfo("en-US", false);
            }


            if (DateTime.TryParseExact(strValue, new[] { "G", "g"},
                    culture, DateTimeStyles.None, out dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return dt;
            }

            return null;

        }
        catch (Exception)
        {
            return null;
        }

        
    }

    public static DateTimeOffset? ParseDateTimeOffset([NotNull] string strValue)
    {
        if (string.IsNullOrWhiteSpace(strValue))
            return null;

        strValue = strValue.Trim();

        try
        {
            if (DateTimeOffset.TryParseExact(strValue, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)) return dt;

            if (DateTimeOffset.TryParseExact(strValue, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "o" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt)) return dt;

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string FormatDateTimeOffset(DateTimeOffset value)
    {
        if (value == default) return value.ToString("o");

        if (value is { Hour: 0, Minute: 0, Second: 0}) return value.ToString("yyyy-MM-dd");

        return value.ToString("o");
    }
}