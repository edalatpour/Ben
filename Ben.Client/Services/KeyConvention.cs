using System.Globalization;

namespace Ben.Services;

public static class KeyConvention
{
    public const string DatePrefix = "date:";
    public const string ProjectPrefix = "project:";
    public const string DateFormat = "yyyy-MM-dd";

    public static string ToDateKey(DateTime date)
    {
        return string.Concat(DatePrefix, date.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    public static string ToProjectKey(string projectName)
    {
        string displayName = NormalizeProjectDisplayName(projectName);
        return string.Concat(ProjectPrefix, displayName);
    }

    public static string NormalizeProjectDisplayName(string? projectName)
    {
        return (projectName ?? string.Empty).Trim();
    }

    public static string NormalizeProjectName(string? projectName)
    {
        return NormalizeProjectDisplayName(projectName).ToUpperInvariant();
    }

    public static bool IsDateKey(string? key)
    {
        return TryParseDateKey(key, out _);
    }

    public static bool IsProjectKey(string? key)
    {
        return TryGetProjectName(key, out _);
    }

    public static bool TryParseDateKey(string? key, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(DatePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string value = key[DatePrefix.Length..];
        return DateTime.TryParseExact(
            value,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    public static bool TryGetProjectName(string? key, out string projectName)
    {
        projectName = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(ProjectPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        projectName = key[ProjectPrefix.Length..].Trim();
        return projectName.Length > 0;
    }

    public static string NormalizeDateOrFallback(string? key, DateTime fallbackDate)
    {
        if (TryParseDateKey(key, out DateTime date))
        {
            return ToDateKey(date);
        }

        return ToDateKey(fallbackDate);
    }

    public static string ToShortDateDisplay(string? key)
    {
        if (TryParseDateKey(key, out DateTime date))
        {
            return date.ToString("M/d", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public static string ToShortPageDisplay(string? key)
    {
        if (TryParseDateKey(key, out DateTime date))
        {
            return date.ToString("M/d", CultureInfo.InvariantCulture);
        }

        if (TryGetProjectName(key, out string projectName))
        {
            return projectName;
        }

        return string.Empty;
    }
}