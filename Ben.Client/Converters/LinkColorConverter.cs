using System.Globalization;

namespace Bennie.Converters;

public class LinkColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline && isOnline)
        {
            return Application.Current?.Resources["Link"] ?? Colors.Blue;
        }
        return Application.Current?.Resources["Ink"] ?? Colors.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
