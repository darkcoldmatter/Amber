using System.Globalization;
using System.Windows.Data;

namespace Amber.Controls;

/// <summary>
/// For radio buttons bound to one value: checked when the value equals ConverterParameter,
/// and checking it sets the value to ConverterParameter.
/// </summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}
