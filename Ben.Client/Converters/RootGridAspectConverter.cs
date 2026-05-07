using System.Globalization;

namespace Ben.Converters;

public sealed class RootGridAspectConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value?.ToString()?.Trim() ?? string.Empty;

        // MAUI has no tiling image brush in XAML; use AspectFill as the closest visual fallback.
        return key.ToLowerInvariant() switch
        {
            "tile" => Aspect.AspectFill,
            "fill" => Aspect.Fill,
            "aspectfit" => Aspect.AspectFit,
            "aspectfill" => Aspect.AspectFill,
            _ => Enum.TryParse<Aspect>(key, true, out Aspect parsed) ? parsed : Aspect.AspectFill,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? nameof(Aspect.AspectFill);
    }
}
