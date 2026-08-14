using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WupmGui.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool b) return Visibility.Collapsed;
        var invert = parameter is string s && bool.TryParse(s, out var p) && p;
        return (invert ? !b : b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Visibility v) return false;
        var invert = parameter is string s && bool.TryParse(s, out var p) && p;
        return (v == Visibility.Visible) ^ invert;
    }
}

public sealed class SizeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes <= 0) return "0 B";
        var suffixes = new[] { "B", "KB", "MB", "GB", "TB" };
        var i = 0;
        var d = (double)bytes;
        while (d >= 1024 && i < suffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", d, suffixes[i]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
