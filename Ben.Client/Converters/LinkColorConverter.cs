using System.Globalization;

namespace Ben.Converters;

public class LinkColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline && isOnline)
        {
            if (Application.Current?.Resources.TryGetValue("Link", out var link) == true)
            {
                return link;
            }
            return Colors.Blue;
        }
        if (Application.Current?.Resources.TryGetValue("Ink", out var ink) == true)
        {
            return ink;
        }
        return Colors.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
