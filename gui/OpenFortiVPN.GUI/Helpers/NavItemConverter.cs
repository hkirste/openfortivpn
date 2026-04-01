using System.Globalization;
using System.Windows.Data;

namespace OpenFortiVPN.GUI.Helpers;

/// <summary>
/// Converts a selected nav item string to a boolean for RadioButton binding.
/// ConverterParameter is the nav item name to compare against.
/// </summary>
public class NavItemConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string selected && parameter is string target)
            return string.Equals(selected, target, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string target)
            return target;
        return Binding.DoNothing;
    }
}
