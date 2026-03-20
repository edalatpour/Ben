using System.Globalization;
using Microsoft.Maui.Controls;

namespace Bennie.Converters;

public class LinkDecorationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline && isOnline)
        {
            return TextDecorations.Underline;
        }
        return TextDecorations.None;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
