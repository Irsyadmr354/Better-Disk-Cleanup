using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BetterDiskCleanup.App.Converters;

public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter is string paramStr && int.TryParse(paramStr, out var target))
        {
            return intVal == target;
        }

        return Equals(value, parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // When the RadioButton becomes checked (value=true), push the
        // target index back to the source so SelectedIndex updates.
        // When it becomes unchecked (value=false), do nothing — the
        // newly-checked RadioButton will drive the update instead.
        if (value is true
            && parameter is string paramStr
            && int.TryParse(paramStr, out var target))
        {
            return target;
        }

        return Binding.DoNothing;
    }
}
